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
        private int selectedTab = 0; // 0 = General, 1 = Ships

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
            // push tabs down slightly so they don't overlap the window title
            var tabBarRect = new Rect(inRect.x, inRect.y + 20f, inRect.width, 32f);
            var contentRect = new Rect(inRect.x, inRect.y + 48f, inRect.width, inRect.height - 48f);


            DrawTabs(tabBarRect);

            if (selectedTab == 0)
            {
                DrawGeneralSettings(contentRect);
            }
            else
            {
                DrawShipSettings(contentRect);
            }
        }

        private void DrawTabs(Rect rect)
        {
            var tabs = new List<TabRecord>
            {
                new TabRecord("General", () => selectedTab = 0, selectedTab == 0),
                new TabRecord("Ships", () => selectedTab = 1, selectedTab == 1)
            };

            TabDrawer.DrawTabs(rect, tabs);
        }

        private void DrawGeneralSettings(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            // Storyteller / incident
            listing.Label("Storyteller");
            listing.Label("Incident Base Chance: " + Settings.incidentBaseChance.ToStringPercent());
            Settings.incidentBaseChance = listing.Slider(Settings.incidentBaseChance, 0f, 1f);

            listing.GapLine();

            // Crash damage
            listing.Label("Crash Damage");
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
            listing.Label("Defenders");
            listing.Label("Pawn Injury Severity Min: " + Settings.pawnInjurySeverityRange.min.ToStringPercent());
            Settings.pawnInjurySeverityRange.min = listing.Slider(Settings.pawnInjurySeverityRange.min, 0f, 1f);
            listing.Label("Pawn Injury Severity Max: " + Settings.pawnInjurySeverityRange.max.ToStringPercent());
            Settings.pawnInjurySeverityRange.max = listing.Slider(Settings.pawnInjurySeverityRange.max, Settings.pawnInjurySeverityRange.min, 1f);

            listing.GapLine();

            // Debug logging
            listing.CheckboxLabeled("Enable Logging", ref Settings.debugLogging, "Enable to get logs out to submit for a bug report.");

            listing.End();
        }

        private void DrawShipSettings(Rect inRect)
        {
            var ships = ShipLayouts.All; // IReadOnlyList<ShipLayoutDefV2>
            Settings.SynchroniseShips(ships?.Select(s => s.defName));

            var listing = new Listing_Standard();
            listing.Begin(inRect);

            if (ships == null || ships.Count == 0)
            {
                listing.Label("No ShipLayoutDefV2 defs were discovered. Ensure the Gravship Exporter mod is loaded above this mod.");
                listing.End();
                return;
            }

            // Buttons: Select All / Deselect All
            var buttonRow = listing.GetRect(32f);
            float buttonWidth = buttonRow.width / 2f - 4f;
            if (Widgets.ButtonText(new Rect(buttonRow.x, buttonRow.y, buttonWidth, 32f), "Select All"))
            {
                Settings.SetAllShips(true);
            }
            if (Widgets.ButtonText(new Rect(buttonRow.x + buttonWidth + 8f, buttonRow.y, buttonWidth, 32f), "Deselect All"))
            {
                Settings.SetAllShips(false);
            }

            listing.GapLine();

            // Scrollable list
            var scrollRect = listing.GetRect(inRect.height - 100f);
            var view = new Rect(0f, 0f, scrollRect.width - 16f, ships.Count * 24f);
            Widgets.BeginScrollView(scrollRect, ref shipScrollPosition, view);

            float curY = 0f;
            foreach (var ship in ships)
            {
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
            listing.End();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
        }
    }
}
