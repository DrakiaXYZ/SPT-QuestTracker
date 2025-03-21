﻿using SPT.Reflection.Utils;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using DrakiaXYZ.QuestTracker.Helpers;
using EFT;
using EFT.Quests;
using EFT.UI;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrakiaXYZ.QuestTracker.Components
{
    internal class QuestTrackerComponent : MonoBehaviour
    {
        private QuestTrackerPanelComponent panel;

        private float lastUpdate;
        private float updateFrequency = 0.1f;
        private int lastQuestsHash;
        private string locationId;
        private float hideTime;

        private GameWorld gameWorld;
        private Player player;
        private AbstractGame abstractGame;
        private AbstractQuestControllerClass questController;

        private List<QuestClass> mapQuests = new List<QuestClass>();
        private Dictionary<string, Tuple<QuestClass, TrackedQuestData>> trackedQuests = new Dictionary<string, Tuple<QuestClass, TrackedQuestData>>();
        private CommonUI commonUi;

        protected ManualLogSource Logger;

        public QuestTrackerComponent()
        {
            Logger = BepInEx.Logging.Logger.CreateLogSource(GetType().Name);
        }

        public void Awake()
        {
#if DEBUG
            Logger.LogInfo("QuestTrackerComponent Awake");
#endif

            // Setup access to game objects
            gameWorld = Singleton<GameWorld>.Instance;
            abstractGame = Singleton<AbstractGame>.Instance;
            player = gameWorld?.MainPlayer;
            commonUi = MonoBehaviourSingleton<CommonUI>.Instance;

            if (gameWorld == null || abstractGame == null || player == null)
            {
                throw new Exception("Error creating QuestTrackerComponent, gameWorld, botGame or player was null");
            }
            if (player.Profile == null || player.Profile.QuestsData == null)
            {
                throw new Exception("Error creating QuestTrackerComponent, profile or QuestData null");
            }
            if (commonUi == null)
            {
                throw new Exception("Error creating QuestTrackerComponent, commonUi was null");
            }
            
            questController = AccessTools.Field(typeof(Player), "_questController").GetValue(player) as AbstractQuestControllerClass;
            if (questController == null)
            {
                throw new Exception("Error creating QuestTrackerComponent, questController was null");
            }

            // Add the panel to the BattleUiScreen
            panel = Utils.GetOrAddComponent<QuestTrackerPanelComponent>(Singleton<CommonUI>.Instance.EftBattleUIScreen);
            panel.Visible = Settings.VisibleAtRaidStart.Value;
            if (panel.Visible && Settings.AutoHide.Value)
            {
                hideTime = Time.time + Settings.AutoHideTimer.Value;
            }

            AttachEvents();

            // Add any current map quests
            var localGameBaseType = PatchConstants.LocalGameType.BaseType;
            locationId = AccessTools.Property(localGameBaseType, "LocationObjectId").GetValue(abstractGame) as string;
            foreach (var quest in player.Profile.QuestsData)
            {
                if (quest == null) continue;

                if (quest.Template == null)
                {
                    continue;
                }

                EQuestStatus status = quest.Status;
                if ((status == EQuestStatus.Started ||
                    status == EQuestStatus.AvailableForFinish ||
                    status == EQuestStatus.MarkedAsFailed) &&
                    IsSameMap(quest.Template.LocationId, locationId))
                {
                    QuestClass questInstance = Utils.GetQuest(questController, quest.Id);
                    if (questInstance == null)
                    {
                        Logger.LogWarning($"Quest instance null {quest.Id}");
                        continue;
                    }
                    mapQuests.Add(questInstance);
                }
            }

            // Cache the quest data, this also sends it off to the panel
            CacheQuests();

            // Store the current hash so we don't treat startup as an objective change
            HaveQuestsChanged();
        }

        private bool IsSameMap(string questLocationId, string mapLocationId)
        {
            // Exact match, obviously
            if (questLocationId == mapLocationId)
            {
                return true;
            }

            // Include factory4_night for factory4_day quests, but not the other way around
            const string factory4_day = "55f2d3fd4bdc2d5f408b4567";
            const string factory4_night = "59fc81d786f774390775787e";
            if (questLocationId == factory4_day && mapLocationId == factory4_night)
            {
                return true;
            }

            // Include Sandbox_high for Sandbox quests, but not the other way around
            const string sandbox = "653e6760052c01c1c805532f";
            const string sandbox_high = "65b8d6f5cdde2479cb2a3125";
            if (questLocationId == sandbox && mapLocationId == sandbox_high)
            {
                return true;
            }

            return false;
        }

        private void SettingsChanged(object sender, EventArgs args)
        {
            SettingChangedEventArgs e = (SettingChangedEventArgs)args;

            if (e.ChangedSetting.Definition.Section == Settings.DisplaySectionTitle)
            {
                GuiSettingsChanged(sender, args);
                return;
            }

            // If an AutoHide setting was changed, update the hide time
            if (e.ChangedSetting == Settings.AutoHide || e.ChangedSetting == Settings.AutoHideTimer)
            {
                // If the panel is already visible, set the hide time
                if (panel.Visible && Settings.AutoHide.Value)
                {
                    hideTime = Time.time + Settings.AutoHideTimer.Value;
                }
                else
                {
                    hideTime = 0;
                }
            }

            // Force a refresh
            CacheQuests();
        }

        private void GuiSettingsChanged(object sender, EventArgs args)
        {
            SettingChangedEventArgs e = (SettingChangedEventArgs)args;

            if (e.ChangedSetting == Settings.MainFontSize)
            {
                panel.SetMainFontSize(Settings.MainFontSize.Value);
            }

            if (e.ChangedSetting == Settings.SubFontSize)
            {
                panel.SetSubFontSize(Settings.SubFontSize.Value);
            }

            if (e.ChangedSetting == Settings.Transparency)
            {
                panel.SetTransparency(Settings.Transparency.Value);
            }

            if (e.ChangedSetting == Settings.Alignment)
            {
                panel.SetAlignment(Settings.Alignment.Value);
            }

            if (e.ChangedSetting == Settings.CoolKidsClub)
            {
                panel.SetFont(Settings.CoolKidsClub.Value);
            }

            if (e.ChangedSetting == Settings.MaxWidth)
            {
                panel.SetWidth(Settings.MaxWidth.Value);
            }
        }

        private void CacheQuests()
        {
            trackedQuests.Clear();

            foreach (var trackedQuest in QuestsTracker.GetTrackedQuests())
            {
                // Skip any quest that's not actually tracked
                if (!trackedQuest.Value.Tracked) continue;

                string questId = trackedQuest.Key;
                QuestClass quest = Utils.GetQuest(questController, questId);
                if (quest == null)
                {
#if DEBUG
                    Logger.LogDebug($"Skipping {questId} because it's not in quest controller");
#endif
                    continue;
                }

                if (!Settings.ExcludeOtherMapQuests.Value
                    || quest.Template.LocationId == locationId
                    || quest.Template.LocationId.ToLower() == "any"
                    || quest.Template.LocationId.ToLower() == "marathon")
                {
                    trackedQuests.Add(questId, new Tuple<QuestClass, TrackedQuestData>(quest, trackedQuest.Value));
                }
            }

            // Flag that an update check is needed
            lastUpdate = 0;
            panel.SetQuests(trackedQuests, mapQuests);
        }

        private void RebuildCache(object sender, object el)
        {
            CacheQuests();
        }

        public void Update()
        {
            // Check for the toggle button
            if (IsKeyPressed(Settings.PanelToggleKey.Value))
            {
                panel.Visible = !panel.Visible;

                if (panel.Visible && Settings.AutoHide.Value)
                {
                    hideTime = Time.time + Settings.AutoHideTimer.Value;
                }
            }

            // Handle auto hiding
            if (hideTime > 0 && Time.time > hideTime)
            {
                panel.Visible = false;
                hideTime = 0;
            }

            // Only update quest data at the set frequency
            if (Time.time < (lastUpdate + updateFrequency))
            {
                return;
            }
            lastUpdate = Time.time;

            if (HaveQuestsChanged())
            {
                panel.SetQuests(trackedQuests, mapQuests);

                // If show on objective is true, and the panel is hidden or already on a timer, update the timer
                if (Settings.ShowOnObjective.Value && (!panel.Visible || hideTime > 0))
                {
                    hideTime = Time.time + Settings.AutoHideTimer.Value;
                    panel.Visible = true;
                }
            }
        }

        private bool HaveQuestsChanged()
        {
            int questsHash = 17;
            foreach (var quest in trackedQuests.Values)
            {
                questsHash = questsHash * 31 + quest.Item1.Id.GetHashCode();
                questsHash = questsHash * 31 + quest.Item1.Progress.GetHashCode();
                questsHash = questsHash * 31 + quest.Item2.GetHashCode();
            }

            if (Settings.IncludeMapQuests.Value)
            {
                foreach (var quest in mapQuests)
                {
                    questsHash = questsHash * 31 + quest.Id.GetHashCode();
                    questsHash = questsHash * 31 + quest.Progress.GetHashCode();
                }
            }

            if (questsHash != lastQuestsHash)
            {
                lastQuestsHash = questsHash;
                return true;
            }
            return false;
        }

        public static void Enable()
        {
            if (Singleton<AbstractGame>.Instantiated)
            {
                var gameWorld = Singleton<GameWorld>.Instance;
                Utils.GetOrAddComponent<QuestTrackerComponent>(gameWorld);
            }
        }

        public void OnDestroy()
        {
#if DEBUG
            Logger.LogInfo("QuestTrackerComponent OnDestroy");
#endif
            DetachEvents();
            Destroy(panel);
        }

        private void AttachEvents()
        {
            Settings.Config.SettingChanged += SettingsChanged;

            QuestsTracker.QuestTracked += RebuildCache;
            QuestsTracker.QuestUntracked += RebuildCache;
            QuestsTracker.ConditionTracked += RebuildCache;
            QuestsTracker.ConditionUntracked += RebuildCache;
        }

        private void DetachEvents()
        {
            Settings.Config.SettingChanged -= SettingsChanged;

            QuestsTracker.QuestTracked -= RebuildCache;
            QuestsTracker.QuestUntracked -= RebuildCache;
            QuestsTracker.ConditionTracked -= RebuildCache;
            QuestsTracker.ConditionUntracked -= RebuildCache;
        }

        /**
         * Custom KeyPressed check that handles modifiers, but also lets you hit more than one key at a time
         */
        bool IsKeyPressed(KeyboardShortcut key)
        {
            if (!Input.GetKeyDown(key.MainKey))
            {
                return false;
            }

            foreach (var modifier in key.Modifiers)
            {
                if (!Input.GetKey(modifier))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
