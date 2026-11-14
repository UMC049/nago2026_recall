using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace VN.EditorTools
{
    /// <summary>
    /// 剧本编辑器：左侧行列表，右侧逐行编辑背景 / 立绘 / 音频 / 文字演出。
    /// 菜单：VN → 剧本编辑器（Ctrl+Shift+E）
    /// </summary>
    public class VNScriptEditorWindow : EditorWindow
    {
        private const string DefaultPath = "Assets/VN/Resources/VN/Data/chapter01.json";
        private const string SizedKey = "VNScriptEditor.Sized";
        private const string PosKey = "VNScriptEditor.Pos";

        private bool _collapsed;
        private Vector2 _expandedSize = new Vector2(1000f, 700f);

        // 撤销 / 重做：存整份剧本的 JSON 快照
        private readonly List<string> _undoStack = new List<string>();
        private readonly List<string> _redoStack = new List<string>();
        private const int MaxUndo = 30;

        private static readonly KeyCode[] NoKeys = new KeyCode[0];
        private const string BgFolder = "Assets/VN/Resources/VN/BG";
        private const string CharFolder = "Assets/VN/Resources/VN/Char";
        private const string BgmFolder = "Assets/VN/Resources/VN/Audio/BGM";
        private const string SfxFolder = "Assets/VN/Resources/VN/Audio/SFX";

        private VNScript _script;
        private string _path;
        private int _selected = -1;
        private bool _dirty;

        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private Vector2 _actorScroll;

        private bool _stageOpen = true;
        private bool _actorOpen = true;
        private bool _styleOpen = true;
        private bool _cameraOpen = true;
        private bool _stepsOpen = true;
        private bool _branchOpen = true;

        private List<string> _bgOptions = new List<string>();
        private List<string> _charOptions = new List<string>();
        private List<string> _bgmOptions = new List<string>();
        private List<string> _sfxOptions = new List<string>();
        private List<string> _voiceOptions = new List<string>();
        private List<string> _fontOptions = new List<string>();

        private static readonly string[] Motions = { "fade", "none", "slideUp", "slideSide", "bounce", "pop" };
        private static readonly string[] Effects = { "none", "shake", "wave", "pulse", "glitch" };
        private static readonly string[] ShakeModes = { "none", "light", "medium", "strong" };

        [MenuItem("VN/剧本编辑器 %#e", priority = 0)]
        public static void Open()
        {
            var win = GetWindow<VNScriptEditorWindow>("VN 剧本");
            win.minSize = new Vector2(720f, 480f);

            // 首次打开给个合适的尺寸；之后记住用户自己调过的大小
            if (!SessionState.GetBool(SizedKey, false))
            {
                SessionState.SetBool(SizedKey, true);
                if (!EditorPrefs.HasKey(PosKey))
                    win.position = new Rect(win.position.x, win.position.y, 1000f, 700f);
            }

            win.TryAutoLoad();
        }

        private void OnEnable()
        {
            RefreshAssetLists();
            RestorePosition();
        }

        private void OnDisable()
        {
            SavePosition();
        }

        private void RestorePosition()
        {
            var raw = EditorPrefs.GetString(PosKey, "");
            if (string.IsNullOrEmpty(raw)) return;

            var parts = raw.Split(',');
            if (parts.Length != 4) return;

            if (float.TryParse(parts[0], out var x) &&
                float.TryParse(parts[1], out var y) &&
                float.TryParse(parts[2], out var w) &&
                float.TryParse(parts[3], out var h))
            {
                position = new Rect(x, y, Mathf.Max(720f, w), Mathf.Max(480f, h));
                _expandedSize = new Vector2(w, h);
            }
        }

        private void SavePosition()
        {
            if (_collapsed) return; // 收起状态不存，免得下次打开是一条
            EditorPrefs.SetString(PosKey,
                $"{position.x},{position.y},{position.width},{position.height}");
        }

        /// <summary>
        /// Unity 的浮动窗口没有最小化按钮，这里用「收起成一条」模拟：
        /// 收起后只留标题栏 + 工具栏，再点一次恢复原大小。
        /// </summary>
        private void ToggleCollapse()
        {
            if (!_collapsed)
            {
                _expandedSize = position.size;
                minSize = Vector2.zero;
                maxSize = new Vector2(4000f, 4000f);
                position = new Rect(position.x, position.y, Mathf.Max(720f, position.width), 62f);
                _collapsed = true;
            }
            else
            {
                minSize = new Vector2(720f, 480f);
                var w = Mathf.Max(720f, _expandedSize.x);
                var h = Mathf.Max(480f, _expandedSize.y);
                position = new Rect(position.x, position.y, w, h);
                _collapsed = false;
            }

            Repaint();
        }

        private void TryAutoLoad()
        {
            if (_script != null) return;
            if (File.Exists(DefaultPath)) Load(DefaultPath);
            else NewScript();
        }

        // ---------------- 资源列表 ----------------

        private void RefreshAssetLists()
        {
            _bgOptions = Scan(BgFolder, "t:Texture2D");
            _charOptions = Scan(CharFolder, "t:Texture2D");
            _bgmOptions = Scan(BgmFolder, "t:AudioClip");
            _sfxOptions = Scan(SfxFolder, "t:AudioClip");
            _voiceOptions = Scan("Assets/VN/Resources/VN/Audio/Voice", "t:AudioClip");
            _fontOptions = Scan("Assets/VN/Fonts", "t:Font");
        }

        private static List<string> Scan(string folder, string filter)
        {
            var result = new List<string>();
            if (!AssetDatabase.IsValidFolder(folder)) return result;

            foreach (var guid in AssetDatabase.FindAssets(filter, new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path)) result.Add(path);
            }

            result.Sort();
            return result;
        }

        // ---------------- 读写 ----------------

        private void NewScript()
        {
            _script = new VNScript
            {
                title = "新章节",
                lines = new List<VNLine>
                {
                    new VNLine
                    {
                        name = "",
                        text = "……",
                        note = "第一条。填好背景、立绘、音乐后按 ▶ 播放。",
                    }
                }
            };
            _path = DefaultPath;
            _selected = 0;
            _dirty = true;
            _undoStack.Clear();
            _redoStack.Clear();
        }

        private void Load(string path)
        {
            try
            {
                var json = File.ReadAllText(path);
                _script = JsonUtility.FromJson<VNScript>(json) ?? new VNScript();
                if (_script.lines == null) _script.lines = new List<VNLine>();
                _path = path;
                _selected = _script.lines.Count > 0 ? 0 : -1;
                _dirty = false;
                _undoStack.Clear();
                _redoStack.Clear();
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("读取失败", $"{path}\n\n{ex.Message}", "好");
            }
        }

        private void Save()
        {
            if (_script == null || string.IsNullOrEmpty(_path)) return;

            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(_path, JsonUtility.ToJson(_script, true), System.Text.Encoding.UTF8);
            AssetDatabase.ImportAsset(_path);
            _dirty = false;
            Debug.Log($"[VN] 剧本已保存：{_path}");
        }

        // ---------------- GUI ----------------

        // ---------------- 撤销 / 重做 ----------------

        /// <summary>在改动之前调用，把当前状态压入撤销栈。</summary>
        private void PushUndo()
        {
            if (_script == null) return;

            var snap = JsonUtility.ToJson(_script);
            if (_undoStack.Count > 0 && _undoStack[_undoStack.Count - 1] == snap) return;

            _undoStack.Add(snap);
            if (_undoStack.Count > MaxUndo) _undoStack.RemoveAt(0);
            _redoStack.Clear();
        }

        private void Undo()
        {
            if (_script == null || _undoStack.Count == 0)
            {
                ShowNotification(new GUIContent("没有可撤销的操作"));
                return;
            }

            _redoStack.Add(JsonUtility.ToJson(_script));

            var snap = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);

            _script = JsonUtility.FromJson<VNScript>(snap) ?? _script;
            if (_script.lines == null) _script.lines = new List<VNLine>();

            _selected = Mathf.Clamp(_selected, -1, _script.lines.Count - 1);
            _dirty = true;
            Repaint();
        }

        private void Redo()
        {
            if (_script == null || _redoStack.Count == 0)
            {
                ShowNotification(new GUIContent("没有可恢复的操作"));
                return;
            }

            _undoStack.Add(JsonUtility.ToJson(_script));

            var snap = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);

            _script = JsonUtility.FromJson<VNScript>(snap) ?? _script;
            if (_script.lines == null) _script.lines = new List<VNLine>();

            _selected = Mathf.Clamp(_selected, -1, _script.lines.Count - 1);
            _dirty = true;
            Repaint();
        }

        private void HandleUndoKeys()
        {
            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown || !e.control) return;

            if (e.keyCode == KeyCode.Z)
            {
                Undo();
                e.Use();
            }
            else if (e.keyCode == KeyCode.Y)
            {
                Redo();
                e.Use();
            }
        }

        private void OnGUI()
        {
            // 标签别占太宽，把空间留给控件
            EditorGUIUtility.labelWidth = 88f;

            HandleUndoKeys();

            DrawToolbar();

            // 收起状态只画工具栏，省得下面内容被裁切还占布局
            if (_collapsed) return;
            if (_script == null)
            {
                EditorGUILayout.HelpBox("还没有打开剧本。点上面的「新建」或「打开」。", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();

            DrawListPanel();
            DrawDetailPanel();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("新建", EditorStyles.toolbarButton, GUILayout.Width(52)))
            {
                if (!_dirty || EditorUtility.DisplayDialog("未保存", "当前改动还没保存，继续会丢失。", "继续", "取消"))
                    NewScript();
            }

            if (GUILayout.Button("打开", EditorStyles.toolbarButton, GUILayout.Width(52)))
            {
                var picked = EditorUtility.OpenFilePanel("选择剧本 JSON", "Assets/VN/Resources/VN/Data", "json");
                if (!string.IsNullOrEmpty(picked))
                {
                    var rel = ToProjectPath(picked);
                    if (rel != null) Load(rel);
                }
            }

            using (new EditorGUI.DisabledScope(_script == null))
            {
                if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(52))) Save();
            }

            GUILayout.Space(6);

            using (new EditorGUI.DisabledScope(_script == null || _undoStack.Count == 0))
            {
                if (GUILayout.Button("↶ 撤销", EditorStyles.toolbarButton, GUILayout.Width(62))) Undo();
            }
            using (new EditorGUI.DisabledScope(_script == null || _redoStack.Count == 0))
            {
                if (GUILayout.Button("↷ 重做", EditorStyles.toolbarButton, GUILayout.Width(62))) Redo();
            }

            GUILayout.Space(10);

            GUILayout.Label(_path ?? "(未命名)", EditorStyles.miniLabel);
            if (_dirty) GUILayout.Label("● 未保存", EditorStyles.miniLabel, GUILayout.Width(70));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("↻ 素材列表", EditorStyles.toolbarButton, GUILayout.Width(88))) RefreshAssetLists();

            using (new EditorGUI.DisabledScope(_script == null))
            {
                if (GUILayout.Button("👁 预览", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    PreviewSelectedInEditor();

                if (GUILayout.Button("▶ 播放", EditorStyles.toolbarButton, GUILayout.Width(66)))
                    PlayFromSelected();
            }

            GUILayout.Space(6);
            if (GUILayout.Button(_collapsed ? "▣ 展开" : "— 收起", EditorStyles.toolbarButton, GUILayout.Width(76)))
                ToggleCollapse();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(360));

            EditorGUILayout.LabelField($"行（{_script.lines.Count}）", EditorStyles.boldLabel);

            using (var scroll = new EditorGUILayout.ScrollViewScope(_listScroll, GUILayout.ExpandHeight(true)))
            {
                _listScroll = scroll.scrollPosition;

                for (int i = 0; i < _script.lines.Count; i++)
                {
                    var line = _script.lines[i];
                    var label = string.IsNullOrWhiteSpace(line.name) ? "旁白" : line.name;
                    var preview = (line.text ?? "").Replace("\n", " ");
                    if (preview.Length > 18) preview = preview.Substring(0, 18) + "…";

                    var style = i == _selected ? EditorStyles.toolbarButton : GUI.skin.button;
                    var text = $"{i + 1:D2}  [{label}] {preview}";

                    var rect = GUILayoutUtility.GetRect(new GUIContent(text), style, GUILayout.Height(24));
                    if (GUI.Button(rect, text, style))
                    {
                        if (i != _selected) PushUndo();
                        _selected = i;
                        _detailScroll = Vector2.zero;
                    }
                }
            }

            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("＋ 新行"))
            {
                PushUndo();
                _script.lines.Insert(_selected + 1 <= 0 ? _script.lines.Count : _selected + 1, CloneOf(CurrentLine()));
                _selected = Mathf.Clamp(_selected + 1, 0, _script.lines.Count - 1);
                _dirty = true;
            }

            using (new EditorGUI.DisabledScope(_selected < 0))
            {
                if (GUILayout.Button("⧉ 复制"))
                {
                    PushUndo();
                    _script.lines.Insert(_selected + 1, CloneOf(CurrentLine()));
                    _selected++;
                    _dirty = true;
                }
                if (GUILayout.Button("✕ 删除"))
                {
                    PushUndo();
                    _script.lines.RemoveAt(_selected);
                    _selected = Mathf.Clamp(_selected, 0, _script.lines.Count - 1);
                    _dirty = true;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(_selected <= 0))
            {
                if (GUILayout.Button("↑ 上移"))
                {
                    PushUndo();
                    (_script.lines[_selected - 1], _script.lines[_selected]) = (_script.lines[_selected], _script.lines[_selected - 1]);
                    _selected--;
                    _dirty = true;
                }
            }
            using (new EditorGUI.DisabledScope(_selected < 0 || _selected >= _script.lines.Count - 1))
            {
                if (GUILayout.Button("↓ 下移"))
                {
                    PushUndo();
                    (_script.lines[_selected + 1], _script.lines[_selected]) = (_script.lines[_selected], _script.lines[_selected + 1]);
                    _selected++;
                    _dirty = true;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawDetailPanel()
        {
            EditorGUILayout.BeginVertical();

            if (_selected < 0 || _selected >= _script.lines.Count)
            {
                EditorGUILayout.HelpBox("选左边的一行开始编辑。", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(_detailScroll))
            {
                _detailScroll = scroll.scrollPosition;

                SyncPlayerDefaults();

                var line = _script.lines[_selected];
                EditorGUI.BeginChangeCheck();

                EditorGUILayout.LabelField($"第 {_selected + 1} 行", EditorStyles.boldLabel);

                // 节点信息置顶：跳转、分支树都靠它
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                line.nodeId = EditorGUILayout.TextField("节点 ID", line.nodeId);
                line.nodeTitle = EditorGUILayout.TextField("显示名", line.nodeTitle);
                EditorGUILayout.EndHorizontal();

                if (string.IsNullOrWhiteSpace(line.nodeId))
                {
                    EditorGUILayout.LabelField("留空 = 这一行不是节点；选项跳转和分支树都靠节点 ID 定位", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField("已被使用的节点：" + string.Join("、", CollectNodeIds()), EditorStyles.miniLabel);
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginHorizontal();
                line.name = EditorGUILayout.TextField("说话人", line.name);
                line.autoDelay = EditorGUILayout.FloatField("自动等待", line.autoDelay, GUILayout.Width(96));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField("台词");
                line.text = EditorGUILayout.TextArea(line.text, GUILayout.MinHeight(56));
                EditorGUILayout.LabelField("停顿标记：[p:0.8] = 停 0.8 秒，如「……[p:1.2]你来了。」", EditorStyles.miniLabel);
                line.note = EditorGUILayout.TextField("备注", line.note);

                GUILayout.Space(6);
                _stageOpen = EditorGUILayout.BeginFoldoutHeaderGroup(_stageOpen, "舞台 / 音频");
                if (_stageOpen)
                {
                    DrawAssetPopup("背景", line.background, _bgOptions, v => { line.background = v; _dirty = true; });
                    line.backgroundFade = EditorGUILayout.Slider("背景淡入（秒）", line.backgroundFade, 0.01f, 3f);

                    DrawAssetPopup("BGM", line.bgm, _bgmOptions, v => { line.bgm = v; _dirty = true; });
                    line.bgmVolume = EditorGUILayout.Slider("BGM 音量", line.bgmVolume, 0f, 1f);

                    line.bgmAction = BgmActionPopup(line.bgmAction);
                    line.bgmFade = EditorGUILayout.Slider("淡入 / 淡出（秒）", line.bgmFade, 0.05f, 5f);

                    if (line.bgmAction == "stop" || line.bgmAction == "pitch")
                    {
                        line.bgmPitch = EditorGUILayout.Slider("目标音调", line.bgmPitch, 0.3f, 2f);
                        line.bgmPitchDuration = EditorGUILayout.Slider("变调时长（秒，0 = 立即）", line.bgmPitchDuration, 0f, 5f);
                    }

                    if (!string.IsNullOrWhiteSpace(line.bgm) && line.bgmAction != "keep")
                        EditorGUILayout.HelpBox("填了 BGM 音乐时，「指令」会被忽略，直接播放这首。", MessageType.None);

                    DrawAssetPopup("音效", line.sfx, _sfxOptions, v => { line.sfx = v; _dirty = true; });
                    line.sfxVolume = EditorGUILayout.Slider("音效音量", line.sfxVolume, 0f, 1f);

                    GUILayout.Space(4);
                    line.speakerSlot = SpeakerSlotPopup(line.speakerSlot);
                    line.transition = TransitionPopup(line.transition);
                    using (new EditorGUI.DisabledScope(line.transition == "none"))
                        line.transitionDuration = EditorGUILayout.Slider("转场时长（秒）", line.transitionDuration, 0.05f, 2f);

                    line.stageTiming = StageTimingPopup(line.stageTiming);

                    GUILayout.Space(4);
                    DrawAssetPopup("默认说话音", _playerDefaultVoice, _voiceOptions, v =>
                    {
                        ApplyDefaultVoice(v);
                    });
                    EditorGUILayout.LabelField("角色没单独配语音时用这个（写在 VNPlayer 上）", EditorStyles.miniLabel);

                    GUILayout.Space(4);
                    _cameraOpen = EditorGUILayout.BeginFoldoutHeaderGroup(_cameraOpen, "镜头（只影响背景与立绘）");
                    if (_cameraOpen) DrawCamera(line);
                    EditorGUILayout.EndFoldoutHeaderGroup();
                }
                EditorGUILayout.EndFoldoutHeaderGroup();

                GUILayout.Space(4);
                _actorOpen = EditorGUILayout.BeginFoldoutHeaderGroup(_actorOpen, "立绘");
                if (_actorOpen) DrawActors(line);
                EditorGUILayout.EndFoldoutHeaderGroup();

                GUILayout.Space(4);
                _styleOpen = EditorGUILayout.BeginFoldoutHeaderGroup(_styleOpen, "文字演出");
                if (_styleOpen) DrawStyle(line);
                EditorGUILayout.EndFoldoutHeaderGroup();

                GUILayout.Space(4);
                _stepsOpen = EditorGUILayout.BeginFoldoutHeaderGroup(_stepsOpen, "演出顺序表（留空 = 全部同时）");
                if (_stepsOpen) DrawSteps(line);
                EditorGUILayout.EndFoldoutHeaderGroup();

                GUILayout.Space(4);
                _branchOpen = EditorGUILayout.BeginFoldoutHeaderGroup(_branchOpen, "分支 / 选项 / 数值");
                if (_branchOpen) DrawBranch(line);
                EditorGUILayout.EndFoldoutHeaderGroup();

                if (EditorGUI.EndChangeCheck()) _dirty = true;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActors(VNLine line)
        {
            EditorGUILayout.LabelField("立绘（留空则保持上一行）", EditorStyles.boldLabel);

            if (line.actors == null) line.actors = new List<VNActor>();

            // 立绘区高度跟着窗口走：窗口越大给得越多，最少也留 260
            float actorHeight = Mathf.Clamp(position.height - 540f, 260f, 660f);
            _actorScroll = EditorGUILayout.BeginScrollView(
                _actorScroll,
                GUILayout.MinHeight(actorHeight),
                GUILayout.MaxHeight(actorHeight));

            for (int i = 0; i < line.actors.Count; i++)
            {
                var a = line.actors[i];
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("#" + (i + 1), GUILayout.Width(24));
                a.slot = Popup("", a.slot, new[] { "left", "center", "right" }, 60f);
                a.hide = GUILayout.Toggle(a.hide, "隐藏", GUILayout.Width(52));
                if (GUILayout.Button("✕", GUILayout.Width(22)))
                {
                    line.actors.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                if (!a.hide)
                {
                    DrawAssetPopup("图片", a.sprite, _charOptions, v => { a.sprite = v; _dirty = true; });

                    EditorGUILayout.BeginHorizontal();
                    a.character = EditorGUILayout.TextField("角色", a.character);
                    a.expression = EditorGUILayout.TextField("表情", a.expression);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.HelpBox("填了角色+表情会找 Char/角色/表情.png；都空则用上面的图片", MessageType.None);

                    EditorGUILayout.BeginHorizontal();
                    a.motion = Popup("入场", a.motion, Motions, 80f);
                    a.fadeDuration = EditorGUILayout.FloatField("时长", a.fadeDuration, GUILayout.Width(64));
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    a.offsetX = EditorGUILayout.FloatField("X", a.offsetX, GUILayout.Width(74));
                    a.offsetY = EditorGUILayout.FloatField("Y", a.offsetY, GUILayout.Width(74));
                    a.scale = EditorGUILayout.FloatField("缩放", a.scale, GUILayout.Width(86));
                    EditorGUILayout.EndHorizontal();

                    GUILayout.Space(2);
                    DrawAssetPopup("说话音", a.voice, _voiceOptions, v => { a.voice = v; _dirty = true; });
                    EditorGUILayout.BeginHorizontal();
                    a.voiceMode = Popup("模式", string.IsNullOrWhiteSpace(a.voiceMode) ? "char" : a.voiceMode,
                        new[] { "char", "loop" }, 44f);
                    a.voiceVolume = EditorGUILayout.Slider("音量", a.voiceVolume, 0f, 1f, GUILayout.Width(150));
                    a.voicePitchRandom = EditorGUILayout.Slider("抖动", a.voicePitchRandom, 0f, 0.3f, GUILayout.Width(150));
                    EditorGUILayout.EndHorizontal();
                    if (a.voiceEveryNChars < 1) a.voiceEveryNChars = 1;
                    a.voiceEveryNChars = EditorGUILayout.IntSlider("每隔几个字", a.voiceEveryNChars, 1, 6);

                    DrawFontPopup("角色字体", a.font, v => { a.font = v; _dirty = true; });
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("＋ 左")) { line.actors.Add(new VNActor { slot = "left" }); _dirty = true; }
            if (GUILayout.Button("＋ 中")) { line.actors.Add(new VNActor { slot = "center" }); _dirty = true; }
            if (GUILayout.Button("＋ 右")) { line.actors.Add(new VNActor { slot = "right" }); _dirty = true; }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawStyle(VNLine line)
        {
            EditorGUILayout.LabelField("文字演出", EditorStyles.boldLabel);
            if (line.style == null) line.style = new VNTextStyle();

            line.style.fontSize = EditorGUILayout.IntSlider("字号", line.style.fontSize, 8, 96);
            DrawFontPopup("字体", line.style.font, v => { line.style.font = v; _dirty = true; });

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("常用", GUILayout.Width(36));
            foreach (var size in new[] { 24, 36, 48, 60 })
                if (GUILayout.Button(size.ToString(), GUILayout.Width(44)))
                    line.style.fontSize = size;
            EditorGUILayout.EndHorizontal();

            line.style.charsPerSecond = EditorGUILayout.Slider("速度（字/秒）", line.style.charsPerSecond, 0f, 120f);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("常用", GUILayout.Width(36));
            if (GUILayout.Button("慢 12", GUILayout.Width(60))) line.style.charsPerSecond = 12f;
            if (GUILayout.Button("中 30", GUILayout.Width(60))) line.style.charsPerSecond = 30f;
            if (GUILayout.Button("快 60", GUILayout.Width(60))) line.style.charsPerSecond = 60f;
            if (GUILayout.Button("瞬间", GUILayout.Width(60))) line.style.instant = true;
            EditorGUILayout.EndHorizontal();

            line.style.effect = Popup("演出效果", line.style.effect, Effects);
            using (new EditorGUI.DisabledScope(line.style.effect == "none"))
            {
                line.style.effectStrength = EditorGUILayout.Slider("强度", line.style.effectStrength, 0f, 20f);
                line.style.effectSpeed = EditorGUILayout.Slider("速度倍率", line.style.effectSpeed, 0.1f, 5f);
            }

            line.style.color = EditorGUILayout.ColorField("颜色", VNColors.Parse(line.style.color, Color.white)).ToHex();
            line.style.outline = EditorGUILayout.Toggle("描边", line.style.outline);
            line.style.instant = EditorGUILayout.Toggle("跳过打字机", line.style.instant);
        }

        private void DrawCamera(VNLine line)
        {            line.useCamera = EditorGUILayout.Toggle("启用镜头", line.useCamera);
            using (new EditorGUI.DisabledScope(!line.useCamera))
            {
                if (line.camera == null) line.camera = new VNCamera();

                line.camera.offsetX = EditorGUILayout.FloatField("平移 X", line.camera.offsetX);
                line.camera.offsetY = EditorGUILayout.FloatField("平移 Y", line.camera.offsetY);
                line.camera.zoom = EditorGUILayout.Slider("缩放", line.camera.zoom, 0.3f, 3f);
                line.camera.duration = EditorGUILayout.Slider("补间时长（秒）", line.camera.duration, 0.01f, 3f);

                line.camera.shake = Popup("晃动", string.IsNullOrWhiteSpace(line.camera.shake) ? "none" : line.camera.shake, ShakeModes);
                using (new EditorGUI.DisabledScope(line.camera.shake == "none"))
                {
                    line.camera.shakeDuration = EditorGUILayout.Slider("晃动时长（秒）", line.camera.shakeDuration, 0.05f, 3f);
                    line.camera.shakeStrength = EditorGUILayout.Slider("晃动幅度", line.camera.shakeStrength, 1f, 80f);
                }
            }
        }

        // ---------------- 资源字段 ----------------

        private void DrawSteps(VNLine line)
        {
            if (line.steps == null) line.steps = new List<VNStageStep>();

            EditorGUILayout.LabelField("时间(秒)  备注      同一行的勾选会同时发生", EditorStyles.miniLabel);

            for (int i = 0; i < line.steps.Count; i++)
            {
                var s = line.steps[i];
                if (s == null) { line.steps[i] = s = new VNStageStep(); }

                EditorGUILayout.BeginHorizontal();

                s.at = EditorGUILayout.FloatField(s.at, GUILayout.Width(42));
                s.label = EditorGUILayout.TextField(s.label, GUILayout.Width(92));

                s.background = GUILayout.Toggle(s.background, "背景", GUILayout.Width(46));
                s.actors = GUILayout.Toggle(s.actors, "立绘", GUILayout.Width(46));
                s.camera = GUILayout.Toggle(s.camera, "镜头", GUILayout.Width(46));
                s.transition = GUILayout.Toggle(s.transition, "转场", GUILayout.Width(46));
                s.text = GUILayout.Toggle(s.text, "文字", GUILayout.Width(46));
                s.sfx = GUILayout.Toggle(s.sfx, "音效", GUILayout.Width(46));
                s.bgm = GUILayout.Toggle(s.bgm, "BGM", GUILayout.Width(52));

                if (GUILayout.Button("✕", GUILayout.Width(22)))
                {
                    PushUndo();
                    line.steps.RemoveAt(i);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("＋ 添加步骤", GUILayout.Width(110)))
            {
                PushUndo();
                line.steps.Add(new VNStageStep
                {
                    at = line.steps.Count == 0 ? 0f : line.steps[line.steps.Count - 1].at + 0.6f,
                    label = line.steps.Count == 0 ? "开场" : "下一步",
                    text = line.steps.Count == 0,
                });
            }
            if (line.steps.Count > 0 && GUILayout.Button("清空顺序表", GUILayout.Width(100)))
            {
                PushUndo();
                line.steps.Clear();
            }
            EditorGUILayout.EndHorizontal();

            if (line.steps.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "按时间从小到大执行；同一行勾多个动作 = 同时开始。\n" +
                    "没有「文字」步骤时会自动在序列结束时开始打字。",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "留空 = 按默认顺序：背景 / 立绘 / 镜头立刻应用，文字随后开始。\n\n" +
                    "示例（三级演出，适合「先演后说」）：\n" +
                    "  0.0s    转场 + BGM                 ← 先黑场\n" +
                    "  0.35s   背景 + 立绘 + 镜头 + 音效   ← 换景并推镜\n" +
                    "  1.2s    文字                       ← 最后开口说话",
                    MessageType.Info);

                if (GUILayout.Button("一键填入这个示例", GUILayout.Height(24)))
                {
                    PushUndo();
                    line.steps.Add(new VNStageStep
                    {
                        at = 0f, label = "黑场", transition = true, bgm = true,
                    });
                    line.steps.Add(new VNStageStep
                    {
                        at = 0.35f, label = "换景推镜", background = true, actors = true,
                        camera = true, sfx = true,
                    });
                    line.steps.Add(new VNStageStep
                    {
                        at = 1.2f, label = "开口", text = true,
                    });
                    _dirty = true;
                }

                EditorGUILayout.LabelField(
                    "提示：这些动作要生效，这一行本身得配好背景 / 立绘 / 镜头 / 转场 / 音效。",
                    EditorStyles.miniLabel);
            }
        }

        private void DrawBranch(VNLine line)
        {
            line.effects = EditorGUILayout.TextField("经过时效果", line.effects);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("", GUILayout.Width(88));
            if (GUILayout.Button("affection += 1", GUILayout.Width(120))) line.effects = "affection += 1";
            if (GUILayout.Button("flag = 1", GUILayout.Width(90))) line.effects = "flag_met = 1";
            if (GUILayout.Button("visit(节点)", GUILayout.Width(110))) line.effects = "visit(node_id)";
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);
            EditorGUILayout.LabelField("播完这行后自动跳转（一般不用填）", EditorStyles.miniBoldLabel);
            line.gotoNode = EditorGUILayout.TextField("跳转到节点", line.gotoNode);
            line.gotoLine = EditorGUILayout.IntField("跳转到行号（-1 = 不跳）", line.gotoLine);

            GUILayout.Space(6);
            EditorGUILayout.LabelField("选项（有选项时必须选择才能继续）", EditorStyles.boldLabel);
            if (line.choices == null) line.choices = new List<VNChoice>();

            for (int i = 0; i < line.choices.Count; i++)
            {
                var c = line.choices[i];
                if (c == null) { line.choices[i] = c = new VNChoice(); }

                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();
                c.text = EditorGUILayout.TextField($"选项 {i + 1}", c.text);
                if (GUILayout.Button("✕", GUILayout.Width(24)))
                {
                    PushUndo();
                    line.choices.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                c.targetNode = EditorGUILayout.TextField("选完后跳到节点", c.targetNode);
                c.targetLine = EditorGUILayout.IntField("或跳到行号（-1 = 不用）", c.targetLine);
                c.condition = EditorGUILayout.TextField("出现条件（留空=总是出现）", c.condition);
                c.effects = EditorGUILayout.TextField("选完后的效果", c.effects);

                EditorGUILayout.BeginHorizontal();
                c.showWhenLocked = EditorGUILayout.Toggle("条件不足时也显示", c.showWhenLocked, GUILayout.Width(180));
                if (c.showWhenLocked)
                    c.lockedText = EditorGUILayout.TextField("显示为", c.lockedText);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("", GUILayout.Width(88));
                if (GUILayout.Button("条件 +affection", GUILayout.Width(130))) c.condition = "affection >= 3";
                if (GUILayout.Button("条件 !visited", GUILayout.Width(130))) c.condition = "!visited(node_id)";
                if (GUILayout.Button("效果 +affection", GUILayout.Width(130))) c.effects = "affection += 1";
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("＋ 添加选项"))
            {
                PushUndo();
                line.choices.Add(new VNChoice { text = "选项" });
                _dirty = true;
            }
            EditorGUILayout.EndHorizontal();
        }

        private List<string> CollectNodeIds()
        {
            var list = new List<string>();
            if (_script == null || _script.lines == null) return list;
            foreach (var l in _script.lines)
            {
                if (l == null || string.IsNullOrWhiteSpace(l.nodeId)) continue;
                if (!list.Contains(l.nodeId)) list.Add(l.nodeId);
            }
            return list;
        }

        private static string StageTimingPopup(string value)
        {
            int idx = (value ?? "").Trim().ToLowerInvariant() == "wait" ? 1 : 0;

            idx = EditorGUILayout.Popup("舞台与文字", idx, new[]
            {
                "同时开始（默认）", "等舞台播完再打字（先演后说）"
            });

            return idx == 1 ? "wait" : "parallel";
        }

        private static string BgmActionPopup(string value)
        {
            int idx;
            switch ((value ?? "").Trim().ToLowerInvariant())
            {
                case "stop": idx = 1; break;
                case "pitch": idx = 2; break;
                case "restart": idx = 3; break;
                default: idx = 0; break;
            }

            idx = EditorGUILayout.Popup("BGM 指令", idx, new[]
            {
                "保持当前（不变）", "停止 BGM", "只改变音调", "重新播放"
            });

            switch (idx)
            {
                case 1: return "stop";
                case 2: return "pitch";
                case 3: return "restart";
                default: return "keep";
            }
        }

        /// <summary>高亮人物：用中文说明，存的值仍是 left/center/right。</summary>
        private static string SpeakerSlotPopup(string value)
        {
            int idx;
            switch ((value ?? "").Trim().ToLowerInvariant())
            {
                case "left": idx = 1; break;
                case "right": idx = 3; break;
                case "center": idx = 2; break;
                default: idx = 0; break;
            }

            idx = EditorGUILayout.Popup("高亮人物", idx, new[]
            {
                "（不压暗任何人）", "左边角色（left）", "中间角色（center）", "右边角色（right）"
            });

            switch (idx)
            {
                case 1: return "left";
                case 2: return "center";
                case 3: return "right";
                default: return "";
            }
        }

        private static string TransitionPopup(string value)
        {
            int idx;
            switch ((value ?? "").Trim().ToLowerInvariant())
            {
                case "fadeblack": idx = 1; break;
                case "fadewhite": idx = 2; break;
                case "flash": idx = 3; break;
                default: idx = 0; break;
            }

            idx = EditorGUILayout.Popup("转场效果", idx, new[]
            {
                "无", "黑场淡入（fadeBlack）", "白场淡入（fadeWhite）", "白闪（flash）"
            });

            switch (idx)
            {
                case 1: return "fadeBlack";
                case 2: return "fadeWhite";
                case 3: return "flash";
                default: return "none";
            }
        }

        private string _playerDefaultVoice = "";

        private void SyncPlayerDefaults()
        {
            var player = Object.FindFirstObjectByType<VNPlayer>();
            _playerDefaultVoice = player != null ? (player.defaultVoice ?? "") : "";
        }

        private void ApplyDefaultVoice(string value)
        {
            var player = Object.FindFirstObjectByType<VNPlayer>();
            if (player == null)
            {
                EditorUtility.DisplayDialog("找不到 VNPlayer",
                    "当前场景里没有 VNPlayer，无法设置默认说话音。先打开 VN 场景或用「VN → 构建场景」。", "好");
                return;
            }

            UnityEditor.Undo.RecordObject(player, "Set Default Voice");
            player.defaultVoice = value ?? "";
            EditorUtility.SetDirty(player);
            _playerDefaultVoice = player.defaultVoice;
        }

        private void DrawFontPopup(string label, string current, System.Action<string> apply)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(56));

            current = current ?? "";
            var display = string.IsNullOrWhiteSpace(current) ? "（默认字体）" : VNAssetLoader.ShortName(current);

            if (GUILayout.Button(display, EditorStyles.popup))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("（默认字体）"), string.IsNullOrEmpty(current), () =>
                {
                    apply("");
                    _dirty = true;
                    Repaint();
                });

                foreach (var opt in _fontOptions)
                {
                    var captured = opt;
                    menu.AddItem(new GUIContent(VNAssetLoader.ShortName(captured)), current == captured, () =>
                    {
                        apply(captured);
                        _dirty = true;
                        Repaint();
                    });
                }

                menu.ShowAsContext();
            }

            var obj = AssetDatabase.LoadAssetAtPath<Font>(current);
            var picked = EditorGUILayout.ObjectField(obj, typeof(Font), false, GUILayout.Width(120));
            if (picked != null)
            {
                var newPath = AssetDatabase.GetAssetPath(picked);
                if (!string.IsNullOrEmpty(newPath) && newPath != current)
                {
                    apply(newPath);
                    _dirty = true;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAssetPopup(string label, string current, List<string> options, System.Action<string> apply)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(56));

            current = current ?? "";
            var display = string.IsNullOrWhiteSpace(current) ? "(无)" : VNAssetLoader.ShortName(current);
            if (GUILayout.Button(display, EditorStyles.popup))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("(无)"), string.IsNullOrEmpty(current), () =>
                {
                    apply("");
                    _dirty = true;
                    Repaint();
                });

                foreach (var opt in options)
                {
                    var captured = opt;
                    menu.AddItem(new GUIContent(VNAssetLoader.ShortName(captured)), current == captured, () =>
                    {
                        apply(captured);
                        _dirty = true;
                        Repaint();
                    });
                }

                menu.ShowAsContext();
            }

            var obj = AssetDatabase.LoadAssetAtPath<Object>(current);
            var picked = EditorGUILayout.ObjectField(obj, typeof(Object), false, GUILayout.Width(120));
            if (picked != null)
            {
                var newPath = AssetDatabase.GetAssetPath(picked);
                if (!string.IsNullOrEmpty(newPath) && newPath != current)
                {
                    apply(newPath);
                    _dirty = true;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private static string Popup(string label, string value, string[] options, float labelWidth = -1f)
        {
            if (labelWidth > 0f)
            {
                var old = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = labelWidth;
                var result = Popup(label, value, options);
                EditorGUIUtility.labelWidth = old;
                return result;
            }

            if (string.IsNullOrWhiteSpace(value)) value = options[0];
            int index = System.Array.IndexOf(options, value.ToLowerInvariant());
            if (index < 0) index = 0;
            index = EditorGUILayout.Popup(label, index, options);
            return options[index];
        }

        // ---------------- 播放 / 预览 ----------------

        /// <summary>不进 Play 模式，直接把选中行应用到场景里的 UI，在 Game 视图里看效果。</summary>
        private void PreviewSelectedInEditor()
        {
            if (_script == null) return;
            Save();

            var player = Object.FindFirstObjectByType<VNPlayer>();
            if (player == null)
            {
                EditorUtility.DisplayDialog("找不到播放器",
                    "当前场景里没有 VNPlayer。\n\n请先打开 Assets/VN/Scenes/VN.unity，或用「VN → 构建场景」生成。", "好");
                return;
            }

            player.scriptAssetPath = _path;
            player.PreviewInEditor(Mathf.Max(0, _selected));

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            SceneView.RepaintAll();
        }

        private void PlayFromSelected()
        {
            if (_script == null) return;
            Save();

            var player = Object.FindFirstObjectByType<VNPlayer>();
            if (player == null)
            {
                if (!EditorUtility.DisplayDialog("找不到播放器",
                        "当前场景里没有 VNPlayer。\n\n是否现在构建 VN 场景？", "构建场景", "取消"))
                    return;
                VNSceneBuilder.BuildScene();
                return;
            }

            player.scriptAssetPath = _path;
            player.startLineIndex = Mathf.Max(0, _selected);
            player.LoadScript(_path);

            var scene = player.gameObject.scene;
            if (scene.IsValid() && !EditorApplication.isPlaying)
                EditorSceneManager.SaveScene(scene);

            EditorApplication.isPlaying = true;
        }

        private VNLine CurrentLine()
        {
            if (_script == null || _selected < 0 || _selected >= _script.lines.Count) return new VNLine();
            return _script.lines[_selected];
        }

        private static VNLine CloneOf(VNLine src)
        {
            if (src == null) return new VNLine();
            return JsonUtility.FromJson<VNLine>(JsonUtility.ToJson(src)) ?? new VNLine();
        }

        private static string ToProjectPath(string absolute)
        {
            var full = Path.GetFullPath(absolute).Replace('\\', '/');
            var project = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            var root = project.Substring(0, project.Length - "Assets".Length);
            if (!full.StartsWith(root, System.StringComparison.OrdinalIgnoreCase)) return null;
            return full.Substring(root.Length);
        }
    }

    internal static class VNColorExtensions
    {
        public static string ToHex(this Color c)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(c);
        }
    }
}
