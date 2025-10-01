using RimWorld.Planet;
using RimWorld;
using Verse;

namespace GravshipCrashes.Util
{
    /// <summary>
    /// Stores per-site data required to generate the crashed gravship map.
    /// </summary>
    public class WorldObjectComp_CrashedGravship : WorldObjectComp
    {
        public string ShipDefName = string.Empty;
        public int StructureDamageSeed;
        public int ThingDamageSeed;
        public int LootSeed;
        public int DefenderSeed;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ShipDefName, nameof(ShipDefName));
            Scribe_Values.Look(ref StructureDamageSeed, nameof(StructureDamageSeed));
            Scribe_Values.Look(ref ThingDamageSeed, nameof(ThingDamageSeed));
            Scribe_Values.Look(ref LootSeed, nameof(LootSeed));
            Scribe_Values.Look(ref DefenderSeed, nameof(DefenderSeed));
        }
    }

    public class WorldObjectCompProperties_CrashedGravship : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_CrashedGravship()
        {
            compClass = typeof(WorldObjectComp_CrashedGravship);
        }
    }
}
