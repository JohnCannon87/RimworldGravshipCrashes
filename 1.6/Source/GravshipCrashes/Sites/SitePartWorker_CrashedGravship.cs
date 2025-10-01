using GravshipCrashes.Util;
using RimWorld;
using RimWorld.Planet;
using Verse;
using GravshipExport; // ShipLayoutDefV2
using GravshipCrashes.Settings;

namespace GravshipCrashes.Sites
{
    /// <summary>
    /// Coordinates map generation for the crashed gravship site part.
    /// </summary>
    public class SitePartWorker_CrashedGravship : SitePartWorker
    {
        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);

            var site = map != null ? map.Parent as Site : null;
            if (site == null)
            {
                GravshipDebugUtil.LogWarning("PostMapGenerate called but site is NULL.");
                return;
            }

            var comp = site.GetComponent<WorldObjectComp_CrashedGravship>();
            ShipLayoutDefV2 layout = null;

            if (comp != null && !string.IsNullOrEmpty(comp.ShipDefName))
            {
                GravshipDebugUtil.LogMessage(string.Format("Resolved ShipDefName from site: {0}", comp.ShipDefName));
                layout = ShipLayouts.Get(comp.ShipDefName);
                if (layout == null)
                {
                    GravshipDebugUtil.LogWarning(string.Format("ShipLayouts.Get could not find '{0}'.", comp.ShipDefName));
                }
            }

            if (layout == null)
            {
                GravshipDebugUtil.LogWarning("No ShipLayout on site. Picking a random layout.");
                var settings = Mod_GravshipCrashes.Instance != null ? Mod_GravshipCrashes.Instance.Settings : null;
                layout = ShipLayouts.GetRandomAllowed(settings);
                if (layout == null) layout = ShipLayouts.GetRandom();
            }

            GravshipDebugUtil.LogMessage("Beginning crashed gravship map generation...");
            MapGenerator_CrashedGravship.Generate(map, site, layout);
            GravshipDebugUtil.LogMessage("Map generation completed successfully.");
        }
    }
}
