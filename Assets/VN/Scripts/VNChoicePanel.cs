using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VN
{
    public class VNChoiceItem
    {
        public string text;
        public bool enabled = true;
        public int index;
    }

    /// <summary>
    /// 选项按钮列表。运行时生成，显示在对话框上方，不影响系统面板。
    /// </summary>
    public class VNChoicePanel : MonoBehaviour
    {
        private Font _font;
        private Canvas _canvas;
        private GameObject _root;
        private readonly List<GameObject> _buttons = new List<GameObject>();
        private Action<int> _onPick;

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Init(Font font)
        {
            _font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
            _root.SetActive(false);
        }

        private void Build()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);

            _canvas = canvasGO.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 150;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _root = new GameObject("Root", typeof(RectTransform));
            _root.transform.SetParent(canvasGO.transform, false);
            Stretch((RectTransform)_root.transform);
        }

        public void Show(List<VNChoiceItem> items, Action<int> onPick)
        {
            if (_root == null) Init(_font);

            _onPick = onPick;
            ClearButtons();

            if (items == null || items.Count == 0) return;

            const float width = 980f;
            const float height = 84f;
            const float gap = 18f;
            float totalHeight = items.Count * height + (items.Count - 1) * gap;
            float startY = totalHeight * 0.5f + 110f; // 稍微上移，避开对话框

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var btn = CreateButton(item, width, height);
                var rt = (RectTransform)btn.transform;
                rt.anchoredPosition = new Vector2(0f, startY - i * (height + gap) - height * 0.5f);
                _buttons.Add(btn);
            }

            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
            ClearButtons();
        }

        private void ClearButtons()
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i] != null) Destroy(_buttons[i]);
            }
            _buttons.Clear();
        }

        private GameObject CreateButton(VNChoiceItem item, float width, float height)
        {
            var go = new GameObject("Choice", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_root.transform, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);

            var img = go.GetComponent<Image>();
            img.color = item.enabled
                ? new Color(0.11f, 0.13f, 0.18f, 1f)
                : new Color(0.17f, 0.17f, 0.21f, 1f);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.interactable = item.enabled;

            if (item.enabled)
            {
                int captured = item.index;
                btn.onClick.AddListener(() =>
                {
                    Hide();
                    _onPick?.Invoke(captured);
                });
            }

            var text = new GameObject("Label", typeof(RectTransform), typeof(Text));
            text.transform.SetParent(go.transform, false);
            var t = text.GetComponent<Text>();
            t.font = _font;
            t.fontSize = 32;
            t.alignment = TextAnchor.MiddleCenter;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.text = item.text;
            t.color = item.enabled ? Color.white : new Color(0.72f, 0.75f, 0.82f);
            if (!item.enabled) t.fontStyle = FontStyle.Italic;

            // 锁定项加一条左侧竖线，方便一眼区分
            if (!item.enabled)
            {
                var mark = new GameObject("LockMark", typeof(RectTransform), typeof(Image));
                mark.transform.SetParent(go.transform, false);
                var mrt = (RectTransform)mark.transform;
                mrt.anchorMin = new Vector2(0f, 0.2f);
                mrt.anchorMax = new Vector2(0f, 0.8f);
                mrt.pivot = new Vector2(0f, 0.5f);
                mrt.sizeDelta = new Vector2(5f, 0f);
                mrt.anchoredPosition = new Vector2(14f, 0f);
                mark.GetComponent<Image>().color = new Color(0.55f, 0.58f, 0.66f, 0.9f);
                
            }

            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(24f, 8f);
            trt.offsetMax = new Vector2(-24f, -8f);

            return go;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
