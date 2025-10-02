using System.Collections.Generic;
using System.Linq;
using GravshipCrashes.Settings;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using GravshipExport;

namespace GravshipCrashes.Util
{
    public static class DefenderGen
    {
        private static Faction cachedFaction;

        public static List<Pawn> SpawnDefenders(
            Map map,
            CellRect area,
            ModSettings_GravshipCrashes settings,
            ShipLayoutDefV2 layout = null,
            Faction faction = null)
        {
            GravshipDebugUtil.LogMessage("Spawning defenders for crashed gravship...");

            if (faction == null)
                faction = EnsureFaction();

            if (faction == null)
            {
                GravshipDebugUtil.LogWarning("No valid survivor faction found. No defenders will spawn.");
                return new List<Pawn>();
            }

            int defenderCount = CalculateDefenderCountFromLayout(layout, settings);
            GravshipDebugUtil.LogMessage($"[Defenders] Final defender count: {defenderCount}");

            var pawns = new List<Pawn>();

            Rand.PushState(map.Tile + 1337);
            try
            {
                for (int i = 0; i < defenderCount; i++)
                {
                    var request = new PawnGenerationRequest(
                        DefDatabase<PawnKindDef>.GetNamed("GravshipCrew"),
                        faction,
                        PawnGenerationContext.NonPlayer,
                        map.Tile,
                        forceGenerateNewPawn: true,
                        allowDowned: false,
                        canGeneratePawnRelations: true,
                        mustBeCapableOfViolence: true,
                        fixedIdeo: faction.ideos?.PrimaryIdeo,
                        allowFood: true,
                        allowPregnant: true,
                        allowAddictions: true,
                        forceNoBackstory: false,
                        onlyUseForcedBackstories: false
                    );

                    var pawn = PawnGenerator.GeneratePawn(request);

                    if (pawn.Faction != faction)
                        pawn.SetFaction(faction);

                    EquipCrewLoadout(pawn);
                    ApplyCrashInjuries(pawn, settings?.pawnInjurySeverityRange ?? new FloatRange(0.05f, 0.2f));

                    pawns.Add(pawn);
                }

                GravshipDebugUtil.LogMessage("Placing defenders on the map...");
                var cells = area.Cells.Where(c => c.InBounds(map) && c.Standable(map)).InRandomOrder().ToList();

                foreach (var pawn in pawns)
                {
                    IntVec3 cell = cells.Count > 0
                        ? cells.PopLast()
                        : CellFinder.RandomClosewalkCellNear(area.CenterCell, map, 10);

                    GenSpawn.Spawn(pawn, cell, map);
                    GravshipDebugUtil.LogMessage($"Spawned defender {pawn.LabelShortCap} at {cell}.");
                }

                GravshipDebugUtil.LogMessage($"✅ Successfully spawned {pawns.Count} defenders.");
            }
            finally
            {
                Rand.PopState();
            }

            return pawns;
        }

        private static int CalculateDefenderCountFromLayout(ShipLayoutDefV2 layout, ModSettings_GravshipCrashes settings)
        {
            if (layout == null)
            {
                GravshipDebugUtil.LogWarning("[Defenders] No layout provided, falling back to default 4 defenders.");
                return 4;
            }

            var allThings = layout.rows
                .Where(r => r != null)
                .SelectMany(r => r)
                .Where(c => c?.things != null)
                .SelectMany(c => c.things)
                .ToList();

            int bedCount = allThings.Count(t => t.defName.ToLowerInvariant().Contains("bed"));
            if (bedCount > 0)
            {
                GravshipDebugUtil.LogMessage($"[Defenders] Defender count from beds: {bedCount}");
                return bedCount;
            }

            int seatCount = allThings.Count(t =>
                t.defName.ToLowerInvariant().Contains("chair") ||
                t.defName.ToLowerInvariant().Contains("seat") ||
                t.defName.ToLowerInvariant().Contains("bench"));
            if (seatCount > 0)
            {
                GravshipDebugUtil.LogMessage($"[Defenders] Defender count from seats: {seatCount}");
                return seatCount;
            }

            int areaCells = layout.width * layout.height;
            int fallbackCount = Mathf.Max(1, areaCells / 20);
            GravshipDebugUtil.LogMessage($"[Defenders] Defender count from ship size ({areaCells} cells): {fallbackCount}");
            return fallbackCount;
        }

        private static void ApplyCrashInjuries(Pawn pawn, FloatRange severity)
        {
            if (pawn == null) return;

            float injuryFraction = Mathf.Max(0.05f, severity.RandomInRange); // 👈 never below 5%
            int injuryCount = Rand.RangeInclusive(1, 3);
            GravshipDebugUtil.LogMessage($"Applying light crash injuries to {pawn.LabelShortCap} (severity {injuryFraction:P0}, injuries: {injuryCount})");

            var injuryDefs = new[] { "Bruise", "Cut", "Crack", "Scratch" };

            for (int i = 0; i < injuryCount; i++)
            {
                var part = pawn.health.hediffSet
                    .GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Outside)
                    .Where(p => !p.def.conceptual && p.coverageAbs > 0f)
                    .InRandomOrder()
                    .FirstOrDefault();

                if (part != null)
                {
                    var chosen = injuryDefs.RandomElement();
                    var def = DefDatabase<HediffDef>.GetNamed(chosen);
                    var hediff = HediffMaker.MakeHediff(def, pawn, part);
                    hediff.Severity = Rand.Range(0.05f, 0.2f) * injuryFraction;
                    pawn.health.AddHediff(hediff);
                }
            }
        }

        private static void EquipCrewLoadout(Pawn pawn)
        {
            pawn.apparel?.DestroyAll();
            pawn.equipment?.DestroyAllEquipment();

            var weaponDefs = DefDatabase<ThingDef>.AllDefs
                .Where(td => td.IsWeapon && td.techLevel >= TechLevel.Industrial)
                .ToList();

            if (weaponDefs.TryRandomElement(out var weapon))
            {
                var weaponThing = ThingMaker.MakeThing(weapon);
                pawn.equipment.AddEquipment((ThingWithComps)weaponThing);
            }

            var vacSuitDefs = DefDatabase<ThingDef>.AllDefs
                .Where(td => td.IsApparel && td.defName.ToLower().Contains("vacsuit"))
                .Where(td => !td.defName.ToLower().Contains("children")) // 👈 filter out child suits
                .Where(td => ApparelUtility.HasPartsToWear(pawn, td))
                .InRandomOrder()
                .Take(3)
                .ToList();

            foreach (var apparelDef in vacSuitDefs)
            {
                var apparel = ThingMaker.MakeThing(apparelDef);
                if (!pawn.apparel.CanWearWithoutDroppingAnything(apparelDef))
                {
                    GravshipDebugUtil.LogMessage($"Skipping {apparelDef.defName} for {pawn.LabelShortCap}: cannot wear.");
                    continue;
                }

                pawn.apparel.Wear((Apparel)apparel, dropReplacedApparel: true);
            }
        }

        private static Faction EnsureFaction()
        {
            if (cachedFaction != null && !cachedFaction.defeated)
                return cachedFaction;

            var def = GravshipCrashesDefOf.Gravship_Survivors;
            if (def == null)
            {
                GravshipDebugUtil.LogWarning("Gravship_Survivors faction def not found.");
                return null;
            }

            cachedFaction = Find.FactionManager.FirstFactionOfDef(def);
            if (cachedFaction == null)
            {
                GravshipDebugUtil.LogMessage("Generating Gravship Survivors faction...");
                cachedFaction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def));
                Find.FactionManager.Add(cachedFaction);
            }

            GravshipDebugUtil.LogMessage($"Using hostile defender faction: {cachedFaction.Name}");
            return cachedFaction;
        }

        private static T PopLast<T>(this List<T> list)
        {
            var item = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
            return item;
        }
    }
}
