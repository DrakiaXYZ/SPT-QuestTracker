using Aki.Reflection.Patching;
using Aki.Reflection.Utils;
using BepInEx;
using DrakiaXYZ.QuestTracker.Components;
using DrakiaXYZ.QuestTracker.Helpers;
using EFT;
using EFT.Quests;
using EFT.UI;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using QuestClass = GClass1249;

namespace DrakiaXYZ.QuestTracker
{
    [BepInPlugin("xyz.drakia.questtracker", "DrakiaXYZ-QuestTracker", "1.1.0")]
    // We have a soft dependency on TaskListFixes so that our sort will run after its sort
    [BepInDependency("xyz.drakia.tasklistfixes", BepInDependency.DependencyFlags.SoftDependency)]
    public class QuestTrackerPlugin : BaseUnityPlugin
    {
        public static string PluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public static string ConfigFolder = Path.Combine(PluginFolder, "config");
        public static bool TasksScreenActive = false;
        public static bool QuestsScreenActive = false;
        public static GraphicRaycaster[] GraphicsRaycasters;

        public static Color TrackedColor = new Color(0, 72, 87);
        public static string TrackedStatus = "Tracked!";

        private List<RaycastResult> _hits = new List<RaycastResult>();
        private EventSystem _eventSystem;

        private static FieldInfo _questClassField;
        private static FieldInfo _statusLabelField;
        private static MethodInfo _getColorMethod;
        private static MethodInfo _questListItemUpdateViewMethod;

        private void Awake()
        {
            Settings.Init(Config);
            Utils.Init();
            QuestsTracker.SetLogger(Logger);
            Directory.CreateDirectory(ConfigFolder);

            _eventSystem = FindObjectOfType<EventSystem>();

            Type notesTaskType = typeof(NotesTask);
            _questClassField = AccessTools.GetDeclaredFields(notesTaskType).Single(x => x.FieldType == typeof(QuestClass));
            _statusLabelField = AccessTools.Field(notesTaskType, "_statusLabel");
            _getColorMethod = AccessTools.GetDeclaredMethods(notesTaskType).Single(x => x.ReturnType == typeof(Color));
            _questListItemUpdateViewMethod = AccessTools.Method(typeof(QuestListItem), "UpdateView");

            new MainMenuControllerShowScreenPatch().Enable();
            new NewGamePatch().Enable();

            // Player task list
            new TasksScreenShowPatch().Enable();
            new TasksScreenClosePatch().Enable();
            new NotesTaskStatusPatch().Enable();
            new TasksScreenStatusComparePatch().Enable();

            // Vendor quest list
            new QuestsScreenShowPatch().Enable();
            new QuestsScreenClosePatch().Enable();
            new QuestsListItemUpdateViewPatch().Enable();

            // Load UI bundle
            LoadBundle();
        }

        public void Update()
        {
            if (TasksScreenActive && Input.GetMouseButtonDown(1))
            {
                NotesTask notesTask = GetNotesTaskUnderMouse();
                if (notesTask != null)
                {
                    var quest = _questClassField.GetValue(notesTask) as QuestClass;
                    HandleQuestRightClick(quest);

                    // Update the task status
                    UpdateTaskStatus(notesTask);
                }
            }

            if (QuestsScreenActive && Input.GetMouseButtonDown(1))
            {
                QuestListItem questListItem = GetQuestListItemUnderMouse();
                if (questListItem != null)
                {
                    QuestClass quest = questListItem.Quest;
                    HandleQuestRightClick(quest);

                    // Update the quest text
                    _questListItemUpdateViewMethod.Invoke(questListItem, null);
                }
            }
        }

        private void HandleQuestRightClick(QuestClass quest)
        {
            // If the quest isn't in progress, don't do anything
            EQuestStatus status = quest.QuestStatus;
            if (status != EQuestStatus.Started && status != EQuestStatus.AvailableForFinish && status != EQuestStatus.MarkedAsFailed)
            {
                return;
            }

            // Toggle whether the quest is tracked
            if (!QuestsTracker.IsTracked(quest))
            {
                Logger.LogDebug($"Tracking: {quest.Template.Name}");
                QuestsTracker.TrackQuest(quest);
            }
            else
            {
                Logger.LogDebug($"Untracking: {quest.Template.Name}");
                QuestsTracker.UntrackQuest(quest);
            }
            QuestsTracker.Save();
        }

        private NotesTask GetNotesTaskUnderMouse()
        {
            // Get the elements under where the user clicked
            var eventData = new PointerEventData(_eventSystem) { position = Input.mousePosition };
            _hits.Clear();
            foreach (var graphicsRaycaster in GraphicsRaycasters)
            {
                graphicsRaycaster.Raycast(eventData, _hits);
            }

            // Check if the user clicked on a NotesTask element
            NotesTask notesTask;
            foreach (var result in _hits)
            {
                if ((notesTask = result.gameObject.GetComponent<NotesTask>()) != null)
                {
                    return notesTask;
                }
            }

            return null;
        }

        private QuestListItem GetQuestListItemUnderMouse()
        {
            // Get the elements under where the user clicked
            var eventData = new PointerEventData(_eventSystem) { position = Input.mousePosition };
            _hits.Clear();
            foreach (var graphicsRaycaster in GraphicsRaycasters)
            {
                graphicsRaycaster.Raycast(eventData, _hits);
            }

            // Check if the user clicked on a NotesTask element
            QuestListItem questListItem;
            foreach (var result in _hits)
            {
                if ((questListItem = result.gameObject.GetComponent<QuestListItem>()) != null)
                {
                    return questListItem;
                }
            }

            return null;
        }

        /**
         * Update the task "Status" column with the correct value
         */
        private void UpdateTaskStatus(NotesTask notesTask)
        {
            var quest = _questClassField.GetValue(notesTask) as QuestClass;
            var statusLabel = _statusLabelField.GetValue(notesTask) as TextMeshProUGUI;

            if (QuestsTracker.IsTracked(quest))
            {
                statusLabel.text = TrackedStatus;
                statusLabel.color = TrackedColor;
                return;
            }

            switch (quest.QuestStatus)
            {
                case EQuestStatus.Started:
                    statusLabel.text = Utils.Localized("QuestStatusStarted");
                    statusLabel.color = (Color)_getColorMethod.Invoke(notesTask, new object[] { "active_font" });
                    break;
                case EQuestStatus.AvailableForFinish:
                    statusLabel.text = Utils.Localized("QuestStatusSuccess");
                    statusLabel.color = (Color)_getColorMethod.Invoke(notesTask, new object[] { "finished_font" });
                    break;
                case EQuestStatus.MarkedAsFailed:
                    statusLabel.text = Utils.Localized("QuestStatusFail");
                    statusLabel.color = (Color)_getColorMethod.Invoke(notesTask, new object[] { "failed_font" });
                    break;
            }
        }
        
        private void LoadBundle()
        {
            var bundlePath = Path.Combine(PluginFolder, "questtrackerui.bundle");
            var bundle = AssetBundle.LoadFromFile(bundlePath);
            if (bundle == null)
            {
                throw new Exception($"Error loading bundle: {bundlePath}");
            }

            QuestTrackerPanelComponent.QuestTrackerPanelPrefab = LoadAsset<GameObject>(bundle, "Assets/QuestTrackerPanel.prefab");
            QuestTrackerPanelComponent.QuestEntryPrefab = LoadAsset<GameObject>(bundle, "Assets/QuestEntry.prefab");
            QuestTrackerPanelComponent.QuestObjectivesPrefab = LoadAsset<GameObject>(bundle, "Assets/QuestObjectives.prefab");
        }

        private T LoadAsset<T>(AssetBundle bundle, string assetPath) where T : UnityEngine.Object
        {
            T asset = bundle.LoadAsset<T>(assetPath);

            if (asset == null)
            {
                throw new Exception($"Error loading asset {assetPath}");
            }

            DontDestroyOnLoad(asset);
            return asset;
        }
    }

    /**
     * The first time a menu screen is shown, fetch the player's quests
     * 
     * Note: We do this here for an easier way of fetching QuestControllerClass after it's populated
     */
    class MainMenuControllerShowScreenPatch : ModulePatch
    {
        private static FieldInfo _questControllerField;
        private static bool _questsLoaded = false;
        protected override MethodBase GetTargetMethod()
        {
            _questControllerField = AccessTools.GetDeclaredFields(typeof(MainMenuController)).FirstOrDefault(x => typeof(AbstractQuestControllerClass).IsAssignableFrom(x.FieldType));

            return AccessTools.Method(typeof(MainMenuController), "ShowScreen");
        }

        [PatchPostfix]
        private static void PatchPostfix(MainMenuController __instance)
        {
            if (_questsLoaded) return;
            _questsLoaded = true;

            Logger.LogDebug("MenuScreenShowPatch Postfix");

            // Load list of tracked quests
            AbstractQuestControllerClass questController = _questControllerField.GetValue(__instance) as AbstractQuestControllerClass;
            if (!QuestsTracker.Load(questController))
            {
                Logger.LogError("Error, unable to load tracked quests");
            }
        }
    }

    /**
     * Toggle the TasksScreenActive flag on and fetch the GraphicsRaycasters for mouse handling
     */
    class TasksScreenShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            Type tasksScreenType = typeof(TasksScreen);
            return AccessTools.Method(tasksScreenType, "Show");
        }

        [PatchPostfix]
        public static void PatchPostfix(TasksScreen __instance)
        {
            Logger.LogDebug("Task Screen Shown, enable hooks");

            QuestTrackerPlugin.TasksScreenActive = true;
            QuestTrackerPlugin.GraphicsRaycasters = UnityEngine.Object.FindObjectsOfType<GraphicRaycaster>();
        }
    }

    /**
     * Toggle the TasksScreenActive flag off
     */
    class TasksScreenClosePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            Type tasksScreenType = typeof(TasksScreen);
            return AccessTools.Method(tasksScreenType, "Close");
        }

        [PatchPostfix]
        public static void PatchPostfix(TasksScreen __instance)
        {
            Logger.LogDebug("Task Screen Closed, disable hooks");
            QuestTrackerPlugin.TasksScreenActive = false;
        }
    }

    /**
     * Toggle the QuestsScreenActive flag on
     */
    class QuestsScreenShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(QuestsScreen), "Show");
        }

        [PatchPostfix]
        public static void PatchPostfix()
        {
            Logger.LogDebug("Quest Screen Shown, enable hooks");

            QuestTrackerPlugin.QuestsScreenActive = true;
            QuestTrackerPlugin.GraphicsRaycasters = UnityEngine.Object.FindObjectsOfType<GraphicRaycaster>();
        }
    }

    /**
     * Toggle the QuestsScreenActive flag off
     */
    class QuestsScreenClosePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(QuestsScreen), "Close");
        }

        [PatchPostfix]
        public static void PatchPostfix()
        {
            Logger.LogDebug("Quest Screen Closed, disable hooks");
            QuestTrackerPlugin.QuestsScreenActive = false;
        }
    }

    /**
     * Set the status text of tracked quests in the vendor quest window
     */
    class QuestsListItemUpdateViewPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(QuestListItem), "UpdateView");
        }

        [PatchPostfix]
        public static void PatchPostfix(QuestListItem __instance, TextMeshProUGUI ____status)
        {
            var quest = __instance.Quest;
            if (QuestsTracker.IsTracked(quest))
            {
                ____status.text = QuestTrackerPlugin.TrackedStatus;
                ____status.color = QuestTrackerPlugin.TrackedColor;
            }
        }
    }

    /**
     * Set the status text of tracked quests
     */
    class NotesTaskStatusPatch : ModulePatch
    {
        private static FieldInfo _questClassField;

        protected override MethodBase GetTargetMethod()
        {
            Type notesTaskType = typeof(NotesTask);
            _questClassField = AccessTools.GetDeclaredFields(notesTaskType).Single(x => x.FieldType == typeof(QuestClass));

            return AccessTools.GetDeclaredMethods(notesTaskType).Single(x => x.GetParameters().Length == 1 && x.GetParameters()[0].ParameterType == typeof(Color));
        }

        [PatchPostfix]
        public static void PatchPostfix(NotesTask __instance, TextMeshProUGUI ____statusLabel)
        {
            var quest = _questClassField.GetValue(__instance) as QuestClass;
            if (QuestsTracker.IsTracked(quest))
            {
                ____statusLabel.text = QuestTrackerPlugin.TrackedStatus;
                ____statusLabel.color = QuestTrackerPlugin.TrackedColor;
            }
        }
    }

    /**
     * To have tracked quests at the top of the task list, override the TasksScreen.QuestStatusComparer Compare method
     */
    class TasksScreenStatusComparePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            Type questStatusComparerType = PatchConstants.EftTypes.First(x => x.Name == "QuestStatusComparer");

            return AccessTools.Method(questStatusComparerType, "Compare");
        }

        [PatchPostfix]
        public static void PatchPostfix(QuestClass x, QuestClass y, ref int __result)
        {
            bool leftTracked = QuestsTracker.IsTracked(x);
            bool rightTracked = QuestsTracker.IsTracked(y);
            if (leftTracked && !rightTracked)
            {
                __result = 1;
            }
            else if (!leftTracked && rightTracked)
            {
                __result = -1;
            }
        }
    }

    /**
     * Add the component every time a match starts
     */
    internal class NewGamePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(GameWorld).GetMethod(nameof(GameWorld.OnGameStarted));

        [PatchPrefix]
        public static void PatchPrefix()
        {
            QuestTrackerComponent.Enable();
        }
    }
}
