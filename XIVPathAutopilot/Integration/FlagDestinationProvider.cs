using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace XIVPathAutopilot.Integration;

public sealed class FlagDestinationProvider
{
    public sealed record FlagDestination(uint TerritoryId, uint MapId, Vector3 WorldPosition);

    public unsafe bool TryGetFlagDestination(out FlagDestination destination)
    {
        var mapAgent = AgentMap.Instance();
        if (mapAgent == null || mapAgent->FlagMarkerCount == 0)
        {
            destination = default!;
            return false;
        }

        var marker = mapAgent->FlagMapMarkers[0];
        destination = new FlagDestination(
            marker.TerritoryId,
            marker.MapId,
            new Vector3(marker.XFloat, 1024f, marker.YFloat));
        return true;
    }
}
