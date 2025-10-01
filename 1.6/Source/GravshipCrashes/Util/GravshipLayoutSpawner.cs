using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using GravshipExport; // Needed for ShipLayoutDefV2, ShipCell, ShipThingEntry

namespace GravshipCrashes.Util
{
    /// <summary>
    /// Spawns a crashed gravship from a ShipLayoutDefV2 layout.
    /// Pass order: Foundations → Floors → Things → Roofs.
    /// </summary>
    public static class GravshipLayoutSpawner
    {
        public static bool TrySpawnLayout(
            Map map,
            ShipLayoutResolver.ShipEntry entry,
            IntVec3 center,
            List<Thing> placedThings
        )
        {
            if (map == null)
            {
                Log.Warning("[GravshipCrashes] [Spawner] Map is NULL.");
                return false;
            }

            if (entry?.RawDef == null)
            {
                Log.Warning("[GravshipCrashes] [Spawner] Entry or RawDef is NULL.");
                return false;
            }

            var layout = (ShipLayoutDefV2)entry.RawDef;
            if (layout.rows == null || layout.rows.Count == 0)
            {
                Log.Warning("[GravshipCrashes] [Spawner] Layout has no rows.");
                return false;
            }

            int halfWidth = layout.width / 2;
            int halfHeight = layout.height / 2;
            int spawnedCount = 0;
            List<IntVec3> processedCells = new List<IntVec3>();

            // Pass 1️⃣: Foundations (terrain)
            for (int z = 0; z < layout.rows.Count; z++)
            {
                var row = layout.rows[z];
                if (row == null) continue;

                for (int x = 0; x < row.Count; x++)
                {
                    var cell = row[x];
                    if (cell == null || cell.foundationDef.NullOrEmpty()) continue;

                    IntVec3 pos = new IntVec3(center.x - halfWidth + x, 0, center.z - halfHeight + z);
                    if (!pos.InBounds(map)) continue;

                    SpawnFoundationAsTerrain(cell, map, pos, ref spawnedCount);
                    processedCells.Add(pos);
                }
            }

            // Pass 2️⃣: Floors
            for (int z = 0; z < layout.rows.Count; z++)
            {
                var row = layout.rows[z];
                if (row == null) continue;

                for (int x = 0; x < row.Count; x++)
                {
                    var cell = row[x];
                    if (cell == null || cell.terrainDef.NullOrEmpty()) continue;

                    IntVec3 pos = new IntVec3(center.x - halfWidth + x, 0, center.z - halfHeight + z);
                    if (!pos.InBounds(map)) continue;

                    SpawnTerrain(cell, map, pos, ref spawnedCount);
                    processedCells.Add(pos);
                }
            }

            // Pass 3️⃣: Things
            for (int z = 0; z < layout.rows.Count; z++)
            {
                var row = layout.rows[z];
                if (row == null) continue;

                for (int x = 0; x < row.Count; x++)
                {
                    var cell = row[x];
                    if (cell == null || cell.things == null || cell.things.Count == 0) continue;

                    IntVec3 pos = new IntVec3(center.x - halfWidth + x, 0, center.z - halfHeight + z);
                    if (!pos.InBounds(map)) continue;

                    SpawnThings(cell, map, pos, placedThings, ref spawnedCount);
                    processedCells.Add(pos);
                }
            }

            // Pass 4️⃣: Roofs — always build them
            BuildRoofs(map, processedCells);

            return spawnedCount > 0;
        }

        // ---------- Spawn Helpers ----------

        private static void SpawnFoundationAsTerrain(ShipCell cell, Map map, IntVec3 pos, ref int spawnedCount)
        {
            TerrainDef terrain = DefDatabase<TerrainDef>.GetNamedSilentFail(cell.foundationDef);

            if (terrain == null && cell.foundationDef.Equals("Substructure", StringComparison.OrdinalIgnoreCase))
            {
                terrain = TerrainDefOf.MetalTile;
            }

            if (terrain == null) return;

            map.terrainGrid.SetTerrain(pos, terrain);
            spawnedCount++;
        }

        private static void SpawnTerrain(ShipCell cell, Map map, IntVec3 pos, ref int spawnedCount)
        {
            TerrainDef terrain = DefDatabase<TerrainDef>.GetNamedSilentFail(cell.terrainDef);
            if (terrain == null) return;

            map.terrainGrid.SetTerrain(pos, terrain);
            spawnedCount++;
        }

        private static void SpawnThings(ShipCell cell, Map map, IntVec3 pos, List<Thing> placedThings, ref int spawnedCount)
        {
            foreach (var thingEntry in cell.things)
            {
                if (thingEntry == null || thingEntry.defName.NullOrEmpty()) continue;

                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(thingEntry.defName);
                if (def == null) continue;

                ThingDef stuff = !thingEntry.stuffDef.NullOrEmpty()
                    ? DefDatabase<ThingDef>.GetNamedSilentFail(thingEntry.stuffDef)
                    : null;

                Thing thing = ThingMaker.MakeThing(def, stuff);
                Rot4 rot = new Rot4(thingEntry.rotInteger);
                GenSpawn.Spawn(thing, pos, map, rot, WipeMode.Vanish, false);

                placedThings?.Add(thing);
                spawnedCount++;
            }
        }

        // ---------- Roof Logic ----------

        private static void BuildRoofs(Map map, List<IntVec3> cells)
        {
            HashSet<IntVec3> visited = new HashSet<IntVec3>();
            Queue<IntVec3> queue = new Queue<IntVec3>();

            foreach (var start in cells)
            {
                if (!start.InBounds(map) || visited.Contains(start)) continue;
                if (HoldsRoof(map, start)) continue;

                List<IntVec3> region = new List<IntVec3>();
                queue.Enqueue(start);
                visited.Add(start);
                bool touchesEdge = false;

                while (queue.Count > 0)
                {
                    IntVec3 c = queue.Dequeue();
                    region.Add(c);

                    if (IsMapEdge(c, map))
                        touchesEdge = true;

                    foreach (var dir in GenAdj.CardinalDirections)
                    {
                        IntVec3 n = c + dir;
                        if (!n.InBounds(map) || visited.Contains(n)) continue;
                        if (HoldsRoof(map, n)) continue;

                        visited.Add(n);
                        queue.Enqueue(n);
                    }
                }

                if (touchesEdge) continue;

                foreach (var c in region)
                {
                    if (!c.Roofed(map))
                    {
                        map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
                    }
                }
            }
        }

        private static bool HoldsRoof(Map map, IntVec3 c)
        {
            var edifice = c.GetEdifice(map);
            if (edifice != null && (edifice.def.holdsRoof || edifice.def.passability == Traversability.Impassable))
            {
                return true;
            }
            return false;
        }

        private static bool IsMapEdge(IntVec3 c, Map map)
        {
            return c.x <= 0 || c.z <= 0 || c.x >= map.Size.x - 1 || c.z >= map.Size.z - 1;
        }

        /// <summary>
        /// Calculate the bounding rect of placed objects for post-processing.
        /// </summary>
        public static CellRect CalculateBounds(List<Thing> placedThings, Map map)
        {
            if (placedThings == null || placedThings.Count == 0)
            {
                return CellRect.CenteredOn(map.Center, 20, 20);
            }

            var min = new IntVec3(int.MaxValue, 0, int.MaxValue);
            var max = new IntVec3(int.MinValue, 0, int.MinValue);

            foreach (var thing in placedThings)
            {
                if (thing == null) continue;
                min.x = Mathf.Min(min.x, thing.Position.x);
                min.z = Mathf.Min(min.z, thing.Position.z);
                max.x = Mathf.Max(max.x, thing.Position.x);
                max.z = Mathf.Max(max.z, thing.Position.z);
            }

            int width = Mathf.Max(10, max.x - min.x + 10);
            int height = Mathf.Max(10, max.z - min.z + 10);

            return CellRect.CenteredOn(map.Center, width, height).ClipInsideMap(map);
        }
    }
}
