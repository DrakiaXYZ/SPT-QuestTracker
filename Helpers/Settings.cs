using BepInEx.Configuration;
using System.Collections.Generic;
using UnityEngine;

namespace DrakiaXYZ.QuestTracker.Helpers
{
    internal class Settings
    {
        public static class Sections
        {
            public const string GeneralSectionTitle = "1. General";
            public const string RowsSettingsSectionTitle = "2. Rows Settings";
            public const string PanelSectionTitle = "3. Panel Settings";
            public const string TextSectionTitle = "4. Text Settings";

            private static readonly HashSet<string> Gui = new HashSet<string>
            {
                RowsSettingsSectionTitle,
                PanelSectionTitle,
                TextSectionTitle
            };

            public static bool IsGui(string section) => Gui.Contains(section);
        }

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
        public static ConfigEntry<bool> WrapText;
        public static ConfigEntry<int> MainFontSize;
        public static ConfigEntry<int> SubFontSize;
        public static ConfigEntry<int> MainProgressWidth;
        public static ConfigEntry<int> SubProgressWidth;
        public static ConfigEntry<Color> MainTextColor;
        public static ConfigEntry<Color> SubTextColor;
        public static ConfigEntry<EAlignment> Alignment;
        public static ConfigEntry<EVerticalAlignment> VerticalAlignment;
        public static ConfigEntry<int> HorizontalOffset;
        public static ConfigEntry<int> VerticalOffset;
        public static ConfigEntry<bool> TestContent;

        public static List<ConfigEntryBase> ConfigEntries = new List<ConfigEntryBase>();

        public static void Init(ConfigFile Config)
        {
            Settings.Config = Config;

            ConfigEntries.Add(VisibleAtRaidStart = Config.Bind(
                Sections.GeneralSectionTitle,
                "Visible At Raid Start",
                true,
                new ConfigDescription(
                    "Whether the panel is visible at raid start",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(PanelToggleKey = Config.Bind(
                Sections.GeneralSectionTitle,
                "Panel Toggle Key",
                new KeyboardShortcut(KeyCode.I),
                new ConfigDescription(
                    "Key used to toggle the Quest Tracker panel",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(AutoHide = Config.Bind(
                Sections.GeneralSectionTitle,
                "Auto Hide",
                true,
                new ConfigDescription(
                    "Whether to automatically hide the list after a timeout",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(AutoHideTimer = Config.Bind(
                Sections.GeneralSectionTitle,
                "Auto Hide Timer",
                5,
                new ConfigDescription(
                    "How long to show before hiding",
                    new AcceptableValueRange<int>(1, 60),
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ShowOnObjective = Config.Bind(
                Sections.GeneralSectionTitle,
                "Show On Objective Progress",
                true,
                new ConfigDescription(
                    "Whether to temporarily show the list on objective progress",
                    null,
                    new ConfigurationManagerAttributes())));
            
            ConfigEntries.Add(TestContent = Config.Bind(
                Sections.GeneralSectionTitle,
                "Use Test Content",
                false,
                new ConfigDescription(
                    "If enabled, the panel shows 3 fake quests for tweaking UI settings instead of real quest data",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(IncludeMapQuests = Config.Bind(
                Sections.RowsSettingsSectionTitle,
                "Include Current Map Quests",
                false,
                new ConfigDescription(
                    "Whether to always include the current map's quests",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ExcludeOtherMapQuests = Config.Bind(
                Sections.RowsSettingsSectionTitle,
                "Exclude Other Map Quests",
                true,
                new ConfigDescription(
                    "Whether to always exclude quests not for the current or 'Any' map",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ProgressAsPercent = Config.Bind(
                Sections.RowsSettingsSectionTitle,
                "Progress As Percent",
                true,
                new ConfigDescription(
                    "Whether to show total quest progress as a percentage, or numeric count",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(HideCompletedQuests = Config.Bind(
                Sections.RowsSettingsSectionTitle,
                "Hide Completed Quests",
                true,
                new ConfigDescription(
                    "Whether to hide quests that are ready to be handed in",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ShowObjectives = Config.Bind(
                Sections.RowsSettingsSectionTitle,
                "Show Objectives",
                true,
                new ConfigDescription(
                    "Whether to show individual quest objectives",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ObjectivesAsPercent = Config.Bind(
                Sections.RowsSettingsSectionTitle,
                "Objectives As Percent",
                false,
                new ConfigDescription(
                    "Whether to show quest objectives as a percentage, or numeric count",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(HideCompletedObjectives = Config.Bind(
                Sections.RowsSettingsSectionTitle,
                "Hide Completed Objectives",
                true,
                new ConfigDescription(
                    "Whether to hide completed objectives from the objective list",
                    null,
                    new ConfigurationManagerAttributes())));
            
            ConfigEntries.Add(Alignment = Config.Bind(
                Sections.PanelSectionTitle,
                "Alignment",
                EAlignment.Right,
                new ConfigDescription(
                    "Whether to align the tracker to the right or left",
                    null,
                    new ConfigurationManagerAttributes())));
            
            ConfigEntries.Add(VerticalAlignment = Config.Bind(
                Sections.PanelSectionTitle,
                "Vertical Alignment",
                EVerticalAlignment.Middle,
                new ConfigDescription(
                    "Vertical alignment of the panel on the screen (Top/Middle/Bottom)",
                    null,
                    new ConfigurationManagerAttributes())));
            
            ConfigEntries.Add(HorizontalOffset = Config.Bind(
                Sections.PanelSectionTitle,
                "Horizontal Offset",
                10,
                new ConfigDescription(
                    "Horizontal offset in pixels from the screen edge",
                    new AcceptableValueRange<int>(0, 500),
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(VerticalOffset = Config.Bind(
                Sections.PanelSectionTitle,
                "Vertical Offset",
                10,
                new ConfigDescription(
                    "Vertical offset in pixels from the screen edge",
                    new AcceptableValueRange<int>(0, 500),
                    new ConfigurationManagerAttributes())));

            // Special case where I want to allow different values for different resolutions
            string maxWidthName = $"Maximum Width ({Screen.width}p)";
            ConfigEntries.Add(MaxWidth = Config.Bind(
                Sections.PanelSectionTitle,
                maxWidthName,
                Screen.width / 6,
                new ConfigDescription(
                    "The maximum width of the panel before text wraps",
                    new AcceptableValueRange<int>(250, Screen.width),
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(Transparency = Config.Bind(
                Sections.PanelSectionTitle,
                "Panel Background Transparency",
                0.4f,
                new ConfigDescription(
                    "The transparency value for the panel background",
                    new AcceptableValueRange<float>(0f, 1f),
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(WrapText = Config.Bind(
                Sections.PanelSectionTitle,
                "Wrap Text",
                true,
                new ConfigDescription(
                    "If enabled, quest/objective text will wrap to multiple lines.",
                    null,
                    new ConfigurationManagerAttributes())));
            
            ConfigEntries.Add(MainFontSize = Config.Bind(
                Sections.TextSectionTitle,
                "Quest Font Size",
                18,
                new ConfigDescription(
                    "The quest title font size",
                    new AcceptableValueRange<int>(8, 32),
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(SubFontSize = Config.Bind(
                Sections.TextSectionTitle,
                "Objective Font Size",
                14,
                new ConfigDescription(
                    "The objective font size",
                    new AcceptableValueRange<int>(8, 32),
                    new ConfigurationManagerAttributes())));
            
            ConfigEntries.Add(MainProgressWidth = Config.Bind(
                Sections.TextSectionTitle,
                "Quest Progress Width",
                110,
                new ConfigDescription(
                    "Width of the right progress column for quest header rows.",
                    new AcceptableValueRange<int>(20, 400),
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(SubProgressWidth = Config.Bind(
                Sections.TextSectionTitle,
                "Objective Progress Width",
                90,
                new ConfigDescription(
                    "Width of the right progress column for objective rows.",
                    new AcceptableValueRange<int>(20, 400),
                    new ConfigurationManagerAttributes())));
            
            ConfigEntries.Add(MainTextColor = Config.Bind(
                Sections.TextSectionTitle,
                "Quest Color",
                new Color(0.85f, 0.85f, 0.75f),
                new ConfigDescription(
                    "The quest title text color",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(SubTextColor = Config.Bind(
                Sections.TextSectionTitle,
                "Objective Color",
                new Color(1f, 1f, 1f),
                new ConfigDescription(
                    "The objective text color",
                    null,
                    new ConfigurationManagerAttributes())));

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
        
        public enum EVerticalAlignment
        {
            Top = 0,
            Middle = 1,
            Bottom = 2
        }
    }
}
