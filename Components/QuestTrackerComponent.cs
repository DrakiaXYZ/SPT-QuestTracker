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
using System.Linq;
using System.Security.Claims;
using System.Text;
using UnityEngine;

namespace DrakiaXYZ.QuestTracker.Components
{
    internal class QuestTrackerComponent : MonoBehaviour, IDisposable
    {
        private GUIContent guiContentQuest;
        private GUIContent guiContentProgress;
        private GUIStyle guiStyleQuest;
        private GUIStyle guiStyleProgress;
        private Rect guiRectQuest;
        private Rect guiRectProgress;
        private StringBuilder stringBuilderQuestNames = new StringBuilder();
        private StringBuilder stringBuilderProgress = new StringBuilder();

        private float lastUpdate;
        private float updateFrequency = 0.1f;
        private bool updateGuiPending = false;
        private bool panelVisible = false;
        private bool menuActive = false;
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

            panelVisible = Settings.VisibleAtRaidStart.Value;
            if (panelVisible && Settings.AutoHide.Value)
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
                    QuestClass questInstance = questController.Quests.GetQuest(quest.Template.Id);
                    if (questInstance == null)
                    {
                        Logger.LogWarning($"Quest instance null {quest.Id}");
                        continue;
                    }
                    mapQuests.Add(questInstance);
                }
            }
            
            // Pre-load the tracked quest dict
            CacheQuests();
        }

        private void SettingsChanged(object sender, EventArgs args)
        {
            SettingChangedEventArgs e = (SettingChangedEventArgs)args;

            // If an AutoHide setting was changed, update the hide time
            if (e.ChangedSetting == Settings.AutoHide || e.ChangedSetting == Settings.AutoHideTimer)
            {
                // If the panel is already visible, set the hide time
                if (panelVisible && Settings.AutoHide.Value)
                {
                    hideTime = Time.time + Settings.AutoHideTimer.Value;
                }
                else
                {
                    hideTime = 0;
                }
            }

            // If the exclude option was changed, we need to re-cache quests
            if (e.ChangedSetting == Settings.ExcludeOtherMapQuests)
            {
                CacheQuests();
            }
        }

        private void GuiSettingsChanged(object sender, EventArgs e)
        {
            UpdateGuiStyle();
        }

        private void CacheQuests()
        {
            trackedQuests.Clear();

            foreach (var questId in QuestsTracker.GetTrackedQuests())
            {
                QuestClass quest = questController.Quests.GetQuest(questId);
                if (quest == null) continue;

                if (!Settings.ExcludeOtherMapQuests.Value
                    || quest.Template.LocationId == locationId
                    || quest.Template.LocationId.ToLower() == "any")
                {
                    trackedQuests.Add(questId, quest);
                }
            }

            // Flag that an update check is needed
            lastUpdate = 0;
            updateGuiPending = true;
        }

        private void QuestTracked(object sender, QuestClass quest)
        {
            CacheQuests();
        }

        private void QuestUntracked(object sender, QuestClass quest)
        {
            CacheQuests();
        }

        private void UpdateGuiStyle()
        {
            if (guiStyleQuest == null)
            {
                guiStyleQuest = new GUIStyle(GUI.skin.box);
                guiStyleProgress = new GUIStyle(GUI.skin.box);
            }

            guiStyleQuest.alignment = TextAnchor.MiddleLeft;
            guiStyleQuest.fontSize = Settings.MainFontSize.Value;
            guiStyleQuest.padding = new RectOffset(25, 25, 15, 15);
            guiStyleQuest.richText = true;

            guiStyleProgress.alignment = TextAnchor.MiddleCenter;
            guiStyleProgress.fontSize = Settings.MainFontSize.Value;
            guiStyleProgress.padding = new RectOffset(25, 25, 15, 15);
            guiStyleProgress.richText = true;
        }

        public void Update()
        {
            // Check for the toggle button
            if (IsKeyPressed(Settings.PanelToggleKey.Value))
            {
                panelVisible = !panelVisible;

                if (panelVisible && Settings.AutoHide.Value)
                {
                    hideTime = Time.time + Settings.AutoHideTimer.Value;
                }
            }

            // Handle auto hiding
            if (hideTime > 0 && Time.time > hideTime)
            {
                panelVisible = false;
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
                updateGuiPending = true;

                // If show on objective is true, and the panel is hidden or already on a timer, update the timer
                if (Settings.ShowOnObjective.Value && (!panelVisible || hideTime > 0))
                {
                    hideTime = Time.time + Settings.AutoHideTimer.Value;
                    panelVisible = true;
                }
            }

            menuActive = IsMenuActive();
        }

        public void OnGUI()
        {
            if (guiStyleQuest == null)
            {
                UpdateGuiStyle();
                guiContentQuest = new GUIContent();
                guiContentProgress = new GUIContent();
                guiRectQuest = new Rect();
                guiRectProgress = new Rect();
            }

            // Don't draw if the panel isn't visible
            if (!panelVisible || menuActive) return;

            if (updateGuiPending)
            {
                stringBuilderQuestNames.Clear();
                stringBuilderProgress.Clear();

                foreach (var quest in trackedQuests.Values.ToList().OrderBy(x => x.Template.Name))
                {
                    AddQuestToStringBuilder(quest);
                }

                if (Settings.IncludeMapQuests.Value)
                {
                    foreach (var quest in mapQuests.OrderBy(x => x.Template.Name))
                    {
                        if (trackedQuests.ContainsKey(quest.Template.Id)) continue;
                        AddQuestToStringBuilder(quest);
                    }
                }

                guiContentQuest.text = stringBuilderQuestNames.ToString().TrimEnd(Environment.NewLine.ToCharArray());
                guiContentProgress.text = stringBuilderProgress.ToString().TrimEnd(Environment.NewLine.ToCharArray());

                updateGuiPending = false;
            }

            // If there's no text to draw, don't do anything
            if (guiContentQuest.text.Length == 0)
            {
                return;
            }

            Vector2 guiSizeProgress = guiStyleProgress.CalcSize(guiContentProgress);
            guiSizeProgress.x = Math.Max(guiSizeProgress.x, 150);
            guiRectProgress.x = Screen.width - guiSizeProgress.x - 5f;
            guiRectProgress.y = (Screen.height / 2) - (guiSizeProgress.y / 2);
            guiRectProgress.size = guiSizeProgress;
            GUI.Box(guiRectProgress, guiContentProgress, guiStyleProgress);

            Vector2 guiSize = guiStyleQuest.CalcSize(guiContentQuest);
            guiRectQuest.x = Screen.width - guiSize.x - guiSizeProgress.x - 5f;
            guiRectQuest.y = (Screen.height / 2) - (guiSize.y / 2);
            guiRectQuest.size = guiSize;
            GUI.Box(guiRectQuest, guiContentQuest, guiStyleQuest);
        }

        private void AddQuestToStringBuilder(QuestClass quest)
        {
            // Filter out any quest that isn't currently started/done/failed
            EQuestStatus status = quest.QuestStatus;
            if (status != EQuestStatus.Started && status != EQuestStatus.AvailableForFinish && status != EQuestStatus.MarkedAsFailed)
            {
                return;
            }

            stringBuilderQuestNames.AppendLine($"{quest.Template.Name}:");

            switch (status)
            {
                case EQuestStatus.AvailableForFinish:
                    stringBuilderProgress.AppendLine("<color=#00ff00ff>✓</color>");
                    break;
                case EQuestStatus.MarkedAsFailed:
                    stringBuilderProgress.AppendLine("<color=#ff0000ff>✗</color>");
                    break;
                default:
                    // No divide by zero errors here
                    float current = quest.Progress.current;
                    float max = quest.Progress.absolute;
                    if (max.ApproxEquals(0f))
                    {
                        stringBuilderProgress.AppendLine("<color=#00ff00ff>✓</color>");
                    }
                    else
                    {
                        if (Settings.ProgressAsPercent.Value)
                        {
                            stringBuilderProgress.AppendLine($"{Mathf.FloorToInt((current / max) * 100)}%");
                        }
                        else
                        {
                            stringBuilderProgress.AppendLine($"{current} / {max}");
                        }
                    }
                    break;
            }
        }

        private bool HaveQuestsChanged()
        {
            int questsHash = 17;
            foreach (var quest in trackedQuests.Values)
            {
                questsHash = questsHash * 31 + quest.Template.Id.GetHashCode();
                questsHash = questsHash * 31 + quest.Progress.GetHashCode();
            }

            if (Settings.IncludeMapQuests.Value)
            {
                foreach (var quest in mapQuests)
                {
                    questsHash = questsHash * 31 + quest.Template.Id.GetHashCode();
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

        private bool IsMenuActive()
        {
            return commonUi.MenuScreen.isActiveAndEnabled
                || commonUi.InventoryScreen.isActiveAndEnabled
                || commonUi.ScavengerInventoryScreen.isActiveAndEnabled
                || commonUi.SettingsScreen.isActiveAndEnabled
                || commonUi.ReconnectionScreen.isActiveAndEnabled;
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
            Destroy(this);
        }

        private void AttachEvents()
        {
            Settings.MainFontSize.SettingChanged += GuiSettingsChanged;

            Settings.IncludeMapQuests.SettingChanged += SettingsChanged;
            Settings.ExcludeOtherMapQuests.SettingChanged += SettingsChanged;
            Settings.AutoHide.SettingChanged += SettingsChanged;
            Settings.ShowOnObjective.SettingChanged += SettingsChanged;
            Settings.ProgressAsPercent.SettingChanged += SettingsChanged;

            QuestsTracker.QuestTracked += QuestTracked;
            QuestsTracker.QuestUntracked += QuestUntracked;
        }

        private void DetachEvents()
        {
            Settings.MainFontSize.SettingChanged -= SettingsChanged;

            Settings.IncludeMapQuests.SettingChanged -= SettingsChanged;
            Settings.ExcludeOtherMapQuests.SettingChanged -= SettingsChanged;
            Settings.AutoHide.SettingChanged -= SettingsChanged;
            Settings.ShowOnObjective.SettingChanged -= SettingsChanged;

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
