using System.Numerics;
using Dalamud.Game.Command;
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
    private readonly ICommandManager _commandManager;
    private readonly VNavmeshIpcClient _navmesh;
    private readonly TeleportHelper _teleportHelper;
    private readonly FlagDestinationProvider _flagDestinationProvider;
    private readonly Configuration _config;
    private DateTimeOffset _lastMountAttemptAt = DateTimeOffset.MinValue;

    private DateTimeOffset _startedAt;
    private Vector3? _destination;
    private bool _isFlagNavigation;
    private bool _waitingForTeleportZoneChange;
    private uint _pendingFlagTerritoryId;
    private DateTimeOffset _teleportRequestedAt;
    public AutoPilotState State { get; private set; } = AutoPilotState.Idle;
    public string StatusText { get; private set; } = "Idle";

    public AutoPilotController(
        IClientState clientState,
        IFramework framework,
        IPluginLog log,
        ICondition condition,
        ICommandManager commandManager,
        VNavmeshIpcClient navmesh,
        TeleportHelper teleportHelper,
        FlagDestinationProvider flagDestinationProvider,
        Configuration config)
    {
        _clientState = clientState;
        _framework = framework;
        _log = log;
        _condition = condition;
        _commandManager = commandManager;
        _navmesh = navmesh;
        _teleportHelper = teleportHelper;
        _flagDestinationProvider = flagDestinationProvider;
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

        if (_config.AutoTeleportOnZoneChangeForFlag &&
            _flagDestinationProvider.TryGetFlagDestination(out var flagDestination) &&
            flagDestination.TerritoryId != 0 &&
            flagDestination.TerritoryId != _clientState.TerritoryType)
        {
            if (_teleportHelper.TryTeleportToTerritory(flagDestination.TerritoryId, out var teleportReason))
            {
                _startedAt = DateTimeOffset.UtcNow;
                _destination = null;
                _isFlagNavigation = true;
                _waitingForTeleportZoneChange = true;
                _pendingFlagTerritoryId = flagDestination.TerritoryId;
                _teleportRequestedAt = DateTimeOffset.UtcNow;
                State = AutoPilotState.Moving;
                StatusText = teleportReason;
                return true;
            }

            StatusText = $"Teleport unavailable ({teleportReason}), trying direct route...";
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
        _waitingForTeleportZoneChange = false;
        _pendingFlagTerritoryId = 0;
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
        _waitingForTeleportZoneChange = false;
        _pendingFlagTerritoryId = 0;
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

        if (_waitingForTeleportZoneChange)
        {
            if (_clientState.TerritoryType == _pendingFlagTerritoryId)
            {
                _waitingForTeleportZoneChange = false;
                _pendingFlagTerritoryId = 0;
                TryAutoMount();
                var preferFlyParam = _config.PreferFlyingWhenPossible || _config.UseMountWhenPossible;
                if (!_navmesh.TryStartMoveToFlag(preferFlyParam))
                {
                    Fail("Teleported, but unable to start move-to-flag.");
                    return;
                }

                StatusText = "Zone changed, moving to map flag...";
                _startedAt = DateTimeOffset.UtcNow;
                return;
            }

            if ((DateTimeOffset.UtcNow - _teleportRequestedAt).TotalSeconds > 30)
            {
                Fail("Zone change timeout after teleport request.");
            }

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
        _waitingForTeleportZoneChange = false;
        _pendingFlagTerritoryId = 0;
        _log.Warning("[XIVPathAutopilot] {Reason}", reason);
    }

    private void TryAutoMount()
    {
        if (!_config.UseMountWhenPossible) return;
        if (_condition[ConditionFlag.Mounted]) return;
        if ((DateTimeOffset.UtcNow - _lastMountAttemptAt).TotalSeconds < 3) return;

        if (_commandManager.ProcessCommand("/mount roulette"))
        {
            _lastMountAttemptAt = DateTimeOffset.UtcNow;
        }
    }

    public void Dispose()
    {
        _framework.Update -= OnFrameworkUpdate;
    }
}
