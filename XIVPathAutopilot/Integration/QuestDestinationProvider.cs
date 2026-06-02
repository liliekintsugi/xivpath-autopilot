using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
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

            var result = new List<QuestDestination>(20);
            var seen = new HashSet<(ushort QuestId, uint MapId)>();

            CollectContainer(mapAgent->MapQuestLinkContainer, result, seen);
            CollectContainer(mapAgent->MiniMapQuestLinkContainer, result, seen);
            CollectAcceptedQuestsFallback(result, seen);

            return result;
        }
        catch (Exception ex)
        {
            log.Debug(ex, "[XIVPathAutopilot] Unable to read quest map links");
            return [];
        }
    }

    private static unsafe void CollectContainer(
        QuestLinkContainer container,
        List<QuestDestination> result,
        HashSet<(ushort QuestId, uint MapId)> seen)
    {
        var count = Math.Min(container.MarkerCount, (ushort)20);
        for (var i = 0; i < count; i++)
        {
            var marker = container.Markers[i];
            if (marker.Valid == 0)
            {
                continue;
            }

            var mapId = marker.TargetMapId != 0 ? marker.TargetMapId : marker.SourceMapId;
            if (mapId == 0)
            {
                continue;
            }

            if (!seen.Add((marker.QuestId, mapId)))
            {
                continue;
            }

            var tooltip = marker.TooltipText.ToString();
            var label = string.IsNullOrWhiteSpace(tooltip)
                ? $"Quest #{marker.QuestId} (Map {mapId})"
                : tooltip;
            result.Add(new QuestDestination(marker.QuestId, mapId, label));
        }
    }

    private static unsafe void CollectAcceptedQuestsFallback(
        List<QuestDestination> result,
        HashSet<(ushort QuestId, uint MapId)> seen)
    {
        var questManager = QuestManager.Instance();
        if (questManager == null)
        {
            return;
        }

        foreach (ref var entry in questManager->NormalQuests)
        {
            if (entry.QuestId == 0)
            {
                continue;
            }

            var alreadyPresent = false;
            for (var idx = 0; idx < result.Count; idx++)
            {
                if (result[idx].QuestId == entry.QuestId)
                {
                    alreadyPresent = true;
                    break;
                }
            }

            if (alreadyPresent)
            {
                continue;
            }

            if (!seen.Add((entry.QuestId, 0)))
            {
                continue;
            }

            result.Add(new QuestDestination(entry.QuestId, 0, $"Quest #{entry.QuestId}"));
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

    public unsafe bool TryOpenQuestDestination(QuestDestination destination)
    {
        if (destination.TargetMapId != 0)
        {
            return TryOpenQuestMap(destination.TargetMapId);
        }
        
        // We only have an accepted quest id without a reliable target map.
        // Opening quest-log map directly can land on arbitrary coordinates on some clients.
        return false;
    }
}
