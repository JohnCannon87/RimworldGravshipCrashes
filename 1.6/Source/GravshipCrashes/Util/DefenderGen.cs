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
            var faction = EnsureFaction();
            if (faction == null)
            {
                return;
            }

            Rand.PushState(map.Tile + 1337);
            try
            {
                var maxDefenders = settings?.maxDefenders ?? 6;
                var count = Mathf.Clamp(Rand.RangeInclusive(Mathf.Min(2, maxDefenders), maxDefenders), 1, maxDefenders);
                var pawns = new List<Pawn>();
                for (var i = 0; i < count; i++)
                {

                    var request = new PawnGenerationRequest(PawnKindDefOf.Pirate, faction, PawnGenerationContext.NonPlayer, map.Tile,
                        allowDowned: false, forceGenerateNewPawn: true, canGeneratePawnRelations: false, mustBeCapableOfViolence: true,
                        fixedIdeo: faction.ideos?.PrimaryIdeo, allowFood: false, allowPregnant: false);
    
                    var pawn = PawnGenerator.GeneratePawn(request);
                    PawnWeaponGenerator.TryGenerateWeaponFor(pawn, request);
                    if (pawn.apparel == null || pawn.apparel.WornApparelCount == 0)
                    {
                        PawnApparelGenerator.GenerateStartingApparelFor(pawn, request);
                    }

                    ApplyCrashInjuries(pawn, settings?.pawnInjurySeverityRange ?? new FloatRange(0.1f, 0.4f));
                    pawns.Add(pawn);
                }

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
                }
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

            // ✅ Just call the built-in utility — it will pick injuries and stop before death.
            // The severity multiplier just makes it more likely to add a few extra injuries.
            int extraInjuries = Mathf.RoundToInt(Mathf.Lerp(1, 4, injuryFraction));

            // Base: do a normal "crash-style" injury pass
            HealthUtility.DamageUntilDowned(pawn, allowBleedingWounds: true);

            // Optional: add a few small extra wounds for variety
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
                return null;
            }

            cachedFaction = Find.FactionManager.FirstFactionOfDef(def);
            if (cachedFaction == null)
            {
                cachedFaction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def));
                cachedFaction.hidden = true;
                Find.FactionManager.Add(cachedFaction);
            }

            return cachedFaction;
        }
    }
}
