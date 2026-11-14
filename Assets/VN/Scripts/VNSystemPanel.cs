using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VN
{
    /// <summary>
    /// 系统页面：右上角常驻「菜单」按钮 -> 打开面板。
    /// 第 1 页 存档 / 读档（6 槽位），第 2 页 音乐 / 音效音量。
    /// </summary>
    public class VNSystemPanel : MonoBehaviour
    {
        private const int SlotCount = VNSaveSystem.SlotCount;

        private VNPlayer _player;
        private Font _font;
        private Canvas _canvas;

        private GameObject _hud;
        private GameObject _root;
        private GameObject _pageSave;
        private GameObject _pageAudio;

        private readonly Text[] _slotInfo = new Text[SlotCount];
        private readonly Button[] _tabButtons = new Button[2];

        private Slider _bgmSlider;
        private Slider _sfxSlider;
        private Text _bgmValue;
        private Text _sfxValue;

        private static readonly Color TabActiveColor = new Color(0.86f, 0.62f, 0.32f, 1f);
        private static readonly Color TabIdleColor = new Color(0.18f, 0.2f, 0.26f, 1f);

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Init(VNPlayer player, Font font)
        {
            _player = player;
            _font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
            _root.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (_root == null) Init(_player, _font);
            RefreshSavePage();
            SyncAudioPage();
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            if (_hud != null) _hud.SetActive(false);
            SetPage(0);
        }

        public void Close()
        {
            if (_root != null) _root.SetActive(false);
            if (_hud != null) _hud.SetActive(true);
        }

        // ---------------- 构建 ----------------

        private void Build()
        {
            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);

            _canvas = canvasGO.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            BuildHud(canvasGO.transform);
            BuildPanel(canvasGO.transform);
        }

        private void BuildHud(Transform parent)
        {
            _hud = new GameObject("HUD", typeof(RectTransform));
            _hud.transform.SetParent(parent, false);
            Stretch((RectTransform)_hud.transform);

            var btn = CreateButton(_hud.transform, "菜单", new Color(0.12f, 0.14f, 0.2f, 0.85f), Open);
            var rt = (RectTransform)btn.transform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(136f, 64f);
            rt.anchoredPosition = new Vector2(-28f, -24f);
        }

        private void BuildPanel(Transform parent)
        {
            _root = new GameObject("Root", typeof(RectTransform));
            _root.transform.SetParent(parent, false);
            Stretch((RectTransform)_root.transform);

            var dim = CreateImage("Dim", _root.transform, new Color(0f, 0f, 0f, 0.72f));
            Stretch(dim.rectTransform);

            var panel = CreateImage("Panel", _root.transform, new Color(0.08f, 0.09f, 0.13f, 0.99f));
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(1260f, 800f);

            var title = CreateText("Title", prt, "系统", 44, TextAnchor.MiddleLeft);
            LayoutTop(title.rectTransform, 48f, 0f, -180f, 30f, 84f);
            title.color = new Color(1f, 0.9f, 0.7f);

            var closeBtn = CreateButton(prt, "✕", new Color(0.55f, 0.2f, 0.25f), Close);
            var crt = (RectTransform)closeBtn.transform;
            crt.anchorMin = new Vector2(1f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(1f, 1f);
            crt.sizeDelta = new Vector2(64f, 56f);
            crt.anchoredPosition = new Vector2(-28f, -26f);

            // 两个页签
            _tabButtons[0] = CreateButton(prt, "存档 / 读档", TabIdleColor, () => SetPage(0));
            PlaceTab(_tabButtons[0], 48f);
            _tabButtons[1] = CreateButton(prt, "设置", TabIdleColor, () => SetPage(1));
            PlaceTab(_tabButtons[1], 246f);

            _pageSave = new GameObject("PageSave", typeof(RectTransform));
            _pageSave.transform.SetParent(prt, false);
            Stretch((RectTransform)_pageSave.transform);

            _pageAudio = new GameObject("PageAudio", typeof(RectTransform));
            _pageAudio.transform.SetParent(prt, false);
            Stretch((RectTransform)_pageAudio.transform);

            BuildSavePage((RectTransform)_pageSave.transform);
            BuildAudioPage((RectTransform)_pageAudio.transform);
        }

        private void BuildSavePage(RectTransform page)
        {
            var hint = CreateText("Hint", page, "F1 关闭　·　F5 快速存档　·　F9 快速读档", 22, TextAnchor.MiddleRight);
            LayoutTop(hint.rectTransform, 300f, 0f, -260f, 30f, 84f);
            hint.color = new Color(0.65f, 0.7f, 0.8f);

            for (int i = 0; i < SlotCount; i++)
                BuildSlotRow(page, i);
        }

        private void BuildSlotRow(RectTransform page, int index)
        {
            float top = -170f - index * 100f;

            var row = CreateImage($"Slot{index}", page, new Color(1f, 1f, 1f, 0.05f));
            var rrt = row.rectTransform;
            rrt.anchorMin = new Vector2(0f, 1f);
            rrt.anchorMax = new Vector2(1f, 1f);
            rrt.pivot = new Vector2(0.5f, 1f);
            rrt.offsetMin = new Vector2(40f, top - 88f);
            rrt.offsetMax = new Vector2(-40f, top);

            var info = CreateText($"Info{index}", rrt, "", 26, TextAnchor.MiddleLeft);
            var irt = info.rectTransform;
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(20f, 8f);
            irt.offsetMax = new Vector2(-420f, -8f);
            info.horizontalOverflow = HorizontalWrapMode.Wrap;
            info.verticalOverflow = VerticalWrapMode.Truncate;
            _slotInfo[index] = info;

            var save = CreateButton(rrt, "保存", new Color(0.2f, 0.45f, 0.32f), () =>
            {
                _player.QuickSave(index);
                RefreshSavePage();
            });
            PlaceRight(save, -356f, 110f);

            var load = CreateButton(rrt, "读取", new Color(0.22f, 0.35f, 0.55f), () =>
            {
                if (_player.LoadSlot(index)) Close();
            });
            PlaceRight(load, -226f, 110f);

            var del = CreateButton(rrt, "删除", new Color(0.45f, 0.22f, 0.25f), () =>
            {
                _player.DeleteSlot(index);
                RefreshSavePage();
            });
            PlaceRight(del, -96f, 110f);
        }

        private void BuildAudioPage(RectTransform page)
        {
            var audio = _player != null ? _player.audioManager : null;

            var label1 = CreateText("BgmLabel", page, "音乐音量", 34, TextAnchor.MiddleLeft);
            LayoutRow(label1.rectTransform, -190f);
            _bgmValue = CreateText("BgmValue", page, "100%", 30, TextAnchor.MiddleRight);
            LayoutRow(_bgmValue.rectTransform, -190f);

            float bgmStart = audio != null ? audio.globalBgmVolume : 1f;
            _bgmSlider = CreateSlider(page, 120f, -280f, 1020f, 44f, bgmStart, v =>
            {
                if (_bgmValue != null) _bgmValue.text = Mathf.RoundToInt(v * 100f) + "%";
                if (audio != null) audio.SetGlobalBgmVolume(v);
            });

            var label2 = CreateText("SfxLabel", page, "音效音量", 34, TextAnchor.MiddleLeft);
            LayoutRow(label2.rectTransform, -400f);
            _sfxValue = CreateText("SfxValue", page, "100%", 30, TextAnchor.MiddleRight);
            LayoutRow(_sfxValue.rectTransform, -400f);

            float sfxStart = audio != null ? audio.globalSfxVolume : 1f;
            _sfxSlider = CreateSlider(page, 120f, -490f, 1020f, 44f, sfxStart, v =>
            {
                if (_sfxValue != null) _sfxValue.text = Mathf.RoundToInt(v * 100f) + "%";
                if (audio != null) audio.SetGlobalSfxVolume(v);
            });

            var tip = CreateText("Tip", page, "音量设置会立即生效，并在下次启动时保留。", 22, TextAnchor.MiddleLeft);
            var trt = tip.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(120f, -650f);
            trt.offsetMax = new Vector2(-120f, -610f);
            tip.color = new Color(0.6f, 0.66f, 0.76f);
        }

        // ---------------- 刷新 ----------------

        private void SetPage(int index)
        {
            if (_pageSave != null) _pageSave.SetActive(index == 0);
            if (_pageAudio != null) _pageAudio.SetActive(index == 1);

            for (int i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] == null) continue;
                _tabButtons[i].GetComponent<Image>().color = i == index ? TabActiveColor : TabIdleColor;
            }
        }

        private void SyncAudioPage()
        {
            var audio = _player != null ? _player.audioManager : null;
            if (audio == null) return;

            if (_bgmSlider != null) _bgmSlider.SetValueWithoutNotify(audio.globalBgmVolume);
            if (_sfxSlider != null) _sfxSlider.SetValueWithoutNotify(audio.globalSfxVolume);
            if (_bgmValue != null) _bgmValue.text = Mathf.RoundToInt(audio.globalBgmVolume * 100f) + "%";
            if (_sfxValue != null) _sfxValue.text = Mathf.RoundToInt(audio.globalSfxVolume * 100f) + "%";
        }

        private void RefreshSavePage()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (_slotInfo[i] == null) continue;

                var data = VNSaveSystem.Read(i);
                if (data == null || data.IsEmpty)
                {
                    _slotInfo[i].text = $"<color=#7A8296>槽位 {i + 1}　—　空</color>";
                }
                else
                {
                    var when = string.IsNullOrEmpty(data.savedAt) ? "" : data.savedAt;
                    _slotInfo[i].text = $"<color=#FFD98A>槽位 {i + 1}</color>　<size=22><color=#9AA3B8>{when}</color></size>\n{data.preview}";
                }
            }
        }

        // ---------------- 布局工具 ----------------

        private static void LayoutTop(RectTransform rt, float left, float right, float rightMax, float top, float bottom)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, -bottom);
            rt.offsetMax = new Vector2(rightMax, -top);
        }

        private static void LayoutRow(RectTransform rt, float y)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(120f, y - 70f);
            rt.offsetMax = new Vector2(-120f, y - 10f);
        }

        private static void PlaceTab(Button button, float left)
        {
            var rt = (RectTransform)button.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(186f, 56f);
            rt.anchoredPosition = new Vector2(left, -120f);
        }

        private static void PlaceRight(Button button, float rightOffset, float width)
        {
            var rt = (RectTransform)button.transform;
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(width, 56f);
            rt.anchoredPosition = new Vector2(rightOffset, 0f);
        }

        private Slider CreateSlider(Transform parent, float x, float y, float width, float height, float value, UnityAction<float> onChanged)
        {
            var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);

            var background = CreateImage("Background", go.transform, new Color(0.2f, 0.22f, 0.3f, 1f));
            var bgRT = background.rectTransform;
            bgRT.anchorMin = new Vector2(0f, 0.3f);
            bgRT.anchorMax = new Vector2(1f, 0.7f);
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var faRT = (RectTransform)fillArea.transform;
            faRT.anchorMin = new Vector2(0f, 0.3f);
            faRT.anchorMax = new Vector2(1f, 0.7f);
            faRT.offsetMin = new Vector2(10f, 0f);
            faRT.offsetMax = new Vector2(-10f, 0f);

            var fill = CreateImage("Fill", fillArea.transform, new Color(0.9f, 0.63f, 0.34f, 1f));
            var fillRT = fill.rectTransform;
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.sizeDelta = new Vector2(10f, 0f);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            var haRT = (RectTransform)handleArea.transform;
            haRT.anchorMin = Vector2.zero;
            haRT.anchorMax = Vector2.one;
            haRT.offsetMin = new Vector2(10f, 0f);
            haRT.offsetMax = new Vector2(-10f, 0f);

            var handle = CreateImage("Handle", handleArea.transform, new Color(1f, 0.93f, 0.78f, 1f));
            var handleRT = handle.rectTransform;
            handleRT.anchorMin = new Vector2(0f, 0f);
            handleRT.anchorMax = new Vector2(0f, 1f);
            handleRT.sizeDelta = new Vector2(28f, 0f);

            var slider = go.GetComponent<Slider>();
            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(onChanged);
            slider.onValueChanged.Invoke(value);

            return slider;
        }

        private static EventSystem FindEventSystem() => Object.FindFirstObjectByType<EventSystem>();

        private Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        private Text CreateText(string name, Transform parent, string content, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.alignment = anchor;
            text.text = content;
            text.color = Color.white;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private Button CreateButton(Transform parent, string label, Color color, UnityAction onClick)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = color;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var text = CreateText("Label", go.transform, label, 26, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            text.color = Color.white;

            return btn;
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
