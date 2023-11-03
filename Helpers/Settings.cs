using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrakiaXYZ.QuestTracker.Helpers
{
    internal class Settings
    {
        public const string GeneralSectionTitle = "1. General";
        public const string PanelSettingsSectionTitle = "2. Panel Settings";
        public const string DisplaySectionTitle = "3. Display";

        public static ConfigFile Config;

        public static ConfigEntry<bool> VisibleAtRaidStart;
        public static ConfigEntry<KeyboardShortcut> PanelToggleKey;
        public static ConfigEntry<bool> AutoHide;
        public static ConfigEntry<int> AutoHideTimer;
        public static ConfigEntry<bool> ShowOnObjective;

        public static ConfigEntry<bool> IncludeMapQuests;
        public static ConfigEntry<bool> ExcludeOtherMapQuests;
        public static ConfigEntry<bool> ProgressAsPercent;
        public static ConfigEntry<bool> HideCompletedQuests;
        public static ConfigEntry<bool> ShowObjectives;
        public static ConfigEntry<bool> ObjectivesAsPercent;
        public static ConfigEntry<bool> HideCompletedObjectives;

        public static ConfigEntry<int> MaxWidth;
        public static ConfigEntry<float> Transparency;
        public static ConfigEntry<int> MainFontSize;
        public static ConfigEntry<int> SubFontSize;
        public static ConfigEntry<EAlignment> Alignment;
        public static ConfigEntry<bool> CoolKidsClub;

        public static List<ConfigEntryBase> ConfigEntries = new List<ConfigEntryBase>();

        public static void Init(ConfigFile Config)
        {
            Settings.Config = Config;

            ConfigEntries.Add(VisibleAtRaidStart = Config.Bind(
                GeneralSectionTitle,
                "Visible At Raid Start",
                true,
                new ConfigDescription(
                    "Whether the panel is visible at raid start",
                    null,
                    new ConfigurationManagerAttributes { })));

            ConfigEntries.Add(PanelToggleKey = Config.Bind(
                GeneralSectionTitle,
                "Panel Toggle Key",
                new KeyboardShortcut(KeyCode.I),
                new ConfigDescription(
                    "Key used to toggle the Quest Tracker panel",
                    null,
                    new ConfigurationManagerAttributes { })));

            ConfigEntries.Add(AutoHide = Config.Bind(
                GeneralSectionTitle,
                "Auto Hide",
                true,
                new ConfigDescription(
                    "Whether to automatically hide the list after a timeout",
                    null,
                    new ConfigurationManagerAttributes { })));

            ConfigEntries.Add(AutoHideTimer = Config.Bind(
                GeneralSectionTitle,
                "Auto Hide Timer",
                5,
                new ConfigDescription(
                    "How long to show before hiding",
                    new AcceptableValueRange<int>(1, 60),
                    new ConfigurationManagerAttributes { })));

            ConfigEntries.Add(ShowOnObjective = Config.Bind(
                GeneralSectionTitle,
                "Show On Objective Progress",
                true,
                new ConfigDescription(
                    "Whether to temporarily show the list on objective progress",
                    null,
                    new ConfigurationManagerAttributes { })));

            ConfigEntries.Add(IncludeMapQuests = Config.Bind(
                PanelSettingsSectionTitle,
                "Include Current Map Quests",
                false,
                new ConfigDescription(
                    "Whether to always include the current map's quests",
                    null,
                    new ConfigurationManagerAttributes { })));

            ConfigEntries.Add(ExcludeOtherMapQuests = Config.Bind(
                PanelSettingsSectionTitle,
                "Exclude Other Map Quests",
                true,
                new ConfigDescription(
                    "Whether to always exclude quests not for the current or 'Any' map",
                    null,
                    new ConfigurationManagerAttributes { })));

            ConfigEntries.Add(ProgressAsPercent = Config.Bind(
                PanelSettingsSectionTitle,
                "Progress As Percent",
                true,
                new ConfigDescription(
                    "Whether to show total quest progress as a percentage, or numeric count",
                    null,
                    new ConfigurationManagerAttributes { })));

            ConfigEntries.Add(HideCompletedQuests = Config.Bind(
                PanelSettingsSectionTitle,
                "Hide Completed Quests",
                true,
                new ConfigDescription(
                    "Whether to hide quests that are ready to be handed in",
                    null,
                    new ConfigurationManagerAttributes { })));

            ConfigEntries.Add(ShowObjectives = Config.Bind(
                PanelSettingsSectionTitle,
                "Show Objectives",
                true,
                new ConfigDescription(
                    "Whether to show individual quest objectives",
                    null,
                    new ConfigurationManagerAttributes { })));

            ConfigEntries.Add(ObjectivesAsPercent = Config.Bind(
                PanelSettingsSectionTitle,
                "Objectives As Percent",
                false,
                new ConfigDescription(
                    "Whether to show quest objectives as a percentage, or numeric count",
                    null,
                    new ConfigurationManagerAttributes { })));

            ConfigEntries.Add(HideCompletedObjectives = Config.Bind(
                PanelSettingsSectionTitle,
                "Hide Completed Objectives",
                true,
                new ConfigDescription(
                    "Whether to hide completed objectives from the objective list",
                    null,
                    new ConfigurationManagerAttributes { })));

            // Special case where I want to allow different values for different resolutions
            string maxWidthName = $"Maximum Width ({Screen.width}p)";
            ConfigEntries.Add(MaxWidth = Config.Bind(
                DisplaySectionTitle,
                maxWidthName,
                Screen.width / 6,
                new ConfigDescription(
                    "The maximum width of the panel before text wraps",
                    new AcceptableValueRange<int>(250, Screen.width),
                    new ConfigurationManagerAttributes { })));

            ConfigEntries.Add(Transparency = Config.Bind(
                DisplaySectionTitle,
                "Panel Background Transparency",
                0.4f,
                new ConfigDescription(
                    "The transparency value for the panel background",
                    new AcceptableValueRange<float>(0f, 1f),
                    new ConfigurationManagerAttributes { })));

            ConfigEntries.Add(MainFontSize = Config.Bind(
                DisplaySectionTitle,
                "Font Size",
                30,
                new ConfigDescription(
                    "The main font size",
                    new AcceptableValueRange<int>(12, 92),
                    new ConfigurationManagerAttributes { })));

            ConfigEntries.Add(SubFontSize = Config.Bind(
                DisplaySectionTitle,
                "Objective Font Size",
                20,
                new ConfigDescription(
                    "The objective font size",
                    new AcceptableValueRange<int>(12, 92),
                    new ConfigurationManagerAttributes { })));

            ConfigEntries.Add(Alignment = Config.Bind(
                DisplaySectionTitle,
                "Alignment",
                EAlignment.Right,
                new ConfigDescription(
                    "Whether to align the tracker to the right or left",
                    null,
                    new ConfigurationManagerAttributes { })));

            ConfigEntries.Add(CoolKidsClub = Config.Bind(
                DisplaySectionTitle,
                "Cool Kids Mode",
                false,
                new ConfigDescription(
                    "Join the cool kids club",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true })));

            RecalcOrder();
        }

        private static void RecalcOrder()
        {
            // Set the Order field for all settings, to avoid unnecessary changes when adding new settings
            int settingOrder = ConfigEntries.Count;
            foreach (var entry in ConfigEntries) {
                ConfigurationManagerAttributes attributes = entry.Description.Tags[0] as ConfigurationManagerAttributes;
                if (attributes != null)
                {
                    attributes.Order = settingOrder;
                }

                settingOrder--;
            }
        }

        public enum EAlignment
        {
            Right = 0,
            Left = 1,
        }
    }
}
