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
            var settings = Mod_GravshipCrashes.Instance?.Settings;
            var comp = site.GetComponent<WorldObjectComp_CrashedGravship>();

            var usedRect = CellRect.CenteredOn(map.Center, 30, 30);
            var placedThings = new List<Thing>();

            if (shipEntry != null && GravshipLayoutSpawner.TrySpawnLayout(map, shipEntry, usedRect.CenterCell, placedThings))
            {
                usedRect = GravshipLayoutSpawner.CalculateBounds(placedThings, map);
            }
            else
            {
                GenerateFallbackHull(map, usedRect);
            }

            GravshipSpawnUtility.RemoveGravEngines(map, usedRect.Cells);
            GravshipSpawnUtility.ApplyStructureDamage(map, settings?.shipStructureDamageRange ?? new FloatRange(0.15f, 0.45f), comp?.StructureDamageSeed ?? Rand.Int);
            GravshipSpawnUtility.ApplyThingDamage(map, settings?.thingDamageRange ?? new FloatRange(0.1f, 0.35f), comp?.ThingDamageSeed ?? Rand.Int);
            GravshipSpawnUtility.ScatterDebris(map, usedRect, comp?.StructureDamageSeed ?? Rand.Int);
            GravshipSpawnUtility.SpawnLoot(map, usedRect, comp?.LootSeed ?? Rand.Int);

            DefenderGen.SpawnDefenders(map, usedRect, settings);
        }

        private static void GenerateFallbackHull(Map map, CellRect rect)
        {
            rect = rect.ClipInsideMap(map);
            foreach (var cell in rect.EdgeCells)
            {
                var wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.Plasteel);
                GenSpawn.Spawn(wall, cell, map, WipeMode.Vanish);
            }

            foreach (var cell in rect.Cells.InRandomOrder())
            {
                if (!cell.InBounds(map) || !cell.Standable(map))
                {
                    continue;
                }

                if (Rand.Chance(0.2f))
                {
                    var floor = TerrainDefOf.MetalTile;
                    map.terrainGrid.SetTerrain(cell, floor);
                }
            }
        }
    }
}
