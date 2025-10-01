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
            return TileFinder.TryFindNewSiteTile(out tile, 6, 30, false, TileFinderMode.Nearby, -1);
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
                    threatPoints = 0f,
                    pawnGroupKindDef = PawnGroupKindDefOf.Combat
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
                FireUtility.TryStartFireIn(cell, map, Rand.Range(0.1f, 0.2f));
            }
        }

        public static void ApplyStructureDamage(Map map, FloatRange damageRange, int seed)
        {
            Rand.PushState(seed);
            try
            {
                foreach (var building in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
                {
                    if (!building.def.useHitPoints || building.Destroyed)
                    {
                        continue;
                    }

                    var damageFraction = damageRange.RandomInRange;
                    if (Rand.Value < damageFraction)
                    {
                        var hitPoints = Mathf.Max(1, Mathf.RoundToInt(building.MaxHitPoints * (1f - damageFraction)));
                        building.HitPoints = Mathf.Clamp(hitPoints, 1, building.MaxHitPoints);
                        if (Rand.Value < 0.25f)
                        {
                            building.TakeDamage(new DamageInfo(DamageDefOf.Bomb, Mathf.Max(5f, building.MaxHitPoints * damageFraction * 0.5f)));
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
                foreach (var thing in map.listerThings.AllThings)
                {
                    if (thing is Pawn || !thing.def.useHitPoints || thing.Destroyed)
                    {
                        continue;
                    }

                    if (thing.def.category == ThingCategory.Building)
                    {
                        continue; // handled by structure damage
                    }

                    var damageFraction = damageRange.RandomInRange;
                    if (Rand.Value < damageFraction)
                    {
                        thing.HitPoints = Mathf.Max(1, Mathf.RoundToInt(thing.MaxHitPoints * (1f - damageFraction)));
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

                    FilthMaker.TryMakeFilth(cell, map, ThingDefOf.FilthAsh);
                    if (Rand.Chance(0.35f))
                    {
                        FireUtility.TryStartFireIn(cell, map, Rand.Range(0.05f, 0.2f));
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
                        cell = CellFinderLoose.TryFindCentralCell(map, 5f) ?? map.Center;
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
