using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using GravshipExport; // ShipLayoutDefV2, ShipCell, ShipThingEntry

namespace GravshipCrashes.Util
{
    /// <summary>
    /// Spawns a crashed gravship from a ShipLayoutDefV2 layout.
    /// Pass order: Foundations → Floors → Things → Roofs, then BrokenGravEngine at layout coords.
    /// </summary>
    public static class GravshipLayoutSpawner
    {
        public static bool TrySpawnLayout(
            Map map,
            ShipLayoutDefV2 layout,
            IntVec3 center,
            List<Thing> placedThings)
        {
            if (map == null)
            {
                GravshipDebugUtil.LogWarning("[Spawner] Map is NULL.");
                return false;
            }

            if (layout == null)
            {
                GravshipDebugUtil.LogWarning("[Spawner] Layout is NULL.");
                return false;
            }

            if (layout.rows == null || layout.rows.Count == 0)
            {
                GravshipDebugUtil.LogWarning("[Spawner] Layout has no rows.");
                return false;
            }

            GravshipDebugUtil.LogMessage(
                string.Format("[Spawner] Spawning layout: {0} ({1}x{2})", layout.defName, layout.width, layout.height));

            int halfWidth = layout.width / 2;
            int halfHeight = layout.height / 2;
            int spawnedCount = 0;
            List<IntVec3> processedCells = new List<IntVec3>();

            // 1) Foundations
            GravshipDebugUtil.LogMessage("[Spawner] Pass 1: Foundations");
            for (int z = 0; z < layout.rows.Count; z++)
            {
                var row = layout.rows[z];
                if (row == null) continue;

                for (int x = 0; x < row.Count; x++)
                {
                    var cell = row[x];
                    if (cell == null || string.IsNullOrEmpty(cell.foundationDef)) continue;

                    IntVec3 pos = new IntVec3(center.x - halfWidth + x, 0, center.z - halfHeight + z);
                    if (!pos.InBounds(map)) continue;

                    SpawnFoundationAsTerrain(cell, map, pos, ref spawnedCount);
                    processedCells.Add(pos);
                }
            }

            // 2) Floors
            GravshipDebugUtil.LogMessage("[Spawner] Pass 2: Floors");
            for (int z = 0; z < layout.rows.Count; z++)
            {
                var row = layout.rows[z];
                if (row == null) continue;

                for (int x = 0; x < row.Count; x++)
                {
                    var cell = row[x];
                    if (cell == null || string.IsNullOrEmpty(cell.terrainDef)) continue;

                    IntVec3 pos = new IntVec3(center.x - halfWidth + x, 0, center.z - halfHeight + z);
                    if (!pos.InBounds(map)) continue;

                    SpawnTerrain(cell, map, pos, ref spawnedCount);
                    processedCells.Add(pos);
                }
            }

            // 3) Things
            GravshipDebugUtil.LogMessage("[Spawner] Pass 3: Things");
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

            // 4) Roofs
            GravshipDebugUtil.LogMessage("[Spawner] Pass 4: Roofs");
            BuildRoofs(map, processedCells);

            // 5) Place the Broken Grav Engine at exporter coords
            if (layout.gravEngineX >= 0 && layout.gravEngineZ >= 0)
            {
                IntVec3 enginePos = new IntVec3(center.x - halfWidth + layout.gravEngineX, 0,
                                                center.z - halfHeight + layout.gravEngineZ);
                if (enginePos.InBounds(map))
                {
                    GravshipDebugUtil.LogMessage(string.Format(
                        "[Spawner] Placing BrokenGravEngine at layout coords ({0},{1}) -> map {2}",
                        layout.gravEngineX, layout.gravEngineZ, enginePos));

                    // default facing; adjust if you later add rot to the exporter
                    SpawnBrokenGravEngineAt(map, enginePos, Rot4.North);
                }
                else
                {
                    GravshipDebugUtil.LogWarning("[Spawner] Computed BrokenGravEngine position is out of bounds.");
                }
            }
            else
            {
                GravshipDebugUtil.LogMessage("[Spawner] Layout has no gravEngineX/Z; skipping BrokenGravEngine placement.");
            }

            GravshipDebugUtil.LogMessage(string.Format(
                "[Spawner] Layout spawn complete. Placed {0} objects total.", spawnedCount));
            return spawnedCount > 0;
        }

        // ---------- Spawn Helpers ----------
        private static void SpawnBrokenGravEngineAt(Map map, IntVec3 cell, Rot4 rot)
        {
            var brokenDef = DefDatabase<ThingDef>.GetNamedSilentFail("BrokenGravEngine");
            if (brokenDef == null)
            {
                GravshipDebugUtil.LogWarning("[SpawnUtility] BrokenGravEngine def missing - falling back to slag.");
                var slag = ThingMaker.MakeThing(ThingDefOf.ChunkSlagSteel);
                GenPlace.TryPlaceThing(slag, cell, map, ThingPlaceMode.Direct);
                return;
            }

            GravshipDebugUtil.LogMessage(string.Format("[SpawnUtility] Spawning BrokenGravEngine at {0}", cell));
            var wreck = ThingMaker.MakeThing(brokenDef);
            GenSpawn.Spawn(wreck, cell, map, rot, WipeMode.Vanish);
        }

        private static void SpawnFoundationAsTerrain(ShipCell cell, Map map, IntVec3 pos, ref int spawnedCount)
        {
            TerrainDef terrain = DefDatabase<TerrainDef>.GetNamedSilentFail(cell.foundationDef);

            if (terrain == null && string.Equals(cell.foundationDef, "Substructure", System.StringComparison.OrdinalIgnoreCase))
            {
                terrain = TerrainDefOf.MetalTile;
                GravshipDebugUtil.LogMessage(string.Format(
                    "[Spawner] Substructure replaced with MetalTile at {0}", pos));
            }

            if (terrain == null)
            {
                GravshipDebugUtil.LogWarning(string.Format(
                    "[Spawner] Unknown foundation '{0}' at {1}", cell.foundationDef, pos));
                return;
            }

            map.terrainGrid.SetTerrain(pos, terrain);
            spawnedCount++;
        }

        private static void SpawnTerrain(ShipCell cell, Map map, IntVec3 pos, ref int spawnedCount)
        {
            TerrainDef terrain = DefDatabase<TerrainDef>.GetNamedSilentFail(cell.terrainDef);
            if (terrain == null)
            {
                GravshipDebugUtil.LogWarning(string.Format(
                    "[Spawner] Unknown terrain '{0}' at {1}", cell.terrainDef, pos));
                return;
            }

            map.terrainGrid.SetTerrain(pos, terrain);
            spawnedCount++;
        }

        private static void SpawnThings(ShipCell cell, Map map, IntVec3 pos, List<Thing> placedThings, ref int spawnedCount)
        {
            for (int i = 0; i < cell.things.Count; i++)
            {
                var thingEntry = cell.things[i];
                if (thingEntry == null || string.IsNullOrEmpty(thingEntry.defName)) continue;

                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(thingEntry.defName);
                if (def == null)
                {
                    GravshipDebugUtil.LogWarning(string.Format(
                        "[Spawner] Unknown thing '{0}' at {1}", thingEntry.defName, pos));
                    continue;
                }

                ThingDef stuff = null;
                if (!string.IsNullOrEmpty(thingEntry.stuffDef))
                {
                    stuff = DefDatabase<ThingDef>.GetNamedSilentFail(thingEntry.stuffDef);
                }

                Thing thing = ThingMaker.MakeThing(def, stuff);
                Rot4 rot = new Rot4(thingEntry.rotInteger);
                GenSpawn.Spawn(thing, pos, map, rot, WipeMode.Vanish, false);

                if (placedThings != null) placedThings.Add(thing);
                spawnedCount++;
            }
        }

        // ---------- Roof Logic ----------

        private static void BuildRoofs(Map map, List<IntVec3> cells)
        {
            HashSet<IntVec3> visited = new HashSet<IntVec3>();
            Queue<IntVec3> queue = new Queue<IntVec3>();

            for (int idx = 0; idx < cells.Count; idx++)
            {
                var start = cells[idx];
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

                    if (IsMapEdge(c, map)) touchesEdge = true;

                    for (int d = 0; d < GenAdj.CardinalDirections.Length; d++)
                    {
                        IntVec3 n = c + GenAdj.CardinalDirections[d];
                        if (!n.InBounds(map) || visited.Contains(n)) continue;
                        if (HoldsRoof(map, n)) continue;

                        visited.Add(n);
                        queue.Enqueue(n);
                    }
                }

                if (touchesEdge) continue;

                for (int r = 0; r < region.Count; r++)
                {
                    var c = region[r];
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
            return edifice != null && (edifice.def.holdsRoof || edifice.def.passability == Traversability.Impassable);
        }

        private static bool IsMapEdge(IntVec3 c, Map map)
        {
            return c.x <= 0 || c.z <= 0 || c.x >= map.Size.x - 1 || c.z >= map.Size.z - 1;
        }

        public static CellRect CalculateBounds(List<Thing> placedThings, Map map)
        {
            if (placedThings == null || placedThings.Count == 0)
            {
                return CellRect.CenteredOn(map.Center, 20, 20);
            }

            var min = new IntVec3(int.MaxValue, 0, int.MaxValue);
            var max = new IntVec3(int.MinValue, 0, int.MinValue);

            for (int i = 0; i < placedThings.Count; i++)
            {
                var thing = placedThings[i];
                if (thing == null) continue;
                if (thing.Position.x < min.x) min.x = thing.Position.x;
                if (thing.Position.z < min.z) min.z = thing.Position.z;
                if (thing.Position.x > max.x) max.x = thing.Position.x;
                if (thing.Position.z > max.z) max.z = thing.Position.z;
            }

            int width = Mathf.Max(10, max.x - min.x + 10);
            int height = Mathf.Max(10, max.z - min.z + 10);

            return CellRect.CenteredOn(map.Center, width, height).ClipInsideMap(map);
        }
    }
}
