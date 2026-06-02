using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
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
    private readonly ICondition _condition;
    private readonly IChatGui _chatGui;
    private readonly VNavmeshIpcClient _navmesh;
    private readonly Configuration _config;
    private DateTimeOffset _lastMountAttemptAt = DateTimeOffset.MinValue;

    private DateTimeOffset _startedAt;
    private Vector3? _destination;
    private bool _isFlagNavigation;
    public AutoPilotState State { get; private set; } = AutoPilotState.Idle;
    public string StatusText { get; private set; } = "Idle";

    public AutoPilotController(
        IClientState clientState,
        IFramework framework,
        IPluginLog log,
        ICondition condition,
        IChatGui chatGui,
        VNavmeshIpcClient navmesh,
        Configuration config)
    {
        _clientState = clientState;
        _framework = framework;
        _log = log;
        _condition = condition;
        _chatGui = chatGui;
        _navmesh = navmesh;
        _config = config;

        _framework.Update += OnFrameworkUpdate;
    }

    public bool Start(Vector3 destination)
    {
        if (!_clientState.IsLoggedIn)
        {
            Fail("Not logged in.");
            return false;
        }

        TryAutoMount();

        if (_navmesh.TrySnapToNearestPoint(destination, out var snapped))
        {
            destination = snapped;
        }

        var preferFlyParam = _config.PreferFlyingWhenPossible || _config.UseMountWhenPossible;
        if (!_navmesh.TryStartMoveTo(destination, preferFlyParam))
        {
            Fail("Unable to start vnavmesh route.");
            return false;
        }

        _startedAt = DateTimeOffset.UtcNow;
        _destination = destination;
        _isFlagNavigation = false;
        State = AutoPilotState.Moving;
        StatusText = $"Moving to {destination.X:F1}, {destination.Y:F1}, {destination.Z:F1}";
        return true;
    }

    public bool StartToFlag()
    {
        if (!_clientState.IsLoggedIn)
        {
            Fail("Not logged in.");
            return false;
        }

        TryAutoMount();

        var preferFlyParam = _config.PreferFlyingWhenPossible || _config.UseMountWhenPossible;
        if (!_navmesh.TryStartMoveToFlag(preferFlyParam))
        {
            Fail("Unable to start move-to-flag.");
            return false;
        }

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
        if (!_clientState.IsLoggedIn)
        {
            Fail("Player unavailable.");
            return;
        }

        if (_config.EnableSafetyTimeout &&
            (DateTimeOffset.UtcNow - _startedAt).TotalSeconds > _config.SafetyTimeoutSeconds)
        {
            _navmesh.TryStop();
            Fail("Safety timeout reached.");
            return;
        }

        if (_navmesh.TryIsRunningOrPathfinding(out var running) && !running)
        {
            if (_isFlagNavigation)
            {
                State = AutoPilotState.Arrived;
                StatusText = "Flag route completed.";
                _isFlagNavigation = false;
                _destination = null;
            }
            else
            {
                State = AutoPilotState.Arrived;
                StatusText = "Route completed.";
                _destination = null;
            }
        }
    }

    private void Fail(string reason)
    {
        State = AutoPilotState.Failed;
        StatusText = reason;
        _destination = null;
        _isFlagNavigation = false;
        _log.Warning("[XIVPathAutopilot] {Reason}", reason);
    }

    private void TryAutoMount()
    {
        if (!_config.UseMountWhenPossible) return;
        if (_condition[ConditionFlag.Mounted]) return;
        if ((DateTimeOffset.UtcNow - _lastMountAttemptAt).TotalSeconds < 3) return;

        if (_chatGui.SendMessage("/mount roulette"))
        {
            _lastMountAttemptAt = DateTimeOffset.UtcNow;
        }
    }

    public void Dispose()
    {
        _framework.Update -= OnFrameworkUpdate;
    }
}
