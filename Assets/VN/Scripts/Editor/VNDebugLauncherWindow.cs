using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VN.EditorTools
{
    /// <summary>
    /// 调试启动器：选项目 + 选章节，直接进 Play。
    /// 项目之间默认隔离，跨项目跳转要显式勾选。
    /// </summary>
    public class VNDebugLauncherWindow : EditorWindow
    {
        private const string DataRoot = "Assets/VN/Resources";
        private const string PrefProject = "VN.Debug.Project";
        private const string PrefChapter = "VN.Debug.Chapter";
        private const string PrefSkipTitle = "VN.Debug.SkipTitle";
        private const string PrefAllowCross = "VN.Debug.AllowCross";
        private const string PrefCrossProject = "VN.Debug.Cross.Project";
        private const string PrefCrossChapter = "VN.Debug.Cross.Chapter";
        private const string PrefCrossLineMode = "VN.Debug.Cross.LineMode";
        private const string PrefCrossLine = "VN.Debug.Cross.Line";

        private string _project = "Recall";
        private string _chapter = "story01.json";
        private int _startLine;
        private bool _skipTitle = true;
        private bool _allowCross;

        // 跨项目跳转的目标
        private string _crossProject = "";
        private string _crossChapter = "";
        private int _crossLineMode;     // 0 = 第一行，1 = 指定行，2 = 随机行
        private int _crossLine;

        private Vector2 _scroll;
        private List<string> _projects = new List<string>();
        private List<string> _chapters = new List<string>();
        private List<string> _crossProjects = new List<string>();
        private List<string> _crossChapters = new List<string>();

        [MenuItem("VN/调试启动器", false, 2)]
        public static void Open()
        {
            var win = GetWindow<VNDebugLauncherWindow>(false, "VN 调试", true);
            win.minSize = new Vector2(400, 300);
            win.maxSize = new Vector2(560, 620);
            win.Show();
        }

        private void OnEnable()
        {
            _project = EditorPrefs.GetString(PrefProject, "Recall");
            _chapter = EditorPrefs.GetString(PrefChapter, "story01.json");
            _skipTitle = EditorPrefs.GetBool(PrefSkipTitle, true);
            _allowCross = EditorPrefs.GetBool(PrefAllowCross, false);

            _crossProject = EditorPrefs.GetString(PrefCrossProject, "");
            _crossChapter = EditorPrefs.GetString(PrefCrossChapter, "");
            _crossLineMode = EditorPrefs.GetInt(PrefCrossLineMode, 0);
            _crossLine = EditorPrefs.GetInt(PrefCrossLine, 0);

            RefreshProjects();
            RefreshChapters();
            RefreshCrossTargets();
        }

        private void RefreshProjects()
        {
            _projects.Clear();
            if (!AssetDatabase.IsValidFolder(DataRoot)) return;

            foreach (var guid in AssetDatabase.FindAssets("t:Folder", new[] { DataRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;

                var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
                if (parent != DataRoot) continue;

                var name = System.IO.Path.GetFileName(path);
                if (!string.IsNullOrEmpty(name)) _projects.Add(name);
            }

            _projects.Sort(System.StringComparer.OrdinalIgnoreCase);

            if (_projects.Count > 0 && !_projects.Contains(_project))
                _project = _projects[0];
        }

        private void RefreshChapters()
        {
            _chapters.Clear();

            var dir = DataRoot + "/" + _project + "/Data";
            if (!AssetDatabase.IsValidFolder(dir)) return;

            foreach (var guid in AssetDatabase.FindAssets("t:TextAsset", new[] { dir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".json")) continue;
                if (path.EndsWith("characters.json")) continue;

                _chapters.Add(System.IO.Path.GetFileName(path));
            }

            _chapters.Sort(System.StringComparer.OrdinalIgnoreCase);

            if (_chapters.Count > 0 && !_chapters.Contains(_chapter))
                _chapter = _chapters[0];
        }

        /// <summary>目标项目列表（排除当前项目） + 目标章节列表。</summary>
        private void RefreshCrossTargets()
        {
            _crossProjects.Clear();

            foreach (var p in _projects)
            {
                if (p != _project) _crossProjects.Add(p);
            }

            if (_crossProjects.Count > 0 && !_crossProjects.Contains(_crossProject))
                _crossProject = _crossProjects[0];

            RefreshCrossChapters();
        }

        private void RefreshCrossChapters()
        {
            _crossChapters.Clear();

            if (string.IsNullOrEmpty(_crossProject)) return;

            var dir = DataRoot + "/" + _crossProject + "/Data";
            if (!AssetDatabase.IsValidFolder(dir)) return;

            foreach (var guid in AssetDatabase.FindAssets("t:TextAsset", new[] { dir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".json")) continue;
                if (path.EndsWith("characters.json")) continue;

                _crossChapters.Add(System.IO.Path.GetFileName(path));
            }

            _crossChapters.Sort(System.StringComparer.OrdinalIgnoreCase);

            if (_crossChapters.Count > 0 && !_crossChapters.Contains(_crossChapter))
                _crossChapter = _crossChapters[0];
        }

        /// <summary>读某个章节有多少行（给随机行用）。</summary>
        private static int CountLines(string project, string chapterFile)
        {
            var path = DataRoot + "/" + project + "/Data/" + chapterFile;
            var text = VNAssetLoader.LoadText(path);
            if (text == null) return 0;

            try
            {
                var script = JsonUtility.FromJson<VNScript>(text.text);
                return script != null && script.lines != null ? script.lines.Count : 0;
            }
            catch
            {
                return 0;
            }
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("选择要调试的游戏项目与章节", EditorStyles.boldLabel);

            // 项目
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("项目", GUILayout.Width(44));

            var projIndex = _projects.IndexOf(_project);
            if (projIndex < 0) projIndex = 0;

            var newProj = _projects.Count > 0
                ? EditorGUILayout.Popup(projIndex, _projects.ToArray())
                : -1;

            if (newProj >= 0 && newProj != projIndex)
            {
                _project = _projects[newProj];
                RefreshChapters();
                RefreshCrossTargets();
            }

            if (GUILayout.Button("↻", GUILayout.Width(26)))
            {
                RefreshProjects();
                RefreshChapters();
            }

            EditorGUILayout.EndHorizontal();

            // 章节
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("章节", GUILayout.Width(44));

            var chIndex = _chapters.IndexOf(_chapter);
            if (chIndex < 0) chIndex = 0;

            if (_chapters.Count > 0)
            {
                var newCh = EditorGUILayout.Popup(chIndex, _chapters.ToArray());
                if (newCh != chIndex) _chapter = _chapters[newCh];
            }
            else
            {
                EditorGUILayout.LabelField("（这个项目还没有章节）");
            }

            EditorGUILayout.EndHorizontal();

            // 起始行
            _startLine = Mathf.Max(0, EditorGUILayout.IntField("从第几行开始（0 起）", _startLine));

            GUILayout.Space(6);

            _skipTitle = EditorGUILayout.Toggle("跳过主菜单，直接进剧情", _skipTitle);

            GUILayout.Space(6);

            _allowCross = EditorGUILayout.Toggle("允许跨项目跳转（meta / 彩蛋）", _allowCross);

            if (_allowCross)
            {
                EditorGUI.indentLevel++;

                if (_crossProjects.Count == 0)
                {
                    EditorGUILayout.HelpBox("没有其他项目可跳（或者只存在当前项目一个）。", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.LabelField("跳转目标（调试时直接从这里开始）", EditorStyles.miniBoldLabel);

                    // 目标项目
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("目标项目", GUILayout.Width(60));

                    var cpIndex = _crossProjects.IndexOf(_crossProject);
                    if (cpIndex < 0) cpIndex = 0;

                    var newCp = EditorGUILayout.Popup(cpIndex, _crossProjects.ToArray());
                    if (newCp != cpIndex)
                    {
                        _crossProject = _crossProjects[newCp];
                        RefreshCrossChapters();
                    }
                    EditorGUILayout.EndHorizontal();

                    // 目标章节
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("目标章节", GUILayout.Width(60));

                    if (_crossChapters.Count > 0)
                    {
                        var ccIndex = _crossChapters.IndexOf(_crossChapter);
                        if (ccIndex < 0) ccIndex = 0;

                        var newCc = EditorGUILayout.Popup(ccIndex, _crossChapters.ToArray());
                        if (newCc != ccIndex) _crossChapter = _crossChapters[newCc];
                    }
                    else
                    {
                        EditorGUILayout.LabelField("（该项目没有章节）");
                    }
                    EditorGUILayout.EndHorizontal();

                    // 目标行
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("跳转到", GUILayout.Width(60));
                    _crossLineMode = EditorGUILayout.Popup(_crossLineMode,
                        new[] { "第一行（默认）", "指定行", "随机行" }, GUILayout.Width(140));

                    if (_crossLineMode == 1)
                        _crossLine = Mathf.Max(0, EditorGUILayout.IntField(_crossLine, GUILayout.Width(70)));

                    EditorGUILayout.EndHorizontal();

                    if (_crossLineMode == 2)
                    {
                        int total = CountLines(_crossProject, _crossChapter);
                        EditorGUILayout.LabelField(
                            $"   随机范围：0 ~ {Mathf.Max(0, total - 1)}（该章共 {total} 行）",
                            EditorStyles.miniLabel);
                    }

                    EditorGUILayout.LabelField(
                        _crossLineMode == 0 ? "   启动后直接进入目标项目的第一行" :
                        _crossLineMode == 1 ? $"   启动后直接进入目标项目第 {_crossLine + 1} 行" :
                        "   每次启动随机一行（调试用）",
                        EditorStyles.miniLabel);
                }

                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "默认隔离：不同游戏的章节不会互相串。\n" +
                    "想做彩蛋/ meta 要素时，再打开上面的开关。",
                    MessageType.None);
            }

            GUILayout.Space(8);

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("▶ 开始调试", GUILayout.Height(36)))
                    Launch();
            }

            if (Application.isPlaying)
            {
                GUILayout.Space(6);
                EditorGUILayout.HelpBox("已经在 Play 模式里了，先停止再启动新的调试。", MessageType.Info);
            }

            GUILayout.Space(10);

            var player = Object.FindFirstObjectByType<VNPlayer>();
            if (player != null)
            {
                EditorGUILayout.LabelField("场景里的 VNPlayer", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("当前项目", player.projectId);
                EditorGUILayout.LabelField("起始章节", player.startChapterFile);
                EditorGUILayout.LabelField("跨项目", player.allowCrossProject ? "允许" : "隔离");
                EditorGUILayout.LabelField("实际路径", player.StartChapterPath, EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.HelpBox("当前场景没有 VNPlayer，请先打开 VN 场景。", MessageType.Warning);
            }

            EditorGUILayout.EndScrollView();
        }

        private void Launch()
        {
            var player = Object.FindFirstObjectByType<VNPlayer>();
            if (player == null)
            {
                EditorUtility.DisplayDialog("找不到 VNPlayer",
                    "请先打开 Assets/VN/Scenes/VN.unity，或用「VN → 构建场景」生成。", "好");
                return;
            }

            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("正在运行", "先停止当前 Play，再启动新的调试。", "好");
                return;
            }

            bool jumpCross = _allowCross && _crossProjects.Count > 0 && !string.IsNullOrEmpty(_crossChapter);

            if (!jumpCross && string.IsNullOrEmpty(_chapter))
            {
                EditorUtility.DisplayDialog("没有章节", "这个项目下还没有章节文件。", "好");
                return;
            }

            // 记住选择
            EditorPrefs.SetString(PrefProject, _project);
            EditorPrefs.SetString(PrefChapter, _chapter);
            EditorPrefs.SetBool(PrefSkipTitle, _skipTitle);
            EditorPrefs.SetBool(PrefAllowCross, _allowCross);
            EditorPrefs.SetString(PrefCrossProject, _crossProject);
            EditorPrefs.SetString(PrefCrossChapter, _crossChapter);
            EditorPrefs.SetInt(PrefCrossLineMode, _crossLineMode);
            EditorPrefs.SetInt(PrefCrossLine, _crossLine);

            int startLine;
            string startProject;
            string startChapter;

            if (jumpCross)
            {
                // 直接以目标项目为起点（调试跨项目那一段）
                startProject = _crossProject;
                startChapter = _crossChapter;

                if (_crossLineMode == 2)
                {
                    int total = CountLines(_crossProject, _crossChapter);
                    startLine = total > 0 ? Random.Range(0, total) : 0;
                }
                else if (_crossLineMode == 1)
                {
                    startLine = Mathf.Max(0, _crossLine);
                }
                else
                {
                    startLine = 0;
                }
            }
            else
            {
                startProject = _project;
                startChapter = _chapter;
                startLine = _startLine;
            }

            // 只改内存（不保存场景），退出 Play 后场景文件保持原样
            player.projectId = startProject;
            player.startChapterFile = startChapter;
            player.allowCrossProject = _allowCross;

            if (_skipTitle)
            {
                SessionState.SetString("VN.TestScriptPath", player.StartChapterPath);
                SessionState.SetInt("VN.TestStartLine", startLine);
            }
            else
            {
                SessionState.EraseString("VN.TestScriptPath");
                SessionState.EraseInt("VN.TestStartLine");
            }

            Debug.Log($"[VN] 调试启动：{startProject}/{startChapter}，从第 {startLine} 行开始" +
                      (jumpCross ? $"，跨项目={_crossLineMode}（源：{_project}/{_chapter}）" : "") +
                      $"，跳过主菜单={_skipTitle}");

            EditorApplication.isPlaying = true;
        }
    }
}
