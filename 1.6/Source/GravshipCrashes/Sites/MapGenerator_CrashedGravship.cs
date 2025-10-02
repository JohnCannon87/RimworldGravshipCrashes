using System.Collections.Generic;
using System.Linq;
using GravshipCrashes.Settings;
using GravshipCrashes.Util;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
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

            var settings = Mod_GravshipCrashes.Instance?.Settings;
            var comp = site?.GetComponent<WorldObjectComp_CrashedGravship>();

            var usedRect = CellRect.CenteredOn(map.Center, 30, 30);
            var placedThings = new List<Thing>();
            Faction hostileFaction = null;

            GravshipDebugUtil.LogMessage("[Generator] Attempting to clear area...");
            GravshipSpawnUtility.ClearArea(usedRect, map);

            GravshipDebugUtil.LogMessage("[Generator] Adding crash debris...");
            GravshipSpawnUtility.AddCrashScarring(map, usedRect, Rand.Int);

            GravshipDebugUtil.LogMessage("[Generator] Attempting to spawn ship layout...");

            bool spawnedLayout = false;
            if (layout != null)
            {
                // ✅ Updated call: include the 'out Faction' parameter
                spawnedLayout = GravshipLayoutSpawner.TrySpawnLayout(map, layout, usedRect.CenterCell, placedThings, out hostileFaction);
                GravshipDebugUtil.LogMessage($"[Generator] TrySpawnLayout returned: {spawnedLayout} (Placed {placedThings.Count} things)");
            }

            if (spawnedLayout)
            {
                usedRect = GravshipLayoutSpawner.CalculateBounds(placedThings, map);
                GravshipDebugUtil.LogMessage($"[Generator] Calculated usedRect: {usedRect}");
            }
            else
            {
                GravshipDebugUtil.LogWarning("[Generator] Ship layout spawn failed. Using fallback hull.");
                GenerateFallbackHull(map, usedRect);
                if (hostileFaction == null)
                {
                    hostileFaction = Find.FactionManager.FirstFactionOfDef(GravshipCrashesDefOf.Gravship_Survivors);
                }

            }

            GravshipDebugUtil.LogMessage("[Generator] Removing grav engines...");
            GravshipSpawnUtility.RemoveGravEngines(map, usedRect.Cells);

            GravshipDebugUtil.LogMessage("[Generator] Applying structure damage...");
            GravshipSpawnUtility.ApplyStructureDamage(
                map,
                usedRect, // ✅ pass area
                settings?.shipStructureDamageRange ?? new FloatRange(0.15f, 0.45f),
                comp?.StructureDamageSeed ?? Rand.Int);

            GravshipDebugUtil.LogMessage("[Generator] Applying thing damage...");
            GravshipSpawnUtility.ApplyThingDamage(
                map,
                usedRect, // ✅ pass area
                settings?.thingDamageRange ?? new FloatRange(0.1f, 0.35f),
                comp?.ThingDamageSeed ?? Rand.Int);

            GravshipDebugUtil.LogMessage("[Generator] Scattering debris...");
            GravshipSpawnUtility.ScatterDebris(
                map, usedRect, comp?.StructureDamageSeed ?? Rand.Int);

            GravshipDebugUtil.LogMessage("[Generator] Spawning loot...");
            GravshipSpawnUtility.SpawnLoot(
                map, usedRect, comp?.LootSeed ?? Rand.Int);

            GravshipDebugUtil.LogMessage("[Generator] Spawning defenders...");
            var defenders = DefenderGen.SpawnDefenders(map, usedRect, settings, layout, hostileFaction);

            // ✅ Create a Lord with all pawns
            if (defenders.Count > 0 && hostileFaction != null)
            {
                GravshipDebugUtil.LogMessage("[Generator] Creating Lord for defenders...");
                Lord lord = LordMaker.MakeNewLord(
                    hostileFaction,
                    new LordJob_DefendPoint(usedRect.CenterCell),
                    map,
                    defenders
                );

                // ✅ Optional: Auto-man mortars if present
                var unmannedMortars = placedThings
                    .OfType<Building_Turret>()
                    .Where(t => t.def.building.IsMortar)
                    .ToList();

                foreach (var mortar in unmannedMortars)
                {
                    var freePawn = defenders
                        .FirstOrDefault(p =>
                            p.Spawned &&
                            p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) &&
                            (p.CurJob == null || !p.CurJob.def.playerInterruptible));

                    if (freePawn != null)
                    {
                        GravshipDebugUtil.LogMessage($"[Generator] Assigning {freePawn.LabelShortCap} to man mortar at {mortar.Position}");
                        Job job = JobMaker.MakeJob(JobDefOf.ManTurret, mortar);
                        freePawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    }
                }
            }

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

            GravshipDebugUtil.LogMessage($"[Generator] Fallback hull generation complete. Placed floor on {floorCount} cells.");
        }
    }
}
