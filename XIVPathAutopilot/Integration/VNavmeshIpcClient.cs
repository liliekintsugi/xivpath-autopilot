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
    private readonly ICallGateSubscriber<Vector3, float, float, Vector3?> _nearestPoint;
    private readonly ICallGateSubscriber<Vector3?> _flagToPoint;

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
        _nearestPoint =
            pluginInterface.GetIpcSubscriber<Vector3, float, float, Vector3?>("vnavmesh.Query.Mesh.NearestPoint");
        _flagToPoint = pluginInterface.GetIpcSubscriber<Vector3?>("vnavmesh.Query.Mesh.FlagToPoint");
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

    public bool TryStartMoveToFlag(bool preferFlight)
    {
        try
        {
            var point = _flagToPoint.InvokeFunc();
            if (point is null)
            {
                _log.Warning("[XIVPathAutopilot] No map flag found");
                return false;
            }

            return _pathfindAndMoveTo.InvokeFunc(point.Value, preferFlight);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[XIVPathAutopilot] vnavmesh move-to-flag IPC failed");
            return false;
        }
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

    public bool TrySnapToNearestPoint(Vector3 approximatePosition, out Vector3 snappedPosition)
    {
        try
        {
            // Search in a moderate local radius and wide vertical range.
            var point = _nearestPoint.InvokeFunc(approximatePosition, 15.0f, 2048.0f);
            if (point is null)
            {
                snappedPosition = approximatePosition;
                return false;
            }

            snappedPosition = point.Value;
            return true;
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "[XIVPathAutopilot] vnavmesh nearest-point IPC unavailable");
            snappedPosition = approximatePosition;
            return false;
        }
    }
}
