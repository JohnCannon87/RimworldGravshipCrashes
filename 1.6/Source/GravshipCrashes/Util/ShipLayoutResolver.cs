using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GravshipCrashes.Settings;
using RimWorld;
using Verse;

namespace GravshipCrashes.Util
{
    /// <summary>
    /// Discovers and exposes ShipLayoutDefV2 defs provided by the Gravship Exporter mod.
    /// </summary>
    public static class ShipLayoutResolver
    {
        private const string LayoutTypeName = "GravshipExport.ShipLayoutDefV2";
        private static Type shipLayoutType;
        private static readonly List<ShipEntry> allShips = new List<ShipEntry>();
        private static bool missingExporterWarningPrinted;

        /// <summary>
        /// Information about a ship layout option.
        /// </summary>
        public class ShipEntry
        {
            public string DefName { get; internal set; } = string.Empty;
            public string Label { get; internal set; } = string.Empty;
            public string SourceMod { get; internal set; } = string.Empty;
            internal Def Def { get; set; }
            internal object RawDef { get; set; }

            public string LabelWithSource => string.IsNullOrEmpty(SourceMod) ? Label : $"{Label} ({SourceMod})";
        }

        public static IReadOnlyList<ShipEntry> AllShips => allShips;

        public static IEnumerable<string> AllShipsDefNames => allShips.Select(s => s.DefName);

        public static bool HasExporterContent => shipLayoutType != null && allShips.Count > 0;

        public static void RefreshIfNeeded()
        {
            if (shipLayoutType != null && allShips.Count > 0)
            {
                return;
            }

            Refresh();
        }

        public static void Refresh()
        {
            allShips.Clear();
            missingExporterWarningPrinted = false;

            shipLayoutType = GenTypes.GetTypeInAnyAssembly(LayoutTypeName);
            if (shipLayoutType == null)
            {
                WarnMissingExporter();
                return;
            }

            try
            {
                var dbType = typeof(DefDatabase<>).MakeGenericType(shipLayoutType);
                var prop = dbType.GetProperty("AllDefsListForReading", BindingFlags.Static | BindingFlags.Public);
                if (prop?.GetValue(null) is IEnumerable enumerable)
                {
                    foreach (var obj in enumerable)
                    {
                        if (obj is Def def)
                        {
                            var label = def.LabelCap;
                            if (label.NullOrEmpty())
                            {
                                label = def.label;
                            }

                            var entry = new ShipEntry
                            {
                                DefName = def.defName,
                                Label = label,
                                SourceMod = def.modContentPack?.Name ?? string.Empty,
                                Def = def,
                                RawDef = obj
                            };

                            allShips.Add(entry);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("[GravshipCrashes] Failed to enumerate ShipLayoutDefV2 defs: " + ex);
            }

            allShips.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));

            if (allShips.Count == 0)
            {
                WarnMissingExporter();
            }
        }

        public static void NotifySettingsChanged()
        {
            missingExporterWarningPrinted = false;
        }

        public static ShipEntry ResolveRandomLayout()
        {
            // Ensure we've tried to populate the list
            RefreshIfNeeded();

            if (allShips.Count == 0)
            {
                Log.Warning("[GravshipCrashes] ResolveRandomLayout() called but no ship layouts were found. " +
                            "This usually means the Gravship Exporter mod is not installed or no ShipLayoutDefV2 defs are loaded.");
                return null;
            }

            var chosen = allShips.RandomElement();
            Log.Message($"[GravshipCrashes] Randomly selected ship layout: {chosen.DefName} ({chosen.Label})");
            return chosen;
        }

        public static bool TryGetEntry(string defName, out ShipEntry entry)
        {
            entry = allShips.FirstOrDefault(e => string.Equals(e.DefName, defName, StringComparison.OrdinalIgnoreCase));
            return entry != null;
        }

        public static List<ShipEntry> AllowedShips(ModSettings_GravshipCrashes settings)
        {
            var result = new List<ShipEntry>();
            if (settings == null)
            {
                return result;
            }

            foreach (var ship in allShips)
            {
                if (settings.AllowsShip(ship.DefName))
                {
                    result.Add(ship);
                }
            }

            return result;
        }

        public static bool TryResolveLayoutWorker(ShipEntry entry, out object worker)
        {
            worker = null;
            if (entry?.RawDef == null)
            {
                return false;
            }

            var workerField = shipLayoutType?.GetField("resolvedLayout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (workerField != null)
            {
                worker = workerField.GetValue(entry.RawDef);
                if (worker != null)
                {
                    return true;
                }
            }

            var property = shipLayoutType?.GetProperty("ResolvedLayout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                worker = property.GetValue(entry.RawDef);
                return worker != null;
            }

            return false;
        }

        private static void WarnMissingExporter()
        {
            if (missingExporterWarningPrinted)
            {
                return;
            }

            missingExporterWarningPrinted = true;
            Log.Warning("[GravshipCrashes] ShipLayoutDefV2 type not found. Install the Gravship Exporter mod to enable crashed gravship sites.");
        }
    }
}
