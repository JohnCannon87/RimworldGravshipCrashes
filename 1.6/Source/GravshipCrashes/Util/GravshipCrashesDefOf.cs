using RimWorld;
using RimWorld.Planet;
using Verse;

namespace GravshipCrashes.Util
{
    [DefOf]
    public static class GravshipCrashesDefOf
    {
        public static SitePartDef CrashedGravshipSitePart;
        public static WorldObjectDef CrashedGravshipSite;
        public static ThingSetMakerDef GravshipCrashLoot;
        public static IncidentDef CrashedGravshipIncident;
        public static FactionDef Gravship_Survivors;

        static GravshipCrashesDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(GravshipCrashesDefOf));
        }
    }
}
