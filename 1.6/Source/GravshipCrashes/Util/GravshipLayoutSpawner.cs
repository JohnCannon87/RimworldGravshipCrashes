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
        /// <summary>
        /// Spawns the gravship layout and returns true if anything spawned. 
        /// Also returns the hostile faction used for spawning.
        /// </summary>
        public static bool TrySpawnLayout(
            Map map,
            ShipLayoutDefV2 layout,
            IntVec3 center,
            List<Thing> placedThings,
            out Faction spawnedFaction)
        {
            spawnedFaction = null;

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
                $"[Spawner] Spawning layout: {layout.defName} ({layout.width}x{layout.height})");

            int halfWidth = layout.width / 2;
            int halfHeight = layout.height / 2;
            int spawnedCount = 0;
            List<IntVec3> processedCells = new List<IntVec3>();

            // ✅ Resolve or create the hostile faction
            var faction = DefenderGenFactionHelper();
            spawnedFaction = faction;

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

            // 3) Things (buildings, turrets, etc.)
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

                    SpawnThings(cell, map, pos, placedThings, faction, ref spawnedCount);
                    processedCells.Add(pos);
                }
            }

            // 4) Roofs
            GravshipDebugUtil.LogMessage("[Spawner] Pass 4: Roofs");
            BuildRoofs(map, processedCells);

            // 5) Place the Broken Grav Engine
            if (layout.gravEngineX >= 0 && layout.gravEngineZ >= 0)
            {
                IntVec3 enginePos = new IntVec3(center.x - halfWidth + layout.gravEngineX, 0,
                                                center.z - halfHeight + layout.gravEngineZ);
                if (enginePos.InBounds(map))
                {
                    GravshipDebugUtil.LogMessage($"[Spawner] Placing BrokenGravEngine at layout coords ({layout.gravEngineX},{layout.gravEngineZ}) -> map {enginePos}");

                    SpawnBrokenGravEngineAt(map, enginePos, Rot4.North, faction);
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

            GravshipDebugUtil.LogMessage($"[Spawner] Layout spawn complete. Placed {spawnedCount} objects total.");
            return spawnedCount > 0;
        }

        // ---------- Spawn Helpers ----------

        private static void SpawnBrokenGravEngineAt(Map map, IntVec3 cell, Rot4 rot, Faction faction)
        {
            var brokenDef = DefDatabase<ThingDef>.GetNamedSilentFail("BrokenGravEngine");
            if (brokenDef == null)
            {
                GravshipDebugUtil.LogWarning("[SpawnUtility] BrokenGravEngine def missing - falling back to slag.");
                var slag = ThingMaker.MakeThing(ThingDefOf.ChunkSlagSteel);
                GenPlace.TryPlaceThing(slag, cell, map, ThingPlaceMode.Direct);
                return;
            }

            GravshipDebugUtil.LogMessage($"[SpawnUtility] Spawning BrokenGravEngine at {cell}");
            var wreck = ThingMaker.MakeThing(brokenDef);
            var spawned = GenSpawn.Spawn(wreck, cell, map, rot, WipeMode.Vanish);
            spawned?.SetFaction(faction);
        }

        private static void SpawnFoundationAsTerrain(ShipCell cell, Map map, IntVec3 pos, ref int spawnedCount)
        {
            TerrainDef terrain = DefDatabase<TerrainDef>.GetNamedSilentFail(cell.foundationDef);

            if (terrain == null && string.Equals(cell.foundationDef, "Substructure", System.StringComparison.OrdinalIgnoreCase))
            {
                terrain = TerrainDefOf.MetalTile;
                GravshipDebugUtil.LogMessage($"[Spawner] Substructure replaced with MetalTile at {pos}");
            }

            if (terrain == null)
            {
                GravshipDebugUtil.LogWarning($"[Spawner] Unknown foundation '{cell.foundationDef}' at {pos}");
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
                GravshipDebugUtil.LogWarning($"[Spawner] Unknown terrain '{cell.terrainDef}' at {pos}");
                return;
            }

            map.terrainGrid.SetTerrain(pos, terrain);
            spawnedCount++;
        }

        private static void SpawnThings(ShipCell cell, Map map, IntVec3 pos, List<Thing> placedThings, Faction faction, ref int spawnedCount)
        {
            for (int i = 0; i < cell.things.Count; i++)
            {
                var thingEntry = cell.things[i];
                if (thingEntry == null || string.IsNullOrEmpty(thingEntry.defName)) continue;

                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(thingEntry.defName);
                if (def == null)
                {
                    GravshipDebugUtil.LogWarning($"[Spawner] Unknown thing '{thingEntry.defName}' at {pos}");
                    continue;
                }

                ThingDef stuff = null;
                if (!string.IsNullOrEmpty(thingEntry.stuffDef))
                {
                    stuff = DefDatabase<ThingDef>.GetNamedSilentFail(thingEntry.stuffDef);
                }

                Thing thing = ThingMaker.MakeThing(def, stuff);
                Rot4 rot = new Rot4(thingEntry.rotInteger);
                var spawned = GenSpawn.Spawn(thing, pos, map, rot, WipeMode.Vanish, false);

                // ✅ Assign faction to everything that can have one
                if (spawned?.def.CanHaveFaction == true)
                {
                    spawned.SetFaction(faction);
                }

                // ✅ Assign faction to everything (turrets, doors, furniture, etc.)
                if (spawned.Faction != faction && spawned.def.CanHaveFaction)
                {
                    spawned.SetFaction(faction);
                }

                // ✅ NEW: If the thing uses fuel, give it a random initial amount (30% - 70%)
                var refuelable = spawned.TryGetComp<CompRefuelable>();
                if (refuelable != null && refuelable.Props.fuelCapacity > 0f)
                {
                    float minFuel = refuelable.Props.fuelCapacity * 0.3f;
                    float maxFuel = refuelable.Props.fuelCapacity * 0.7f;
                    float chosenFuel = Rand.Range(minFuel, maxFuel);

                    refuelable.Refuel(chosenFuel);
                    GravshipDebugUtil.LogMessage($"[Spawner] Set initial fuel for {spawned.LabelCap} to {chosenFuel}/{refuelable.Props.fuelCapacity}");
                }

                // ✅ If the thing uses power, turn it on
                var power = spawned.TryGetComp<CompPowerTrader>();
                if (power != null)
                {
                    power.PowerOn = true;
                    GravshipDebugUtil.LogMessage($"[Spawner] Set initial power state ON for {spawned.LabelCap}");
                }

                placedThings?.Add(spawned);
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

                    if (IsMapEdge(c, map)) touchesEdge = true;

                    foreach (var n in GenAdj.CardinalDirections)
                    {
                        IntVec3 next = c + n;
                        if (!next.InBounds(map) || visited.Contains(next)) continue;
                        if (HoldsRoof(map, next)) continue;

                        visited.Add(next);
                        queue.Enqueue(next);
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

            foreach (var thing in placedThings)
            {
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

        // ✅ Small helper: get the gravship survivors faction from DefenderGen
        private static Faction DefenderGenFactionHelper()
        {
            var def = GravshipCrashesDefOf.Gravship_Survivors;
            var faction = Find.FactionManager.FirstFactionOfDef(def);
            if (faction == null)
            {
                faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def));
                Find.FactionManager.Add(faction);
            }
            return faction;
        }
    }
}
