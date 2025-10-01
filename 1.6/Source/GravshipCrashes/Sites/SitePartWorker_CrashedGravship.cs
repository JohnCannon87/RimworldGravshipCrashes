using GravshipCrashes.Util;
using RimWorld;
using RimWorld.Planet;
using Verse;

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

            var site = map?.Parent as Site;
            if (site == null)
                return;

            var comp = site.GetComponent<WorldObjectComp_CrashedGravship>();
            ShipLayoutResolver.ShipEntry entry = null;

            if (comp != null && !string.IsNullOrEmpty(comp.ShipDefName))
            {
                ShipLayoutResolver.TryGetEntry(comp.ShipDefName, out entry);
            }

            // ✅ fallback: if no layout is defined, pick one randomly
            if (entry == null)
            {
                Log.Warning("[GravshipCrashes] No ShipDefName on site. Picking a random layout.");
                entry = ShipLayoutResolver.ResolveRandomLayout();
            }

            MapGenerator_CrashedGravship.Generate(map, site, entry);
        }

    }
}
