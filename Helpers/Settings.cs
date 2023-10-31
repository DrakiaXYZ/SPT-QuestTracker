using BepInEx.Configuration;
using UnityEngine;

namespace DrakiaXYZ.QuestTracker.Helpers
{
    internal class Settings
    {
        private const string GeneralSectionTitle = "1. General";
        private const string InGameSectionTitle = "2. In Game";
        private const string DisplaySectionTitle = "3. Display";

        public static ConfigEntry<bool> VisibleAtRaidStart;
        public static ConfigEntry<KeyboardShortcut> PanelToggleKey;

        public static ConfigEntry<bool> IncludeMapQuests;
        public static ConfigEntry<bool> ExcludeOtherMapQuests;
        public static ConfigEntry<bool> AutoHide;
        public static ConfigEntry<int> AutoHideTimer;
        public static ConfigEntry<bool> ShowOnObjective;
        public static ConfigEntry<bool> ProgressAsPercent;
        public static ConfigEntry<bool> ObjectivesAsPercent;
        public static ConfigEntry<bool> ShowObjectives;

        public static ConfigEntry<int> MainFontSize;
        public static ConfigEntry<int> SubFontSize;

        public static void Init(ConfigFile Config)
        {
            VisibleAtRaidStart = Config.Bind(
                GeneralSectionTitle,
                "Visible At Raid Start",
                false,
                new ConfigDescription(
                    "Whether the panel is visible at raid start",
                    null,
                    new ConfigurationManagerAttributes { Order = 2 }));

            PanelToggleKey = Config.Bind(
                GeneralSectionTitle,
                "Panel Toggle Key",
                new KeyboardShortcut(KeyCode.I),
                new ConfigDescription(
                    "Key used to toggle the Quest Tracker panel",
                    null,
                    new ConfigurationManagerAttributes { Order = 1 }));

            IncludeMapQuests = Config.Bind(
                InGameSectionTitle,
                "Include Current Map Quests",
                false,
                new ConfigDescription(
                    "Whether to always include the current map's quests",
                    null,
                    new ConfigurationManagerAttributes { Order = 8 }));

            ExcludeOtherMapQuests = Config.Bind(
                InGameSectionTitle,
                "Exclude Other Map Quests",
                true,
                new ConfigDescription(
                    "Whether to always exclude quests not for the current or 'Any' map",
                    null,
                    new ConfigurationManagerAttributes { Order = 7 }));

            ShowOnObjective = Config.Bind(
                InGameSectionTitle,
                "Show On Objective Progress",
                true,
                new ConfigDescription(
                    "Whether to temporarily show the list on objective progress",
                    null,
                    new ConfigurationManagerAttributes { Order = 6 }));

            AutoHide = Config.Bind(
                InGameSectionTitle,
                "Auto Hide",
                true,
                new ConfigDescription(
                    "Whether to automatically hide the list after a timeout",
                    null,
                    new ConfigurationManagerAttributes { Order = 5 }));

            AutoHideTimer = Config.Bind(
                InGameSectionTitle,
                "Auto Hide Timer",
                5,
                new ConfigDescription(
                    "How long to show before hiding",
                    new AcceptableValueRange<int>(1, 60),
                    new ConfigurationManagerAttributes { Order = 4 }));

            ProgressAsPercent = Config.Bind(
                InGameSectionTitle,
                "Progress As Percent",
                true,
                new ConfigDescription(
                    "Whether to show the progress as a percentage, or numeric count",
                    null,
                    new ConfigurationManagerAttributes { Order = 3 }));

            ObjectivesAsPercent = Config.Bind(
                InGameSectionTitle,
                "Objectives As Percent",
                false,
                new ConfigDescription(
                    "Whether to show individual objectives as a percentage, or numeric count",
                    null,
                    new ConfigurationManagerAttributes { Order = 2 }));

            ShowObjectives = Config.Bind(
                InGameSectionTitle,
                "Show Objectives",
                false,
                new ConfigDescription(
                    "Whether to show individual objetices",
                    null,
                    new ConfigurationManagerAttributes { Order = 1 }));

            MainFontSize = Config.Bind(
                DisplaySectionTitle,
                "Font Size",
                36,
                new ConfigDescription(
                    "The main font size",
                    new AcceptableValueRange<int>(12, 92),
                    new ConfigurationManagerAttributes { Order = 2, IsAdvanced = true }));

            SubFontSize = Config.Bind(
                DisplaySectionTitle,
                "Objective Font Size",
                20,
                new ConfigDescription(
                    "The objective font size",
                    new AcceptableValueRange<int>(12, 92),
                    new ConfigurationManagerAttributes { Order = 1, IsAdvanced = true }));
        }
    }
}
