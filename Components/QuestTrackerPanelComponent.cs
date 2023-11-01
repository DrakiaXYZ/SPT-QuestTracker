using BepInEx.Logging;
using DrakiaXYZ.QuestTracker.Helpers;
using EFT.Quests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace DrakiaXYZ.QuestTracker.Components
{
    internal class QuestTrackerPanelComponent : MonoBehaviour
    {
        public static GameObject QuestTrackerPanelPrefab;
        public static GameObject QuestEntryPrefab;

        private GameObject _panel;

        private StringBuilder _stringBuilderQuest = new StringBuilder();
        private StringBuilder _stringBuilderProgress = new StringBuilder();

        protected ManualLogSource Logger;

        public bool Visible
        {
            get
            {
                return _panel.activeSelf;
            }
            set
            {
                _panel.SetActive(value);
            }
        }

        public QuestTrackerPanelComponent()
        {
            Logger = BepInEx.Logging.Logger.CreateLogSource(GetType().Name);
        }

        public void Awake()
        {
            Logger.LogInfo("QuestTrackerPanelComponent Awake");

            // Create the Panel prefab and add it to the parent object
            _panel = Instantiate(QuestTrackerPanelPrefab);
            _panel.transform.SetParent(transform);

            // Position ourselves on the far right side in the middle
            _panel.transform.position = new Vector3(Screen.width, Screen.height / 2, 0);
        }

        public void SetQuests(Dictionary<string, QuestClass> trackedQuests, List<QuestClass> mapQuests)
        {
            // Make sure we have enough QuestEntry objects
            int totalEntries = trackedQuests.Count;
            if (Settings.IncludeMapQuests.Value)
            {
                totalEntries += mapQuests.Count;
            }

            if (ChildCount() < totalEntries)
            {
                AddChildren(totalEntries - ChildCount());
            }

            // Setup the main tracked quests
            int questIndex = 0;
            foreach (var quest in trackedQuests.Values.ToList().OrderBy(quest => quest.Template.Name))
            {
                AddQuestToStringBuilder(quest);

                if (Settings.ShowObjectives.Value)
                {
                    AddQuestObjectivesToStringBuilder(quest);
                }

                SetQuestText(questIndex, 
                    _stringBuilderQuest.ToString().TrimEnd(Environment.NewLine.ToCharArray()),
                    _stringBuilderProgress.ToString().TrimEnd(Environment.NewLine.ToCharArray()));

                questIndex++;
            }

            if (Settings.IncludeMapQuests.Value)
            {
                foreach (var quest in mapQuests.OrderBy(quest => quest.Template.Name))
                {
                    if (quest.Template != null && trackedQuests.ContainsKey(quest.Id)) continue;
                    AddQuestToStringBuilder(quest);

                    if (Settings.ShowObjectives.Value)
                    {
                        AddQuestObjectivesToStringBuilder(quest);
                    }

                    SetQuestText(questIndex, 
                        _stringBuilderQuest.ToString().TrimEnd(Environment.NewLine.ToCharArray()), 
                        _stringBuilderProgress.ToString().TrimEnd(Environment.NewLine.ToCharArray()));

                    questIndex++;
                }
            }

            // Disable any remaining children
            while (questIndex < ChildCount())
            {
                _panel.transform.GetChild(questIndex).gameObject.SetActive(false);
                questIndex++;
            }

            // Update the font size
            SetFontSize(Settings.MainFontSize.Value);
        }

        public void SetFontSize(int fontSize)
        {
            foreach (var item in _panel.GetComponentsInChildren<Text>())
            {
                item.fontSize = fontSize;
            }
        }

        private void AddQuestToStringBuilder(QuestClass quest)
        {
            // Filter out any quest that isn't currently started/done/failed
            EQuestStatus status = quest.QuestStatus;
            if (status != EQuestStatus.Started && status != EQuestStatus.AvailableForFinish && status != EQuestStatus.MarkedAsFailed)
            {
                return;
            }

            // Clear the StringBuilders
            _stringBuilderQuest.Clear();
            _stringBuilderProgress.Clear();

            _stringBuilderQuest.AppendLine($"{quest.Template.Name}:");

            switch (status)
            {
                case EQuestStatus.AvailableForFinish:
                    _stringBuilderProgress.AppendLine("<color=#00ff00ff>✓</color>");
                    break;
                case EQuestStatus.MarkedAsFailed:
                    _stringBuilderProgress.AppendLine("<color=#ff0000ff>✗</color>");
                    break;
                default:
                    // No divide by zero errors here
                    float current = quest.Progress.current;
                    float max = quest.Progress.absolute;
                    if (max.ApproxEquals(0f))
                    {
                        _stringBuilderProgress.AppendLine("<color=#00ff00ff>✓</color>");
                    }
                    else
                    {
                        if (Settings.ProgressAsPercent.Value)
                        {
                            _stringBuilderProgress.AppendLine($"{Mathf.FloorToInt((current / max) * 100)}%");
                        }
                        else
                        {
                            _stringBuilderProgress.AppendLine($"{current} / {max}");
                        }
                    }
                    break;
            }
        }

        private void AddQuestObjectivesToStringBuilder(QuestClass quest)
        {
            // Filter out any quest that isn't currently started/done/failed
            EQuestStatus status = quest.QuestStatus;
            if (status != EQuestStatus.Started && status != EQuestStatus.AvailableForFinish && status != EQuestStatus.MarkedAsFailed)
            {
                return;
            }

            foreach (var condition in quest.NecessaryConditions)
            {
                string description = condition.FormattedDescription;
                if (description.Length > 60)
                {
                    description = description.Substring(0, description.LastIndexOf(' ', 60)) + "…";
                }
                _stringBuilderQuest.AppendLine($"<size={Settings.SubFontSize.Value}>{description}</size>");

                _stringBuilderProgress.Append($"<size={Settings.SubFontSize.Value}>");
                if (quest.IsConditionDone(condition))
                {
                    _stringBuilderProgress.Append("<color=#00ff00ff>✓</color>");
                }
                else
                {
                    var conditionHandler = quest.ConditionHandlers[condition];
                    if (conditionHandler.HasGetter())
                    {
                        float max = condition.value;
                        float current = Mathf.Min(conditionHandler.CurrentValue, max);

                        if (Settings.ObjectivesAsPercent.Value)
                        {
                            _stringBuilderProgress.Append($"{Mathf.FloorToInt((current / max) * 100)}%");
                        }
                        else
                        {
                            _stringBuilderProgress.Append($"{current} / {max}");
                        }
                    }
                }
                _stringBuilderProgress.AppendLine("</size>");
            }
        }

        private int ChildCount()
        {
            return _panel.transform.childCount;
        }

        private void SetQuestText(int questIndex, string questText, string progressText)
        {
            if (questIndex >= ChildCount())
            {
                throw new IndexOutOfRangeException("Quest index out of range");
            }

            Transform questTransform = _panel.transform.GetChild(questIndex);
            questTransform.gameObject.SetActive(true);

            questTransform.GetChild(0).GetComponent<Text>().text = questText;
            questTransform.GetChild(1).GetComponent<Text>().text = progressText;
        }

        private void AddChildren(int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject entry = Instantiate(QuestEntryPrefab);
                entry.transform.SetParent(_panel.transform);
            }
        }

        private void OnDestroy()
        {
            Logger.LogInfo("QuestTrackerPanelComponent Destroy");
            _panel.DestroyAllChildren();
            Destroy(_panel);
        }
    }
}
