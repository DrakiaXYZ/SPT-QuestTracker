using Aki.Reflection.Patching;
using BepInEx;
using DrakiaXYZ.QuestTracker.Components;
using DrakiaXYZ.QuestTracker.Helpers;
using DrakiaXYZ.QuestTracker.VersionChecker;
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

namespace DrakiaXYZ.QuestTracker
{
    [BepInPlugin("xyz.drakia.questtracker", "DrakiaXYZ-QuestTracker", "1.0.0")]
    public class QuestTrackerPlugin : BaseUnityPlugin
    {
        public static string PluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public static string ConfigFolder = Path.Combine(PluginFolder, "config");
        public static bool TasksScreenActive = false;
        public static bool QuestsScreenActive = false;
        public static GraphicRaycaster[] GraphicsRaycasters;

        private List<RaycastResult> _hits = new List<RaycastResult>();
        private EventSystem _eventSystem;

        private static FieldInfo _questClassField;
        private static FieldInfo _taskDescriptionField;
        private static MethodInfo _getColorMethod;
        private static MethodInfo _setColorMethod;
        private static MethodInfo _questListItemUpdateViewMethod;

        private void Awake()
        {
            if (!TarkovVersion.CheckEftVersion(Logger, Info, Config))
            {
                throw new Exception($"Invalid EFT Version");
            }

            Settings.Init(Config);
            QuestsTracker.SetLogger(Logger);
            Directory.CreateDirectory(ConfigFolder);

            _eventSystem = FindObjectOfType<EventSystem>();

            Type notesTaskType = typeof(NotesTask);
            _questClassField = AccessTools.GetDeclaredFields(notesTaskType).Single(x => x.FieldType == typeof(QuestClass));
            _taskDescriptionField = AccessTools.GetDeclaredFields(notesTaskType).Single(x => x.FieldType == typeof(NotesTaskDescriptionShort));
            _getColorMethod = AccessTools.GetDeclaredMethods(notesTaskType).Single(x => x.ReturnType == typeof(Color));
            _setColorMethod = AccessTools.GetDeclaredMethods(notesTaskType).Single(x => x.GetParameters().Length == 1 && x.GetParameters()[0].ParameterType == typeof(Color));
            _questListItemUpdateViewMethod = AccessTools.Method(typeof(QuestListItem), "UpdateView");

            new MainMenuControllerShowScreenPatch().Enable();
            new NewGamePatch().Enable();

            // Player task list
            new TasksScreenShowPatch().Enable();
            new TasksScreenClosePatch().Enable();
            new NotesTaskBackgroundPatch().Enable();

            // Vendor quest list
            new QuestsScreenShowPatch().Enable();
            new QuestsScreenClosePatch().Enable();
            new QuestsListItemUpdateViewPatch().Enable();
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

                    // Update the task background color
                    UpdateTaskColor(notesTask);
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

        private void UpdateTaskColor(NotesTask notesTask)
        {
            var notesDescription = _taskDescriptionField.GetValue(notesTask) as NotesTaskDescriptionShort;
            var color = (Color)_getColorMethod.Invoke(notesTask, new object[] { "default" });
            if (notesDescription.transform.IsChildOf(notesTask.transform) && notesDescription.gameObject.activeSelf)
            {
                color = (Color)_getColorMethod.Invoke(notesTask, new object[] { "selected" });
            }
            _setColorMethod.Invoke(notesTask, new object[] { color });
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
            _questControllerField = AccessTools.GetDeclaredFields(typeof(MainMenuController)).FirstOrDefault(x => typeof(QuestControllerClass).IsAssignableFrom(x.FieldType));

            return AccessTools.Method(typeof(MainMenuController), "ShowScreen");
        }

        [PatchPostfix]
        private static void PatchPostfix(MainMenuController __instance)
        {
            if (_questsLoaded) return;
            _questsLoaded = true;

            Logger.LogDebug("MenuScreenShowPatch Postfix");

            // Load list of tracked quests
            QuestControllerClass questController = _questControllerField.GetValue(__instance) as QuestControllerClass;
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
        private static Color _trackedColor = new Color(0, 72, 87);
        private static string _trackedStatus = "Tracked!";
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
                ____status.text = _trackedStatus;
                ____status.color = _trackedColor;
            }
        }
    }

    /**
     * Set the background color of the first column for tracked quests
     */
    class NotesTaskBackgroundPatch : ModulePatch
    {
        private static FieldInfo _questClassField;

        private static Color _trackedColor = new Color(0, 72, 87);

        protected override MethodBase GetTargetMethod()
        {
            Type notesTaskType = typeof(NotesTask);
            _questClassField = AccessTools.GetDeclaredFields(notesTaskType).Single(x => x.FieldType == typeof(QuestClass));

            return AccessTools.GetDeclaredMethods(notesTaskType).Single(x => x.GetParameters().Length == 1 && x.GetParameters()[0].ParameterType == typeof(Color));
        }

        [PatchPostfix]
        public static void PatchPostfix(NotesTask __instance, List<Image> ____backgroundImages)
        {
            var quest = _questClassField.GetValue(__instance) as QuestClass;
            if (QuestsTracker.IsTracked(quest))
            {
                ____backgroundImages[0].color = _trackedColor;
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
