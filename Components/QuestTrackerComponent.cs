using Aki.Reflection.Utils;
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
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DrakiaXYZ.QuestTracker.Components
{
    internal class QuestTrackerComponent : MonoBehaviour, IDisposable
    {
        private QuestTrackerPanelComponent panel;

        private float lastUpdate;
        private float updateFrequency = 0.1f;
        private int lastQuestsHash;
        private string locationId;
        private float hideTime;

        private GameWorld gameWorld;
        private Player player;
        private IBotGame botGame;
        private QuestControllerClass questController;

        private List<QuestClass> mapQuests = new List<QuestClass>();
        private Dictionary<string, QuestClass> trackedQuests = new Dictionary<string, QuestClass>();
        private CommonUI commonUi;

        protected ManualLogSource Logger;

        public QuestTrackerComponent()
        {
            Logger = BepInEx.Logging.Logger.CreateLogSource(GetType().Name);
        }

        public void Awake()
        {
            // Setup access to game objects
            gameWorld = Singleton<GameWorld>.Instance;
            botGame = Singleton<IBotGame>.Instance;
            player = gameWorld?.MainPlayer;
            commonUi = MonoBehaviourSingleton<CommonUI>.Instance;

            if (gameWorld == null || botGame == null || player == null)
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
            
            questController = AccessTools.Field(typeof(Player), "_questController").GetValue(player) as QuestControllerClass;
            if (questController == null || questController.Quests == null)
            {
                throw new Exception("Error creating QuestTrackerComponent, questController or Quests was null");
            }

            // Add the panel to the BattleUiScreen
            panel = Singleton<GameUI>.Instance.BattleUiScreen.GetOrAddComponent<QuestTrackerPanelComponent>();
            panel.Visible = Settings.VisibleAtRaidStart.Value;
            if (panel.Visible && Settings.AutoHide.Value)
            {
                hideTime = Time.time + Settings.AutoHideTimer.Value;
            }

            AttachEvents();

            // Add any current map quests
            var localGameBaseType = PatchConstants.LocalGameType.BaseType;
            locationId = AccessTools.Property(localGameBaseType, "LocationObjectId").GetValue(botGame) as string;
            foreach (var quest in player.Profile.QuestsData)
            {
                if (quest == null) continue;

                if (quest.Template == null)
                {
                    Logger.LogDebug($"Quest template null {quest.Id}");
                    continue;
                }

                EQuestStatus status = quest.Status;
                if ((status == EQuestStatus.Started ||
                    status == EQuestStatus.AvailableForFinish ||
                    status == EQuestStatus.MarkedAsFailed) &&
                    quest.Template.LocationId == locationId)
                {
                    QuestClass questInstance = questController.Quests.GetQuest(quest.Id);
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

        private void SettingsChanged(object sender, EventArgs args)
        {
            SettingChangedEventArgs e = (SettingChangedEventArgs)args;

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

        private void GuiSettingsChanged(object sender, EventArgs e)
        {
            panel.SetFontSize(Settings.MainFontSize.Value);
        }

        private void CacheQuests()
        {
            trackedQuests.Clear();

            foreach (var questId in QuestsTracker.GetTrackedQuests())
            {
                QuestClass quest = questController.Quests.GetQuest(questId);
                if (quest == null)
                {
                    Logger.LogDebug($"Skipping {questId} because it's not in quest controller");
                    continue;
                }

                if (!Settings.ExcludeOtherMapQuests.Value
                    || quest.Template.LocationId == locationId
                    || quest.Template.LocationId.ToLower() == "any")
                {
                    trackedQuests.Add(questId, quest);
                }
            }

            // Flag that an update check is needed
            lastUpdate = 0;
            panel.SetQuests(trackedQuests, mapQuests);
        }

        private void QuestTracked(object sender, QuestClass quest)
        {
            CacheQuests();
        }

        private void QuestUntracked(object sender, QuestClass quest)
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
                questsHash = questsHash * 31 + quest.Id.GetHashCode();
                questsHash = questsHash * 31 + quest.Progress.GetHashCode();
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
            if (Singleton<IBotGame>.Instantiated)
            {
                var gameWorld = Singleton<GameWorld>.Instance;
                gameWorld.GetOrAddComponent<QuestTrackerComponent>();
            }
        }

        public void Dispose()
        {
            DetachEvents();
            Destroy(panel);
            Destroy(this);
        }

        private void AttachEvents()
        {
            Settings.MainFontSize.SettingChanged += GuiSettingsChanged;

            Settings.SubFontSize.SettingChanged += SettingsChanged;
            Settings.IncludeMapQuests.SettingChanged += SettingsChanged;
            Settings.ExcludeOtherMapQuests.SettingChanged += SettingsChanged;
            Settings.AutoHide.SettingChanged += SettingsChanged;
            Settings.ShowOnObjective.SettingChanged += SettingsChanged;
            Settings.ProgressAsPercent.SettingChanged += SettingsChanged;
            Settings.ObjectivesAsPercent.SettingChanged += SettingsChanged;
            Settings.ShowObjectives.SettingChanged += SettingsChanged;

            QuestsTracker.QuestTracked += QuestTracked;
            QuestsTracker.QuestUntracked += QuestUntracked;
        }

        private void DetachEvents()
        {
            Settings.MainFontSize.SettingChanged -= GuiSettingsChanged;

            Settings.SubFontSize.SettingChanged -= SettingsChanged;
            Settings.IncludeMapQuests.SettingChanged -= SettingsChanged;
            Settings.ExcludeOtherMapQuests.SettingChanged -= SettingsChanged;
            Settings.AutoHide.SettingChanged -= SettingsChanged;
            Settings.ShowOnObjective.SettingChanged -= SettingsChanged;
            Settings.ProgressAsPercent.SettingChanged -= SettingsChanged;
            Settings.ObjectivesAsPercent.SettingChanged -= SettingsChanged;
            Settings.ShowObjectives.SettingChanged -= SettingsChanged;

            QuestsTracker.QuestTracked -= QuestTracked;
            QuestsTracker.QuestUntracked -= QuestUntracked;
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
