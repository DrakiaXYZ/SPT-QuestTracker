using SPT.Reflection.Patching;
using SPT.Reflection.Utils;
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

using QuestStatusComparerClass = GClass3509;

namespace DrakiaXYZ.QuestTracker
{
    [BepInPlugin("xyz.drakia.questtracker", "DrakiaXYZ-QuestTracker", "1.5.1")]
    [BepInDependency("com.SPT.core", "3.11.0")]
    public class QuestTrackerPlugin : BaseUnityPlugin
    {
        public static string PluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public static string ConfigFolder = Path.Combine(PluginFolder, "config");
        public static bool TasksScreenActive = false;
        public static bool QuestsScreenActive = false;
        public static GraphicRaycaster[] GraphicsRaycasters;

        public static Color TrackedColor = new Color(0, 72, 87);
        public static Color TrackedConditionColor = new Color(0, 72, 87);
        public static string TrackedStatus = "Tracked!";
        public static string ConditionTracked = "(Tracked!)";

        private List<RaycastResult> _hits = new List<RaycastResult>();
        private EventSystem _eventSystem;

        private static FieldInfo _taskQuestClassField;
        private static FieldInfo _statusLabelField;
        private static MethodInfo _getColorMethod;
        private static MethodInfo _questListItemUpdateViewMethod;

        private static FieldInfo _objectiveQuestClassField;
        private static FieldInfo _objectiveConditionField;
        private static FieldInfo _descriptionField;
        private static FieldInfo _questListSelectedField;

        private void Awake()
        {
            Settings.Init(Config);
            Utils.Init();
            QuestsTracker.SetLogger(Logger);
            Directory.CreateDirectory(ConfigFolder);

            _eventSystem = FindObjectOfType<EventSystem>();

            Type notesTaskType = typeof(NotesTask);
            _taskQuestClassField = AccessTools.GetDeclaredFields(notesTaskType).Single(x => x.FieldType == typeof(QuestClass));
            _statusLabelField = AccessTools.Field(notesTaskType, "_statusLabel");
            _getColorMethod = AccessTools.GetDeclaredMethods(notesTaskType).Single(x => x.ReturnType == typeof(Color));
            _questListItemUpdateViewMethod = AccessTools.Method(typeof(QuestListItem), "UpdateView");

            _objectiveQuestClassField = AccessTools.Field(typeof(QuestObjectiveView), "questClass");
            _objectiveConditionField = AccessTools.Field(typeof(QuestObjectiveView), "Condition");
            _descriptionField = AccessTools.Field(typeof(QuestObjectiveView), "_description");
            _questListSelectedField = AccessTools.Field(typeof(QuestsListView), "_questListItemSelected");

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

            // Condition tracking
            new QuestObjectiveViewShowPatch().Enable();

            // Load UI bundle
            LoadBundle();
        }

        public void Update()
        {
            // Character task list
            if (TasksScreenActive && Input.GetMouseButtonDown(1))
            {
                QuestObjectiveView questObjective = GetObjectUnderMouse<QuestObjectiveView>();
                if (questObjective != null)
                {
                    var quest = _objectiveQuestClassField.GetValue(questObjective) as QuestClass;
                    var condition = _objectiveConditionField.GetValue(questObjective) as Condition;

                    HandleConditionRightClick(quest, condition);
                    UpdateConditionDescription(questObjective, quest, condition);

                    // Find the parent NotesTask and update the status if necessary
                    NotesTask conditionNotesTask = questObjective.GetComponentInParent<NotesTask>();
                    if (conditionNotesTask != null)
                    {
                        UpdateTaskConditions(conditionNotesTask.gameObject);
                        UpdateTaskStatus(conditionNotesTask);
                    }

                    return;
                }

                NotesTask notesTask = GetObjectUnderMouse<NotesTask>();
                if (notesTask != null)
                {
                    var quest = _taskQuestClassField.GetValue(notesTask) as QuestClass;
                    HandleQuestRightClick(quest);

                    // Update the task status
                    UpdateTaskStatus(notesTask);

                    // Update any conditions that may be individually tracked
                    UpdateTaskConditions(notesTask.gameObject);

                    return;
                }
            }

            // Trader task list
            if (QuestsScreenActive && Input.GetMouseButtonDown(1))
            {
                QuestsScreen questScreen = null;

                // Handle toggling quests
                QuestListItem questListItem = GetObjectUnderMouse<QuestListItem>();
                if (questListItem != null)
                {
                    HandleQuestRightClick(questListItem.Quest);
                    _questListItemUpdateViewMethod.Invoke(questListItem, null);

                    questScreen = questListItem.GetComponentInParent<QuestsScreen>();
                }

                // Handle toggling specific objectives
                QuestObjectiveView questObjective = GetObjectUnderMouse<QuestObjectiveView>();
                if (questObjective != null)
                {
                    var quest = _objectiveQuestClassField.GetValue(questObjective) as QuestClass;
                    var condition = _objectiveConditionField.GetValue(questObjective) as Condition;
                    HandleConditionRightClick(quest, condition);

                    questScreen = questObjective.GetComponentInParent<QuestsScreen>();
                }

                // Refresh the UI
                if (questScreen != null)
                {
                    // Update the selected quest list entry
                    var questListView = questScreen.GetComponentInChildren<QuestsListView>();
                    if (questListView != null)
                    {
                        QuestListItem selected = _questListSelectedField.GetValue(questListView) as QuestListItem;
                        _questListItemUpdateViewMethod.Invoke(selected, null);
                    }

                    // Update the objective list
                    var questView = questScreen.GetComponentInChildren<QuestView>();
                    if (questView != null)
                    {
                        UpdateTaskConditions(questView.gameObject);
                    }
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
                QuestsTracker.TrackQuest(quest);
            }
            else
            {
                QuestsTracker.UntrackQuest(quest);
            }
            QuestsTracker.Save();
        }

        private void HandleConditionRightClick(QuestClass quest, Condition condition)
        {
            // If the quest isn't in progress, don't do anything
            EQuestStatus status = quest.QuestStatus;
            if (status != EQuestStatus.Started && status != EQuestStatus.AvailableForFinish && status != EQuestStatus.MarkedAsFailed)
            {
                return;
            }

            // If the quest isn't tracked, track the quest
            if (!QuestsTracker.IsTracked(quest))
            {
                QuestsTracker.TrackQuest(quest);

                // Special handling of the case where the quest wasn't tracked, but we already tracked the condition, we don't want to _untrack_ it
                if (QuestsTracker.IsTracked(quest, condition))
                {
                    return;
                }
            }

            // Toggle whether the condition is tracked
            if (!QuestsTracker.IsTracked(quest, condition))
            {
                QuestsTracker.TrackCondition(quest, condition);
            }
            else
            {
                QuestsTracker.UntrackCondition(quest, condition);
            }

            QuestsTracker.Save();
        }

        private T GetObjectUnderMouse<T>() where T : class
        {
            // Get the elements under where the user clicked
            var eventData = new PointerEventData(_eventSystem) { position = Input.mousePosition };
            _hits.Clear();
            foreach (var graphicsRaycaster in GraphicsRaycasters)
            {
                graphicsRaycaster.Raycast(eventData, _hits);
            }

            // Check if the user clicked on the requested type
            T targetElement;
            foreach (var result in _hits)
            {
                // Loop through the hit, and all parents, to look for the object
                var element = result.gameObject;
                while (element != null)
                {
                    if ((targetElement = element.GetComponent<T>()) != null)
                    {
                        return targetElement;
                    }

                    element = element.transform?.parent?.gameObject;
                }
            }

            return null;
        }

        /**
         * Update the task "Status" column with the correct value
         */
        private void UpdateTaskStatus(NotesTask notesTask)
        {
            var quest = _taskQuestClassField.GetValue(notesTask) as QuestClass;
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

        private void UpdateTaskConditions(GameObject parentObject)
        {
            var questObjectives = parentObject.GetComponentsInChildren<QuestObjectiveView>();
            foreach (var questObjective in questObjectives)
            {
                var quest = _objectiveQuestClassField.GetValue(questObjective) as QuestClass;
                var condition = _objectiveConditionField.GetValue(questObjective) as Condition;

                UpdateConditionDescription(questObjective, quest, condition);
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

        public static void UpdateConditionDescription(QuestObjectiveView questObjective, QuestClass quest, Condition condition)
        {
            TextMeshProUGUI description = _descriptionField.GetValue(questObjective) as TextMeshProUGUI;

            string descriptionText = condition.FormattedDescription;
            if (!condition.IsNecessary)
            {
                descriptionText = "(optional)".Localized(null) + " " + descriptionText;
            }

            if (QuestsTracker.IsTracked(quest, condition))
            {
                descriptionText = descriptionText +
                    " <color=#" + ColorUtility.ToHtmlStringRGB(QuestTrackerPlugin.TrackedConditionColor) + ">" + ConditionTracked + "</color>";
            }

            description.text = descriptionText;
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
            _questControllerField = AccessTools.GetDeclaredFields(typeof(MainMenuControllerClass)).FirstOrDefault(x => typeof(AbstractQuestControllerClass).IsAssignableFrom(x.FieldType));

            return AccessTools.Method(typeof(MainMenuControllerClass), "ShowScreen");
        }

        [PatchPostfix]
        private static void PatchPostfix(MainMenuControllerClass __instance)
        {
            if (_questsLoaded) return;
            _questsLoaded = true;
#if DEBUG
            Logger.LogDebug("MenuScreenShowPatch Postfix");
#endif

            // Load list of tracked quests
            AbstractQuestControllerClass questController = _questControllerField.GetValue(__instance) as AbstractQuestControllerClass;
            if (!QuestsTracker.Load(questController))
            {
                Logger.LogError("Error, unable to load tracked quests. Clearing");
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
#if DEBUG
            Logger.LogDebug("Task Screen Shown, enable hooks");
#endif

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
#if DEBUG
            Logger.LogDebug("Task Screen Closed, disable hooks");
#endif
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
#if DEBUG
            Logger.LogDebug("Quest Screen Shown, enable hooks");
#endif

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
#if DEBUG
            Logger.LogDebug("Quest Screen Closed, disable hooks");
#endif
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
     * Add the text "(Tracked)" to condition description if task is tracked
     */
    class QuestObjectiveViewShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(QuestObjectiveView), nameof(QuestObjectiveView.Show));
        }

        [PatchPostfix]
        public static void PatchPostfix(QuestObjectiveView __instance, QuestClass quest, Condition condition)
        {
            QuestTrackerPlugin.UpdateConditionDescription(__instance, quest, condition);
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
     * To have tracked quests at the top of the task list, override the status comparer class Compare method
     */
    class TasksScreenStatusComparePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(QuestStatusComparerClass), nameof(QuestStatusComparerClass.Compare));
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
