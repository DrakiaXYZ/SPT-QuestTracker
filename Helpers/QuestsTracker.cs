using BepInEx.Logging;
using EFT;
using EFT.Quests;
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
        private static ManualLogSource _logger;
        private static Profile _profile = null;

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
            string questId = quest.Id;
            bool wasAdded = _instance.TrackedQuests.Add(questId);
            QuestTracked?.Invoke(null, quest);

            return wasAdded;
        }

        public static bool UntrackQuest(QuestClass quest)
        {
            string questId = quest.Id;
            bool wasRemoved = _instance.TrackedQuests.Remove(questId);
            QuestUntracked?.Invoke(null, quest);
            return wasRemoved;
        }

        public static bool IsTracked(QuestClass quest)
        {
            string questId = quest.Id;
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
            // Make sure we have a stored profile
            if (_profile == null)
            {
                throw new Exception("Unable to save, missing profile");
            }

            string questsPath = Path.Combine(QuestTrackerPlugin.ConfigFolder, $"{_profile.Id}.json");
            string jsonString = JsonConvert.SerializeObject(GetTrackedQuests(), Formatting.Indented);
            File.Create(questsPath).Dispose();
            StreamWriter streamWriter = new StreamWriter(questsPath);
            streamWriter.Write(jsonString);
            streamWriter.Flush();
            streamWriter.Close();
        }

        public static bool Load(AbstractQuestControllerClass questController)
        {
            _profile = questController.Profile;

            string questsPath = Path.Combine(QuestTrackerPlugin.ConfigFolder, $"{_profile.Id}.json");
            if (!File.Exists(questsPath))
            {
                _instance._trackedQuests = new HashSet<string>();
                return true;
            }

            try
            {
                _instance._trackedQuests = JsonConvert.DeserializeObject<HashSet<string>>(File.ReadAllText(questsPath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                return false;
            }

            // Check for any quests that are no longer trackable, and remove them
            int removedCount = _instance._trackedQuests.RemoveWhere(questId =>
            {
                QuestClass quest = Utils.GetQuest(questController, questId);
                if (quest == null)
                {
                    return true;
                }

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
