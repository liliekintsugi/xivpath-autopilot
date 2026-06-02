using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace XIVPathAutopilot.Integration;

/// <summary>
/// Small adapter around vnavmesh IPC.
/// Replace IPC channel names with the exact exported names if needed.
/// </summary>
public sealed class VNavmeshIpcClient
{
    private readonly IPluginLog _log;
    private readonly ICallGateSubscriber<Vector3, bool, bool> _pathfindAndMoveTo;
    private readonly ICallGateSubscriber<object> _pathStop;
    private readonly ICallGateSubscriber<bool> _isRunning;
    private readonly ICallGateSubscriber<bool> _pathfindInProgress;
    private readonly ICallGateSubscriber<bool, object> _setMovementAllowed;

    public VNavmeshIpcClient(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        _log = log;

        _pathfindAndMoveTo =
            pluginInterface.GetIpcSubscriber<Vector3, bool, bool>("vnavmesh.SimpleMove.PathfindAndMoveTo");
        _pathStop = pluginInterface.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
        _isRunning = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning");
        _pathfindInProgress = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.SimpleMove.PathfindInProgress");
        _setMovementAllowed =
            pluginInterface.GetIpcSubscriber<bool, object>("vnavmesh.Path.SetMovementAllowed");
    }

    public bool TryStartMoveTo(Vector3 destination, bool preferFlight)
    {
        try
        {
            return _pathfindAndMoveTo.InvokeFunc(destination, preferFlight);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[XIVPathAutopilot] vnavmesh move IPC failed");
            return false;
        }
    }

    public bool TryStartMoveToFlag()
    {
        _log.Warning("[XIVPathAutopilot] Move-to-flag IPC not implemented in this build");
        return false;
    }

    public bool TryStop()
    {
        try
        {
            _pathStop.InvokeAction();
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[XIVPathAutopilot] vnavmesh stop IPC failed");
            return false;
        }
    }

    public bool TryPause()
    {
        try
        {
            _setMovementAllowed.InvokeAction(false);
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[XIVPathAutopilot] vnavmesh pause IPC failed");
            return false;
        }
    }

    public bool TryResume()
    {
        try
        {
            _setMovementAllowed.InvokeAction(true);
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[XIVPathAutopilot] vnavmesh resume IPC failed");
            return false;
        }
    }

    public bool TryIsRunningOrPathfinding(out bool active)
    {
        try
        {
            active = _isRunning.InvokeFunc() || _pathfindInProgress.InvokeFunc();
            return true;
        }
        catch
        {
            active = false;
            return false;
        }
    }
}
