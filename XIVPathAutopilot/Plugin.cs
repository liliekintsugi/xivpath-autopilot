using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Globalization;
using XIVPathAutopilot.Integration;
using XIVPathAutopilot.Navigation;
using XIVPathAutopilot.Windows;

namespace XIVPathAutopilot;

public sealed class Plugin : IDalamudPlugin
{
    private readonly ICommandManager _commandManager;
    private readonly WindowSystem _windowSystem = new("XIVPathAutopilot");
    private readonly Configuration _configuration;
    private readonly AutoPilotController _controller;
    private readonly ConfigWindow _configWindow;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IClientState clientState,
        IFramework framework,
        ICondition condition,
        IChatGui chatGui,
        IPluginLog log)
    {
        _commandManager = commandManager;

        _configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        _configuration.Initialize(pluginInterface);

        var navmesh = new VNavmeshIpcClient(pluginInterface, log);
        var mapConverter = new MapCoordinateConverter();
        var questDestinations = new QuestDestinationProvider(log);
        _controller = new AutoPilotController(
            clientState,
            framework,
            log,
            condition,
            chatGui,
            navmesh,
            _configuration);
        _configWindow = new ConfigWindow(_configuration, _controller, mapConverter, questDestinations);

        _windowSystem.AddWindow(_configWindow);
        pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
        pluginInterface.UiBuilder.OpenMainUi += OpenConfig;

        _commandManager.AddHandler("/xivpathauto", new CommandInfo(OnCommand)
        {
            HelpMessage = "Open XIVPath Autopilot config, or use: stop | pause | resume | flag | goto <x> <y> <z>.",
        });
    }

    private void OpenConfig()
    {
        _configWindow.IsOpen = true;
    }

    private void OnCommand(string command, string args)
    {
        var trimmed = args.Trim().ToLowerInvariant();
        switch (trimmed)
        {
            case "stop":
                _controller.Stop();
                break;
            case "pause":
                _controller.Pause();
                break;
            case "resume":
                _controller.Resume();
                break;
            case "flag":
                _controller.StartToFlag();
                break;
            default:
                if (trimmed.StartsWith("goto "))
                {
                    var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 4 &&
                        float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                        float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
                        float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                    {
                        _controller.Start(new System.Numerics.Vector3(x, y, z));
                        break;
                    }
                }
                OpenConfig();
                break;
        }
    }

    public void Dispose()
    {
        _commandManager.RemoveHandler("/xivpathauto");
        _windowSystem.RemoveAllWindows();
        _controller.Dispose();
    }
}
