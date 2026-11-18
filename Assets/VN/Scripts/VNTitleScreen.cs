using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace VN
{
    /// <summary>
    /// 主菜单：新游戏 / 继续游戏 / 设置 / 退出。
    /// 运行时自建 UI，不依赖场景里摆好的预制体。
    /// </summary>
    public class VNTitleScreen : MonoBehaviour
    {
        public VNPlayer player;
        public Font font;

        [Header("外观")]
        public Sprite backgroundSprite;
        public string title = "nago2026 recall";
        public string subtitle = "recall the rain";
        public Color titleColor = new Color(0.98f, 0.96f, 0.92f, 1f);
        public Color accentColor = new Color(0.86f, 0.62f, 0.32f, 1f);

        [Header("按钮")]
        public string newGameLabel = "新游戏";
        public string continueLabel = "继续游戏";
        public string settingsLabel = "设置";
        public string quitLabel = "退出游戏";

        [Header("背景音乐")]
        [Tooltip("标题画面的循环音乐")]
        public string titleMusic = "";

        [Range(0f, 1f)]
        public float titleMusicVolume = 0.6f;

        [Tooltip("淡入 / 淡出时长（秒）")]
        public float titleMusicFade = 1.2f;

        [Header("鼠标视差")]
        [Tooltip("背景随鼠标移动的幅度（0 = 关闭）。移动时只挪背景，不会露出黑边，也不影响按钮位置。")]
        [Range(0f, 2f)]
        public float parallaxStrength = 1f;

        [Tooltip("背景额外放大的倍数，越大可移动的范围越大")]
        public float parallaxZoom = 1.12f;

        [Tooltip("跟随鼠标的平滑时间（秒）")]
        public float parallaxSmooth = 0.12f;

        private Canvas _canvas;
        private GameObject _root;
        private Button _continueButton;
        private Text _continueText;

        private RectTransform _canvasRect;
        private RectTransform _bgRect;
        private Sprite _bgSprite;
        private float _panMaxX;
        private float _panMaxY;
        private Vector2 _panVelocity;

        public bool IsVisible => _root != null && _root.activeSelf;

        private void Update()
        {
            if (_root == null || !_root.activeSelf) return;
            UpdateParallax();
        }

        /// <summary>按屏幕尺寸等比铺满 + 放大一圈，多出来的部分就是可平移余量。</summary>
        private void UpdateBackgroundLayout()
        {
            if (_bgRect == null || _bgSprite == null || _canvasRect == null) return;

            float cw = _canvasRect.rect.width;
            float ch = _canvasRect.rect.height;
            if (cw < 1f || ch < 1f) return;

            float imgAspect = _bgSprite.rect.width / Mathf.Max(1f, _bgSprite.rect.height);
            float screenAspect = cw / ch;

            float w, h;
            if (imgAspect > screenAspect)
            {
                // 图比屏幕更宽：以高度铺满，左右裁掉
                h = ch;
                w = ch * imgAspect;
            }
            else
            {
                // 图比屏幕更窄：以宽度铺满，上下裁掉
                w = cw;
                h = cw / imgAspect;
            }

            float zoom = Mathf.Max(1.01f, parallaxZoom);
            var size = new Vector2(w * zoom, h * zoom);

            if ((_bgRect.sizeDelta - size).sqrMagnitude > 0.5f)
                _bgRect.sizeDelta = size;

            // 放大后每边多出 (zoom-1)/2，平移留 10% 安全余量
            float usable = (zoom - 1f) * 0.5f * 0.8f;
            _panMaxX = w * zoom * usable;
            _panMaxY = h * zoom * usable;
        }

        private void UpdateParallax()
        {
            UpdateBackgroundLayout();

            if (_bgRect == null || parallaxStrength <= 0.001f) return;

            float nx = Mathf.Clamp01(Input.mousePosition.x / Mathf.Max(1f, Screen.width)) - 0.5f;
            float ny = Mathf.Clamp01(Input.mousePosition.y / Mathf.Max(1f, Screen.height)) - 0.5f;

            // 鼠标往右 → 画面往左，模拟视线右移
            var target = new Vector2(-nx * 2f * _panMaxX, -ny * 2f * _panMaxY) * parallaxStrength;

            _bgRect.anchoredPosition = Vector2.SmoothDamp(
                _bgRect.anchoredPosition, target, ref _panVelocity,
                Mathf.Max(0.01f, parallaxSmooth), Mathf.Infinity, Time.unscaledDeltaTime);
        }

        public void Show()
        {
            EnsureBuilt();
            _root.SetActive(true);
            RefreshContinueButton();
            PlayMusic();
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
            StopMusic();
        }

        // ---------------- 标题音乐 ----------------

        private AudioSource _music;
        private Coroutine _musicRoutine;

        private void EnsureMusic()
        {
            if (_music != null) return;

            var go = new GameObject("Title Music", typeof(AudioSource));
            go.transform.SetParent(transform, false);

            _music = go.GetComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.volume = 0f;
        }

        public void PlayMusic()
        {
            if (string.IsNullOrWhiteSpace(titleMusic)) return;

            var clip = VNAssetLoader.LoadClip(titleMusic);
            if (clip == null)
            {
                Debug.LogWarning("[VN] 找不到标题音乐：" + titleMusic);
                return;
            }

            EnsureMusic();

            if (_music.clip == clip && _music.isPlaying)
            {
                // 已经在放同一首，只把音量淡回来
                if (_musicRoutine != null) StopCoroutine(_musicRoutine);
                _musicRoutine = StartCoroutine(FadeMusic(titleMusicVolume, titleMusicFade, false));
                return;
            }

            _music.clip = clip;
            _music.loop = true;
            _music.time = 0f;
            _music.volume = 0f;
            _music.Play();

            if (_musicRoutine != null) StopCoroutine(_musicRoutine);
            _musicRoutine = StartCoroutine(FadeMusic(titleMusicVolume, titleMusicFade, false));
        }

        public void StopMusic()
        {
            if (_music == null || !_music.isPlaying) return;

            if (_musicRoutine != null) StopCoroutine(_musicRoutine);
            _musicRoutine = StartCoroutine(FadeMusic(0f, titleMusicFade, true));
        }

        private IEnumerator FadeMusic(float target, float duration, bool stopAfter)
        {
            float start = _music.volume;
            float t = 0f;
            duration = Mathf.Max(0.01f, duration);

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _music.volume = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
                yield return null;
            }

            _music.volume = target;

            if (stopAfter && target <= 0.001f)
            {
                _music.Stop();
                _music.clip = null;
            }

            _musicRoutine = null;
        }

        public void Toggle()
        {
            if (IsVisible) Hide();
            else Show();
        }

        private void RefreshContinueButton()
        {
            if (_continueButton == null) return;

            bool hasSave = VNSaveSystem.HasAny();
            _continueButton.interactable = hasSave;
            if (_continueText != null)
                _continueText.color = hasSave
                    ? new Color(1f, 1f, 1f, 1f)
                    : new Color(0.55f, 0.58f, 0.65f, 1f);
        }

        private void EnsureBuilt()
        {
            if (_canvas != null) return;

            var go = new GameObject("VN Title Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);

            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            // 全屏背景
            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(go.transform, false);

            var bgImage = bg.GetComponent<Image>();
            bgImage.sprite = backgroundSprite;
            bgImage.color = backgroundSprite != null ? Color.white : new Color(0.06f, 0.07f, 0.09f, 1f);
            bgImage.raycastTarget = true;
            bgImage.preserveAspect = false;

            // 居中锚点 + 手动算尺寸：等比铺满后再放大一圈，
            // 留出来的余量正好用来做鼠标视差，保证移动时不露黑边。
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = Vector2.zero;

            _canvasRect = go.GetComponent<RectTransform>();
            _bgRect = bgRect;
            _bgSprite = backgroundSprite;

            if (backgroundSprite != null)
            {
                // 叠一层暗色，保证文字可读（不跟随背景移动）
                var shade = new GameObject("Shade", typeof(RectTransform), typeof(Image));
                shade.transform.SetParent(go.transform, false);
                var shadeImg = shade.GetComponent<Image>();
                shadeImg.color = new Color(0.02f, 0.03f, 0.05f, 0.55f);
                shadeImg.raycastTarget = false;

                var shadeRect = shade.GetComponent<RectTransform>();
                shadeRect.anchorMin = Vector2.zero;
                shadeRect.anchorMax = Vector2.one;
                shadeRect.offsetMin = Vector2.zero;
                shadeRect.offsetMax = Vector2.zero;

                UpdateBackgroundLayout();
            }

            _root = go;

            // 按钮区面板：挡住背后的明亮背景，保证按钮和文字可读
            var panel = new GameObject("Menu Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(go.transform, false);
            var panelImg = panel.GetComponent<Image>();
            panelImg.color = new Color(0.03f, 0.04f, 0.06f, 0.62f);
            panelImg.raycastTarget = false;

            var pr = panel.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(0.5f, 0.5f);
            pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.anchoredPosition = new Vector2(0, -118);
            pr.sizeDelta = new Vector2(560, 420);

            // 标题
            var titleText = MakeText(go.transform, title, 96, titleColor, TextAnchor.MiddleCenter, FontStyle.Normal, true);
            var tr = titleText.rectTransform;
            tr.anchorMin = new Vector2(0.5f, 0.5f);
            tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.pivot = new Vector2(0.5f, 0.5f);
            tr.anchoredPosition = new Vector2(0, 244);
            tr.sizeDelta = new Vector2(1400, 120);

            // 副标题（留空就不显示）
            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                var subText = MakeText(go.transform, subtitle, 42, new Color(1f, 0.98f, 0.94f, 1f), TextAnchor.MiddleCenter, FontStyle.Normal, true);
                var sr2 = subText.rectTransform;
                sr2.anchorMin = new Vector2(0.5f, 0.5f);
                sr2.anchorMax = new Vector2(0.5f, 0.5f);
                sr2.pivot = new Vector2(0.5f, 0.5f);
                sr2.anchoredPosition = new Vector2(0, 166);
                sr2.sizeDelta = new Vector2(1200, 56);
            }

            // 分隔线
            var line = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            line.transform.SetParent(go.transform, false);
            var lineImg = line.GetComponent<Image>();
            lineImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.55f);
            lineImg.raycastTarget = false;
            var lr = line.GetComponent<RectTransform>();
            lr.anchorMin = new Vector2(0.5f, 0.5f);
            lr.anchorMax = new Vector2(0.5f, 0.5f);
            lr.pivot = new Vector2(0.5f, 0.5f);
            lr.anchoredPosition = new Vector2(0, 160);
            lr.sizeDelta = new Vector2(520, 3);

            // 按钮
            float startY = 20f;
            float gap = 92f;
            float w = 420f, h = 74f;

            MakeButton(go.transform, newGameLabel, new Vector2(0, startY), w, h, () =>
            {
                Hide();
                player?.StartNewGame();
            });

            _continueButton = MakeButton(go.transform, continueLabel, new Vector2(0, startY - gap), w, h, () =>
            {
                Hide();
                player?.ContinueGame();
            });
            _continueText = _continueButton.GetComponentInChildren<Text>();

            MakeButton(go.transform, settingsLabel, new Vector2(0, startY - gap * 2), w, h, () =>
            {
                player?.OpenSettingsFromTitle();
            });

            MakeButton(go.transform, quitLabel, new Vector2(0, startY - gap * 3), w, h, () =>
            {
                player?.QuitGame();
            });

            // 版本 / 提示
            var hint = MakeText(go.transform, "点击「继续游戏」读取最近一次存档", 22,
                new Color(0.55f, 0.59f, 0.67f, 1f), TextAnchor.MiddleCenter, FontStyle.Normal);
            var hr = hint.rectTransform;
            hr.anchorMin = new Vector2(0.5f, 0f);
            hr.anchorMax = new Vector2(0.5f, 0f);
            hr.pivot = new Vector2(0.5f, 0f);
            hr.anchoredPosition = new Vector2(0, 40);
            hr.sizeDelta = new Vector2(900, 32);

            RefreshContinueButton();
        }

        private Text MakeText(Transform parent, string content, int size, Color color,
            TextAnchor anchor, FontStyle style, bool withOutline = false)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var t = go.GetComponent<Text>();
            t.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.text = content;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;

            if (withOutline)
            {
                var outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                outline.effectDistance = new Vector2(3f, -3f);
                outline.useGraphicAlpha = false;
            }

            return t;
        }

        private Button MakeButton(Transform parent, string label, Vector2 pos, float w, float h,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button " + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(w, h);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.13f, 0.15f, 0.20f, 0.97f);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor = new Color(0.13f, 0.15f, 0.20f, 0.97f);
            colors.highlightedColor = new Color(0.22f, 0.25f, 0.33f, 1f);
            colors.pressedColor = new Color(0.30f, 0.24f, 0.16f, 1f);
            colors.disabledColor = new Color(0.10f, 0.11f, 0.14f, 0.85f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.12f;
            btn.colors = colors;

            btn.onClick.AddListener(onClick);

            var text = MakeText(go.transform, label, 34, new Color(0.96f, 0.95f, 0.92f, 1f),
                TextAnchor.MiddleCenter, FontStyle.Normal, true);
            var tr = text.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;

            // 左侧强调条
            var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(go.transform, false);
            var barImg = bar.GetComponent<Image>();
            barImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.9f);
            barImg.raycastTarget = false;

            var br = bar.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0f, 0.5f);
            br.anchorMax = new Vector2(0f, 0.5f);
            br.pivot = new Vector2(0f, 0.5f);
            br.anchoredPosition = new Vector2(0, 0);
            br.sizeDelta = new Vector2(6, h * 0.7f);

            return btn;
        }
    }
}
