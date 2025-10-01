using System.Collections.Generic;
using GravshipCrashes.Settings;
using GravshipCrashes.Util;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace GravshipCrashes.Sites
{
    /// <summary>
    /// Responsible for placing the crashed gravship, damage, loot and defenders on the generated map.
    /// </summary>
    public static class MapGenerator_CrashedGravship
    {
        public static void Generate(Map map, Site site, ShipLayoutResolver.ShipEntry shipEntry)
        {
            Log.Message("[GravshipCrashes] Generate() called.");

            if (map == null)
            {
                Log.Error("[GravshipCrashes] Map is NULL!");
                return;
            }

            if (site == null)
            {
                Log.Warning("[GravshipCrashes] Site is NULL!");
            }

            if (shipEntry == null)
            {
                Log.Warning("[GravshipCrashes] shipEntry is NULL - fallback hull will be used.");
            }
            else
            {
                Log.Message($"[GravshipCrashes] shipEntry: {shipEntry.Def.defName}");
            }

            var settings = Mod_GravshipCrashes.Instance?.Settings;
            if (settings == null)
            {
                Log.Warning("[GravshipCrashes] Settings is NULL - using default ranges.");
            }

            var comp = site?.GetComponent<WorldObjectComp_CrashedGravship>();
            if (comp == null)
            {
                Log.Warning("[GravshipCrashes] WorldObjectComp_CrashedGravship is NULL - damage seeds may be random.");
            }

            var usedRect = CellRect.CenteredOn(map.Center, 30, 30);
            var placedThings = new List<Thing>();

            Log.Message("[GravshipCrashes] Attempting to spawn ship layout...");

            bool spawnedLayout = false;
            if (shipEntry != null)
            {
                spawnedLayout = GravshipLayoutSpawner.TrySpawnLayout(map, shipEntry, usedRect.CenterCell, placedThings);
                Log.Message($"[GravshipCrashes] TrySpawnLayout returned: {spawnedLayout} (Placed {placedThings.Count} things)");
            }

            if (spawnedLayout)
            {
                usedRect = GravshipLayoutSpawner.CalculateBounds(placedThings, map);
                Log.Message($"[GravshipCrashes] Calculated usedRect: {usedRect}");
            }
            else
            {
                Log.Warning("[GravshipCrashes] Ship layout spawn failed. Using fallback hull.");
                GenerateFallbackHull(map, usedRect);
            }

            Log.Message("[GravshipCrashes] Removing grav engines...");
            GravshipSpawnUtility.RemoveGravEngines(map, usedRect.Cells);

            Log.Message("[GravshipCrashes] Applying structure damage...");
            GravshipSpawnUtility.ApplyStructureDamage(map, settings?.shipStructureDamageRange ?? new FloatRange(0.15f, 0.45f), comp?.StructureDamageSeed ?? Rand.Int);

            Log.Message("[GravshipCrashes] Applying thing damage...");
            GravshipSpawnUtility.ApplyThingDamage(map, settings?.thingDamageRange ?? new FloatRange(0.1f, 0.35f), comp?.ThingDamageSeed ?? Rand.Int);

            Log.Message("[GravshipCrashes] Scattering debris...");
            GravshipSpawnUtility.ScatterDebris(map, usedRect, comp?.StructureDamageSeed ?? Rand.Int);

            Log.Message("[GravshipCrashes] Spawning loot...");
            GravshipSpawnUtility.SpawnLoot(map, usedRect, comp?.LootSeed ?? Rand.Int);

            Log.Message("[GravshipCrashes] Spawning defenders...");
            DefenderGen.SpawnDefenders(map, usedRect, settings);

            Log.Message("[GravshipCrashes] Generate() finished.");
        }

        private static void GenerateFallbackHull(Map map, CellRect rect)
        {
            Log.Message("[GravshipCrashes] Generating fallback hull...");
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
                    var floor = TerrainDefOf.MetalTile;
                    map.terrainGrid.SetTerrain(cell, floor);
                    floorCount++;
                }
            }

            Log.Message($"[GravshipCrashes] Fallback hull generation complete. Placed floor on {floorCount} cells.");
        }
    }
}
