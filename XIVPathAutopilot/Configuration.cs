using Dalamud.Configuration;
using Dalamud.Plugin;

namespace XIVPathAutopilot;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool EnableSafetyTimeout { get; set; } = true;
    public int SafetyTimeoutSeconds { get; set; } = 180;
    public float ArrivalDistance { get; set; } = 2.0f;
    public bool UseMountWhenPossible { get; set; } = true;
    public bool PreferFlyingWhenPossible { get; set; } = true;
    public bool AutoTeleportOnZoneChangeForFlag { get; set; } = true;
    public bool EnableAntiStuck { get; set; } = true;
    public int StuckThresholdSeconds { get; set; } = 10;
    public float MinProgressDistancePerSample { get; set; } = 0.15f;

    [NonSerialized]
    private IDalamudPluginInterface? _pluginInterface;

    public void Initialize(IDalamudPluginInterface pluginInterface)
        => _pluginInterface = pluginInterface;

    public void Save()
        => _pluginInterface?.SavePluginConfig(this);
}
