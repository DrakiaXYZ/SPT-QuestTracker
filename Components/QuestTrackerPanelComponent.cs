using BepInEx.Logging;
using DrakiaXYZ.QuestTracker.Helpers;
using EFT.Quests;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DrakiaXYZ.QuestTracker.Components
{
    internal class QuestTrackerPanelComponent : MonoBehaviour
    {
        private QuestTrackerPanelUI _ui;
        
        protected ManualLogSource Logger;
        
        private Coroutine _fadeRoutine;
        private bool _visible;
        private const float FadeDuration = 0.20f;

        public bool Visible
        {
            get => _visible;
            set => SetVisible(value);
        }

        public QuestTrackerPanelComponent()
        {
            Logger = BepInEx.Logging.Logger.CreateLogSource(GetType().Name);
        }

        public void Awake()
        {
            _ui = new QuestTrackerPanelUI();
            _ui.Build(transform);

            _visible = false;
            _ui.Group.alpha = 0f;
            _ui.Group.interactable = false;
            _ui.Group.blocksRaycasts = false;

            _ui.SetWrapText(Settings.WrapText.Value);
            _ui.SetAlignment(Settings.Alignment.Value, Settings.VerticalAlignment.Value);
            _ui.SetOffsets(Settings.Alignment.Value, Settings.VerticalAlignment.Value,
                Settings.HorizontalOffset.Value, Settings.VerticalOffset.Value);
            _ui.SetTransparency(Settings.Transparency.Value);
            _ui.SetMaxWidth(Settings.MaxWidth.Value);
            
            _ui.SetMainProgressWidth(Settings.MainProgressWidth.Value);
            _ui.SetSubProgressWidth(Settings.SubProgressWidth.Value);
        
            _ui.SetMainFontSize(Settings.MainFontSize.Value);
            _ui.SetSubFontSize(Settings.SubFontSize.Value);
            
            _ui.SetMainColor(Settings.MainTextColor.Value);
            _ui.SetSubColor(Settings.SubTextColor.Value);
        }

        public void SetQuests(Dictionary<string, Tuple<QuestClass, TrackedQuestData>> trackedQuests, List<QuestClass> mapQuests)
        {
            // Logger.LogInfo($"SetQuests called. tracked={trackedQuests.Count} map={mapQuests.Count} includeMap={Settings.IncludeMapQuests.Value} showObj={Settings.ShowObjectives.Value}");
            
            // Make sure we have enough QuestEntry objects
            int totalEntries = trackedQuests.Count;
            if (Settings.IncludeMapQuests.Value)
            {
                totalEntries += mapQuests.Count;
            }

            // Make sure we have enough QuestEntry objects
            _ui.CreateQuestRows(totalEntries);
            
            // Make sure we have enough QuestObjective objects if show objectives is enabled
            if (Settings.ShowObjectives.Value)
            {
                int objectiveCount = 0;

                foreach (var quest in trackedQuests.Values)
                {
                    objectiveCount += GetObjectiveCount(quest.Item1, quest.Item2);
                }

                if (Settings.IncludeMapQuests.Value)
                {
                    foreach (var quest in mapQuests)
                    {
                        if (quest.Template != null && trackedQuests.ContainsKey(quest.Id)) continue;
                        objectiveCount += GetObjectiveCount(quest, null);
                    }
                }

                _ui.CreateObjectiveRows(objectiveCount);
            }

            // Setup the main tracked quests
            int questIndex = 0;
            int objectiveIndex = 0;
            int siblingIndex = 0;

            foreach (var quest in trackedQuests.Values.ToList().OrderBy(q => q.Item1.Template.Name))
            {
                SetupQuest(quest.Item1, quest.Item2, ref questIndex, ref objectiveIndex, ref siblingIndex);
            }

            if (Settings.IncludeMapQuests.Value)
            {
                foreach (var quest in mapQuests.OrderBy(q => q.Template.Name))
                {
                    if (quest.Template != null && trackedQuests.ContainsKey(quest.Id)) continue;

                    SetupQuest(quest, null, ref questIndex, ref objectiveIndex, ref siblingIndex);
                }
            }

            // Disable any remaining child elements
            _ui.HideQuestRowsFrom(questIndex);
            _ui.HideObjectiveRowsFrom(objectiveIndex);
            
            if (questIndex == 0)
                Visible = false;

            // Set the width with our new elements
            _ui.SetMaxWidth(Settings.MaxWidth.Value);
        }
        
        public void SetTestContent()
        {
            bool showObjectives = Settings.ShowObjectives.Value;
            bool hideCompletedQuests = Settings.HideCompletedQuests.Value;
            bool hideCompletedObjectives = Settings.HideCompletedObjectives.Value;

            int questCount = !hideCompletedQuests ? 3 : 2;
            int objectiveCount = !showObjectives ? 0 : hideCompletedObjectives ? 3 : hideCompletedQuests ? 4 : 5;
            
            _ui.CreateQuestRows(questCount);

            if (showObjectives)
            {
                _ui.CreateObjectiveRows(objectiveCount);
            }
            
            int q = 0;
            int o = 0;
            int sib = 0;

            // In-progress quest
            string q1Prog = Settings.ProgressAsPercent.Value ? "33%" : "1 / 3";

            _ui.SetQuestRow(sib++, q++, "TEST QUEST: In Progress - Long title to test wrap", q1Prog, State.InProgress);

            if (showObjectives)
            {
                // Obj1: condition = 1
                _ui.SetObjectiveRow(sib++, o++, "Objective (condition = 1, progress hidden)", "", State.InProgress);

                // Obj2: 3000/5000
                float o2Cur = 3000f;
                float o2Max = 5000f;
                string o2Prog = Settings.ObjectivesAsPercent.Value
                    ? $"{Mathf.FloorToInt((o2Cur / o2Max) * 100f)}%"
                    : $"{o2Cur:0} / {o2Max:0}";

                _ui.SetObjectiveRow(sib++, o++, "Objective (in progress, with high value) - Long objective text to test wrap setting", o2Prog, State.InProgress);

                // Obj3: completed
                if (!hideCompletedObjectives)
                {
                    float o3Cur = 5000f;
                    float o3Max = 5000f;
                    string o3Prog = Settings.ObjectivesAsPercent.Value
                        ? "100%"
                        : $"{o3Cur:0} / {o3Max:0}";

                    _ui.SetObjectiveRow(sib++, o++, "Objective (completed)", o3Prog, State.Complete);
                }
            }

            // Completed quest with completed objective
            if (!hideCompletedQuests)
            {
                _ui.SetQuestRow(sib++, q++, "TEST QUEST: Completed - Long title to test wrap", "", State.Complete);

                if (showObjectives && !hideCompletedObjectives)
                {
                    string oProg = Settings.ObjectivesAsPercent.Value ? "100%" : "5000 / 5000";
                    _ui.SetObjectiveRow(sib++, o++, "Objective (completed)", oProg, State.Complete);
                }
            }

            // Failed quest
            _ui.SetQuestRow(sib++, q++, "TEST QUEST: Failed - Long title to test wrap", "", State.Failed);

            if (showObjectives)
            {
                float cur = 1234f;
                float max = 5000f;
                string prog = Settings.ObjectivesAsPercent.Value
                    ? $"{Mathf.FloorToInt((cur / max) * 100f)}%"
                    : $"{cur:0} / {max:0}";

                _ui.SetObjectiveRow(sib++, o++, "Some objective (with some value)", prog, State.InProgress);
            }
            
            _ui.HideQuestRowsFrom(q);
            _ui.HideObjectiveRowsFrom(o);

            _ui.SetMaxWidth(Settings.MaxWidth.Value);
        }

        public void SetupQuest(QuestClass quest, TrackedQuestData trackedQuestData, ref int questIndex, ref int objectiveIndex, ref int siblingIndex)
        {
            // Skip quests that are ready to hand in if we're hiding completed quests
            if (Settings.HideCompletedQuests.Value && quest.QuestStatus == EQuestStatus.AvailableForFinish)
            {
                return;
            }

            questIndex = SetupQuestObject(quest, questIndex, ref siblingIndex);
            
            if (Settings.ShowObjectives.Value)
            {
                objectiveIndex = SetupQuestObjectiveObjects(quest, trackedQuestData, objectiveIndex, ref siblingIndex);
            }
        }

        public void SetWrapText(bool wrap)
        {
            _ui?.SetWrapText(wrap);
        }
        
        public void SetWidth(int maxWidth)
        {
            // Force a rebuild of the layout so we get an accurate width
            _ui?.SetMaxWidth(maxWidth);
        }

        public void SetMainFontSize(int fontSize)
        {
            _ui?.SetMainFontSize(fontSize);
            _ui?.SetMaxWidth(Settings.MaxWidth.Value);
        }

        public void SetSubFontSize(int fontSize)
        {
            _ui?.SetSubFontSize(fontSize);
            _ui?.SetMaxWidth(Settings.MaxWidth.Value);
        }

        public void SetMainProgressWidth(int width)
        {
            _ui?.SetMainProgressWidth(width);
        }
        
        public void SetSubProgressWidth(int width)
        {
            _ui?.SetSubProgressWidth(width);
        }

        public void SetMainColor(Color color)
        {
            _ui?.SetMainColor(color);
        }

        public void SetSubColor(Color color)
        {
            _ui?.SetSubColor(color);
        }

        public void SetTransparency(float transparency)
        {
            _ui?.SetTransparency(transparency);
        }

        public void SetAlignment(Settings.EAlignment alignment)
        {
            _ui?.SetAlignment(alignment, Settings.VerticalAlignment.Value);
            _ui?.SetOffsets(alignment, Settings.VerticalAlignment.Value,
                Settings.HorizontalOffset.Value, Settings.VerticalOffset.Value);
        }
        
        public void SetVerticalAlignment(Settings.EVerticalAlignment vAlignment)
        {
            _ui?.SetAlignment(Settings.Alignment.Value, vAlignment);
            _ui?.SetOffsets(Settings.Alignment.Value, vAlignment,
                Settings.HorizontalOffset.Value, Settings.VerticalOffset.Value);
        }
        
        public void SetOffsets(int horizontalOffset, int verticalOffset)
        { 
            _ui?.SetOffsets(Settings.Alignment.Value, Settings.VerticalAlignment.Value, 
                horizontalOffset, verticalOffset);
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
            string questName = $"{quest.Template.Name}";
            string questProgress = "";
            State state = State.InProgress;
            
            switch (status)
            {
                case EQuestStatus.AvailableForFinish:
                    state = State.Complete;
                    break;
                case EQuestStatus.MarkedAsFailed:
                    state = State.Failed;
                    break;
                default:
                    // No divide by zero errors here
                    double current = quest.Progress.current;
                    double max = quest.Progress.absolute;
                    if (Utils.ApproxEquals(max, 0f))
                    {
                        state = State.Complete;
                    }
                    else
                    {
                        questProgress = Settings.ProgressAsPercent.Value ? $"{Mathf.FloorToInt((float)(current / max) * 100)}%" : $"{current} / {max}";
                    }
                    break;
            }

            _ui.SetQuestRow(siblingIndex++, questIndex++, questName, questProgress, state);

            return questIndex;
        }

        private int SetupQuestObjectiveObjects(QuestClass quest, TrackedQuestData trackedQuestData, int objectiveIndex, ref int siblingIndex)
        {
            // Filter out any quest that isn't currently started/done/failed
            EQuestStatus status = quest.QuestStatus;
            if (status != EQuestStatus.Started && status != EQuestStatus.AvailableForFinish && status != EQuestStatus.MarkedAsFailed)
            {
                return objectiveIndex;
            }

            var conditionHandlers = Utils.GetConditionHandlers(quest);

            foreach (var condition in quest.NecessaryConditions)
            {
                bool isConditionDone = quest.IsConditionDone(condition);

                if (Settings.HideCompletedObjectives.Value && isConditionDone)
                {
                    continue;
                }

                if (trackedQuestData != null && trackedQuestData.Conditions.Count > 0 && !trackedQuestData.Conditions.Contains(condition.id))
                {
                    continue;
                }

                string description = condition.FormattedDescription;
                string progress = "";
                State state = State.InProgress;

                if (isConditionDone)
                {
                    state = State.Complete;
                }
                else
                {
                    var conditionHandler = conditionHandlers[condition];
                
                    if (Utils.ConditionHasGetter(conditionHandler))
                    {
                        float max = condition.value;
                        float current = Mathf.Min((float)Utils.ConditionCurrentValue(conditionHandler), max);

                        if (Mathf.RoundToInt(max) <= 1)
                        {
                            progress = "";
                        }
                        else
                        {
                            progress = Settings.ObjectivesAsPercent.Value ? $"{Mathf.FloorToInt((current / max) * 100)}%" : $"{current} / {max}";
                        }
                    }
                }

                _ui.SetObjectiveRow(siblingIndex++, objectiveIndex++, description, progress, state);
            }

            return objectiveIndex;
        }

        private int GetObjectiveCount(QuestClass quest, TrackedQuestData trackedQuestData)
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

                // If we're tracking specific conditions, and this isn't one of them, skip
                if (trackedQuestData != null && trackedQuestData.Conditions.Count > 0 && !trackedQuestData.Conditions.Contains(condition.id))
                {
                    continue;
                }

                objectiveCount++;
            }

            return objectiveCount;
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            if (_ui?.Group == null)
            {
                return;
            }

            _ui.Group.interactable = visible;
            _ui.Group.blocksRaycasts = visible;
            
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                _ui.Group.alpha = visible ? 1f : 0f;
                return;
            }

            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }

            _fadeRoutine = StartCoroutine(FadeTo(visible ? 1f : 0f, FadeDuration));
        }
        
        private IEnumerator FadeTo(float target, float duration)
        {
            float start = _ui.Group.alpha;

            if (duration <= 0f)
            {
                _ui.Group.alpha = target;
                _fadeRoutine = null;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _ui.Group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
                yield return null;
            }

            _ui.Group.alpha = target;
            _fadeRoutine = null;
        }

        private void OnDestroy()
        {
            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }

            _ui?.Destroy();
        }
    }
}
