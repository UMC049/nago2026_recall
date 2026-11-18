using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VN.EditorTools
{
    /// <summary>
    /// 角色卡可视化编辑：配一次角色（立绘 / 语音 / 字体），剧本里选角色就自动带上。
    /// </summary>
    public class VNCharacterEditorWindow : EditorWindow
    {
        private const string CharacterPath = "Assets/VN/Resources/VN/Data/characters.json";
        private const string PortraitFolder = "Assets/VN/Resources/VN/Char";
        private const string VoiceFolder = "Assets/VN/Resources/VN/Audio";
        private const string FontFolder = "Assets/VN/Fonts";

        private VNCharacterSet _set;
        private int _selected;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private bool _dirty;

        private List<string> _portraitOptions = new List<string>();
        private List<string> _voiceOptions = new List<string>();
        private List<string> _fontOptions = new List<string>();

        [MenuItem("VN/角色卡", false, 31)]
        public static void Open()
        {
            var win = GetWindow<VNCharacterEditorWindow>(false, "VN 角色卡", true);
            win.minSize = new Vector2(860, 480);
            win.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("VN 角色卡");
            Load();
            RefreshAssets();

            // 打开窗口时先扫一遍：有没有剧本引用了已删除的角色
            SyncChapters(false);
        }

        private void Load()
        {
            var text = VNAssetLoader.LoadText(CharacterPath);
            _set = text != null ? JsonUtility.FromJson<VNCharacterSet>(text.text) : null;
            if (_set == null) _set = new VNCharacterSet();
            if (_set.characters == null) _set.characters = new List<VNCharacter>();

            _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, _set.characters.Count - 1));
            _dirty = false;
        }

        private void Save()
        {
            try
            {
                var json = JsonUtility.ToJson(_set, true);
                var dir = Path.GetDirectoryName(CharacterPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                File.WriteAllText(CharacterPath, json, new System.Text.UTF8Encoding(false));
                AssetDatabase.Refresh();
                _dirty = false;

                Debug.Log("[VN] 角色卡已保存：" + CharacterPath);

                SyncChapters();
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[VN] 保存角色卡失败：" + ex.Message);
                EditorUtility.DisplayDialog("保存失败", ex.Message, "好");
            }
        }

        /// <summary>按 id 在内存中的角色卡里找角色。</summary>
        private VNCharacter FindInSet(string id)
        {
            if (_set == null || _set.characters == null) return null;
            var key = (id ?? "").Trim();
            if (key.Length == 0) return null;

            foreach (var c in _set.characters)
            {
                if (c != null && c.id == key) return c;
            }
            return null;
        }

        /// <summary>
        /// 把角色卡的立绘同步到所有剧本：谁的 character 字段指向这个角色，
        /// 就按角色卡当前有没有立绘来更新或移除对应的 actors 条目。
        /// </summary>
        private void SyncChapters(bool applyChanges = true)
        {
            var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/VN/Resources/VN/Data" });

            int updated = 0;
            int removed = 0;
            int fileCount = 0;
            var missing = new List<string>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".json")) continue;
                if (path.EndsWith("characters.json")) continue;

                string raw;
                try
                {
                    raw = File.ReadAllText(path);
                }
                catch
                {
                    continue;
                }

                raw = raw.TrimStart('\uFEFF');

                VNScript script;
                try
                {
                    script = JsonUtility.FromJson<VNScript>(raw);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[VN] 跳过无法解析的剧本 {Path.GetFileName(path)}：{ex.Message}");
                    continue;
                }

                if (script == null || script.lines == null) continue;

                bool changed = false;

                foreach (var line in script.lines)
                {
                    if (line == null || line.actors == null) continue;

                    for (int i = line.actors.Count - 1; i >= 0; i--)
                    {
                        var a = line.actors[i];
                        if (a == null || string.IsNullOrWhiteSpace(a.character)) continue;

                        var card = FindInSet(a.character);
                        if (card == null)
                        {
                            var tag = a.character + "（" + Path.GetFileName(path) + "）";
                            if (!missing.Contains(tag)) missing.Add(tag);
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(card.portrait))
                        {
                            // 角色卡里已经没有立绘了 → 从剧本里移除这条
                            if (!applyChanges) continue;
                            line.actors.RemoveAt(i);
                            removed++;
                            changed = true;
                        }
                        else if (a.sprite != card.portrait)
                        {
                            if (!applyChanges) continue;
                            a.sprite = card.portrait;
                            updated++;
                            changed = true;
                        }
                    }
                }

                if (!changed) continue;

                if (!applyChanges) continue;

                File.WriteAllText(path, JsonUtility.ToJson(script, true), new System.Text.UTF8Encoding(false));
                fileCount++;
            }

            AssetDatabase.Refresh();

            if (updated > 0 || removed > 0)
            {
                _syncMessage = $"已同步 {fileCount} 个剧本：更新 {updated} 处立绘，移除 {removed} 处（角色卡已无立绘）";
                _syncType = MessageType.Info;
                Debug.Log("[VN] " + _syncMessage);
            }
            else
            {
                _syncMessage = null;
            }

            _missingRefs = missing;
        }

        private string _syncMessage;
        private MessageType _syncType = MessageType.Info;
        private List<string> _missingRefs = new List<string>();

        private void RefreshAssets()
        {
            _portraitOptions = Scan(PortraitFolder, "t:texture2d");
            _voiceOptions = Scan(VoiceFolder, "t:audioclip");
            _fontOptions = Scan(FontFolder, "t:Font");
        }

        private static List<string> Scan(string folder, string filter)
        {
            var result = new List<string>();
            if (!AssetDatabase.IsValidFolder(folder)) return result;

            var guids = AssetDatabase.FindAssets(filter, new[] { folder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path)) result.Add(path);
            }

            result.Sort(System.StringComparer.OrdinalIgnoreCase);
            return result;
        }

        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.BeginHorizontal();

            DrawList();

            EditorGUILayout.Space(4);

            DrawDetail();

            EditorGUILayout.EndHorizontal();

            DrawFooter();
        }

        private void DrawFooter()
        {
            if (!string.IsNullOrEmpty(_syncMessage))
                EditorGUILayout.HelpBox(_syncMessage, _syncType);

            if (_missingRefs != null && _missingRefs.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "⚠ 有 " + _missingRefs.Count + " 处剧本引用了角色卡里已经不存在的角色：\n" +
                    string.Join("\n", _missingRefs.ToArray()),
                    MessageType.Warning);
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("＋ 新建角色", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                _set.characters.Add(new VNCharacter
                {
                    id = "char" + (_set.characters.Count + 1),
                    displayName = "新角色",
                    voiceMode = "char",
                });
                _selected = _set.characters.Count - 1;
                _dirty = true;
            }

            using (new EditorGUI.DisabledScope(!_dirty))
            {
                if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(60))) Save();
            }

            if (GUILayout.Button("重新加载", EditorStyles.toolbarButton, GUILayout.Width(76)))
            {
                if (_dirty && !EditorUtility.DisplayDialog("放弃修改？", "有未保存的改动，确定重新加载吗？", "重新加载", "取消"))
                    return;
                Load();
            }

            if (GUILayout.Button("↻ 素材", EditorStyles.toolbarButton, GUILayout.Width(66))) RefreshAssets();

            GUILayout.FlexibleSpace();

            GUILayout.Label(CharacterPath + (_dirty ? "  ● 未保存" : ""), EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(220));

            GUILayout.Label($"角色（{_set.characters.Count}）", EditorStyles.boldLabel);

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));

            for (int i = 0; i < _set.characters.Count; i++)
            {
                var c = _set.characters[i];
                if (c == null) continue;

                var label = string.IsNullOrWhiteSpace(c.displayName) ? c.id : c.displayName;
                if (!string.IsNullOrWhiteSpace(c.id)) label += "（" + c.id + "）";

                var style = i == _selected ? EditorStyles.toolbarButton : GUI.skin.button;

                var rect = GUILayoutUtility.GetRect(new GUIContent(label), style, GUILayout.Height(26));

                var prev = GUI.backgroundColor;
                if ((c.voiceMode ?? "").ToLowerInvariant() == "none")
                    GUI.backgroundColor = new Color(0.85f, 0.85f, 0.9f);

                if (GUI.Button(rect, label, style))
                {
                    _selected = i;
                    _detailScroll = Vector2.zero;
                }

                GUI.backgroundColor = prev;
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(_set.characters.Count == 0))
            {
                if (GUILayout.Button("↑", GUILayout.Width(30)))
                {
                    if (_selected > 0)
                    {
                        (_set.characters[_selected - 1], _set.characters[_selected]) =
                            (_set.characters[_selected], _set.characters[_selected - 1]);
                        _selected--;
                        _dirty = true;
                    }
                }
                if (GUILayout.Button("↓", GUILayout.Width(30)))
                {
                    if (_selected < _set.characters.Count - 1)
                    {
                        (_set.characters[_selected + 1], _set.characters[_selected]) =
                            (_set.characters[_selected], _set.characters[_selected + 1]);
                        _selected++;
                        _dirty = true;
                    }
                }
                if (GUILayout.Button("✕ 删除", GUILayout.Width(70)))
                {
                    if (EditorUtility.DisplayDialog("删除角色", "确定删除「" + _set.characters[_selected].displayName + "」？", "删除", "取消"))
                    {
                        _set.characters.RemoveAt(_selected);
                        _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, _set.characters.Count - 1));
                        _dirty = true;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawDetail()
        {
            if (_set.characters.Count == 0)
            {
                EditorGUILayout.HelpBox("还没有角色，点左上角「＋ 新建角色」。", MessageType.Info);
                return;
            }

            _selected = Mathf.Clamp(_selected, 0, _set.characters.Count - 1);
            var c = _set.characters[_selected];
            if (c == null)
            {
                _set.characters[_selected] = c = new VNCharacter();
            }

            EditorGUILayout.BeginVertical();

            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll, GUILayout.ExpandHeight(true));

            EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(c.displayName) ? c.id : c.displayName, EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            c.id = EditorGUILayout.TextField("id（内部）", c.id);
            c.displayName = EditorGUILayout.TextField("显示名（说话人）", c.displayName);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("显示名要和剧本里写的说话人一致，例：凛 / 薇 / 我。", MessageType.None);

            GUILayout.Space(6);

            EditorGUILayout.LabelField("立绘", EditorStyles.boldLabel);

            c.portraitEnabled = EditorGUILayout.Toggle("在剧本里自动出图", c.portraitEnabled);

            using (new EditorGUI.DisabledScope(!c.portraitEnabled))
            {
                DrawAssetPopup("默认立绘", c.portrait, _portraitOptions, v => { c.portrait = v; _dirty = true; });

                c.portraitSlot = Popup("立绘位置", string.IsNullOrWhiteSpace(c.portraitSlot) ? "center" : c.portraitSlot,
                    new[] { "left", "center", "right" }, 70f);
            }

            if (!c.portraitEnabled)
            {
                EditorGUILayout.HelpBox("关掉后：这个角色说话时不出立绘（只闻其声 / 结局段用）。", MessageType.None);
            }
            else if (string.IsNullOrWhiteSpace(c.portrait))
            {
                EditorGUILayout.HelpBox("还没选立绘，运行时会回退到剧本行里的 actors 配置。", MessageType.None);
            }

            if (!string.IsNullOrWhiteSpace(c.portrait))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(c.portrait);
                if (tex != null)
                {
                    var preview = AssetPreview.GetAssetPreview(tex);
                    if (preview != null)
                    {
                        float h = 160f;
                        float w = h * preview.width / Mathf.Max(1f, preview.height);
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(preview, GUILayout.Width(w), GUILayout.Height(h));
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                    }
                }
                EditorGUILayout.LabelField("留空 = 这个角色不出立绘（例如第一人称「我」）", EditorStyles.miniLabel);
            }

            GUILayout.Space(6);

            EditorGUILayout.LabelField("说话音", EditorStyles.boldLabel);
            DrawAssetPopup("语音文件", c.voice, _voiceOptions, v => { c.voice = v; _dirty = true; });

            c.voiceMode = Popup("模式", string.IsNullOrWhiteSpace(c.voiceMode) ? "char" : c.voiceMode,
                new[] { "char", "loop", "none" }, 60f);

            if ((c.voiceMode ?? "").ToLowerInvariant() == "none")
            {
                EditorGUILayout.HelpBox("none = 这个角色永远不出声（第一人称 / 旁白型角色用这个）。", MessageType.None);
            }
            else
            {
                c.voiceVolume = EditorGUILayout.Slider("音量", c.voiceVolume, 0f, 1f);
                c.voicePitchRandom = EditorGUILayout.Slider("音调抖动", c.voicePitchRandom, 0f, 0.3f);
                c.voiceEveryNChars = EditorGUILayout.IntSlider("每隔几个字", Mathf.Max(1, c.voiceEveryNChars), 1, 6);
            }

            GUILayout.Space(6);

            EditorGUILayout.LabelField("文字", EditorStyles.boldLabel);
            DrawFontPopup("字体", c.font, v => { c.font = v; _dirty = true; });

            GUILayout.Space(6);

            c.note = EditorGUILayout.TextArea(c.note ?? "", GUILayout.MinHeight(48));

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            if (Event.current.type == EventType.Used) Repaint();
        }

        private static string Popup(string label, string value, string[] options, float width)
        {
            int idx = System.Array.IndexOf(options, value);
            if (idx < 0) idx = 0;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(64));
            idx = EditorGUILayout.Popup(idx, options, GUILayout.Width(width));
            EditorGUILayout.EndHorizontal();

            return options[Mathf.Clamp(idx, 0, options.Length - 1)];
        }

        private void DrawAssetPopup(string label, string current, List<string> options, System.Action<string> apply)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(64));

            current = current ?? "";
            var display = string.IsNullOrWhiteSpace(current) ? "（无）" : VNAssetLoader.ShortName(current);

            if (GUILayout.Button(display, EditorStyles.popup))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("（无）"), string.IsNullOrEmpty(current), () =>
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
            var picked = EditorGUILayout.ObjectField(obj, typeof(Object), false, GUILayout.Width(140));
            if (picked != null)
            {
                var path = AssetDatabase.GetAssetPath(picked);
                if (!string.IsNullOrEmpty(path) && path != current)
                {
                    apply(path);
                    _dirty = true;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFontPopup(string label, string current, System.Action<string> apply)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(64));

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
            var picked = EditorGUILayout.ObjectField(obj, typeof(Font), false, GUILayout.Width(140));
            if (picked != null)
            {
                var path = AssetDatabase.GetAssetPath(picked);
                if (!string.IsNullOrEmpty(path) && path != current)
                {
                    apply(path);
                    _dirty = true;
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
