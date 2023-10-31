using BepInEx.Logging;
using EFT;
using EFT.Quests;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace DrakiaXYZ.QuestTracker.Helpers
{
    internal class QuestsTracker
    {
        public static event EventHandler<QuestClass> QuestTracked;
        public static event EventHandler<QuestClass> QuestUntracked;
        
        private static QuestsTracker _instance = new QuestsTracker();
        private static string _questsPath = Path.Combine(QuestTrackerPlugin.ConfigFolder, "quests.json");
        private static ManualLogSource _logger;

        private HashSet<string> _trackedQuests = new HashSet<string>();
        public HashSet<string> TrackedQuests
        {
            get
            {
                return _trackedQuests;
            }
        }

        public static void SetLogger(ManualLogSource logger)
        {
            _logger = logger;
        }

        public static bool TrackQuest(QuestClass quest)
        {
            string questId = quest.Template.TemplateId;
            QuestTracked?.Invoke(null, quest);

            return _instance.TrackedQuests.Add(questId);
        }

        public static bool UntrackQuest(QuestClass quest)
        {
            string questId = quest.Template.TemplateId;
            QuestUntracked?.Invoke(null, quest);
            return _instance.TrackedQuests.Remove(questId);
        }

        public static bool IsTracked(QuestClass quest)
        {
            string questId = quest.Template.TemplateId;
            bool tracked = _instance.TrackedQuests.Contains(questId);

            // Remove the quest if it's no longer in a trackable status
            EQuestStatus status = quest.QuestStatus;
            if (tracked && status != EQuestStatus.Started && status != EQuestStatus.AvailableForFinish && status != EQuestStatus.MarkedAsFailed)
            {
                UntrackQuest(quest);
                tracked = false;
            }

            return tracked;
        }

        public static HashSet<string> GetTrackedQuests()
        {
            return _instance.TrackedQuests;
        }

        public static void Save()
        {
            string jsonString = JsonConvert.SerializeObject(GetTrackedQuests(), Formatting.Indented);
            File.Create(_questsPath).Dispose();
            StreamWriter streamWriter = new StreamWriter(_questsPath);
            streamWriter.Write(jsonString);
            streamWriter.Flush();
            streamWriter.Close();
        }

        public static bool Load(QuestControllerClass questController)
        {
            if (!File.Exists(_questsPath))
            {
                _instance._trackedQuests = new HashSet<string>();
                return true;
            }

            try
            {
                _instance._trackedQuests = JsonConvert.DeserializeObject<HashSet<string>>(File.ReadAllText(_questsPath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                return false;
            }

            // Check for any quests that are no longer trackable, and remove them
            int removedCount = _instance._trackedQuests.RemoveWhere(questId =>
            {
                QuestClass quest = questController.Quests.GetQuest(questId);
                EQuestStatus status = quest.QuestStatus;
                if (status != EQuestStatus.Started && status != EQuestStatus.AvailableForFinish && status != EQuestStatus.MarkedAsFailed)
                {
                    return true;
                }

                return false;
            });

            // If we removed elements, re-save to disk
            if (removedCount > 0)
            {
                Save();
            }

            return true;
        }
    }
}
