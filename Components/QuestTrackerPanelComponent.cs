using BepInEx.Logging;
using DrakiaXYZ.QuestTracker.Helpers;
using EFT.Quests;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DrakiaXYZ.QuestTracker.Components
{
    internal class QuestTrackerPanelComponent : MonoBehaviour
    {
        public static GameObject QuestTrackerPanelPrefab;
        public static GameObject QuestEntryPrefab;
        public static GameObject QuestObjectivesPrefab;

        private GameObject _panel;
        private Image _background;
        private LayoutElement _layoutElement;
        private VerticalLayoutGroup _layoutGroup;

        private List<GameObject> _questEntryObjects = new List<GameObject>();
        private List<GameObject> _questObjectiveObjects = new List<GameObject>();

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

            // Create the Panel prefab, clear its children and add it to the parent object
            _panel = Instantiate(QuestTrackerPanelPrefab);
            _panel.DestroyAllChildren();
            _panel.transform.SetParent(transform);

            _background = _panel.GetComponent<Image>();
            _layoutElement = _panel.GetComponent<LayoutElement>();
            _layoutGroup = _panel.GetComponent<VerticalLayoutGroup>();

            // Restore settings
            SetAlignment(Settings.Alignment.Value);
            SetTransparency(Settings.Transparency.Value);
            SetWidth(Settings.MaxWidth.Value);
        }

        public void SetQuests(Dictionary<string, QuestClass> trackedQuests, List<QuestClass> mapQuests)
        {
            // Make sure we have enough QuestEntry objects
            int totalEntries = trackedQuests.Count;
            if (Settings.IncludeMapQuests.Value)
            {
                totalEntries += mapQuests.Count;
            }

            // Make sure we have enough QuestEntry objects
            CreatePrefabs(QuestEntryPrefab, _questEntryObjects, totalEntries, Settings.MainFontSize.Value);

            // Make sure we have enough QuestObjective objects if show objectives is enabled
            if (Settings.ShowObjectives.Value)
            {
                CreateObjectivePrefabs(trackedQuests, mapQuests);
            }

            // Setup the main tracked quests
            int questIndex = 0;
            int objectiveIndex = 0;
            int siblingIndex = 0;
            foreach (var quest in trackedQuests.Values.ToList().OrderBy(quest => quest.Template.Name))
            {
                SetupQuest(quest, ref questIndex, ref objectiveIndex, ref siblingIndex);
            }

            if (Settings.IncludeMapQuests.Value)
            {
                foreach (var quest in mapQuests.OrderBy(quest => quest.Template.Name))
                {
                    if (quest.Template != null && trackedQuests.ContainsKey(quest.Id)) continue;

                    SetupQuest(quest, ref questIndex, ref objectiveIndex, ref siblingIndex);
                }
            }

            // Disable any remaining child elements
            while (questIndex < _questEntryObjects.Count)
            {
                _questEntryObjects[questIndex].SetActive(false);
                questIndex++;
            }
            while (objectiveIndex < _questObjectiveObjects.Count)
            {
                _questObjectiveObjects[objectiveIndex].SetActive(false);
                objectiveIndex++;
            }

            // Set the width with our new elements
            SetWidth(Settings.MaxWidth.Value);
        }

        public void SetupQuest(QuestClass quest, ref int questIndex, ref int objectiveIndex, ref int siblingIndex)
        {
            // Skip quests that are ready to hand in if we're hiding completed quests
            if (Settings.HideCompletedQuests.Value && quest.QuestStatus == EQuestStatus.AvailableForFinish)
            {
                return;
            }

            questIndex = SetupQuestObject(quest, questIndex, ref siblingIndex);

            if (Settings.ShowObjectives.Value)
            {
                objectiveIndex = SetupQuestObjectiveObjects(quest, objectiveIndex, ref siblingIndex);
            }
        }

        public void SetWidth(int maxWidth)
        {
            // Force a rebuild of the layout so we get an accurate width
            LayoutRebuilder.ForceRebuildLayoutImmediate(_layoutGroup.RectTransform());

            if (_layoutGroup.preferredWidth > maxWidth)
            {
                _layoutElement.preferredWidth = maxWidth;
            }
            else
            {
                _layoutElement.preferredWidth = -1;
            }    
        }

        public void SetMainFontSize(int fontSize)
        {
            foreach (var entry in _questEntryObjects)
            {
                foreach (var item in entry.GetComponentsInChildren<Text>())
                {
                    item.fontSize = fontSize;
                }
            }

            SetWidth(Settings.MaxWidth.Value);
        }

        public void SetSubFontSize(int fontSize)
        {
            foreach (var entry in _questObjectiveObjects)
            {
                foreach (var item in entry.GetComponentsInChildren<Text>())
                {
                    item.fontSize = fontSize;
                }
            }

            SetWidth(Settings.MaxWidth.Value);
        }

        public void SetFont(bool coolKidsClub)
        {
            Font font = GetActiveFont(coolKidsClub);

            foreach (var item in _panel.GetComponentsInChildren<Text>())
            {
                item.font = font;
            }

            SetWidth(Settings.MaxWidth.Value);
        }

        public Font GetActiveFont(bool coolKidsClub)
        {
            Font font;
            if (coolKidsClub || this.coolKidsClub())
            {
                font = Font.CreateDynamicFontFromOSFont("Comic Sans MS", Settings.MainFontSize.Value);
            }
            else
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return font;
        }

        // Shhhhhhhhhhhhhhh
        private bool coolKidsClub()
        {
            return (DateTime.Now.Month == 4 && DateTime.Now.Day == 1);
        }

        public void SetTransparency(float transparency)
        {
            _background.color = _background.color.SetAlpha(transparency);
        }

        public void SetAlignment(Settings.EAlignment alignment)
        {
            if (alignment == Settings.EAlignment.Left)
            {
                _panel.RectTransform().pivot = new Vector2(0f, 0.5f);
                _panel.transform.position = new Vector3(0, Screen.height / 2, 0);
            }
            else
            {
                _panel.RectTransform().pivot = new Vector2(1f, 0.5f);
                _panel.transform.position = new Vector3(Screen.width, Screen.height / 2, 0);
            }
        }

        private int SetupQuestObject(QuestClass quest, int questIndex, ref int siblingIndex)
        {
            // Filter out any quest that isn't currently started/done/failed
            EQuestStatus status = quest.QuestStatus;
            if (status != EQuestStatus.Started && status != EQuestStatus.AvailableForFinish && status != EQuestStatus.MarkedAsFailed)
            {
                return questIndex;
            }

            // Clear the StringBuilders
            string questName = $"{quest.Template.Name}:";
            string questProgress;
            switch (status)
            {
                case EQuestStatus.AvailableForFinish:
                    questProgress = "<color=#00ff00ff>✓</color>";
                    break;
                case EQuestStatus.MarkedAsFailed:
                    questProgress = "<color=#ff0000ff>✗</color>";
                    break;
                default:
                    // No divide by zero errors here
                    float current = quest.Progress.current;
                    float max = quest.Progress.absolute;
                    if (max.ApproxEquals(0f))
                    {
                        questProgress = "<color=#00ff00ff>✓</color>";
                    }
                    else
                    {
                        if (Settings.ProgressAsPercent.Value)
                        {
                            questProgress = $"{Mathf.FloorToInt((current / max) * 100)}%";
                        }
                        else
                        {
                            questProgress = $"{current} / {max}";
                        }
                    }
                    break;
            }

            SetQuestText(siblingIndex++, questIndex++, questName, questProgress);

            return questIndex;
        }

        private int SetupQuestObjectiveObjects(QuestClass quest, int objectiveIndex, ref int siblingIndex)
        {
            // Filter out any quest that isn't currently started/done/failed
            EQuestStatus status = quest.QuestStatus;
            if (status != EQuestStatus.Started && status != EQuestStatus.AvailableForFinish && status != EQuestStatus.MarkedAsFailed)
            {
                return objectiveIndex;
            }

            foreach (var condition in quest.NecessaryConditions)
            {
                bool isConditionDone = quest.IsConditionDone(condition);

                if (Settings.HideCompletedObjectives.Value && isConditionDone)
                {
                    continue;
                }

                string description = condition.FormattedDescription;
                string progress = "";

                if (isConditionDone)
                {
                    progress = "<color=#00ff00ff>✓</color>";
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
                            progress = $"{Mathf.FloorToInt((current / max) * 100)}%";
                        }
                        else
                        {
                            progress = $"{current} / {max}";
                        }
                    }
                }

                SetObjectiveText(siblingIndex++, objectiveIndex++, description, progress);
            }

            return objectiveIndex;
        }

        private void SetQuestText(int siblingIndex, int questIndex, string questText, string progressText)
        {
            if (questIndex >= _questEntryObjects.Count)
            {
                throw new IndexOutOfRangeException("Quest index out of range");
            }

            Transform questTransform = _questEntryObjects.ElementAt(questIndex).transform;
            questTransform.SetSiblingIndex(siblingIndex);
            questTransform.gameObject.SetActive(true);

            questTransform.GetChild(0).GetComponent<Text>().text = questText;
            questTransform.GetChild(1).GetComponent<Text>().text = progressText;
        }

        private void SetObjectiveText(int siblingIndex, int objectiveIndex, string objectiveText, string progressText)
        {
            if (objectiveIndex >= _questObjectiveObjects.Count)
            {
                throw new IndexOutOfRangeException("Quest index out of range");
            }

            Transform questTransform = _questObjectiveObjects.ElementAt(objectiveIndex).transform;
            questTransform.SetSiblingIndex(siblingIndex);
            questTransform.gameObject.SetActive(true);

            questTransform.GetChild(0).GetComponent<Text>().text = objectiveText;
            questTransform.GetChild(1).GetComponent<Text>().text = progressText;
        }

        private void CreateObjectivePrefabs(Dictionary<string, QuestClass> trackedQuests, List<QuestClass> mapQuests)
        {
            int objectiveCount = 0;
            foreach (var quest in trackedQuests.Values)
            {
                objectiveCount += GetObjectiveCount(quest);
            }

            if (Settings.IncludeMapQuests.Value)
            {
                foreach (var quest in mapQuests)
                {
                    if (quest.Template != null && trackedQuests.ContainsKey(quest.Id)) continue;

                    objectiveCount += GetObjectiveCount(quest);
                }
            }

            CreatePrefabs(QuestObjectivesPrefab, _questObjectiveObjects, objectiveCount, Settings.SubFontSize.Value);
        }

        private int GetObjectiveCount(QuestClass quest)
        {
            // Filter out any quest that isn't currently started/done/failed
            EQuestStatus status = quest.QuestStatus;
            if (status != EQuestStatus.Started && status != EQuestStatus.AvailableForFinish && status != EQuestStatus.MarkedAsFailed)
            {
                return 0;
            }

            int objectiveCount = 0;
            foreach (var condition in quest.NecessaryConditions)
            {
                if (Settings.HideCompletedObjectives.Value && quest.IsConditionDone(condition))
                {
                    continue;
                }

                objectiveCount++;
            }

            return objectiveCount;
        }

        private void CreatePrefabs(GameObject prefab, List<GameObject> objectList, int maxPrefabs, int fontSize)
        {
            while (objectList.Count < maxPrefabs)
            {
                GameObject entry = Instantiate(prefab);
                objectList.Add(entry);

                entry.transform.SetParent(_panel.transform);

                foreach (var text in entry.GetComponentsInChildren<Text>())
                {
                    text.font = GetActiveFont(Settings.CoolKidsClub.Value);
                    text.fontSize = fontSize;
                }
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
