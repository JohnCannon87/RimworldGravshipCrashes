using System.Collections.Generic;
using GravshipCrashes.Settings;
using GravshipCrashes.Sites;
using GravshipCrashes.Util;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace GravshipCrashes.Incident
{
    /// <summary>
    /// Storyteller incident that creates a crashed gravship world site.
    /// </summary>
    public class IncidentWorker_CrashedGravship : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            ShipLayoutResolver.RefreshIfNeeded();

            if (!ShipLayoutResolver.HasExporterContent)
            {
                return false;
            }

            var settings = Mod_GravshipCrashes.Instance?.Settings;
            if (settings != null)
            {
                def.baseChance = Mathf.Max(0f, settings.incidentBaseChance);
            }

            var allowedShips = ShipLayoutResolver.AllowedShips(settings);
            return allowedShips.Count > 0;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            var settings = Mod_GravshipCrashes.Instance?.Settings;
            var allowedShips = ShipLayoutResolver.AllowedShips(settings);
            if (allowedShips.Count == 0)
            {
                return false;
            }

            if (!GravshipSpawnUtility.TryFindSiteTile(out var tile))
            {
                return false;
            }

            var shipEntry = allowedShips.RandomElement();
            var site = GravshipSpawnUtility.CreateSite(tile);
            GravshipSpawnUtility.ConfigureSiteMetadata(site, shipEntry);
            GravshipSpawnUtility.ConfigureTimeout(site, new IntRange(12, 20));
            Find.WorldObjects.Add(site);

            var letterLabel = "Crashed Gravship";
            var letterText = "A gravship has crashed nearby, spilling survivors and salvage across the landscape.";
            SendStandardLetter(letterLabel, letterText, LetterDefOf.PositiveEvent, parms, site);
            return true;
        }
    }
}
