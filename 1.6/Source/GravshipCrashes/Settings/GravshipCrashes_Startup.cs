using GravshipCrashes.Util;
using Verse;

namespace GravshipCrashes.Settings
{
    [StaticConstructorOnStartup]
    public static class GravshipCrashes_Startup
    {
        static GravshipCrashes_Startup()
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                ShipLayoutResolver.RefreshIfNeeded();
                Mod_GravshipCrashes.Instance?.Settings?.SynchroniseShips(ShipLayoutResolver.AllShipsDefNames);
            });
        }
    }
}