using System;
using System.Collections.Generic;
using System.Linq;
using GravshipCrashes.Settings;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace GravshipCrashes.Util
{
    /// <summary>
    /// Helpers for creating world sites and generating the crashed gravship map experience.
    /// </summary>
    public static class GravshipSpawnUtility
    {
        private static readonly string[] GravEngineDefNames =
        {
            "ShipGravEngine", "ShipPart_GravEngine", "VFEI_GravEngine", "SOS2_ShipGravEngine"
        };

        public static bool TryFindSiteTile(out int tile)
        {
            PlanetTile foundTile;
            if (TileFinder.TryFindNewSiteTile(out foundTile, 6, 30, false))
            {
                tile = foundTile.tileId;
                return true;
            }

            tile = -1;
            return false;
        }

        public static Site CreateSite(int tile)
        {
            var siteDef = GravshipCrashesDefOf.CrashedGravshipSite;
            var site = (Site)WorldObjectMaker.MakeWorldObject(siteDef);
            site.Tile = tile;
            site.SetFaction(null);

            var part = new SitePart
            {
                def = GravshipCrashesDefOf.CrashedGravshipSitePart,
                site = site,
                parms = new SitePartParams
                {
                    threatPoints = 0f
                }
            };

            site.parts.Add(part);
            site.customLabel = "Crashed Gravship";

            return site;
        }

        public static void ConfigureTimeout(Site site, IntRange daysRange)
        {
            var timeout = site.GetComponent<TimeoutComp>();
            if (timeout != null)
            {
                timeout.StartTimeout(daysRange.RandomInRange * 60000);
            }
        }

        public static void ConfigureSiteMetadata(Site site, ShipLayoutResolver.ShipEntry entry)
        {
            var comp = site.GetComponent<WorldObjectComp_CrashedGravship>();
            if (comp == null)
            {
                return;
            }

            comp.ShipDefName = entry?.DefName ?? string.Empty;
            comp.StructureDamageSeed = Rand.Int;
            comp.ThingDamageSeed = Rand.Int;
            comp.LootSeed = Rand.Int;
            comp.DefenderSeed = Rand.Int;
        }

        public static void RemoveGravEngines(Map map, IEnumerable<IntVec3> candidateCells)
        {
            if (map == null)
            {
                return;
            }

            var cells = candidateCells?.ToList() ?? new List<IntVec3>();
            foreach (var cell in cells)
            {
                RemoveGravEnginesAt(map, cell);
            }

            foreach (var thing in map.listerThings.AllThings.ToList())
            {
                if (thing.def != null && GravEngineDefNames.Contains(thing.def.defName))
                {
                    var cell = thing.Position;
                    thing.Destroy(DestroyMode.KillFinalize);
                    PlaceWreckage(map, cell);
                }
            }
        }

        private static void RemoveGravEnginesAt(Map map, IntVec3 cell)
        {
            var things = cell.GetThingList(map);
            for (var i = things.Count - 1; i >= 0; i--)
            {
                var thing = things[i];
                if (thing?.def == null)
                {
                    continue;
                }

                if (!GravEngineDefNames.Contains(thing.def.defName))
                {
                    continue;
                }

                thing.Destroy(DestroyMode.KillFinalize);
                PlaceWreckage(map, cell);
            }
        }

        private static void PlaceWreckage(Map map, IntVec3 cell)
        {
            var slag = ThingMaker.MakeThing(ThingDefOf.ChunkSlagSteel);
            GenPlace.TryPlaceThing(slag, cell, map, ThingPlaceMode.Direct);

            if (map.weatherManager.RainRate < 0.5f)
            {
                FireUtility.TryStartFireIn(cell, map, Rand.Range(0.1f, 0.2f), null);
            }
        }

        public static void ApplyStructureDamage(Map map, FloatRange damageRange, int seed)
        {
            Rand.PushState(seed);
            try
            {
                // Snapshot to avoid modifying collection during enumeration
                var buildings = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial).ToList();

                foreach (var building in buildings)
                {
                    if (building == null || building.Destroyed || building.def == null || !building.def.useHitPoints)
                        continue;

                    var damageFraction = damageRange.RandomInRange;
                    if (Rand.Value < damageFraction)
                    {
                        // Reduce HP
                        var hp = Mathf.Max(1, Mathf.RoundToInt(building.MaxHitPoints * (1f - damageFraction)));
                        building.HitPoints = Mathf.Clamp(hp, 1, building.MaxHitPoints);

                        // Occasional extra damage (may destroy the thing)
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
            Rand.PushState(seed);
            try
            {
                // Snapshot for safety; ApplyThingDamage can destroy things
                var things = map.listerThings.AllThings.ToList();

                foreach (var thing in things)
                {
                    if (thing == null || thing.Destroyed || thing.def == null)
                        continue;

                    if (thing is Pawn) continue;
                    if (!thing.def.useHitPoints) continue;

                    // Skip buildings: already handled by structure damage
                    if (thing.def.category == ThingCategory.Building) continue;

                    var damageFraction = damageRange.RandomInRange;
                    if (Rand.Value < damageFraction)
                    {
                        thing.HitPoints = Mathf.Max(1, Mathf.RoundToInt(thing.MaxHitPoints * (1f - damageFraction)));

                        // Occasionally outright destroy
                        if (Rand.Value < 0.1f)
                        {
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
            Rand.PushState(seed);
            try
            {
                var debrisCount = Mathf.Clamp(area.Area / 25, 6, 40);
                for (var i = 0; i < debrisCount; i++)
                {
                    var cell = area.RandomCell;
                    if (!cell.InBounds(map) || !cell.Walkable(map))
                    {
                        continue;
                    }

                    FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Fuel);
                    if (Rand.Chance(0.35f))
                    {
                        FireUtility.TryStartFireIn(cell, map, Rand.Range(0.05f, 0.2f), null);
                    }
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
                return;
            }

            Rand.PushState(seed);
            try
            {
                var parms = default(ThingSetMakerParams);
                parms.totalMarketValueRange = new FloatRange(300f, 900f);
                parms.techLevel = TechLevel.Spacer;

                var loot = GravshipCrashesDefOf.GravshipCrashLoot.root.Generate(parms);
                foreach (var thing in loot)
                {
                    var cell = area.RandomCell;
                    if (!cell.InBounds(map))
                    {
                        cell = map.Center;
                    }

                    GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near);
                }
            }
            finally
            {
                Rand.PopState();
            }
        }
    }
}
