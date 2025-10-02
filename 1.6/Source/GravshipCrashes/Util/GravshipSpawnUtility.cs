using System;
using System.Collections.Generic;
using System.Linq;
using GravshipCrashes.Settings;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using GravshipExport;                  // hard dependency on the exporter
// ^ gives access to ShipLayoutDefV2 (with gravEngineX/gravEngineZ)
namespace GravshipCrashes.Util
{
    /// <summary>
    /// Helpers for creating world sites and generating the crashed gravship map experience.
    /// </summary>
    public static class GravshipSpawnUtility
    {
        // Cache for quick comparisons
        private static ThingDef _brokenGravEngineDef;
        private static ThingDef BrokenGravEngineDef
        {
            get
            {
                if (_brokenGravEngineDef == null)
                {
                    _brokenGravEngineDef = DefDatabase<ThingDef>.GetNamedSilentFail("BrokenGravEngine");
                }
                return _brokenGravEngineDef;
            }
        }

        private static bool IsBrokenGravEngine(ThingDef def)
        {
            if (def == null) return false;
            if (BrokenGravEngineDef != null && def == BrokenGravEngineDef) return true;
            return string.Equals(def.defName, "BrokenGravEngine", StringComparison.OrdinalIgnoreCase);
        }

        private static readonly string[] EngineNameTokens =
        {
            "gravengine","grav_engine","gravityengine","gravdrive","gravitydrive","grav_core","gravitycore"
        };

        private static bool NameLooksLikeGravEngine(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            s = s.ToLowerInvariant();
            for (int i = 0; i < EngineNameTokens.Length; i++)
                if (s.Contains(EngineNameTokens[i])) return true;
            return false;
        }

        private static bool IsIntactGravEngine(ThingDef def)
        {
            if (def == null) return false;
            if (IsBrokenGravEngine(def)) return false;
            if (NameLooksLikeGravEngine(def.defName)) return true;
            if (!def.label.NullOrEmpty() && NameLooksLikeGravEngine(def.label)) return true;
            return false;
        }

        public static void ClearArea(Map map, CellRect area)
        {
            GravshipDebugUtil.LogMessage("[SpawnUtility] Deep-clearing crash site area...");

            foreach (var cell in area.Cells)
            {
                if (!cell.InBounds(map)) continue;

                // Destroy all plants, filth, chunks, and items
                var things = cell.GetThingList(map);
                for (int i = things.Count - 1; i >= 0; i--)
                {
                    var t = things[i];
                    if (t is Pawn) continue;
                    if (t.def.category == ThingCategory.Plant ||
                        t.def.category == ThingCategory.Item ||
                        t.def.category == ThingCategory.Filth ||
                        t is Mote ||
                        t is Blueprint ||
                        t is Frame)
                    {
                        t.Destroy(DestroyMode.Vanish);
                    }
                }

                // Optional: reset terrain to basic soil
                // map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
            }

            // Global cleanup for plants that might not be in cell lists yet
            foreach (var plant in map.listerThings.ThingsInGroup(ThingRequestGroup.Plant)
                         .Where(p => area.Contains(p.Position))
                         .ToList())
            {
                plant.Destroy(DestroyMode.Vanish);
            }

            GravshipDebugUtil.LogMessage("[SpawnUtility] Crash zone fully cleared.");
        }

        public static void AddCrashScarring(Map map, CellRect area, int seed)
        {
            GravshipDebugUtil.LogMessage("[SpawnUtility] Adding crash scar debris...");
            Rand.PushState(seed);
            try
            {
                foreach (var cell in area.Cells.InRandomOrder())
                {
                    if (!cell.InBounds(map)) continue;

                    // 20% chance: slag chunks
                    if (Rand.Chance(0.20f))
                    {
                        var slag = ThingMaker.MakeThing(ThingDefOf.ChunkSlagSteel);
                        GenPlace.TryPlaceThing(slag, cell, map, ThingPlaceMode.Near);
                    }

                    // 10% chance: chemfuel puddle
                    if (Rand.Chance(0.10f))
                    {
                        FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Fuel, Rand.Range(1, 3));
                    }

                    // 8% chance: small fire (only if outdoor / not raining)
                    if (Rand.Chance(0.08f))
                    {
                        FireUtility.TryStartFireIn(cell, map, Rand.Range(0.05f, 0.20f), null, null);
                    }

                    // 15% chance: burn/scorch mark
                    if (Rand.Chance(0.15f))
                    {
                        FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Ash, Rand.Range(1, 2));
                    }

                    // 5% chance: scattered steel resource
                    if (Rand.Chance(0.05f))
                    {
                        var steel = ThingMaker.MakeThing(ThingDefOf.Steel);
                        steel.stackCount = Rand.RangeInclusive(10, 40);
                        GenPlace.TryPlaceThing(steel, cell, map, ThingPlaceMode.Near);
                    }
                }
            }
            finally
            {
                Rand.PopState();
            }
        }

        public static bool TryFindSiteTile(out int tile)
        {
            PlanetTile foundTile;
            if (TileFinder.TryFindNewSiteTile(out foundTile, 6, 30, false))
            {
                tile = foundTile.tileId;
                GravshipDebugUtil.LogMessage($"[SpawnUtility] Found valid site tile: {tile}");
                return true;
            }

            GravshipDebugUtil.LogWarning("[SpawnUtility] Failed to find a valid site tile.");
            tile = -1;
            return false;
        }

        public static Site CreateSite(int tile)
        {
            GravshipDebugUtil.LogMessage($"[SpawnUtility] Creating site at tile {tile}.");

            var siteDef = GravshipCrashesDefOf.CrashedGravshipSite;
            var site = (Site)WorldObjectMaker.MakeWorldObject(siteDef);
            site.Tile = tile;
            site.SetFaction(null);

            var part = new SitePart
            {
                def = GravshipCrashesDefOf.CrashedGravshipSitePart,
                site = site,
                parms = new SitePartParams { threatPoints = 0f }
            };

            site.parts.Add(part);
            site.customLabel = "Crashed Gravship";

            GravshipDebugUtil.LogMessage("[SpawnUtility] Site created successfully.");
            return site;
        }

        public static void ConfigureTimeout(Site site, IntRange daysRange)
        {
            var timeout = site.GetComponent<TimeoutComp>();
            if (timeout != null)
            {
                int ticks = daysRange.RandomInRange * 60000;
                GravshipDebugUtil.LogMessage($"[SpawnUtility] Configured site timeout: {ticks} ticks.");
                timeout.StartTimeout(ticks);
            }
        }

        /// <summary>
        /// Store the ship defName we intend to use (no resolver type involved).
        /// </summary>
        public static void ConfigureSiteMetadata(Site site, string shipDefName)
        {
            var comp = site.GetComponent<WorldObjectComp_CrashedGravship>();
            if (comp == null)
            {
                GravshipDebugUtil.LogWarning("[SpawnUtility] Failed to configure site metadata: component missing.");
                return;
            }

            comp.ShipDefName = shipDefName ?? string.Empty;
            comp.StructureDamageSeed = Rand.Int;
            comp.ThingDamageSeed = Rand.Int;
            comp.LootSeed = Rand.Int;
            comp.DefenderSeed = Rand.Int;

            GravshipDebugUtil.LogMessage($"[SpawnUtility] Site metadata configured. Ship: {comp.ShipDefName}");
        }

        /// <summary>
        /// Ensure the BrokenGravEngine is spawned at the coordinates declared by the ShipLayoutDefV2.
        /// Call this right after you've spawned the layout and computed usedRect.
        /// </summary>
        public static void EnsureBrokenGravEngineFromLayout(Map map, CellRect usedRect, string shipDefName)
        {
            if (map == null) return;

            var layout = ShipLayouts.Get(shipDefName);
            if (layout == null)
            {
                GravshipDebugUtil.LogWarning($"[SpawnUtility] Could not find layout '{shipDefName}' to place BrokenGravEngine.");
                return;
            }

            EnsureBrokenGravEngineAt(map, usedRect, layout.gravEngineX, layout.gravEngineZ);
        }

        /// <summary>
        /// Core placer using layout-local coords (0..width-1 / 0..height-1).
        /// </summary>
        public static void EnsureBrokenGravEngineAt(Map map, CellRect usedRect, int gx, int gz)
        {
            if (map == null) return;
            if (BrokenGravEngineDef == null)
            {
                GravshipDebugUtil.LogWarning("[SpawnUtility] BrokenGravEngine def missing - cannot place.");
                return;
            }

            if (gx < 0 || gz < 0)
            {
                GravshipDebugUtil.LogWarning("[SpawnUtility] Layout did not provide valid grav engine coords.");
                return;
            }

            var cell = new IntVec3(usedRect.minX + gx, 0, usedRect.minZ + gz);
            if (!cell.InBounds(map))
            {
                GravshipDebugUtil.LogWarning($"[SpawnUtility] Grav engine cell {cell} out of bounds.");
                return;
            }

            // don't double place
            if (cell.GetThingList(map).Any(t => t?.def == BrokenGravEngineDef))
                return;

            GravshipDebugUtil.LogMessage($"[SpawnUtility] Spawning BrokenGravEngine at {cell}");
            GenSpawn.Spawn(ThingMaker.MakeThing(BrokenGravEngineDef), cell, map, Rot4.North, WipeMode.Vanish);

            if (Rand.Chance(0.30f))
                FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Fuel);

            if (map.weatherManager.RainRate < 0.5f && Rand.Chance(0.20f))
                FireUtility.TryStartFireIn(cell, map, Rand.Range(0.10f, 0.25f), null);
        }

        /// <summary>
        /// Optional sweep: if any intact engine slipped into the map (some layouts/mods), replace with Broken.
        /// Fine to keep; no harm if the layout never spawns intact engines.
        /// </summary>
        public static void RemoveGravEngines(Map map, IEnumerable<IntVec3> candidateCells)
        {
            if (map == null)
            {
                GravshipDebugUtil.LogWarning("[SpawnUtility] Map was null during grav engine removal.");
                return;
            }

            GravshipDebugUtil.LogMessage("[SpawnUtility] Replacing grav engines with broken variants...");
            int replaced = 0;

            var cells = candidateCells?.ToList() ?? new List<IntVec3>();
            for (int c = 0; c < cells.Count; c++)
                replaced += ReplaceGravEnginesAt(map, cells[c]);

            // Safety pass
            foreach (var thing in map.listerThings.AllThings.ToList())
            {
                if (IsIntactGravEngine(thing.def))
                {
                    GravshipDebugUtil.LogMessage($"[SpawnUtility] Found stray grav engine at {thing.Position}, replacing...");
                    var pos = thing.Position;
                    var rot = thing.Rotation;
                    thing.Destroy(DestroyMode.KillFinalize);
                    SpawnBrokenGravEngine(map, pos, rot);
                    replaced++;
                }
            }

            GravshipDebugUtil.LogMessage($"[SpawnUtility] Total grav engines replaced: {replaced}");
            if (replaced == 0) DumpEngineHints(map);
        }

        private static int ReplaceGravEnginesAt(Map map, IntVec3 cell)
        {
            int replaced = 0;
            var things = cell.GetThingList(map);
            for (int i = things.Count - 1; i >= 0; i--)
            {
                var thing = things[i];
                if (thing?.def == null) continue;
                if (!IsIntactGravEngine(thing.def)) continue;

                GravshipDebugUtil.LogMessage($"[SpawnUtility] Replacing grav engine at {cell}...");
                var rot = thing.Rotation;
                thing.Destroy(DestroyMode.KillFinalize);
                SpawnBrokenGravEngine(map, cell, rot);
                replaced++;
            }
            return replaced;
        }

        private static void DumpEngineHints(Map map)
        {
            var suspects = new HashSet<string>();
            foreach (var t in map.listerThings.AllThings)
            {
                var d = t?.def;
                if (d == null) continue;
                if (NameLooksLikeGravEngine(d.defName) || NameLooksLikeGravEngine(d.label))
                    suspects.Add($"{d.defName} (label: {d.label})");
            }

            if (suspects.Count == 0)
                GravshipDebugUtil.LogMessage("[SpawnUtility] No engine-like defs found on map. This layout may not include a grav engine.");
            else
                GravshipDebugUtil.LogMessage("[SpawnUtility] Engine-like defs on map (no matches replaced): " + string.Join(", ", suspects));
        }

        private static void SpawnBrokenGravEngine(Map map, IntVec3 cell, Rot4 rot)
        {
            if (BrokenGravEngineDef == null)
            {
                GravshipDebugUtil.LogWarning("[SpawnUtility] BrokenGravEngine def missing - falling back to slag.");
                PlaceWreckage(map, cell);
                return;
            }

            GravshipDebugUtil.LogMessage($"[SpawnUtility] Spawning BrokenGravEngine at {cell}");
            var wreck = ThingMaker.MakeThing(BrokenGravEngineDef);
            GenSpawn.Spawn(wreck, cell, map, rot, WipeMode.Vanish);

            if (Rand.Chance(0.3f))
                FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Fuel);

            if (map.weatherManager.RainRate < 0.5f && Rand.Chance(0.2f))
                FireUtility.TryStartFireIn(cell, map, Rand.Range(0.1f, 0.25f), null);
        }

        private static void PlaceWreckage(Map map, IntVec3 cell)
        {
            GravshipDebugUtil.LogMessage($"[SpawnUtility] Placing slag wreckage at {cell}");
            var slag = ThingMaker.MakeThing(ThingDefOf.ChunkSlagSteel);
            GenPlace.TryPlaceThing(slag, cell, map, ThingPlaceMode.Direct);

            if (map.weatherManager.RainRate < 0.5f)
                FireUtility.TryStartFireIn(cell, map, Rand.Range(0.1f, 0.2f), null);
        }

        public static void ApplyStructureDamage(Map map, FloatRange damageRange, int seed)
        {
            GravshipDebugUtil.LogMessage("[SpawnUtility] Applying structural damage...");
            Rand.PushState(seed);
            try
            {
                var buildings = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial).ToList();

                foreach (var building in buildings)
                {
                    if (building == null || building.Destroyed || building.def == null || !building.def.useHitPoints)
                        continue;

                    // 🚫 Skip Broken Grav Engine
                    if (IsBrokenGravEngine(building.def))
                        continue;

                    // 🚫 Skip power conduits or transmitters
                    if (building.TryGetComp<CompPowerTransmitter>() != null ||
                        building.def.defName.ToLower().Contains("conduit") ||
                        building.def.thingClass?.Name.ToLower().Contains("power") == true)
                    {
                        GravshipDebugUtil.LogMessage($"[SpawnUtility] Skipping structure damage for conduit: {building.LabelCap}");
                        continue;
                    }

                    var damageFraction = damageRange.RandomInRange;
                    if (Rand.Value < damageFraction)
                    {
                        var hp = Mathf.Max(1, Mathf.RoundToInt(building.MaxHitPoints * (1f - damageFraction)));
                        building.HitPoints = Mathf.Clamp(hp, 1, building.MaxHitPoints);

                        if (Rand.Value < 0.25f)
                        {
                            var amount = Mathf.Max(5f, building.MaxHitPoints * damageFraction * 0.5f);
                            building.TakeDamage(new DamageInfo(DamageDefOf.Bomb, amount));
                        }
                    }
                }
            }
            finally
            {
                Rand.PopState();
            }
        }



        public static void ApplyThingDamage(Map map, FloatRange damageRange, int seed)
        {
            GravshipDebugUtil.LogMessage("[SpawnUtility] Applying thing damage...");
            Rand.PushState(seed);
            try
            {
                var things = map.listerThings.AllThings.ToList();

                foreach (var thing in things)
                {
                    if (thing == null || thing.Destroyed || thing.def == null) continue;
                    if (thing is Pawn) continue;
                    if (!thing.def.useHitPoints) continue;

                    // extra safety: never damage BrokenGravEngine here either
                    if (IsBrokenGravEngine(thing.def)) continue;

                    // ✅ Don't damage any power network components (conduits, transmitters, etc.)
                    if (thing.def.category == ThingCategory.Building)
                    {
                        // Skip if this is a conduit or any type of power network piece
                        if (thing.TryGetComp<CompPower>() != null
                            || thing.TryGetComp<CompPowerTransmitter>() != null
                            || thing.def.defName.ToLower().Contains("conduit")
                            || thing.def.building?.isPowerConduit == true)
                        {
                            continue;
                        }
                    }


                    var damageFraction = damageRange.RandomInRange;
                    if (Rand.Value < damageFraction)
                    {
                        thing.HitPoints = Mathf.Max(1, Mathf.RoundToInt(thing.MaxHitPoints * (1f - damageFraction)));

                        if (Rand.Value < 0.1f)
                        {
                            GravshipDebugUtil.LogMessage($"[SpawnUtility] {thing.LabelCap} destroyed by crash damage.");
                            thing.Destroy(DestroyMode.KillFinalize);
                        }
                    }
                }
            }
            finally
            {
                Rand.PopState();
            }
        }

        public static void ScatterDebris(Map map, CellRect area, int seed)
        {
            GravshipDebugUtil.LogMessage("[SpawnUtility] Scattering debris...");
            Rand.PushState(seed);
            try
            {
                var debrisCount = Mathf.Clamp(area.Area / 25, 6, 40);
                for (var i = 0; i < debrisCount; i++)
                {
                    var cell = area.RandomCell;
                    if (!cell.InBounds(map) || !cell.Walkable(map))
                        continue;

                    FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Fuel);
                    if (Rand.Chance(0.35f))
                        FireUtility.TryStartFireIn(cell, map, Rand.Range(0.05f, 0.2f), null);
                }
            }
            finally
            {
                Rand.PopState();
            }
        }

        public static void SpawnLoot(Map map, CellRect area, int seed)
        {
            if (GravshipCrashesDefOf.GravshipCrashLoot == null)
            {
                GravshipDebugUtil.LogWarning("[SpawnUtility] Loot table is null, skipping loot generation.");
                return;
            }

            GravshipDebugUtil.LogMessage("[SpawnUtility] Spawning crash loot...");
            Rand.PushState(seed);
            try
            {
                var parms = default(ThingSetMakerParams);
                parms.totalMarketValueRange = new FloatRange(300f, 900f);
                parms.techLevel = TechLevel.Spacer;

                var loot = GravshipCrashesDefOf.GravshipCrashLoot.root.Generate(parms);

                // 1️⃣ Find storage buildings in the crash area
                var storageBuildings = map.listerThings.AllThings
                    .OfType<Building_Storage>()
                    .Where(b => area.Contains(b.Position))
                    .ToList();

                // 2️⃣ Build a list of available storage cells
                var storageCells = new List<IntVec3>();
                foreach (var storage in storageBuildings)
                {
                    foreach (var c in storage.AllSlotCells())
                    {
                        if (c.InBounds(map) && c.Standable(map))
                            storageCells.Add(c);
                    }
                }

                GravshipDebugUtil.LogMessage($"[SpawnUtility] Found {storageBuildings.Count} storage buildings with {storageCells.Count} total storage cells.");

                // 3️⃣ Try to place loot into storage slots first
                foreach (var thing in loot)
                {
                    bool placed = false;

                    // Try a random storage cell
                    storageCells.Shuffle();
                    foreach (var cell in storageCells)
                    {
                        // If storage accepts this item (check Accepts()) — optional but safer
                        var building = cell.GetEdifice(map) as Building_Storage;
                        if (building != null && !building.Accepts(thing))
                            continue;

                        var result = GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Direct);
                        if (result != null)
                        {
                            GravshipDebugUtil.LogMessage($"[SpawnUtility] Placed {thing.LabelCap} into storage cell at {cell}.");
                            placed = true;
                            break;
                        }
                    }

                    // 4️⃣ Fallback: place randomly near the ship
                    if (!placed)
                    {
                        var cell = area.RandomCell;
                        if (!cell.InBounds(map)) cell = map.Center;

                        GravshipDebugUtil.LogMessage($"[SpawnUtility] Placing {thing.LabelCap} on the ground (no storage available).");
                        GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near);
                    }
                }
            }
            finally
            {
                Rand.PopState();
            }
        }


    }
}
