using System;
using System.Collections.Generic;
using GravshipCrashes.Util;
using RimWorld;
using UnityEngine;
using Verse;

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

            listing.Label("Storyteller".Translate());
            listing.Label("Days Between Crash Checks: " + Settings.daysBetweenCrashChecks.ToString("0.0"));
            Settings.daysBetweenCrashChecks = listing.Slider(Settings.daysBetweenCrashChecks, 0.25f, 20f);
            listing.Label("Incident Base Chance: " + Settings.incidentBaseChance.ToStringPercent());
            Settings.incidentBaseChance = listing.Slider(Settings.incidentBaseChance, 0f, 1f);

            listing.GapLine();
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
            listing.Label("Defenders".Translate());
            listing.Label("Pawn Injury Severity Min: " + Settings.pawnInjurySeverityRange.min.ToStringPercent());
            Settings.pawnInjurySeverityRange.min = listing.Slider(Settings.pawnInjurySeverityRange.min, 0f, 1f);
            listing.Label("Pawn Injury Severity Max: " + Settings.pawnInjurySeverityRange.max.ToStringPercent());
            Settings.pawnInjurySeverityRange.max = listing.Slider(Settings.pawnInjurySeverityRange.max, Settings.pawnInjurySeverityRange.min, 1f);
            listing.IntAdjuster(ref Settings.maxDefenders, 1, 1);
            listing.Label("Max defenders: " + Settings.maxDefenders);

            listing.GapLine();
            listing.CheckboxLabeled("Enable Dev Spawn Button", ref Settings.devEnableWorldSpawnButton, "If enabled, a debug action is added on the world map to instantly spawn a crashed gravship site.");

            listing.GapLine();
            listing.Label("Available Ships".Translate());

            DrawShipSelection(listing);

            listing.End();
        }

        private void DrawShipSelection(Listing_Standard listing)
        {
            var ships = ShipLayoutResolver.AllShips;
            Settings.SynchroniseShips(ShipLayoutResolver.AllShipsDefNames);

            if (!ShipLayoutResolver.HasExporterContent)
            {
                listing.Label("Gravship Exporter content not found. Enable the exporter mod to unlock gravship crash sites.");
                return;
            }

            if (ships.Count == 0)
            {
                listing.Label("No ShipLayoutDefV2 defs were discovered. Ensure the exporter mod is loaded.");
                return;
            }

            var rect = listing.GetRect(220f);
            var view = new Rect(0f, 0f, rect.width - 16f, ships.Count * 24f);
            Widgets.BeginScrollView(rect, ref shipScrollPosition, view);
            var curY = 0f;
            foreach (var ship in ships)
            {
                var row = new Rect(0f, curY, view.width, 24f);
                bool allowed = Settings.AllowsShip(ship.DefName);
                Widgets.CheckboxLabeled(row, ship.LabelWithSource, ref allowed);
                Settings.SetAllowsShip(ship.DefName, allowed);
                curY += 24f;
            }

            Widgets.EndScrollView();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            ShipLayoutResolver.NotifySettingsChanged();
        }
    }
}
