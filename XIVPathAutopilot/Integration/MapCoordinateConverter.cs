using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace XIVPathAutopilot.Integration;

public sealed class MapCoordinateConverter
{
    public unsafe bool TryMapToWorld(float mapX, float mapY, out Vector3 worldApproximation)
    {
        var mapAgent = AgentMap.Instance();
        if (mapAgent == null || mapAgent->CurrentMapSizeFactor <= 0)
        {
            worldApproximation = default;
            return false;
        }

        var scale = mapAgent->CurrentMapSizeFactor / 100f;
        var worldX = InvertMapCoordinate(mapX, scale, mapAgent->CurrentOffsetX);
        var worldZ = InvertMapCoordinate(mapY, scale, mapAgent->CurrentOffsetY);
        worldApproximation = new Vector3(worldX, 1024f, worldZ);
        return true;
    }

    private static float InvertMapCoordinate(float mapCoord, float scale, int offset)
    {
        var lhs = (mapCoord - 1f) * scale / 41f * 2048f;
        return (lhs - 1024f) / scale - offset;
    }
}
