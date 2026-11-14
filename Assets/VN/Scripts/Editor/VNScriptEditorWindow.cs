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
        private const string BgFolder = "Assets/Arts/Background";
        private const string CharFolder = "Assets/Arts/Characters";
        private const string BgmFolder = "Assets/Sounds/Music";
        private const string SfxFolder = "Assets/Sounds/SFX";

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

        private List<string> _bgOptions = new List<string>();
        private List<string> _charOptions = new List<string>();
        private List<string> _bgmOptions = new List<string>();
        private List<string> _sfxOptions = new List<string>();

        private static readonly string[] Motions = { "fade", "none", "slideUp", "slideSide", "bounce", "pop" };
        private static readonly string[] Effects = { "none", "shake", "wave", "pulse", "glitch" };
        private static readonly string[] ShakeModes = { "none", "light", "medium", "strong" };

        [MenuItem("VN/剧本编辑器 %#e", priority = 0)]
        public static void Open()
        {
            var win = GetWindow<VNScriptEditorWindow>("VN 剧本");
            win.minSize = new Vector2(1080f, 720f);

            // 首次打开给个够大的窗口；之后完全交给用户，不再硬改尺寸
            if (!SessionState.GetBool(SizedKey, false))
            {
                SessionState.SetBool(SizedKey, true);
                win.position = new Rect(win.position.x, win.position.y,
                    Mathf.Max(win.position.width, 1180f),
                    Mathf.Max(win.position.height, 840f));
            }

            win.TryAutoLoad();
        }

        private void OnEnable()
        {
            RefreshAssetLists();
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

            // 兼容把素材放进 Resources 的情况
            foreach (var p in Scan("Assets/VN/Resources/VN/BG", "t:Texture2D"))
                if (!_bgOptions.Contains(p)) _bgOptions.Add(p);
            foreach (var p in Scan("Assets/VN/Resources/VN/Char", "t:Texture2D"))
                if (!_charOptions.Contains(p)) _charOptions.Add(p);
            foreach (var p in Scan("Assets/VN/Resources/VN/Audio/BGM", "t:AudioClip"))
                if (!_bgmOptions.Contains(p)) _bgmOptions.Add(p);
            foreach (var p in Scan("Assets/VN/Resources/VN/Audio/SFX", "t:AudioClip"))
                if (!_sfxOptions.Contains(p)) _sfxOptions.Add(p);
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

        private void OnGUI()
        {
            // 标签别占太宽，把空间留给控件
            EditorGUIUtility.labelWidth = 88f;

            DrawToolbar();
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
                        _selected = i;
                        _detailScroll = Vector2.zero;
                    }
                }
            }

            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("＋ 新行"))
            {
                _script.lines.Insert(_selected + 1 <= 0 ? _script.lines.Count : _selected + 1, CloneOf(CurrentLine()));
                _selected = Mathf.Clamp(_selected + 1, 0, _script.lines.Count - 1);
                _dirty = true;
            }

            using (new EditorGUI.DisabledScope(_selected < 0))
            {
                if (GUILayout.Button("⧉ 复制"))
                {
                    _script.lines.Insert(_selected + 1, CloneOf(CurrentLine()));
                    _selected++;
                    _dirty = true;
                }
                if (GUILayout.Button("✕ 删除"))
                {
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
                    (_script.lines[_selected - 1], _script.lines[_selected]) = (_script.lines[_selected], _script.lines[_selected - 1]);
                    _selected--;
                    _dirty = true;
                }
            }
            using (new EditorGUI.DisabledScope(_selected < 0 || _selected >= _script.lines.Count - 1))
            {
                if (GUILayout.Button("↓ 下移"))
                {
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

                var line = _script.lines[_selected];
                EditorGUI.BeginChangeCheck();

                EditorGUILayout.LabelField($"第 {_selected + 1} 行", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                line.name = EditorGUILayout.TextField("说话人", line.name);
                line.autoDelay = EditorGUILayout.FloatField("自动等待", line.autoDelay, GUILayout.Width(96));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField("台词");
                line.text = EditorGUILayout.TextArea(line.text, GUILayout.MinHeight(56));
                line.note = EditorGUILayout.TextField("备注", line.note);

                GUILayout.Space(6);
                _stageOpen = EditorGUILayout.BeginFoldoutHeaderGroup(_stageOpen, "舞台 / 音频");
                if (_stageOpen)
                {
                    DrawAssetPopup("背景", line.background, _bgOptions, v => { line.background = v; _dirty = true; });
                    line.backgroundFade = EditorGUILayout.Slider("背景淡入（秒）", line.backgroundFade, 0.01f, 3f);

                    DrawAssetPopup("BGM", line.bgm, _bgmOptions, v => { line.bgm = v; _dirty = true; });
                    line.bgmVolume = EditorGUILayout.Slider("BGM 音量", line.bgmVolume, 0f, 1f);

                    DrawAssetPopup("音效", line.sfx, _sfxOptions, v => { line.sfx = v; _dirty = true; });
                    line.sfxVolume = EditorGUILayout.Slider("音效音量", line.sfxVolume, 0f, 1f);
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
                _cameraOpen = EditorGUILayout.BeginFoldoutHeaderGroup(_cameraOpen, "镜头（只影响背景和立绘）");
                if (_cameraOpen) DrawCamera(line);
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
                    a.motion = Popup("入场", a.motion, Motions, 80f);
                    a.fadeDuration = EditorGUILayout.FloatField("时长", a.fadeDuration, GUILayout.Width(64));
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    a.offsetX = EditorGUILayout.FloatField("X", a.offsetX, GUILayout.Width(74));
                    a.offsetY = EditorGUILayout.FloatField("Y", a.offsetY, GUILayout.Width(74));
                    a.scale = EditorGUILayout.FloatField("缩放", a.scale, GUILayout.Width(86));
                    EditorGUILayout.EndHorizontal();
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
        {
            line.useCamera = EditorGUILayout.Toggle("启用镜头", line.useCamera);
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
