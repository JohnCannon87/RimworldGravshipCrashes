using System.Collections.Generic;
using System.Linq;
using GravshipExport;           // exporter asm
using GravshipCrashes.Settings;
using Verse;

namespace GravshipCrashes.Util
{
    public static class ShipLayouts
    {
        public static IReadOnlyList<ShipLayoutDefV2> All =>
            DefDatabase<ShipLayoutDefV2>.AllDefsListForReading;

        public static IEnumerable<ShipLayoutDefV2> Allowed(ModSettings_GravshipCrashes settings)
        {
            return settings == null
                ? Enumerable.Empty<ShipLayoutDefV2>()
                : All.Where(d => settings.AllowsShip(d.defName));
        }

        public static ShipLayoutDefV2 Get(string defName)
        {
            return DefDatabase<ShipLayoutDefV2>.GetNamedSilentFail(defName);
        }

        public static ShipLayoutDefV2 GetRandom()
        {
            return All.Count > 0 ? All.RandomElement() : null;
        }

        public static ShipLayoutDefV2 GetRandomAllowed(ModSettings_GravshipCrashes settings)
        {
            var list = Allowed(settings).ToList();
            return list.Count > 0 ? list.RandomElement() : null;
        }
    }
}
