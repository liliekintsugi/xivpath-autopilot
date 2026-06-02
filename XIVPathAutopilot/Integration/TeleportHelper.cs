using Dalamud.Game.ClientState.Aetherytes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace XIVPathAutopilot.Integration;

public sealed class TeleportHelper(IAetheryteList aetheryteList, IPluginLog log)
{
    public bool TryTeleportToTerritory(uint territoryId, out string reason)
    {
        var candidate = aetheryteList
            .Where(a => a.TerritoryId == territoryId && !a.IsApartment && !a.IsSharedHouse)
            .OrderBy(a => a.GilCost)
            .FirstOrDefault();

        if (candidate is null)
        {
            reason = "No unlocked aetheryte for destination territory.";
            return false;
        }

        try
        {
            unsafe
            {
                var telepo = Telepo.Instance();
                if (telepo == null)
                {
                    reason = "Telepo unavailable.";
                    return false;
                }

                var ok = telepo->Teleport(candidate.AetheryteId, candidate.SubIndex);
                reason = ok
                    ? $"Teleporting via aetheryte #{candidate.AetheryteId}..."
                    : "Teleport request rejected by game.";
                return ok;
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "[XIVPathAutopilot] teleport request failed");
            reason = "Teleport call failed.";
            return false;
        }
    }
}
