using System.Collections.Generic;
using System.Linq;
using GravshipCrashes.Settings;
using RimWorld;
using UnityEngine;
using Verse;

namespace GravshipCrashes.Util
{
    /// <summary>
    /// Spawns defender pawns for the crashed gravship site.
    /// </summary>
    public static class DefenderGen
    {
        private static Faction cachedFaction;

        public static void SpawnDefenders(Map map, CellRect area, ModSettings_GravshipCrashes settings)
        {
            GravshipDebugUtil.LogMessage("Spawning defenders for crashed gravship...");

            var faction = EnsureFaction();
            if (faction == null)
            {
                GravshipDebugUtil.LogWarning("No valid survivor faction found. No defenders will spawn.");
                return;
            }

            Rand.PushState(map.Tile + 1337);
            try
            {
                var maxDefenders = settings?.maxDefenders ?? 6;
                var count = Mathf.Clamp(Rand.RangeInclusive(Mathf.Min(2, maxDefenders), maxDefenders), 1, maxDefenders);
                GravshipDebugUtil.LogMessage($"Generating {count} defender pawns (max {maxDefenders}).");

                var pawns = new List<Pawn>();
                for (var i = 0; i < count; i++)
                {
                    var request = new PawnGenerationRequest(
                        PawnKindDefOf.Pirate,
                        faction,
                        PawnGenerationContext.NonPlayer,
                        map.Tile,
                        allowDowned: false,
                        forceGenerateNewPawn: true,
                        canGeneratePawnRelations: false,
                        mustBeCapableOfViolence: true,
                        fixedIdeo: faction.ideos?.PrimaryIdeo,
                        allowFood: false,
                        allowPregnant: false
                    );

                    var pawn = PawnGenerator.GeneratePawn(request);
                    PawnWeaponGenerator.TryGenerateWeaponFor(pawn, request);

                    if (pawn.apparel == null || pawn.apparel.WornApparelCount == 0)
                    {
                        PawnApparelGenerator.GenerateStartingApparelFor(pawn, request);
                    }

                    GravshipDebugUtil.LogMessage($"Generated defender: {pawn.LabelShortCap} ({pawn.kindDef.label})");
                    ApplyCrashInjuries(pawn, settings?.pawnInjurySeverityRange ?? new FloatRange(0.1f, 0.4f));
                    pawns.Add(pawn);
                }

                GravshipDebugUtil.LogMessage("Placing defenders on the map...");
                var cells = area.Cells.Where(c => c.InBounds(map) && c.Standable(map)).InRandomOrder().ToList();
                foreach (var pawn in pawns)
                {
                    IntVec3 cell;
                    if (cells.Count > 0)
                    {
                        cell = cells[cells.Count - 1];
                        cells.RemoveAt(cells.Count - 1);
                    }
                    else
                    {
                        cell = CellFinder.RandomClosewalkCellNear(area.CenterCell, map, 10);
                    }

                    GenSpawn.Spawn(pawn, cell, map);
                    GravshipDebugUtil.LogMessage($"Spawned defender {pawn.LabelShortCap} at {cell}.");
                }

                GravshipDebugUtil.LogMessage($"Successfully spawned {pawns.Count} defenders.");
            }
            finally
            {
                Rand.PopState();
            }
        }

        private static void ApplyCrashInjuries(Pawn pawn, FloatRange severity)
        {
            if (pawn == null) return;

            float injuryFraction = Mathf.Clamp01(severity.RandomInRange);
            if (injuryFraction <= 0f) return;

            int extraInjuries = Mathf.RoundToInt(Mathf.Lerp(1, 4, injuryFraction));
            GravshipDebugUtil.LogMessage($"Applying crash injuries to {pawn.LabelShortCap} (severity {injuryFraction:P0}, extra injuries: {extraInjuries})");

            // Base "crash" injuries
            HealthUtility.DamageUntilDowned(pawn, allowBleedingWounds: true);

            // Additional minor wounds
            for (int i = 0; i < extraInjuries; i++)
            {
                var part = pawn.health.hediffSet
                    .GetNotMissingParts()
                    .Where(p => p.depth == BodyPartDepth.Outside)
                    .InRandomOrder()
                    .FirstOrDefault();

                if (part != null)
                {
                    var hediff = HediffMaker.MakeHediff(HediffDefOf.Cut, pawn, part);
                    hediff.Severity = Rand.Range(0.05f, 0.2f) * injuryFraction;
                    pawn.health.AddHediff(hediff);
                }
            }
        }

        private static Faction EnsureFaction()
        {
            if (cachedFaction != null && !cachedFaction.defeated)
            {
                return cachedFaction;
            }

            var def = GravshipCrashesDefOf.Gravship_Survivors;
            if (def == null)
            {
                GravshipDebugUtil.LogWarning("Gravship_Survivors faction def not found.");
                return null;
            }

            cachedFaction = Find.FactionManager.FirstFactionOfDef(def);
            if (cachedFaction == null)
            {
                GravshipDebugUtil.LogMessage("Generating new Gravship Survivors faction...");
                cachedFaction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def));
                cachedFaction.hidden = true;
                Find.FactionManager.Add(cachedFaction);
            }

            GravshipDebugUtil.LogMessage($"Using defender faction: {cachedFaction.Name}");
            return cachedFaction;
        }
    }
}
