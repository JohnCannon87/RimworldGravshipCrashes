using System.Collections.Generic;
using System.Linq;
using GravshipCrashes.Settings;
using GravshipCrashes.Util;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using GravshipExport; // for ShipLayoutDefV2

namespace GravshipCrashes.Incident
{
    /// <summary>
    /// Storyteller incident that creates a crashed gravship world site.
    /// </summary>
    public class IncidentWorker_CrashedGravship : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            // Hard dependency: if no layouts exist, we can't fire.
            if (ShipLayouts.All == null || ShipLayouts.All.Count == 0)
                return false;

            var settings = Mod_GravshipCrashes.Instance?.Settings;
            if (settings != null)
                def.baseChance = Mathf.Max(0f, settings.incidentBaseChance);

            // Any allowed ship?
            // (Allowed returns IEnumerable; ToList keeps it simple for Count)
            var allowedShips = ShipLayouts.Allowed(settings).ToList();
            return allowedShips.Count > 0;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            var settings = Mod_GravshipCrashes.Instance?.Settings;
            var allowedShips = ShipLayouts.Allowed(settings).ToList();
            if (allowedShips.Count == 0)
                return false;

            int tile;
            if (!GravshipSpawnUtility.TryFindSiteTile(out tile))
                return false;

            // Pick a layout and create the site
            ShipLayoutDefV2 chosen = allowedShips.RandomElement();
            var site = GravshipSpawnUtility.CreateSite(tile);

            // Store defName on the site comp; seeds handled inside
            GravshipSpawnUtility.ConfigureSiteMetadata(site, chosen.defName);

            // Timeout & add to world
            GravshipSpawnUtility.ConfigureTimeout(site, new IntRange(12, 20));
            Find.WorldObjects.Add(site);

            // Notify player
            string letterLabel = "Crashed Gravship";
            string letterText = "A hostile gravship has crashed nearby, the survivors will eventually rebuild and leave but until then they will defend their ship with their lives.".Translate();
            SendStandardLetter(letterLabel, letterText, LetterDefOf.PositiveEvent, parms, site);

            return true;
        }
    }
}
