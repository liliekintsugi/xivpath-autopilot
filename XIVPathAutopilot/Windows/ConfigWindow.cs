using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using XIVPathAutopilot.Integration;
using XIVPathAutopilot.Navigation;

namespace XIVPathAutopilot.Windows;

public sealed class ConfigWindow : Window
{
    private readonly Configuration _config;
    private readonly AutoPilotController _controller;
    private readonly MapCoordinateConverter _mapConverter;
    private readonly QuestDestinationProvider _questDestinationProvider;

    private float _targetX;
    private float _targetY;
    private float _targetZ;
    private bool _inputIsMapCoordinates;
    private int _selectedQuestDestination = -1;
    private List<QuestDestinationProvider.QuestDestination> _questDestinations = [];

    public ConfigWindow(
        Configuration config,
        AutoPilotController controller,
        MapCoordinateConverter mapConverter,
        QuestDestinationProvider questDestinationProvider)
        : base("XIVPath Autopilot###XIVPathAutopilotConfig", ImGuiWindowFlags.AlwaysAutoResize)
    {
        _config = config;
        _controller = controller;
        _mapConverter = mapConverter;
        _questDestinationProvider = questDestinationProvider;
    }

    public override void Draw()
    {
        ImGui.Text("Experimental manual destination autopilot");
        ImGui.Separator();

        ImGui.Text("Destination");
        ImGui.Checkbox("Input is map coordinates (X,Y)", ref _inputIsMapCoordinates);
        ImGui.SetNextItemWidth(120);
        ImGui.InputFloat("X", ref _targetX);
        ImGui.SetNextItemWidth(120);
        ImGui.InputFloat(_inputIsMapCoordinates ? "Map Y" : "Y", ref _targetY);
        ImGui.SetNextItemWidth(120);
        ImGui.InputFloat(_inputIsMapCoordinates ? "Unused Z" : "Z", ref _targetZ);

        if (ImGui.Button("Start"))
        {
            if (_inputIsMapCoordinates)
            {
                if (_mapConverter.TryMapToWorld(_targetX, _targetY, out var world))
                {
                    _controller.Start(world);
                }
            }
            else
            {
                _controller.Start(new Vector3(_targetX, _targetY, _targetZ));
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Go To Flag"))
        {
            _controller.StartToFlag();
        }
        ImGui.SameLine();
        if (ImGui.Button("Pause")) _controller.Pause();
        ImGui.SameLine();
        if (ImGui.Button("Resume")) _controller.Resume();
        ImGui.SameLine();
        if (ImGui.Button("Stop")) _controller.Stop();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Quest destinations (active journal links)");
        if (ImGui.Button("Refresh quest destinations"))
        {
            _questDestinations = _questDestinationProvider.GetActiveDestinations().ToList();
            if (_questDestinations.Count == 0)
            {
                _selectedQuestDestination = -1;
            }
            else if (_selectedQuestDestination >= _questDestinations.Count)
            {
                _selectedQuestDestination = 0;
            }
        }

        if (_questDestinations.Count > 0)
        {
            var labels = _questDestinations.Select(q => $"{q.Label} (Map {q.TargetMapId})").ToArray();
            ImGui.SetNextItemWidth(420);
            ImGui.Combo("##questDestinations", ref _selectedQuestDestination, labels, labels.Length);

            if (_selectedQuestDestination >= 0 &&
                _selectedQuestDestination < _questDestinations.Count &&
                ImGui.Button("Open selected quest map"))
            {
                _questDestinationProvider.TryOpenQuestMap(_questDestinations[_selectedQuestDestination].TargetMapId);
            }

            ImGui.SameLine();
            ImGui.TextDisabled("Then place a flag and use Go To Flag");
        }
        else
        {
            ImGui.TextDisabled("No active quest destination links found.");
        }

        ImGui.Spacing();
        ImGui.Text($"State: {_controller.State}");
        ImGui.TextWrapped($"Status: {_controller.StatusText}");

        ImGui.Separator();
        var timeout = _config.EnableSafetyTimeout;
        if (ImGui.Checkbox("Enable safety timeout", ref timeout))
        {
            _config.EnableSafetyTimeout = timeout;
            _config.Save();
        }

        var timeoutSeconds = _config.SafetyTimeoutSeconds;
        if (ImGui.InputInt("Timeout seconds", ref timeoutSeconds))
        {
            _config.SafetyTimeoutSeconds = Math.Clamp(timeoutSeconds, 10, 3600);
            _config.Save();
        }

        var arrivalDistance = _config.ArrivalDistance;
        if (ImGui.SliderFloat("Arrival distance", ref arrivalDistance, 0.5f, 10.0f))
        {
            _config.ArrivalDistance = arrivalDistance;
            _config.Save();
        }

        var useMount = _config.UseMountWhenPossible;
        if (ImGui.Checkbox("Use mount when possible", ref useMount))
        {
            _config.UseMountWhenPossible = useMount;
            _config.Save();
        }

        var preferFlight = _config.PreferFlyingWhenPossible;
        if (ImGui.Checkbox("Prefer flying when possible", ref preferFlight))
        {
            _config.PreferFlyingWhenPossible = preferFlight;
            _config.Save();
        }

        var antiStuck = _config.EnableAntiStuck;
        if (ImGui.Checkbox("Enable anti-stuck", ref antiStuck))
        {
            _config.EnableAntiStuck = antiStuck;
            _config.Save();
        }

        var stuckSeconds = _config.StuckThresholdSeconds;
        if (ImGui.InputInt("Stuck threshold (s)", ref stuckSeconds))
        {
            _config.StuckThresholdSeconds = Math.Clamp(stuckSeconds, 3, 120);
            _config.Save();
        }
    }
}
