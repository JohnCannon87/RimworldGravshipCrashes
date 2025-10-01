using System;
using System.Collections.Generic;
using System.Linq;
using GravshipCrashes.Util;
using RimWorld;
using UnityEngine;
using Verse;
using GravshipExport; // hard dep: ShipLayoutDefV2

namespace GravshipCrashes.Settings
{
    /// <summary>
    /// Entry point that exposes the mod settings and settings UI.
    /// </summary>
    public class Mod_GravshipCrashes : Mod
    {
        public static Mod_GravshipCrashes Instance { get; private set; }

        public ModSettings_GravshipCrashes Settings { get; }

        private Vector2 shipScrollPosition = Vector2.zero;

        public Mod_GravshipCrashes(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<ModSettings_GravshipCrashes>();
        }

        public override string SettingsCategory()
        {
            return "Gravship Crashes";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            // Storyteller / incident
            listing.Label("Storyteller".Translate());
            listing.Label("Incident Base Chance: " + Settings.incidentBaseChance.ToStringPercent());
            Settings.incidentBaseChance = listing.Slider(Settings.incidentBaseChance, 0f, 1f);

            listing.GapLine();

            // Crash damage
            listing.Label("Crash Damage".Translate());
            listing.Label("Structural Damage Min: " + Settings.shipStructureDamageRange.min.ToStringPercent());
            Settings.shipStructureDamageRange.min = listing.Slider(Settings.shipStructureDamageRange.min, 0f, 1f);
            listing.Label("Structural Damage Max: " + Settings.shipStructureDamageRange.max.ToStringPercent());
            Settings.shipStructureDamageRange.max = listing.Slider(Settings.shipStructureDamageRange.max, Settings.shipStructureDamageRange.min, 1f);

            listing.Label("Thing Damage Min: " + Settings.thingDamageRange.min.ToStringPercent());
            Settings.thingDamageRange.min = listing.Slider(Settings.thingDamageRange.min, 0f, 1f);
            listing.Label("Thing Damage Max: " + Settings.thingDamageRange.max.ToStringPercent());
            Settings.thingDamageRange.max = listing.Slider(Settings.thingDamageRange.max, Settings.thingDamageRange.min, 1f);

            listing.Gap();

            // Defenders
            listing.Label("Defenders".Translate());
            listing.Label("Pawn Injury Severity Min: " + Settings.pawnInjurySeverityRange.min.ToStringPercent());
            Settings.pawnInjurySeverityRange.min = listing.Slider(Settings.pawnInjurySeverityRange.min, 0f, 1f);
            listing.Label("Pawn Injury Severity Max: " + Settings.pawnInjurySeverityRange.max.ToStringPercent());
            Settings.pawnInjurySeverityRange.max = listing.Slider(Settings.pawnInjurySeverityRange.max, Settings.pawnInjurySeverityRange.min, 1f);

            listing.IntAdjuster(ref Settings.maxDefenders, 1, 1);
            listing.Label("Max defenders: " + Settings.maxDefenders);

            listing.GapLine();

            // Debug logging
            listing.CheckboxLabeled("Enable Logging", ref Settings.debugLogging, "Enable to get logs out to submit for a bug report.");

            listing.GapLine();
            listing.Label("Available Ships".Translate());

            DrawShipSelection(listing);

            listing.End();
        }

        private void DrawShipSelection(Listing_Standard listing)
        {
            // pull directly from the def database via ShipLayouts
            var ships = ShipLayouts.All; // IReadOnlyList<ShipLayoutDefV2>
            // keep settings’ internal map in sync with current defs
            Settings.SynchroniseShips(ships.Select(s => s.defName));

            if (ships == null || ships.Count == 0)
            {
                listing.Label("No ShipLayoutDefV2 defs were discovered. Ensure the Gravship Exporter mod is loaded above this mod.");
                return;
            }

            // scrollable checklist
            var rect = listing.GetRect(220f);
            var view = new Rect(0f, 0f, rect.width - 16f, ships.Count * 24f);
            Widgets.BeginScrollView(rect, ref shipScrollPosition, view);

            float curY = 0f;
            for (int i = 0; i < ships.Count; i++)
            {
                var ship = ships[i];
                var row = new Rect(0f, curY, view.width, 24f);

                bool allowed = Settings.AllowsShip(ship.defName);
                string label = ship.LabelCap.NullOrEmpty() ? ship.label : ship.LabelCap.ToString();
                string source = ship.modContentPack != null ? ship.modContentPack.Name : null;
                string display = string.IsNullOrEmpty(source) ? label : $"{label} ({source})";

                Widgets.CheckboxLabeled(row, display, ref allowed);
                Settings.SetAllowsShip(ship.defName, allowed);

                curY += 24f;
            }

            Widgets.EndScrollView();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            // no resolver to notify anymore
        }
    }
}
