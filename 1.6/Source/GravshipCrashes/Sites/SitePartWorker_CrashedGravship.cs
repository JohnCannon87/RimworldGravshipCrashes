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

            if (map?.Parent is not Site site)
            {
                return;
            }

            var comp = site.GetComponent<WorldObjectComp_CrashedGravship>();
            ShipLayoutResolver.ShipEntry entry = null;
            if (comp != null && ShipLayoutResolver.TryGetEntry(comp.ShipDefName, out var resolved))
            {
                entry = resolved;
            }

            MapGenerator_CrashedGravship.Generate(map, site, entry);
        }

        public override void SiteRemoved(Site site)
        {
            base.SiteRemoved(site);
            var comp = site.GetComponent<WorldObjectComp_CrashedGravship>();
            if (comp != null)
            {
                comp.ShipDefName = string.Empty;
            }
        }
    }
}
