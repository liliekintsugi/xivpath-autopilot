using System.Numerics;
using Dalamud.Plugin.Services;
using XIVPathAutopilot.Integration;

namespace XIVPathAutopilot.Navigation;

public enum AutoPilotState
{
    Idle,
    Moving,
    Paused,
    Arrived,
    Failed,
}

public sealed class AutoPilotController : IDisposable
{
    private readonly IClientState _clientState;
    private readonly IFramework _framework;
    private readonly IPluginLog _log;
    private readonly VNavmeshIpcClient _navmesh;
    private readonly Configuration _config;

    private DateTimeOffset _startedAt;
    private Vector3? _destination;
    private bool _isFlagNavigation;
    private DateTimeOffset _lastStuckSampleAt = DateTimeOffset.UtcNow;
    private Vector3 _lastStuckSamplePos;
    private DateTimeOffset? _stuckSince;

    public AutoPilotState State { get; private set; } = AutoPilotState.Idle;
    public string StatusText { get; private set; } = "Idle";

    public AutoPilotController(
        IClientState clientState,
        IFramework framework,
        IPluginLog log,
        VNavmeshIpcClient navmesh,
        Configuration config)
    {
        _clientState = clientState;
        _framework = framework;
        _log = log;
        _navmesh = navmesh;
        _config = config;

        _framework.Update += OnFrameworkUpdate;
    }

    public bool Start(Vector3 destination)
    {
        if (_clientState.LocalPlayer is null)
        {
            Fail("No local player.");
            return false;
        }

        _navmesh.TryConfigureTravelPreferences(_config.UseMountWhenPossible, _config.PreferFlyingWhenPossible);
        if (!_navmesh.TryStartMoveTo(destination))
        {
            Fail("Unable to start vnavmesh route.");
            return false;
        }

        ResetMovementTracking();
        _startedAt = DateTimeOffset.UtcNow;
        _destination = destination;
        _isFlagNavigation = false;
        State = AutoPilotState.Moving;
        StatusText = $"Moving to {destination.X:F1}, {destination.Y:F1}, {destination.Z:F1}";
        return true;
    }

    public bool StartToFlag()
    {
        if (_clientState.LocalPlayer is null)
        {
            Fail("No local player.");
            return false;
        }

        _navmesh.TryConfigureTravelPreferences(_config.UseMountWhenPossible, _config.PreferFlyingWhenPossible);
        if (!_navmesh.TryStartMoveToFlag())
        {
            Fail("Unable to start move-to-flag.");
            return false;
        }

        ResetMovementTracking();
        _startedAt = DateTimeOffset.UtcNow;
        _destination = null;
        _isFlagNavigation = true;
        State = AutoPilotState.Moving;
        StatusText = "Moving to map flag...";
        return true;
    }

    public void Stop(string reason = "Stopped.")
    {
        _navmesh.TryStop();
        State = AutoPilotState.Idle;
        StatusText = reason;
        _destination = null;
        _isFlagNavigation = false;
        _stuckSince = null;
    }

    public void Pause()
    {
        if (State != AutoPilotState.Moving) return;
        if (_navmesh.TryPause())
        {
            State = AutoPilotState.Paused;
            StatusText = "Paused.";
        }
    }

    public void Resume()
    {
        if (State != AutoPilotState.Paused) return;
        if (_navmesh.TryResume())
        {
            State = AutoPilotState.Moving;
            StatusText = "Resumed.";
        }
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (State is not AutoPilotState.Moving) return;
        if (_clientState.LocalPlayer is null)
        {
            Fail("Player unavailable.");
            return;
        }

        var position = _clientState.LocalPlayer.Position;
        if (_destination is not null)
        {
            var distance = Vector3.Distance(position, _destination.Value);
            if (distance <= _config.ArrivalDistance)
            {
                State = AutoPilotState.Arrived;
                StatusText = "Arrived.";
                _destination = null;
                _isFlagNavigation = false;
                return;
            }
        }

        if (_config.EnableSafetyTimeout &&
            (DateTimeOffset.UtcNow - _startedAt).TotalSeconds > _config.SafetyTimeoutSeconds)
        {
            _navmesh.TryStop();
            Fail("Safety timeout reached.");
            return;
        }

        if (_config.EnableAntiStuck)
            UpdateAntiStuck(position);

        if (_navmesh.TryIsRunning(out var running) && !running)
        {
            if (_isFlagNavigation)
            {
                State = AutoPilotState.Arrived;
                StatusText = "Flag route completed.";
                _isFlagNavigation = false;
            }
            else
            {
                Fail("Navigation stopped unexpectedly.");
            }
        }
    }

    private void UpdateAntiStuck(Vector3 position)
    {
        var now = DateTimeOffset.UtcNow;
        if ((now - _lastStuckSampleAt).TotalSeconds < 1) return;

        var moved = Vector3.Distance(position, _lastStuckSamplePos);
        if (moved < _config.MinProgressDistancePerSample)
        {
            _stuckSince ??= now;
            if ((now - _stuckSince.Value).TotalSeconds >= _config.StuckThresholdSeconds)
            {
                _navmesh.TryStop();
                Fail("Stuck detected.");
            }
        }
        else
        {
            _stuckSince = null;
        }

        _lastStuckSampleAt = now;
        _lastStuckSamplePos = position;
    }

    private void ResetMovementTracking()
    {
        if (_clientState.LocalPlayer is not null)
            _lastStuckSamplePos = _clientState.LocalPlayer.Position;
        _lastStuckSampleAt = DateTimeOffset.UtcNow;
        _stuckSince = null;
    }

    private void Fail(string reason)
    {
        State = AutoPilotState.Failed;
        StatusText = reason;
        _destination = null;
        _isFlagNavigation = false;
        _log.Warning("[XIVPathAutopilot] {Reason}", reason);
    }

    public void Dispose()
    {
        _framework.Update -= OnFrameworkUpdate;
    }
}
