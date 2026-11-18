using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace VN
{
    /// <summary>
    /// 章节过场卡：淡入黑场 → 显示章节名 → 淡出。
    /// 淡出开始时通知调用方推进剧情，这样新章节的第一行正好从黑场里露出来。
    /// </summary>
    public class VNChapterCard : MonoBehaviour
    {
        public Font font;

        [Header("节奏（秒）")]
        public float fadeToBlack = 0.8f;
        public float fadeIn = 0.5f;
        public float hold = 1.6f;
        public float fadeOut = 0.9f;

        [Header("外观")]
        public Color backgroundColor = Color.black;
        public Color labelColor = new Color(0.78f, 0.72f, 0.62f, 1f);
        public Color titleColor = new Color(0.98f, 0.96f, 0.92f, 1f);
        public string skipHint = "点击继续";

        private Canvas _canvas;
        private CanvasGroup _group;
        private GameObject _root;
        private Text _labelText;
        private Text _titleText;
        private Text _hintText;
        private Coroutine _routine;
        private bool _skip;

        public bool IsPlaying => _routine != null;

        public void Play(string chapterLabel, string chapterTitle, Action onCovered = null)
        {
            EnsureBuilt();

            _root.SetActive(true);
            _labelText.text = chapterLabel ?? "";
            _titleText.text = chapterTitle ?? "";
            _skip = false;

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run(onCovered));
        }

        public void Skip()
        {
            _skip = true;
        }

        private IEnumerator Run(Action onCovered)
        {
            _group.alpha = 0f;
            SetTextAlpha(0f);
            _hintText.color = new Color(0.6f, 0.62f, 0.68f, 0f);

            // 1) 淡到全黑
            yield return Fade(null, 0f, 1f, fadeToBlack, false);

            // 2) 文字淡入
            yield return Fade(SetTextAlpha, 0f, 1f, fadeIn, true);
            _hintText.color = new Color(0.6f, 0.62f, 0.68f, 0.75f);

            // 3) 停留
            float t = 0f;
            while (t < hold)
            {
                if (_skip) break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            // 4) 通知剧情推进（此时屏幕还是黑的）
            onCovered?.Invoke();

            // 5) 淡出
            SetTextAlpha(1f);
            yield return Fade(SetTextAlpha, 1f, 0f, fadeOut, false, true);

            _group.alpha = 0f;
            _root.SetActive(false);
            _routine = null;
        }

        /// <summary>alpha 从 from 到 to；textMode = 控制文字透明度而不是整块黑场。</summary>
        private IEnumerator Fade(Action<float> setter, float from, float to, float duration, bool skippable,
            bool alsoFadeOverlay = false)
        {
            duration = Mathf.Max(0.01f, duration);
            float t = 0f;

            while (t < duration)
            {
                if (skippable && _skip) break;

                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                float v = Mathf.Lerp(from, to, k);

                if (setter != null) setter(v);
                else _group.alpha = v;

                if (alsoFadeOverlay) _group.alpha = v;

                yield return null;
            }

            float end = to;
            if (setter != null) setter(end);
            else _group.alpha = end;
            if (alsoFadeOverlay) _group.alpha = end;
        }

        private void SetTextAlpha(float a)
        {
            var lc = _labelText.color;
            lc.a = a * 0.95f;
            _labelText.color = lc;

            var tc = _titleText.color;
            tc.a = a;
            _titleText.color = tc;
        }

        private void EnsureBuilt()
        {
            if (_canvas != null) return;

            var go = new GameObject("VN Chapter Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);

            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 250;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            _root = go;

            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            _group = group;

            // 黑底
            var bg = new GameObject("Black", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(go.transform, false);
            var bgImg = bg.GetComponent<Image>();
            bgImg.color = backgroundColor;
            bgImg.raycastTarget = false;

            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // 章节标签（第一章）
            _labelText = MakeText(go.transform, "", 32, labelColor, TextAnchor.MiddleCenter);
            var lr = _labelText.rectTransform;
            lr.anchorMin = new Vector2(0.5f, 0.5f);
            lr.anchorMax = new Vector2(0.5f, 0.5f);
            lr.pivot = new Vector2(0.5f, 0.5f);
            lr.anchoredPosition = new Vector2(0, 56);
            lr.sizeDelta = new Vector2(1200, 60);

            // 标题（回忆）
            _titleText = MakeText(go.transform, "", 72, titleColor, TextAnchor.MiddleCenter);
            var tr = _titleText.rectTransform;
            tr.anchorMin = new Vector2(0.5f, 0.5f);
            tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.pivot = new Vector2(0.5f, 0.5f);
            tr.anchoredPosition = new Vector2(0, -20);
            tr.sizeDelta = new Vector2(1400, 120);

            // 提示
            _hintText = MakeText(go.transform, skipHint, 22, new Color(0.6f, 0.62f, 0.68f, 0f), TextAnchor.MiddleCenter);
            var hr = _hintText.rectTransform;
            hr.anchorMin = new Vector2(0.5f, 0f);
            hr.anchorMax = new Vector2(0.5f, 0f);
            hr.pivot = new Vector2(0.5f, 0f);
            hr.anchoredPosition = new Vector2(0, 60);
            hr.sizeDelta = new Vector2(600, 32);

            _root.SetActive(false);
        }

        private Text MakeText(Transform parent, string content, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var t = go.GetComponent<Text>();
            t.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.color = color;
            t.text = content;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;

            return t;
        }
    }
}
