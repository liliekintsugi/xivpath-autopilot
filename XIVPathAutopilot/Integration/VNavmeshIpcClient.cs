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
    private readonly ICallGateSubscriber<Vector3, bool> _moveTo;
    private readonly ICallGateSubscriber<bool> _moveToFlag;
    private readonly ICallGateSubscriber<bool> _stop;
    private readonly ICallGateSubscriber<bool> _pause;
    private readonly ICallGateSubscriber<bool> _resume;
    private readonly ICallGateSubscriber<bool> _isRunning;
    private readonly ICallGateSubscriber<bool, bool, bool> _setTravelPreferences;

    public VNavmeshIpcClient(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        _log = log;

        // NOTE: these names may differ depending on vnavmesh release.
        _moveTo = pluginInterface.GetIpcSubscriber<Vector3, bool>("vnavmesh.PathfindAndMoveTo");
        _moveToFlag = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.PathfindAndMoveToFlag");
        _stop = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Stop");
        _pause = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Pause");
        _resume = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Resume");
        _isRunning = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.IsRunning");
        _setTravelPreferences =
            pluginInterface.GetIpcSubscriber<bool, bool, bool>("vnavmesh.SetTravelPreferences");
    }

    public bool TryStartMoveTo(Vector3 destination)
    {
        try
        {
            return _moveTo.InvokeFunc(destination);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[XIVPathAutopilot] vnavmesh move IPC failed");
            return false;
        }
    }

    public bool TryStartMoveToFlag()
    {
        try
        {
            return _moveToFlag.InvokeFunc();
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[XIVPathAutopilot] vnavmesh move-to-flag IPC failed");
            return false;
        }
    }

    public bool TryConfigureTravelPreferences(bool useMount, bool preferFlight)
    {
        try
        {
            return _setTravelPreferences.InvokeFunc(useMount, preferFlight);
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "[XIVPathAutopilot] vnavmesh travel preference IPC unavailable");
            return false;
        }
    }

    public bool TryStop()
    {
        try
        {
            return _stop.InvokeFunc();
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
            return _pause.InvokeFunc();
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
            return _resume.InvokeFunc();
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[XIVPathAutopilot] vnavmesh resume IPC failed");
            return false;
        }
    }

    public bool TryIsRunning(out bool running)
    {
        try
        {
            running = _isRunning.InvokeFunc();
            return true;
        }
        catch
        {
            running = false;
            return false;
        }
    }
}
