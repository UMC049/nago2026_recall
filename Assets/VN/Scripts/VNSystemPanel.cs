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
        private GameObject _pageRoute;
        private GameObject _pageLog;

        private readonly Text[] _slotInfo = new Text[SlotCount];
        private readonly RawImage[] _thumbs = new RawImage[SlotCount];
        private readonly Texture2D[] _thumbTex = new Texture2D[SlotCount];
        private readonly Button[] _tabButtons = new Button[4];

        // 自动存档（分支点自动写入，不占 6 个手动槽）
        private Text _autoInfo;
        private RawImage _autoThumb;
        private Texture2D _autoThumbTex;
        private GameObject _autoRow;
        private Button _autoLoadButton;

        private Text _routeText;
        private Text _routePageLabel;
        private Text _varText;
        private int _routePage;

        private Text _logText;
        private Text _logPageLabel;
        private int _logPage;

        private Slider _bgmSlider;
        private Slider _sfxSlider;
        private Text _bgmValue;
        private Text _sfxValue;
        private Slider _speedSlider;
        private Text _speedValue;

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

            // 三个页签
            _tabButtons[0] = CreateButton(prt, "存档 / 读档", TabIdleColor, () => SetPage(0));
            PlaceTab(_tabButtons[0], 48f);
            _tabButtons[1] = CreateButton(prt, "设置", TabIdleColor, () => SetPage(1));
            PlaceTab(_tabButtons[1], 246f);
            _tabButtons[2] = CreateButton(prt, "路线", TabIdleColor, () => SetPage(2));
            PlaceTab(_tabButtons[2], 444f);
            _tabButtons[3] = CreateButton(prt, "记录", TabIdleColor, () => SetPage(3));
            PlaceTab(_tabButtons[3], 642f);

            _pageSave = new GameObject("PageSave", typeof(RectTransform));
            _pageSave.transform.SetParent(prt, false);
            Stretch((RectTransform)_pageSave.transform);

            _pageAudio = new GameObject("PageAudio", typeof(RectTransform));
            _pageAudio.transform.SetParent(prt, false);
            Stretch((RectTransform)_pageAudio.transform);

            _pageRoute = new GameObject("PageRoute", typeof(RectTransform));
            _pageRoute.transform.SetParent(prt, false);
            Stretch((RectTransform)_pageRoute.transform);

            _pageLog = new GameObject("PageLog", typeof(RectTransform));
            _pageLog.transform.SetParent(prt, false);
            Stretch((RectTransform)_pageLog.transform);

            BuildSavePage((RectTransform)_pageSave.transform);
            BuildAudioPage((RectTransform)_pageAudio.transform);
            BuildRoutePage((RectTransform)_pageRoute.transform);
            BuildLogPage((RectTransform)_pageLog.transform);
        }

        private void BuildSavePage(RectTransform page)
        {
            var hint = CreateText("Hint", page, "F1 关闭　·　F5 快速存档　·　F9 快速读档", 22, TextAnchor.MiddleRight);
            LayoutTop(hint.rectTransform, 300f, 0f, -260f, 30f, 84f);
            hint.color = new Color(0.65f, 0.7f, 0.8f);

            BuildAutoSaveRow(page);

            for (int i = 0; i < SlotCount; i++)
                BuildSlotRow(page, i);
        }

        private void BuildAutoSaveRow(RectTransform page)
        {
            float top = -128f;

            var row = CreateImage("AutoSlot", page, new Color(0.9f, 0.76f, 0.42f, 0.10f));
            _autoRow = row.gameObject;

            var rrt = row.rectTransform;
            rrt.anchorMin = new Vector2(0f, 1f);
            rrt.anchorMax = new Vector2(1f, 1f);
            rrt.pivot = new Vector2(0.5f, 1f);
            rrt.offsetMin = new Vector2(40f, top - 80f);
            rrt.offsetMax = new Vector2(-40f, top);

            // 缩略图
            var thumbBg = CreateImage("ThumbBg", rrt, new Color(0.26f, 0.28f, 0.34f, 1f));
            var brt = thumbBg.rectTransform;
            brt.anchorMin = new Vector2(0f, 0.5f);
            brt.anchorMax = new Vector2(0f, 0.5f);
            brt.pivot = new Vector2(0f, 0.5f);
            brt.sizeDelta = new Vector2(112f, 62f);
            brt.anchoredPosition = new Vector2(18f, 0f);
            thumbBg.raycastTarget = false;

            var thumbGO = new GameObject("Thumb", typeof(RectTransform), typeof(RawImage));
            thumbGO.transform.SetParent(rrt, false);
            var trt = (RectTransform)thumbGO.transform;
            trt.anchorMin = new Vector2(0f, 0.5f);
            trt.anchorMax = new Vector2(0f, 0.5f);
            trt.pivot = new Vector2(0f, 0.5f);
            trt.sizeDelta = new Vector2(106f, 58f);
            trt.anchoredPosition = new Vector2(21f, 0f);
            _autoThumb = thumbGO.GetComponent<RawImage>();
            _autoThumb.color = Color.white;
            _autoThumb.raycastTarget = false;
            thumbGO.SetActive(false);

            _autoInfo = CreateText("AutoInfo", rrt, "", 24, TextAnchor.MiddleLeft);
            var irt = _autoInfo.rectTransform;
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(146f, 6f);
            irt.offsetMax = new Vector2(-420f, -6f);
            _autoInfo.horizontalOverflow = HorizontalWrapMode.Wrap;
            _autoInfo.verticalOverflow = VerticalWrapMode.Truncate;

            _autoLoadButton = CreateButton(rrt, "读取", new Color(0.22f, 0.35f, 0.55f), () =>
            {
                if (_player != null && _player.LoadAutoSave()) Close();
            });
            PlaceRight(_autoLoadButton, -226f, 110f);

            var del = CreateButton(rrt, "清除", new Color(0.45f, 0.22f, 0.25f), () =>
            {
                VNSaveSystem.Delete(VNSaveSystem.AutoSlot);
                VNSaveSystem.DeleteThumbnail(VNSaveSystem.AutoSlot);
                RefreshSavePage();
            });
            PlaceRight(del, -96f, 110f);
        }

        private void BuildSlotRow(RectTransform page, int index)
        {
            float top = -222f - index * 88f;

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
            irt.offsetMin = new Vector2(150f, 8f);
            irt.offsetMax = new Vector2(-420f, -8f);
            info.horizontalOverflow = HorizontalWrapMode.Wrap;
            info.verticalOverflow = VerticalWrapMode.Truncate;
            _slotInfo[index] = info;

            // 存档缩略图
            var thumbBg = CreateImage("ThumbBg", rrt, new Color(0.26f, 0.28f, 0.34f, 1f));
            var brt2 = thumbBg.rectTransform;
            brt2.anchorMin = new Vector2(0f, 0.5f);
            brt2.anchorMax = new Vector2(0f, 0.5f);
            brt2.pivot = new Vector2(0f, 0.5f);
            brt2.sizeDelta = new Vector2(120f, 70f);
            brt2.anchoredPosition = new Vector2(18f, 0f);
            thumbBg.raycastTarget = false;

            var thumbGO = new GameObject("Thumb", typeof(RectTransform), typeof(RawImage));
            thumbGO.transform.SetParent(rrt, false);
            var trt = (RectTransform)thumbGO.transform;
            trt.anchorMin = new Vector2(0f, 0.5f);
            trt.anchorMax = new Vector2(0f, 0.5f);
            trt.pivot = new Vector2(0f, 0.5f);
            trt.sizeDelta = new Vector2(114f, 64f);
            trt.anchoredPosition = new Vector2(21f, 0f);
            var raw = thumbGO.GetComponent<RawImage>();
            raw.color = new Color(1f, 1f, 1f, 1f);
            raw.raycastTarget = false;
            thumbGO.SetActive(false);
            _thumbs[index] = raw;

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

            var label1 = CreateText("BgmLabel", page, "音乐音量", 32, TextAnchor.MiddleLeft);
            LayoutRow(label1.rectTransform, -150f);
            _bgmValue = CreateText("BgmValue", page, "100%", 28, TextAnchor.MiddleRight);
            LayoutRow(_bgmValue.rectTransform, -150f);

            float bgmStart = audio != null ? audio.globalBgmVolume : 1f;
            _bgmSlider = CreateSlider(page, 120f, -228f, 1020f, 40f, bgmStart, v =>
            {
                if (_bgmValue != null) _bgmValue.text = Mathf.RoundToInt(v * 100f) + "%";
                if (audio != null) audio.SetGlobalBgmVolume(v);
            });

            var label2 = CreateText("SfxLabel", page, "音效音量", 32, TextAnchor.MiddleLeft);
            LayoutRow(label2.rectTransform, -318f);
            _sfxValue = CreateText("SfxValue", page, "100%", 28, TextAnchor.MiddleRight);
            LayoutRow(_sfxValue.rectTransform, -318f);

            float sfxStart = audio != null ? audio.globalSfxVolume : 1f;
            _sfxSlider = CreateSlider(page, 120f, -396f, 1020f, 40f, sfxStart, v =>
            {
                if (_sfxValue != null) _sfxValue.text = Mathf.RoundToInt(v * 100f) + "%";
                if (audio != null) audio.SetGlobalSfxVolume(v);
            });

            // 文字速度（0.5x ~ 2.0x，滑块内部用 0~1）
            var label3 = CreateText("SpeedLabel", page, "文字速度", 32, TextAnchor.MiddleLeft);
            LayoutRow(label3.rectTransform, -486f);
            _speedValue = CreateText("SpeedValue", page, "1.0x", 28, TextAnchor.MiddleRight);
            LayoutRow(_speedValue.rectTransform, -486f);

            float speedNow = _player != null ? _player.textSpeedMultiplier : 1f;
            float speedSlider = Mathf.Clamp01((speedNow - 0.5f) / 1.5f);
            _speedSlider = CreateSlider(page, 120f, -564f, 1020f, 40f, speedSlider, v =>
            {
                float mul = Mathf.Lerp(0.5f, 2.0f, v);
                if (_speedValue != null) _speedValue.text = mul.ToString("0.0") + "x";
                if (_player != null) _player.SetTextSpeed(mul);
            });

            var fsBtn = CreateButton(page, "切换全屏", TabIdleColor, () =>
            {
                Screen.fullScreen = !Screen.fullScreen;
            });
            var frt = (RectTransform)fsBtn.transform;
            frt.anchorMin = new Vector2(0f, 1f);
            frt.anchorMax = new Vector2(0f, 1f);
            frt.pivot = new Vector2(0f, 1f);
            frt.sizeDelta = new Vector2(230f, 52f);
            frt.anchoredPosition = new Vector2(120f, -650f);

            var tip = CreateText("Tip", page, "设置立即生效，并在下次启动时保留。", 22, TextAnchor.MiddleLeft);
            var trt = tip.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(380f, -700f);
            trt.offsetMax = new Vector2(-120f, -660f);
            tip.color = new Color(0.6f, 0.66f, 0.76f);
        }

        // ---------------- 刷新 ----------------

        private void BuildRoutePage(RectTransform page)
        {
            var title = CreateText("RouteTitle", page, "剧情路线（你走过的分支）", 30, TextAnchor.MiddleLeft);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(60f, -240f);
            trt.offsetMax = new Vector2(-60f, -190f);
            title.color = new Color(1f, 0.9f, 0.7f);

            var body = CreateText("RouteBody", page, "", 24, TextAnchor.UpperLeft);
            var brt = body.rectTransform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(60f, 110f);
            brt.offsetMax = new Vector2(-60f, -250f);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            _routeText = body;

            var prev = CreateButton(page, "上一页", TabIdleColor, () =>
            {
                _routePage = Mathf.Max(0, _routePage - 1);
                RefreshRoutePage();
            });
            PlaceBottom(prev, 60f, 44f, 130f);

            var next = CreateButton(page, "下一页", TabIdleColor, () =>
            {
                _routePage++;
                RefreshRoutePage();
            });
            PlaceBottom(next, 206f, 44f, 130f);

            _routePageLabel = CreateText("PageLabel", page, "", 22, TextAnchor.MiddleLeft);
            var plrt = _routePageLabel.rectTransform;
            plrt.anchorMin = new Vector2(0f, 0f);
            plrt.anchorMax = new Vector2(1f, 0f);
            plrt.pivot = new Vector2(0.5f, 0f);
            plrt.offsetMin = new Vector2(360f, 52f);
            plrt.offsetMax = new Vector2(-60f, 88f);
            _routePageLabel.color = new Color(0.65f, 0.7f, 0.8f);
        }

        private static void PlaceBottom(Button button, float left, float bottom, float width)
        {
            var rt = (RectTransform)button.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(width, 48f);
            rt.anchoredPosition = new Vector2(left, bottom);
        }

        private void RefreshRoutePage()
        {
            if (_routeText == null || _player == null) return;

            var history = _player.choiceHistory;
            int total = history != null ? history.Count : 0;
            const int perPage = 8;
            int pages = Mathf.Max(1, Mathf.CeilToInt(total / (float)perPage));
            _routePage = Mathf.Clamp(_routePage, 0, pages - 1);

            var sb = new System.Text.StringBuilder();

            if (total == 0)
            {
                sb.AppendLine("<color=#7A8296>还没有做过任何选择。</color>");
            }
            else
            {
                int start = _routePage * perPage;
                int end = Mathf.Min(start + perPage, total);
                for (int i = start; i < end; i++)
                {
                    var r = history[i];
                    string node = string.IsNullOrEmpty(r.nodeTitle) ? "（未标记节点）" : r.nodeTitle;
                    string target = string.IsNullOrEmpty(r.targetTitle) ? "（下一行）" : r.targetTitle;

                    sb.AppendLine($"<color=#FFD98A>{i + 1}. {node}</color>");
                    sb.AppendLine($"    选择了「{r.choiceText}」");
                    sb.AppendLine($"    → {target}   <size=20><color=#7A8296>{r.time}</color></size>");
                }
            }

            if (_player.state != null && _player.state.numbers.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("<color=#9AA3B8>当前数值</color>");
                foreach (var n in _player.state.numbers)
                    sb.AppendLine($"    {n.key} = {n.value:0.##}");
            }

            if (_player.state != null && _player.state.visited.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"<color=#9AA3B8>已解锁节点：{_player.state.visited.Count} 个</color>");
            }

            _routeText.text = sb.ToString();

            if (_routePageLabel != null)
            {
                _routePageLabel.text = total == 0
                    ? ""
                    : $"第 {_routePage + 1} / {pages} 页　　共 {total} 次选择";
            }
        }

        private void BuildLogPage(RectTransform page)
        {
            var title = CreateText("LogTitle", page, "历史记录", 30, TextAnchor.MiddleLeft);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(60f, -240f);
            trt.offsetMax = new Vector2(-60f, -190f);
            title.color = new Color(1f, 0.9f, 0.7f);

            var body = CreateText("LogBody", page, "", 26, TextAnchor.UpperLeft);
            var brt = body.rectTransform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(60f, 110f);
            brt.offsetMax = new Vector2(-60f, -250f);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            _logText = body;

            var prev = CreateButton(page, "上一页", TabIdleColor, () =>
            {
                _logPage = Mathf.Max(0, _logPage - 1);
                RefreshLogPage();
            });
            PlaceBottom(prev, 60f, 44f, 130f);

            var next = CreateButton(page, "下一页", TabIdleColor, () =>
            {
                _logPage++;
                RefreshLogPage();
            });
            PlaceBottom(next, 206f, 44f, 130f);

            _logPageLabel = CreateText("LogPageLabel", page, "", 22, TextAnchor.MiddleLeft);
            var plrt = _logPageLabel.rectTransform;
            plrt.anchorMin = new Vector2(0f, 0f);
            plrt.anchorMax = new Vector2(1f, 0f);
            plrt.pivot = new Vector2(0.5f, 0f);
            plrt.offsetMin = new Vector2(360f, 52f);
            plrt.offsetMax = new Vector2(-60f, 88f);
            _logPageLabel.color = new Color(0.65f, 0.7f, 0.8f);
        }

        private void RefreshLogPage()
        {
            if (_logText == null || _player == null) return;

            var log = _player.log;
            int total = log != null ? log.Count : 0;
            const int perPage = 7;
            int pages = Mathf.Max(1, Mathf.CeilToInt(total / (float)perPage));
            _logPage = Mathf.Clamp(_logPage, 0, pages - 1);

            var sb = new System.Text.StringBuilder();
            if (total == 0)
            {
                sb.AppendLine("<color=#7A8296>还没有任何台词记录。</color>");
            }
            else
            {
                int start = _logPage * perPage;
                int end = Mathf.Min(start + perPage, total);
                for (int i = start; i < end; i++)
                {
                    var e = log[i];
                    if (!string.IsNullOrEmpty(e.name))
                        sb.AppendLine($"<color=#FFD98A>{e.name}</color>");
                    sb.AppendLine(e.text);
                    sb.AppendLine();
                }
            }

            _logText.text = sb.ToString();

            if (_logPageLabel != null)
                _logPageLabel.text = total == 0 ? "" : $"第 {_logPage + 1} / {pages} 页　　共 {total} 条";
        }

        /// <summary>从外部直接打开某一页（记录页用 L 键 / 滚轮打开）。</summary>
        public void OpenPage(int index)
        {
            if (!IsOpen) Open();
            if (index == 3) _logPage = int.MaxValue;
            SetPage(index);
        }

        private void SetPage(int index)
        {
            if (_pageSave != null) _pageSave.SetActive(index == 0);
            if (_pageAudio != null) _pageAudio.SetActive(index == 1);
            if (_pageRoute != null) _pageRoute.SetActive(index == 2);
            if (_pageLog != null) _pageLog.SetActive(index == 3);

            if (index == 2) RefreshRoutePage();
            if (index == 3) RefreshLogPage();

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

            if (_player != null)
            {
                if (_speedSlider != null)
                    _speedSlider.SetValueWithoutNotify(Mathf.Clamp01((_player.textSpeedMultiplier - 0.5f) / 1.5f));
                if (_speedValue != null)
                    _speedValue.text = _player.textSpeedMultiplier.ToString("0.0") + "x";
            }
        }

        private void RefreshSavePage()
        {
            RefreshAutoSaveRow();

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

                RefreshThumb(i, data != null && !data.IsEmpty);
            }
        }

        private void RefreshAutoSaveRow()
        {
            if (_autoInfo == null) return;

            var data = VNSaveSystem.Read(VNSaveSystem.AutoSlot);
            bool has = data != null && !data.IsEmpty;

            if (!has)
            {
                _autoInfo.text = "<color=#7A8296>自动存档　—　暂无（经过分支点时会自动保存）</color>";
            }
            else
            {
                var when = string.IsNullOrEmpty(data.savedAt) ? "" : data.savedAt;
                _autoInfo.text = $"<color=#FFD98A>自动存档</color>　<size=22><color=#9AA3B8>{when}</color></size>\n{data.preview}";
            }

            if (_autoLoadButton != null) _autoLoadButton.interactable = has;

            if (_autoThumb == null) return;

            if (_autoThumbTex != null)
            {
                Destroy(_autoThumbTex);
                _autoThumbTex = null;
            }

            if (!has)
            {
                _autoThumb.gameObject.SetActive(false);
                return;
            }

            var tex = VNSaveSystem.LoadThumbnail(VNSaveSystem.AutoSlot);
            if (tex == null)
            {
                _autoThumb.gameObject.SetActive(false);
                return;
            }

            _autoThumbTex = tex;
            _autoThumb.texture = tex;
            _autoThumb.gameObject.SetActive(true);
        }

        private void RefreshThumb(int slot, bool hasSave)
        {            var raw = _thumbs[slot];
            if (raw == null) return;

            if (_thumbTex[slot] != null)
            {
                Destroy(_thumbTex[slot]);
                _thumbTex[slot] = null;
            }

            if (!hasSave)
            {
                raw.gameObject.SetActive(false);
                return;
            }

            var tex = VNSaveSystem.LoadThumbnail(slot);
            if (tex == null)
            {
                raw.gameObject.SetActive(false);
                return;
            }

            _thumbTex[slot] = tex;
            raw.texture = tex;
            raw.gameObject.SetActive(true);
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
