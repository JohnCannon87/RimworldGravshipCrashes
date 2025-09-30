using GravshipCrashes.Settings;
using GravshipCrashes.Util;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace GravshipCrashes.Debug
{
    /// <summary>
    /// Adds world debug tools for testing crashed gravship sites.
    /// </summary>
    public static class DebugActionsWorld_CrashedGravship
    {
        [DebugAction("Gravship Crashes", "Spawn Crashed Gravship Site (Dev)", allowedGameStates = AllowedGameStates.WorldView, requiresDevMode = true)]
        public static void SpawnCrashedGravshipSite()
        {
            var settings = Mod_GravshipCrashes.Instance?.Settings;
            if (settings != null && !settings.devEnableWorldSpawnButton)
            {
                Messages.Message("Dev spawn disabled in settings.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            ShipLayoutResolver.RefreshIfNeeded();
            var allowedShips = ShipLayoutResolver.AllowedShips(settings);
            if (allowedShips.Count == 0)
            {
                Messages.Message("No gravship layouts are currently enabled.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            var tile = Find.WorldSelector.SelectedTile;
            if (tile < 0 || !TileFinder.IsValidTileForNewSettlement(tile))
            {
                if (!GravshipSpawnUtility.TryFindSiteTile(out tile))
                {
                    Messages.Message("Could not find a valid tile for the crash site.", MessageTypeDefOf.RejectInput, false);
                    return;
                }
            }

            var entry = allowedShips.RandomElement();
            var site = GravshipSpawnUtility.CreateSite(tile);
            GravshipSpawnUtility.ConfigureSiteMetadata(site, entry);
            GravshipSpawnUtility.ConfigureTimeout(site, new IntRange(6, 10));
            Find.WorldObjects.Add(site);
            Messages.Message("Spawned crashed gravship site.", new GlobalTargetInfo(site.Tile), MessageTypeDefOf.PositiveEvent);
        }
    }
}
