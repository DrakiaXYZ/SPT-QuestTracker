using BepInEx.Logging;
using EFT;
using EFT.Quests;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DrakiaXYZ.QuestTracker.Helpers
{
    internal class QuestsTracker
    {
        public static event EventHandler<QuestClass> QuestTracked;
        public static event EventHandler<QuestClass> QuestUntracked;
        public static event EventHandler<Condition> ConditionTracked;
        public static event EventHandler<Condition> ConditionUntracked;
        
        private static QuestsTracker _instance = new QuestsTracker();
        private static ManualLogSource _logger;
        private static Profile _profile = null;

        private Dictionary<string, TrackedQuestData> _trackedQuests = new Dictionary<string, TrackedQuestData>();

        public static void SetLogger(ManualLogSource logger)
        {
            _logger = logger;
        }

        public static bool TrackQuest(QuestClass quest)
        {
            bool wasTracked = false;
            string questId = quest.Id;

            if (!_instance._trackedQuests.TryGetValue(questId, out var trackedQuestData))
            {
                trackedQuestData = new TrackedQuestData();
                _instance._trackedQuests.Add(questId, trackedQuestData);
                wasTracked = true;
            }
            else if (!trackedQuestData.Tracked)
            {
                trackedQuestData.Tracked = true;
                wasTracked = true;
            }

            if (wasTracked)
            {
                QuestTracked?.Invoke(null, quest);
            }

            return wasTracked;
        }

        public static bool UntrackQuest(QuestClass quest)
        {
            bool wasUntracked = false;
            string questId = quest.Id;
            if (_instance._trackedQuests.TryGetValue(questId, out var trackedQuest) && trackedQuest.Tracked)
            {
                trackedQuest.Tracked = false;
                if (trackedQuest.Conditions.Count == 0)
                {
                    _instance._trackedQuests.Remove(questId);
                }

                wasUntracked = true;
            }

            if (wasUntracked)
            {
                QuestUntracked?.Invoke(null, quest);
            }

            return wasUntracked;
        }

        public static bool IsTracked(QuestClass quest)
        {
            string questId = quest.Id;
            bool tracked = false;
            if (_instance._trackedQuests.TryGetValue(questId, out var trackedQuestData))
            {
                tracked = trackedQuestData.Tracked;
            }

            // Remove the quest if it's no longer in a trackable status
            EQuestStatus status = quest.QuestStatus;
            if (tracked && status != EQuestStatus.Started && status != EQuestStatus.AvailableForFinish && status != EQuestStatus.MarkedAsFailed)
            {
                UntrackQuest(quest);
                tracked = false;
            }

            return tracked;
        }

        public static bool TrackCondition(QuestClass quest, Condition condition)
        {
            if (_instance._trackedQuests.TryGetValue(quest.Id, out var trackedQuestData))
            {
                if (trackedQuestData.Conditions.Add(condition.id))
                {
                    ConditionTracked?.Invoke(null, condition);
                    return true;
                }
            }

            return false;
        }

        public static bool UntrackCondition(QuestClass quest, Condition condition)
        {
            if (_instance._trackedQuests.TryGetValue(quest.Id, out var trackedQuestData))
            {
                if (trackedQuestData.Conditions.Remove(condition.id))
                {
                    ConditionUntracked?.Invoke(null, condition);

                    // If we actually removed a condition, and it was the last one, untrack the quest
                    if (trackedQuestData.Conditions.Count == 0)
                    {
                        UntrackQuest(quest);
                    }

                    return true;
                }
            }

            return false;
        }

        public static bool IsTracked(QuestClass quest, Condition condition)
        {
            // If we're not tracking the parent quest, we're not tracking the condition
            if (_instance._trackedQuests.TryGetValue(quest.Id, out var trackedQuestData) && trackedQuestData.Tracked)
            {
                if (trackedQuestData.Conditions.Contains(condition.id))
                {
                    return true;
                }
            }

            return false;
        }

        public static Dictionary<string, TrackedQuestData> GetTrackedQuests()
        {
            return _instance._trackedQuests;
        }

        public static void Save()
        {
            // Make sure we have a stored profile
            if (_profile == null)
            {
                throw new Exception("Unable to save, missing profile");
            }

            string questsPath = Path.Combine(QuestTrackerPlugin.ConfigFolder, $"{_profile.Id}.json");
            string jsonString = JsonConvert.SerializeObject(_instance._trackedQuests, Formatting.Indented);
            File.Create(questsPath).Dispose();
            StreamWriter streamWriter = new StreamWriter(questsPath);
            streamWriter.Write(jsonString);
            streamWriter.Flush();
            streamWriter.Close();
        }

        public static bool Load(AbstractQuestControllerClass questController)
        {
            _instance._trackedQuests = new Dictionary<string, TrackedQuestData>();
            _profile = questController.Profile;

            // No tracking file, just return
            string questsPath = Path.Combine(QuestTrackerPlugin.ConfigFolder, $"{_profile.Id}.json");
            if (!File.Exists(questsPath))
            {
                return true;
            }

            // If we fail to load the tracked quests, just use a blank list
            try
            {
                _instance._trackedQuests = JsonConvert.DeserializeObject<Dictionary<string, TrackedQuestData>>(File.ReadAllText(questsPath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                Save();
                return false;
            }

            // Check for any quests that are no longer trackable, and remove them
            int initialCount = _instance._trackedQuests.Count;
            _instance._trackedQuests = _instance._trackedQuests.Where((entry) =>
            {
                QuestClass quest = Utils.GetQuest(questController, entry.Key);
                if (quest == null)
                {
                    return false;
                }

                EQuestStatus status = quest.QuestStatus;
                if (status != EQuestStatus.Started && status != EQuestStatus.AvailableForFinish && status != EQuestStatus.MarkedAsFailed)
                {
                    return false;
                }

                return true;
            }).ToDictionary(i => i.Key, i => i.Value);

            // If we removed elements, re-save to disk
            int removedCount = initialCount - _instance._trackedQuests.Count;
            if (removedCount > 0)
            {
                Save();
            }

            return true;
        }
    }

    internal class TrackedQuestData
    {
        public bool Tracked;
        public HashSet<string> Conditions;

        public TrackedQuestData()
        {
            Tracked = true;
            Conditions = new HashSet<string>();
        }
    }
}
