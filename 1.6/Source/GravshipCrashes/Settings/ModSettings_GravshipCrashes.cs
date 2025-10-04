using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace GravshipCrashes.Settings
{
    /// <summary>
    /// Stores all configurable values for the gravship crash content.
    /// </summary>
    public class ModSettings_GravshipCrashes : ModSettings
    {
        private const float MinDamage = 0f;
        private const float MaxDamage = 1f;

        public float incidentBaseChance = 0.04f;

        public FloatRange shipStructureDamageRange = new FloatRange(0.15f, 0.45f);
        public FloatRange thingDamageRange = new FloatRange(0.1f, 0.35f);
        public FloatRange pawnInjurySeverityRange = new FloatRange(0.1f, 0.4f);

        public bool debugLogging = false;

        private Dictionary<string, bool> shipAllowances = new Dictionary<string, bool>();

        public bool AllowsShip(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return false;
            }

            if (!shipAllowances.TryGetValue(defName, out var allowed))
            {
                shipAllowances[defName] = true;
                return true;
            }

            return allowed;
        }

        public void SetAllowsShip(string defName, bool allowed)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return;
            }
            shipAllowances[defName] = allowed;
        }

        public void SetAllShips(bool allowed)
        {
            foreach (var key in shipAllowances.Keys.ToList())
            {
                shipAllowances[key] = allowed;
            }
        }

        public IEnumerable<KeyValuePair<string, bool>> ShipAllowances => shipAllowances;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref incidentBaseChance, nameof(incidentBaseChance), 0.4f);

            Scribe_Values.Look(ref shipStructureDamageRange.min, "shipStructureDamageMin", 0.15f);
            Scribe_Values.Look(ref shipStructureDamageRange.max, "shipStructureDamageMax", 0.45f);

            Scribe_Values.Look(ref thingDamageRange.min, "thingDamageMin", 0.1f);
            Scribe_Values.Look(ref thingDamageRange.max, "thingDamageMax", 0.35f);

            Scribe_Values.Look(ref pawnInjurySeverityRange.min, "pawnInjurySeverityMin", 0.1f);
            Scribe_Values.Look(ref pawnInjurySeverityRange.max, "pawnInjurySeverityMax", 0.4f);

            Scribe_Values.Look(ref debugLogging, nameof(debugLogging), true);

            Scribe_Collections.Look(ref shipAllowances, "shipAllowances", LookMode.Value, LookMode.Value);

            if (shipAllowances == null)
            {
                shipAllowances = new Dictionary<string, bool>();
            }

            ClampValues();
        }

        private void ClampValues()
        {
            incidentBaseChance = Mathf.Clamp01(incidentBaseChance);

            shipStructureDamageRange.min = Mathf.Clamp(shipStructureDamageRange.min, MinDamage, MaxDamage);
            shipStructureDamageRange.max = Mathf.Clamp(shipStructureDamageRange.max, shipStructureDamageRange.min, MaxDamage);

            thingDamageRange.min = Mathf.Clamp(thingDamageRange.min, MinDamage, MaxDamage);
            thingDamageRange.max = Mathf.Clamp(thingDamageRange.max, thingDamageRange.min, MaxDamage);

            pawnInjurySeverityRange.min = Mathf.Clamp(pawnInjurySeverityRange.min, MinDamage, MaxDamage);
            pawnInjurySeverityRange.max = Mathf.Clamp(pawnInjurySeverityRange.max, pawnInjurySeverityRange.min, MaxDamage);
        }

        public void SynchroniseShips(IEnumerable<string> defNames)
        {
            if (defNames == null)
            {
                return;
            }

            var known = defNames.ToList();
            foreach (var defName in known)
            {
                if (!shipAllowances.ContainsKey(defName))
                {
                    shipAllowances[defName] = true;
                }
            }

            var missing = shipAllowances.Keys.Where(key => !known.Contains(key)).ToList();
            foreach (var removed in missing)
            {
                shipAllowances.Remove(removed);
            }
        }
    }
}
