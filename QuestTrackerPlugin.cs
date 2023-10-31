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
        public static bool QuestScreenActive = false;
        public static GraphicRaycaster[] GraphicsRaycasters;

        private List<RaycastResult> _hits = new List<RaycastResult>();
        private EventSystem _eventSystem;

        private static FieldInfo _questClassField;
        private static FieldInfo _taskDescriptionField;
        private static MethodInfo _getColorMethod;
        private static MethodInfo _setColorMethod;

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

            new MainMenuControllerShowScreenPatch().Enable();
            new TasksScreenShowPatch().Enable();
            new TasksScreenClosePatch().Enable();
            new NotesTaskBackgroundPatch().Enable();
            new NewGamePatch().Enable();
        }

        public void Update()
        {
            if (!QuestScreenActive) return;

            if (Input.GetMouseButtonDown(1))
            {
                HandleRightClick();
            }
        }

        private void HandleRightClick()
        {
            NotesTask notesTask = GetNotesTaskUnderMouse();
            if (notesTask == null) return;

            var quest = _questClassField.GetValue(notesTask) as QuestClass;

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

            // Update the task background color
            UpdateTaskColor(notesTask);
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
     * Toggle the QuestScreenActive flag on and fetch the GraphicsRaycasters for mouse handling
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

            QuestTrackerPlugin.QuestScreenActive = true;
            QuestTrackerPlugin.GraphicsRaycasters = UnityEngine.Object.FindObjectsOfType<GraphicRaycaster>();
        }
    }

    /**
     * Toggle the QuestScreenActive flag off
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
            QuestTrackerPlugin.QuestScreenActive = false;
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
