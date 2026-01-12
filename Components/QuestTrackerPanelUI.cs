using System.Collections.Generic;
using DrakiaXYZ.QuestTracker.Helpers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DrakiaXYZ.QuestTracker.Components
{
    internal class QuestTrackerPanelUI
    {
        private sealed class Row
        {
            public GameObject Root;
            public RectTransform RootRt;
            public TextMeshProUGUI Text;
            public TextMeshProUGUI Prefix;
            public TextMeshProUGUI Progress;
            public HorizontalLayoutGroup Layout;
            public LayoutElement ProgressLayoutElement;
            public bool IsObjective;
            public State State;
        }
        
        public GameObject Panel { get; private set; }
        public RectTransform PanelRt { get; private set; }
        public CanvasGroup Group { get; private set; }
        public Image Background { get; private set; }
        public VerticalLayoutGroup LayoutGroup { get; private set; }
        public LayoutElement LayoutElement { get; private set; }

        private readonly List<Row> _questRows = new List<Row>();
        private readonly List<Row> _objectiveRows = new List<Row>();
        
        private int _mainFontSize = 18;
        private int _subFontSize = 14;
        
        private float _mainProgressWidth = 110f;
        private float _subProgressWidth = 90f;
        
        private bool _wrapText = true;

        private Color _mainColor = Color.white;
        private Color _subColor = Color.white;
        
        private readonly Color _completeColor = new Color(0.61f,0.83f,0.15f, 1f);
        private readonly Color _failedColor = new Color(0.71f,0f,0f, 1f);
        
        private static TMP_FontAsset TarkovFont => TMP_Settings.defaultFontAsset;
        
        public void Build(Transform parent)
        {
            Panel = new GameObject(
                "QuestTrackerPanel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter),
                typeof(LayoutElement)
            );

            Panel.transform.SetParent(parent, false);
            PanelRt = Panel.GetComponent<RectTransform>();

            Background = Panel.GetComponent<Image>();
            Background.raycastTarget = false;
            Background.color = new Color(0f, 0f, 0f, 0.35f);

            Group = Panel.GetComponent<CanvasGroup>();
            Group.alpha = 0f;
            Group.interactable = false;
            Group.blocksRaycasts = false;

            LayoutGroup = Panel.GetComponent<VerticalLayoutGroup>();
            LayoutGroup.childControlWidth = true;
            LayoutGroup.childControlHeight = true;
            LayoutGroup.childForceExpandWidth = false;
            LayoutGroup.childForceExpandHeight = false;
            LayoutGroup.spacing = 4f;
            LayoutGroup.padding = new RectOffset(10, 10, 10, 10);

            var fitter = Panel.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            LayoutElement = Panel.GetComponent<LayoutElement>();
            LayoutElement.preferredWidth = -1;
        }
        
        public void Destroy()
        {
            if (Panel != null)
            {
                Object.Destroy(Panel);
                Panel = null;
            }
        }
        
        private Row CreateRow(string name, bool isObjective)
        {
            var rowGo = new GameObject(
                name,
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(ContentSizeFitter)
            );
            rowGo.transform.SetParent(Panel.transform, false);

            var rowRt = rowGo.GetComponent<RectTransform>();

            var layout = rowGo.GetComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 6f;
            layout.padding = new RectOffset(0, 0, 0, 0);

            var cf = rowGo.GetComponent<ContentSizeFitter>();
            cf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            cf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            TextMeshProUGUI prefix = null;

            if (isObjective)
            {
                var prefixGo = new GameObject("Prefix", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
                prefixGo.transform.SetParent(rowGo.transform, false);

                prefix = prefixGo.GetComponent<TextMeshProUGUI>();
                prefix.raycastTarget = false;
                prefix.richText = false;
                prefix.enableWordWrapping = false;
                prefix.overflowMode = TextOverflowModes.Overflow;
                prefix.alignment = TextAlignmentOptions.Left;
                prefix.margin = new Vector4(2f, 0f, 0f, 0f);
                prefix.text = "-";

                var prefixLe = prefixGo.GetComponent<LayoutElement>();
                prefixLe.flexibleWidth = 0f;
                prefixLe.minWidth = 12f;
                prefixLe.preferredWidth = 12f;
            }
            
            var leftGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            leftGo.transform.SetParent(rowGo.transform, false);

            var left = leftGo.GetComponent<TextMeshProUGUI>();
            left.raycastTarget = false;
            left.richText = true;
            left.alignment = TextAlignmentOptions.Left;
            left.font = TarkovFont;
            
            ApplyWrap(left);

            var leftLe = leftGo.GetComponent<LayoutElement>();
            leftLe.flexibleWidth = 1f;
            leftLe.minWidth = 0f;
            
            var rightGo = new GameObject("Progress", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            rightGo.transform.SetParent(rowGo.transform, false);

            var right = rightGo.GetComponent<TextMeshProUGUI>();
            right.raycastTarget = false;
            right.richText = true;
            right.enableWordWrapping = false;
            right.overflowMode = TextOverflowModes.Overflow;
            right.alignment = TextAlignmentOptions.Right;
            right.font = TarkovFont;

            var rightLe = rightGo.GetComponent<LayoutElement>();
            rightLe.flexibleWidth = 0f;

            rowGo.SetActive(false);

            return new Row
            {
                Root = rowGo,
                RootRt = rowRt,
                Layout = layout,
                Prefix = prefix,
                Text = left,
                Progress = right,
                ProgressLayoutElement = rightLe,
                IsObjective = isObjective,
            };
        }
        
        public void CreateQuestRows(int count)
        {
            while (_questRows.Count < count)
            {
                var row = CreateRow("QuestRow", isObjective: false);
                
                ApplyRowStyle(row);
                ApplyProgressWidth(row);
                
                _questRows.Add(row);
            }
        }
        
        public void CreateObjectiveRows(int count)
        {
            while (_objectiveRows.Count < count)
            {
                var row = CreateRow("ObjectiveRow", isObjective: true);
                
                ApplyRowStyle(row);
                ApplyProgressWidth(row);
                
                _objectiveRows.Add(row);
            }
        }
        
        public void SetQuestRow(int siblingIndex, int questIndex, string text, string progress, State state)
        {
            var row = _questRows[questIndex];
            
            row.State = state;
            
            int padding = (questIndex == 0) ? 0 : 8;
            
            row.Layout.padding.top = padding;
            row.Layout.padding.bottom = 0;
            row.RootRt.SetSiblingIndex(siblingIndex);
            row.Root.SetActive(true);
            
            row.Text.text = text ?? string.Empty;
            
            switch (state)
            {
                case State.Complete:
                    row.Progress.text = (Utils.Localized("QuestStatusSuccess") ?? "COMPLETED").ToUpper();
                    break;

                case State.Failed:
                    row.Progress.text = (Utils.Localized("QuestStatusFail") ?? "FAILED").ToUpper();
                    break;

                default:
                    row.Progress.text = progress ?? string.Empty;
                    break;
            }
            
            ApplyRowStyle(row);
        }
        
        public void SetObjectiveRow(int siblingIndex, int objIndex, string text, string progress, State state)
        {
            var row = _objectiveRows[objIndex];
            row.State = state;
            row.RootRt.SetSiblingIndex(siblingIndex);
            row.Root.SetActive(true);
            
            row.Text.text = text ?? string.Empty;
            row.Progress.text = progress ?? string.Empty;
            
            ApplyRowStyle(row);
        }
        
        public void SetMainFontSize(int size)
        {
            _mainFontSize = size;
            
            foreach (var r in _questRows)
            {
                r.Text.fontSize = size;
                r.Progress.fontSize = size;
            }
        }
        
        public void SetSubFontSize(int size)
        {
            _subFontSize = size;
            
            foreach (var r in _objectiveRows)
            {
                r.Text.fontSize = size;
                r.Progress.fontSize = size;
            }
        }
        
        public void SetMainColor(Color c)
        {
            _mainColor = c;
            
            foreach (var r in _questRows)
            {
                ApplyRowStyle(r);
            }
        }
        
        public void SetSubColor(Color c)
        {
            _subColor = c;
            
            foreach (var r in _objectiveRows)
            {
                ApplyRowStyle(r);
            }
        }

        public void SetMainProgressWidth(int width)
        {
            _mainProgressWidth = width;
            
            foreach (var row in _questRows)
            {
                ApplyProgressWidth(row);
            }
        }

        public void SetSubProgressWidth(int width)
        {
            _subProgressWidth = width;
            
            foreach (var row in _objectiveRows)
            {
                ApplyProgressWidth(row);
            }
        }
        
        public void SetWrapText(bool wrap)
        {
            _wrapText = wrap;

            foreach (var r in _questRows)
            {
                ApplyWrap(r.Text);
            }

            foreach (var r in _objectiveRows)
            {
                ApplyWrap(r.Text);
            }
        }
        
        public void SetAlignment(Settings.EAlignment alignment, Settings.EVerticalAlignment vAlignment)
        {
            if (PanelRt == null)
            {
                return;
            }

            float ax = (alignment == Settings.EAlignment.Left) ? 0f : 1f;
            float ay =
                (vAlignment == Settings.EVerticalAlignment.Top) ? 1f :
                (vAlignment == Settings.EVerticalAlignment.Bottom) ? 0f :
                0.5f;

            PanelRt.anchorMin = new Vector2(ax, ay);
            PanelRt.anchorMax = new Vector2(ax, ay);
            PanelRt.pivot = new Vector2(ax, ay);

            float x = (ax == 0f) ? 10f : -10f;
            float y =
                (vAlignment == Settings.EVerticalAlignment.Top) ? -10f :
                (vAlignment == Settings.EVerticalAlignment.Bottom) ? 10f :
                0f;

            PanelRt.anchoredPosition = new Vector2(x, y);
        }
        
        public void SetOffsets(Settings.EAlignment h, Settings.EVerticalAlignment v, int horizontalOffset, int verticalOffset)
        {
            int hOff = Mathf.Clamp(horizontalOffset, 0, 500);
            int vOff = Mathf.Clamp(verticalOffset, 0, 500);

            float x = (h == Settings.EAlignment.Left) ? hOff : -hOff;
            float y = (v == Settings.EVerticalAlignment.Top) ? -vOff :
                (v == Settings.EVerticalAlignment.Bottom) ? vOff : 0f;

            PanelRt.anchoredPosition = new Vector2(x, y);
        }
        
        public void SetTransparency(float alpha)
        {
            if (Background == null)
            {
                return;
            }
            
            var c = Background.color;
            c.a = alpha;
            Background.color = c;
        }
        
        public void SetMaxWidth(int maxWidth)
        {
            if (LayoutGroup == null || LayoutElement == null)
            {
                return;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(LayoutGroup.GetComponent<RectTransform>());

            if (LayoutGroup.preferredWidth > maxWidth)
            {
                LayoutElement.preferredWidth = maxWidth;
            }
            else
            {
                LayoutElement.preferredWidth = -1;
            }
        }

        private void ApplyProgressWidth(Row row)
        {
            float w = row.IsObjective ? _subProgressWidth : _mainProgressWidth;
            
            row.ProgressLayoutElement.preferredWidth = w;
            row.ProgressLayoutElement.minWidth = w;
        }
        
        private void ApplyWrap(TextMeshProUGUI t)
        {
            if (_wrapText)
            {
                t.enableWordWrapping = true;
                t.overflowMode = TextOverflowModes.Overflow;
                t.maxVisibleLines = 4;
            }
            else
            {
                t.enableWordWrapping = false;
                t.overflowMode = TextOverflowModes.Ellipsis;
                t.maxVisibleLines = 1;
            }
        }
        
        private void ApplyRowStyle(Row row)
        {
            if (row == null)
            {
                return;
            }
            
            if (row.IsObjective)
            {
                row.Prefix.fontSize = _subFontSize;
                row.Text.fontSize = _subFontSize;
                row.Progress.fontSize = _subFontSize;
                
                Color c = _subColor;

                if (row.State == State.Complete)
                {
                    c = _completeColor;
                }
                else if (row.State == State.Failed)
                {
                    c = _failedColor;
                }

                row.Prefix.color = c;
                row.Text.color = c;
                row.Progress.color = c;
            }
            else
            {
                row.Text.fontSize = _mainFontSize;
                row.Progress.fontSize = _mainFontSize;
                
                row.Text.color = _mainColor;
                
                switch (row.State)
                {
                    case State.Complete:
                        row.Progress.color = _completeColor;
                        break;
                    case State.Failed:
                        row.Progress.color = _failedColor;
                        break;
                    default:
                        row.Progress.color = _mainColor;
                        break;
                }
            }
        }
        
        public void HideQuestRowsFrom(int startIndex)
        {
            if (_questRows == null || _questRows.Count == 0)
            {
                return;
            }
            
            for (int i = startIndex; i < _questRows.Count; i++)
            {
                _questRows[i].Root.SetActive(false);
            }
        }
        
        public void HideObjectiveRowsFrom(int startIndex)
        {
            if (_objectiveRows == null || _objectiveRows.Count == 0)
            {
                return;
            }

            for (int i = startIndex; i < _objectiveRows.Count; i++)
            {
                _objectiveRows[i].Root.SetActive(false);
            }
        }
    }
    
    public enum State
    { 
        InProgress,
        Complete,
        Failed
    }
}