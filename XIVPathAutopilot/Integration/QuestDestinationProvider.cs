using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace XIVPathAutopilot.Integration;

public sealed class QuestDestinationProvider(IPluginLog log)
{
    public sealed record QuestDestination(ushort QuestId, uint TargetMapId, string Label);

    public unsafe IReadOnlyList<QuestDestination> GetActiveDestinations()
    {
        try
        {
            var mapAgent = AgentMap.Instance();
            if (mapAgent == null)
            {
                return [];
            }

            var container = mapAgent->MapQuestLinkContainer;
            var count = Math.Min(container.MarkerCount, (ushort)20);
            if (count == 0)
            {
                return [];
            }

            var result = new List<QuestDestination>(count);
            for (var i = 0; i < count; i++)
            {
                var marker = container.Markers[i];
                if (marker.Valid == 0 || marker.TargetMapId == 0)
                {
                    continue;
                }

                var tooltip = marker.TooltipText.ToString();
                var label = string.IsNullOrWhiteSpace(tooltip)
                    ? $"Quest #{marker.QuestId} (Map {marker.TargetMapId})"
                    : tooltip;
                result.Add(new QuestDestination(marker.QuestId, marker.TargetMapId, label));
            }

            return result;
        }
        catch (Exception ex)
        {
            log.Debug(ex, "[XIVPathAutopilot] Unable to read quest map links");
            return [];
        }
    }

    public unsafe bool TryOpenQuestMap(uint mapId)
    {
        try
        {
            if (mapId == 0)
            {
                return false;
            }

            var mapAgent = AgentMap.Instance();
            if (mapAgent == null)
            {
                return false;
            }

            mapAgent->OpenMapByMapId(mapId);
            return true;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "[XIVPathAutopilot] Unable to open quest map");
            return false;
        }
    }
}
