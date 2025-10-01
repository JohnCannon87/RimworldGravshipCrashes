using System.Collections.Generic;
using GravshipCrashes.Settings;
using GravshipCrashes.Util;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using GravshipExport; // ShipLayoutDefV2

namespace GravshipCrashes.Sites
{
    /// <summary>
    /// Responsible for placing the crashed gravship, damage, loot and defenders on the generated map.
    /// </summary>
    public static class MapGenerator_CrashedGravship
    {
        public static void Generate(Map map, Site site, ShipLayoutDefV2 layout)
        {
            GravshipDebugUtil.LogMessage("[Generator] Generate() called.");

            if (map == null)
            {
                GravshipDebugUtil.LogError("[Generator] Map is NULL!");
                return;
            }

            if (site == null)
            {
                GravshipDebugUtil.LogWarning("[Generator] Site is NULL!");
            }

            if (layout == null)
            {
                GravshipDebugUtil.LogWarning("[Generator] layout is NULL - fallback hull will be used.");
            }
            else
            {
                GravshipDebugUtil.LogMessage(string.Format("[Generator] layout: {0}", layout.defName));
            }

            var settings = Mod_GravshipCrashes.Instance != null ? Mod_GravshipCrashes.Instance.Settings : null;
            var comp = site != null ? site.GetComponent<WorldObjectComp_CrashedGravship>() : null;

            var usedRect = CellRect.CenteredOn(map.Center, 30, 30);
            var placedThings = new List<Thing>();

            GravshipDebugUtil.LogMessage("[Generator] Attempting to spawn ship layout...");

            bool spawnedLayout = false;
            if (layout != null)
            {
                spawnedLayout = GravshipLayoutSpawner.TrySpawnLayout(map, layout, usedRect.CenterCell, placedThings);
                GravshipDebugUtil.LogMessage(string.Format(
                    "[Generator] TrySpawnLayout returned: {0} (Placed {1} things)", spawnedLayout, placedThings.Count));
            }

            if (spawnedLayout)
            {
                usedRect = GravshipLayoutSpawner.CalculateBounds(placedThings, map);
                GravshipDebugUtil.LogMessage(string.Format("[Generator] Calculated usedRect: {0}", usedRect));
            }
            else
            {
                GravshipDebugUtil.LogWarning("[Generator] Ship layout spawn failed. Using fallback hull.");
                GenerateFallbackHull(map, usedRect);
            }

            GravshipDebugUtil.LogMessage("[Generator] Removing grav engines...");
            GravshipSpawnUtility.RemoveGravEngines(map, usedRect.Cells); // harmless if none exist

            GravshipDebugUtil.LogMessage("[Generator] Applying structure damage...");
            GravshipSpawnUtility.ApplyStructureDamage(
                map,
                settings != null ? settings.shipStructureDamageRange : new FloatRange(0.15f, 0.45f),
                comp != null ? comp.StructureDamageSeed : Rand.Int);

            GravshipDebugUtil.LogMessage("[Generator] Applying thing damage...");
            GravshipSpawnUtility.ApplyThingDamage(
                map,
                settings != null ? settings.thingDamageRange : new FloatRange(0.1f, 0.35f),
                comp != null ? comp.ThingDamageSeed : Rand.Int);

            GravshipDebugUtil.LogMessage("[Generator] Scattering debris...");
            GravshipSpawnUtility.ScatterDebris(
                map, usedRect, comp != null ? comp.StructureDamageSeed : Rand.Int);

            GravshipDebugUtil.LogMessage("[Generator] Spawning loot...");
            GravshipSpawnUtility.SpawnLoot(
                map, usedRect, comp != null ? comp.LootSeed : Rand.Int);

            GravshipDebugUtil.LogMessage("[Generator] Spawning defenders...");
            DefenderGen.SpawnDefenders(map, usedRect, settings);

            GravshipDebugUtil.LogMessage("[Generator] Generate() finished.");
        }

        private static void GenerateFallbackHull(Map map, CellRect rect)
        {
            GravshipDebugUtil.LogMessage("[Generator] Generating fallback hull...");
            rect = rect.ClipInsideMap(map);

            foreach (var cell in rect.EdgeCells)
            {
                var wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.Plasteel);
                GenSpawn.Spawn(wall, cell, map, WipeMode.Vanish);
            }

            int floorCount = 0;
            foreach (var cell in rect.Cells.InRandomOrder())
            {
                if (!cell.InBounds(map) || !cell.Standable(map))
                    continue;

                if (Rand.Chance(0.2f))
                {
                    map.terrainGrid.SetTerrain(cell, TerrainDefOf.MetalTile);
                    floorCount++;
                }
            }

            GravshipDebugUtil.LogMessage(string.Format(
                "[Generator] Fallback hull generation complete. Placed floor on {0} cells.", floorCount));
        }
    }
}
