using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VN
{
    /// <summary>
    /// 视觉小说播放器：读取剧本 JSON，驱动背景 / 立绘 / 台词 / 音频 / 文字演出。
    ///
    /// 操作：
    ///   左键 / 空格 / 回车  —— 推进（文字播放中则立即播完）
    ///   按住 Ctrl           —— 加速文字
    ///   A                   —— 开关自动播放
    ///   R                   —— 重新从本行开始
    ///   N                   —— 跳到下一行（调试用）
    /// </summary>
    [AddComponentMenu("VN/VN Player")]
    public class VNPlayer : MonoBehaviour
    {
        [Header("剧本")]
        [Tooltip("剧本 JSON 的工程路径，例如 Assets/VN/Resources/VN/Data/story01.json")]
        public string scriptAssetPath = "Assets/VN/Resources/VN/Data/story01.json";
        public bool playOnStart = true;

        [Tooltip("「新游戏」从头开始的剧本。和 scriptAssetPath 分开，避免编辑器调试时被改掉")]
        public string startChapter = "Assets/VN/Resources/VN/Data/story01.json";

        [Tooltip("从第几行开始播放（0 开始）；编辑器里的「▶ 播放」会写这个值")]
        public int startLineIndex = 0;

        [Header("舞台")]
        [Tooltip("镜头作用的容器：只包住背景和立绘，不含对话框与按钮")]
        public RectTransform stageRoot;
        public Image backgroundA;
        public Image backgroundB;
        public Image actorLeft;
        public Image actorCenter;
        public Image actorRight;

        [Header("转场")]
        [Tooltip("全屏遮罩；留空会在运行时自动创建（不会盖住右上角菜单按钮）")]
        public Image transitionOverlay;

        [Header("章节过场")]
        [Tooltip("章节开始 / 切换时，用一张全屏卡片显示章节名")]
        public bool showChapterCard = true;

        public float chapterCardFadeToBlack = 0.8f;
        public float chapterCardHold = 1.6f;
        public float chapterCardFadeOut = 0.9f;

        private VNChapterCard _chapterCard;

        [Header("结局")]
        [Tooltip("结局面面结束后回到主菜单（关掉则直接退出程序）")]
        public bool endingReturnToTitle = true;

        [Header("章节")]

        [Tooltip("剧本播完自动进入下一个章节（按 Data 目录文件名排序）。调试整条线时打开；正式发布建议用剧本里的 nextChapter 显式指定。")]
        public bool autoNextChapter = false;

        [Header("主菜单")]
        [Tooltip("启动时先显示主菜单（新游戏 / 继续 / 设置 / 退出）")]
        public bool showTitleOnStart = true;

        [Tooltip("主菜单背景图；留空会自动用一张默认背景")]
        public Sprite titleBackground;

        [Tooltip("主菜单标题")]
        public string gameTitle = "回响RECALL";

        [Tooltip("主菜单副标题")]
        public string gameSubtitle = "recall the rain";

        [Tooltip("主菜单循环音乐（留空则静音）")]
        public string titleMusic = "Assets/VN/Resources/VN/Audio/BGM/tt.ogg";

        [Range(0f, 1f)]
        [Tooltip("主菜单音乐音量")]
        public float titleMusicVolume = 0.6f;

        private VNTitleScreen _titleScreen;

        [Header("视觉效果")]
        [Tooltip("失明 / 视力模糊当前强度（运行时由剧本驱动）")]
        [Range(0f, 1f)]
        public float blindAmount = 0f;

        private Material _blindMatA;
        private Material _blindMatB;
        private float _blindCurrent;
        private float _blindTarget;
        private float _blindFadeTime = 1f;

        [Header("对话 UI")]
        public GameObject dialogueRoot;
        public GameObject namePlate;
        public Text nameText;
        public Text bodyText;
        public VNTextEffect bodyEffect;
        public GameObject nextIndicator;
        public Font dialogueFont;

        [Header("音频")]
        public VNAudioManager audioManager;

        [Header("系统音效")]
        [Tooltip("保存成功时播放")]
        public string saveSfx = "Assets/VN/Resources/VN/Audio/SFX/load&save.wav";

        [Tooltip("读档成功时播放")]
        public string loadSfx = "Assets/VN/Resources/VN/Audio/SFX/load&save.wav";

        [Tooltip("删除存档时播放（留空 = 用保存音效）")]
        public string deleteSfx = "";

        [Range(0f, 1f)]
        public float systemSfxVolume = 0.8f;

        [Header("分支 / 数值")]
        [Tooltip("选项按钮面板；留空会自动创建")]
        public VNChoicePanel choicePanel;

        [Tooltip("玩家状态（数值 + 经过的节点），存档会一起保存")]
        public VNState state = new VNState();

        [Tooltip("玩家做过的选择，分支树用")]
        public List<VNChoiceRecord> choiceHistory = new List<VNChoiceRecord>();

        [Tooltip("已显示的台词，历史记录用")]
        public List<VNLogEntry> log = new List<VNLogEntry>();

        private const int MaxLogEntries = 500;

        private bool _pendingChoices;
        private string _currentNodeId = "";
        private string _currentNodeTitle = "";

        [Header("播放参数")]
        [Tooltip("剧本没写速度时使用的默认值")]
        public float defaultCharsPerSecond = 30f;

        [Tooltip("开启后，长按 Ctrl 会加速打字")]
        public bool ctrlToSpeedUp = true;

        [Tooltip("全局文字速度倍率（系统设置里可调，PlayerPrefs 记住）")]
        public float textSpeedMultiplier = 1f;

        private const string TextSpeedKey = "vn.textSpeed";

        public bool AutoPlay { get; private set; }
        public int CurrentIndex => _index;
        public int LineCount => _script?.lines?.Count ?? 0;

        private VNScript _script;
        private int _index = -1;
        private Coroutine _typeRoutine;
        private Coroutine _autoRoutine;
        private bool _typing;
        private bool _textComplete;
        private string _fullText = "";
        private float _autoTimer;

        private readonly Dictionary<VNSlot, Image> _actors = new Dictionary<VNSlot, Image>();
        private readonly Dictionary<VNSlot, Vector2> _actorBasePos = new Dictionary<VNSlot, Vector2>();
        private readonly Dictionary<VNSlot, Coroutine> _actorRoutines = new Dictionary<VNSlot, Coroutine>();
        private float _lastAdvanceTime;

        // 舞台当前状态（存档用）
        private string _curBackground = "";
        private float _curBgmVolume = 0.7f;
        private readonly Dictionary<VNSlot, VNActor> _curActors = new Dictionary<VNSlot, VNActor>();
        private readonly Dictionary<VNSlot, string> _slotCharacter = new Dictionary<VNSlot, string>();
        private Coroutine _transitionRoutine;

        // 镜头
        private Vector2 _cameraBasePos = Vector2.zero;
        private float _cameraBaseZoom = 1f;
        private Coroutine _cameraMoveRoutine;
        private Coroutine _cameraShakeRoutine;

        // 提示与存档面板
        [Header("存档")]
        [Tooltip("F1 打开系统面板；F5 快速存档；F9 快速读档")]
        public bool enableSaveHotkeys = true;
        public VNSystemPanel savePanel;

        private string _toast;
        private float _toastUntil;
        private GUIStyle _toastStyle;
        private Font _fallbackUiFont;

        private void Awake()
        {
            // 编辑器未聚焦时 Unity 默认暂停循环，会让协程（打字机 / 淡入淡出）卡住
            Application.runInBackground = true;

            textSpeedMultiplier = PlayerPrefs.GetFloat(TextSpeedKey, 1f);

            EnsureActorMap();

            if (bodyEffect == null && bodyText != null)
                bodyEffect = bodyText.GetComponent<VNTextEffect>();

            if (audioManager == null)
                audioManager = GetComponentInChildren<VNAudioManager>();

            InitBlindMaterials();
        }

        // ---------------- 失明滤镜 ----------------

        private void InitBlindMaterials()
        {
            var shader = Resources.Load<Shader>("Shaders/VNBlindEffect");
            if (shader == null) shader = Shader.Find("VN/BlindEffect");
            if (shader == null)
            {
                Debug.LogWarning("[VN] 找不到失明滤镜 Shader（VN/BlindEffect）");
                return;
            }

            if (backgroundA != null)
            {
                _blindMatA = new Material(shader);
                backgroundA.material = _blindMatA;
            }

            if (backgroundB != null)
            {
                _blindMatB = new Material(shader);
                backgroundB.material = _blindMatB;
            }

            _blindCurrent = _blindTarget = Mathf.Clamp01(blindAmount);
            ApplyBlindValues();
        }

        private void ApplyBlindValues()
        {
            blindAmount = _blindCurrent;
            if (_blindMatA != null) _blindMatA.SetFloat("_BlindAmount", _blindCurrent);
            if (_blindMatB != null) _blindMatB.SetFloat("_BlindAmount", _blindCurrent);
        }

        private void UpdateBlind()
        {
            if (Mathf.Abs(_blindCurrent - _blindTarget) < 0.0005f) return;

            float step = _blindFadeTime > 0.01f
                ? Time.unscaledDeltaTime / _blindFadeTime
                : 1f;

            _blindCurrent = Mathf.MoveTowards(_blindCurrent, _blindTarget, step);
            ApplyBlindValues();
        }

        private void OnDestroy()
        {
            if (_blindMatA != null) Destroy(_blindMatA);
            if (_blindMatB != null) Destroy(_blindMatB);
        }

        /// <summary>
        /// 槽位表可能因为 domain reload（改脚本后 play 没退出）被清空，
        /// 而 Awake 不会重跑 —— 每次用到之前确保它已建好。
        /// </summary>
        private void EnsureActorMap()
        {
            if (_actors.Count == 3
                && _actors.TryGetValue(VNSlot.Left, out var l) && l == actorLeft
                && _actors.TryGetValue(VNSlot.Center, out var c) && c == actorCenter
                && _actors.TryGetValue(VNSlot.Right, out var r) && r == actorRight)
            {
                return;
            }

            _actors.Clear();
            _actors[VNSlot.Left] = actorLeft;
            _actors[VNSlot.Center] = actorCenter;
            _actors[VNSlot.Right] = actorRight;

            _actorBasePos.Clear();
            foreach (var kv in _actors)
            {
                if (kv.Value == null) continue;
                _actorBasePos[kv.Key] = ((RectTransform)kv.Value.transform).anchoredPosition;
            }
        }

        private void Start()
        {
            EnsureSystemPanel();

#if UNITY_EDITOR
            // 从编辑器「▶ 播放」进来时，测试起点通过 SessionState 传递，不写进场景
            if (UnityEditor.SessionState.GetString("VN.TestScriptPath", "") == scriptAssetPath)
            {
                int testLine = UnityEditor.SessionState.GetInt("VN.TestStartLine", 0);
                UnityEditor.SessionState.EraseString("VN.TestScriptPath");
                UnityEditor.SessionState.EraseInt("VN.TestStartLine");

                HideTitleScreen();
                LoadAndPlay(scriptAssetPath, testLine);
                return;
            }
#endif

            if (showTitleOnStart)
            {
                ShowTitleScreen();
                return;
            }

            if (playOnStart) LoadAndPlay(scriptAssetPath);
        }

        /// <summary>加载并从指定行开始播放。</summary>
        public void LoadAndPlay(string assetPath, int startLine)
        {
            int old = startLineIndex;
            startLineIndex = startLine;
            LoadAndPlay(assetPath);
            startLineIndex = old;
        }

        // ---------------- 主菜单 ----------------

        public void ShowTitleScreen()
        {
            if (_titleScreen == null)
            {
                var go = new GameObject("VN Title Screen");
                go.transform.SetParent(transform, false);

                _titleScreen = go.AddComponent<VNTitleScreen>();
                _titleScreen.player = this;
                _titleScreen.font = dialogueFont;
                _titleScreen.title = gameTitle;
                _titleScreen.subtitle = gameSubtitle;
                _titleScreen.titleMusic = titleMusic;
                _titleScreen.titleMusicVolume = titleMusicVolume;

                if (titleBackground == null)
                {
                    titleBackground = Resources.Load<Sprite>("VN/BG/title");
                    if (titleBackground == null)
                        titleBackground = Resources.Load<Sprite>("VN/BG/像素田园：晴日与雷云");
                    if (titleBackground == null)
                        titleBackground = Resources.Load<Sprite>("VN/BG/暴雨中的乡间小屋");
                }
                _titleScreen.backgroundSprite = titleBackground;
            }

            if (dialogueRoot != null) dialogueRoot.SetActive(false);
            if (savePanel != null) savePanel.gameObject.SetActive(false);

            _titleScreen.Show();
        }

        private void HideTitleScreen()
        {
            if (_titleScreen != null) _titleScreen.Hide();

            if (dialogueRoot != null) dialogueRoot.SetActive(true);
            if (savePanel != null) savePanel.gameObject.SetActive(true);
        }

        public void StartNewGame()
        {
            HideTitleScreen();

            state = new VNState();
            choiceHistory.Clear();
            log.Clear();
            _curActors.Clear();
            _index = -1;
            _pendingChoices = false;

            if (choicePanel != null) choicePanel.Hide();
            ApplyBackgroundImmediate("");
            ResetCamera();

            // 永远从「起始章节」开始，不受编辑器调试时临时切过的剧本影响
            var chapter = string.IsNullOrWhiteSpace(startChapter) ? scriptAssetPath : startChapter;
            scriptAssetPath = chapter;

            LoadAndPlay(chapter);
        }

        public int FindMostRecentSlot()
        {
            int best = -1;
            string bestTime = "";

            for (int i = 0; i < VNSaveSystem.SlotCount; i++)
            {
                var data = VNSaveSystem.Read(i);
                if (data == null || data.IsEmpty) continue;

                var t = data.savedAt ?? "";
                if (best < 0 || string.CompareOrdinal(t, bestTime) > 0)
                {
                    bestTime = t;
                    best = i;
                }
            }

            return best;
        }

        public void ContinueGame()
        {
            int slot = FindMostRecentSlot();
            if (slot < 0)
            {
                ShowToast("还没有存档，先开始新游戏吧");
                return;
            }

            HideTitleScreen();
            LoadSlot(slot);
        }

        public void OpenSettingsFromTitle()
        {
            EnsureSystemPanel();
            if (savePanel == null) return;

            savePanel.gameObject.SetActive(true);
            savePanel.OpenPage(1);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>回到主菜单（标题画面上仍然可以继续 / 设置 / 退出）。</summary>
        public void ReturnToTitle()
        {
            ShowTitleScreen();
        }

        /// <summary>创建系统面板与右上角按钮（面板保持隐藏）。</summary>
        private void EnsureSystemPanel()
        {
            if (savePanel != null) return;

            savePanel = GetComponentInChildren<VNSystemPanel>(true);
            if (savePanel != null) return;

            var go = new GameObject("VN System Panel");
            savePanel = go.AddComponent<VNSystemPanel>();
            savePanel.Init(this, dialogueFont);
        }

        public void LoadAndPlay(string assetPath)
        {
            if (!LoadScript(assetPath)) return;

            if (showChapterCard && _script != null && !string.IsNullOrWhiteSpace(_script.title))
            {
                PlayChapterCard(startLineIndex);
                return;
            }

            _index = Mathf.Clamp(startLineIndex - 1, -1, _script.lines.Count - 1);
            Advance();
        }

        // ---------------- 章节过场 ----------------

        /// <summary>"第一章 回忆" → 标签「第一章」+ 标题「回忆」。</summary>
        private static void SplitChapterTitle(string full, out string label, out string title)
        {
            label = "";
            title = full ?? "";

            if (string.IsNullOrWhiteSpace(full)) return;

            var s = full.Trim();
            int sp = s.IndexOf(' ');
            if (sp > 0 && sp < s.Length - 1)
            {
                label = s.Substring(0, sp).Trim();
                title = s.Substring(sp + 1).Trim();
            }
        }

        private VNChapterCard EnsureChapterCard()
        {
            if (_chapterCard == null)
            {
                var go = new GameObject("VN Chapter Card");
                go.transform.SetParent(transform, false);

                _chapterCard = go.AddComponent<VNChapterCard>();
                _chapterCard.font = dialogueFont;
                _chapterCard.fadeToBlack = chapterCardFadeToBlack;
                _chapterCard.hold = chapterCardHold;
                _chapterCard.fadeOut = chapterCardFadeOut;
            }

            return _chapterCard;
        }

        private void PlayChapterCard(int startLine)
        {
            var card = EnsureChapterCard();
            SplitChapterTitle(_script.title, out var label, out var title);

            card.Play(label, title, () =>
            {
                _index = Mathf.Clamp(startLine - 1, -1, _script.lines.Count - 1);
                Advance();
            });
        }

        /// <summary>只加载剧本，不开始播放。返回是否成功。</summary>
        public bool LoadScript(string assetPath)
        {
            scriptAssetPath = assetPath;
            var textAsset = VNAssetLoader.LoadText(assetPath);

            if (textAsset == null)
            {
                Debug.LogError($"[VN] 读不到剧本：{assetPath}");
                return false;
            }

            _script = JsonUtility.FromJson<VNScript>(textAsset.text);
            if (_script == null || _script.lines == null || _script.lines.Count == 0)
            {
                Debug.LogError($"[VN] 剧本为空或解析失败：{assetPath}");
                return false;
            }

            log.Clear();
            return true;
        }

        /// <summary>
        /// 切换章节：换一份剧本 JSON，保留数值、选择历史、台词记录。
        /// clearStage = true 时会先清空立绘和镜头，让新章节从干净状态开始。
        /// </summary>
        public bool EnterChapter(string chapterPath, int startLine = 0, bool clearStage = true)
        {
            if (string.IsNullOrWhiteSpace(chapterPath)) return false;

            var textAsset = VNAssetLoader.LoadText(chapterPath);
            if (textAsset == null)
            {
                Debug.LogError($"[VN] 找不到章节：{chapterPath}");
                return false;
            }

            var script = JsonUtility.FromJson<VNScript>(textAsset.text);
            if (script == null || script.lines == null || script.lines.Count == 0)
            {
                Debug.LogError($"[VN] 章节为空或解析失败：{chapterPath}");
                return false;
            }

            scriptAssetPath = chapterPath;
            _script = script;

            if (clearStage) ClearStageForNewChapter();

            if (showChapterCard && !string.IsNullOrWhiteSpace(script.title))
            {
                PlayChapterCard(startLine);
                return true;
            }

            _index = Mathf.Clamp(startLine - 1, -1, _script.lines.Count - 1);
            Advance();
            return true;
        }

        private void ClearStageForNewChapter()
        {
            foreach (var kv in _actors)
            {
                if (kv.Value == null) continue;
                kv.Value.gameObject.SetActive(false);
            }

            _curActors.Clear();
            _slotCharacter.Clear();

            ResetCamera();

            if (choicePanel != null) choicePanel.Hide();
            _pendingChoices = false;
            _typing = false;
        }

        private void Update()
        {
            UpdateBlind();

            // 章节过场期间：点击可跳过，且不推进剧情
            if (_chapterCard != null && _chapterCard.IsPlaying)
            {
                if (Input.GetMouseButtonDown(0)
                    || Input.GetKeyDown(KeyCode.Space)
                    || Input.GetKeyDown(KeyCode.Return)
                    || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    _chapterCard.Skip();
                }
                return;
            }

            if (_script == null) return;

            if (enableSaveHotkeys)
            {
                if (Input.GetKeyDown(KeyCode.F5)) QuickSave(0);
                if (Input.GetKeyDown(KeyCode.F9)) QuickLoad(0);
                if (Input.GetKeyDown(KeyCode.F1)) ToggleSavePanel();
            }

            // L 或滚轮上翻：打开历史记录
            if (Input.GetKeyDown(KeyCode.L))
            {
                EnsureSystemPanel();
                savePanel.OpenPage(3);
                return;
            }
            if ((savePanel == null || !savePanel.IsOpen) && Input.mouseScrollDelta.y > 0.5f)
            {
                EnsureSystemPanel();
                savePanel.OpenPage(3);
                return;
            }

            // 存档面板开着时不要推进剧情
            if (savePanel != null && savePanel.IsOpen) return;

            // 正在等玩家选择时，点击对话不该推进
            if (_pendingChoices) return;

            // 演出顺序表还没播完：点一下直接跳过剩余演出
            if (_stepsRunning)
            {
                SkipSteps();
                return;
            }

            if (Input.GetKeyDown(KeyCode.A)) ToggleAutoPlay();

            if (Input.GetKeyDown(KeyCode.R))
            {
                RestartCurrentLine();
                return;
            }

            if (Input.GetKeyDown(KeyCode.N) && Application.isEditor)
            {
                Advance();
                return;
            }

            if (AutoPlay && _textComplete && !_typing)
            {
                _autoTimer -= Time.deltaTime;
                if (_autoTimer <= 0f) Advance();
                return;
            }

            bool advancePressed =
                Input.GetMouseButtonDown(0) ||
                Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter);

            if (!advancePressed) return;

            // 防止 UI 点击与同一帧的输入重复触发
            if (Time.unscaledTime - _lastAdvanceTime < 0.08f) return;
            _lastAdvanceTime = Time.unscaledTime;

            if (_typing)
            {
                CompleteTypingImmediately();
            }
            else
            {
                Advance();
            }
        }

        public void Advance()
        {
            if (_script == null) return;
            if (_pendingChoices) return; // 有选项时必须先选

            // 当前行带着无条件跳转的话，先跳过去
            if (_index >= 0 && _index < _script.lines.Count)
            {
                var cur = _script.lines[_index];
                if (cur != null)
                {
                    // 章节切换优先级最高
                    if (!string.IsNullOrWhiteSpace(cur.nextChapter))
                    {
                        if (EnterChapter(cur.nextChapter, Mathf.Max(0, cur.nextChapterLine))) return;
                        Debug.LogWarning($"[VN] 章节切换失败：{cur.nextChapter}");
                    }

                    if (!string.IsNullOrWhiteSpace(cur.gotoNode))
                    {
                        int idx = FindLineByNode(cur.gotoNode);
                        if (idx >= 0 && idx != _index) _index = idx - 1;
                        else if (idx == _index) Debug.LogWarning("[VN] goto 指向自己，已忽略");
                    }
                    else if (cur.gotoLine >= 0)
                    {
                        int target = Mathf.Clamp(cur.gotoLine, 0, _script.lines.Count - 1);
                        if (target != _index) _index = target - 1;
                    }
                }
            }

            if (_index + 1 >= _script.lines.Count)
            {
                if (autoNextChapter && TryAutoNextChapter()) return;
                Debug.Log("[VN] 剧本结束");
                return;
            }
            _index++;
            ShowLine(_script.lines[_index]);
        }

        /// <summary>按 Data 目录的文件名顺序，自动进入当前章节的下一章。</summary>
        private bool TryAutoNextChapter()
        {
            var assets = Resources.LoadAll<TextAsset>("VN/Data");
            if (assets == null || assets.Length == 0) return false;

            var paths = new List<string>();
            foreach (var ta in assets)
                paths.Add("Assets/VN/Resources/VN/Data/" + ta.name + ".json");

            paths.Sort(System.StringComparer.OrdinalIgnoreCase);

            int current = paths.IndexOf(scriptAssetPath);
            if (current < 0 || current + 1 >= paths.Count)
            {
                Debug.Log("[VN] 已经是最后一个章节");
                return false;
            }

            var next = paths[current + 1];
            Debug.Log($"[VN] 自动进入下一章：{System.IO.Path.GetFileName(next)}");
            return EnterChapter(next, 0);
        }

        public void RestartCurrentLine()
        {
            if (_script == null || _index < 0 || _index >= _script.lines.Count) return;
            ShowLine(_script.lines[_index]);
        }

        public void ToggleAutoPlay()
        {
            AutoPlay = !AutoPlay;
            Debug.Log($"[VN] 自动播放：{(AutoPlay ? "开" : "关")}");
        }

        private void ShowLine(VNLine line, bool applyStage = true)
        {
            if (line == null) return;

            EnsureActorMap();

            if (applyStage)
            {
                ApplyBackground(line);
                ApplyActors(line);
                ApplyCardPortrait(line);
                ApplyAudio(line);
                ApplyCamera(line);
            }

            // 结局 CG / 视频
            if (!string.IsNullOrWhiteSpace(line.endingVideo) || !string.IsNullOrWhiteSpace(line.endingCg))
                ShowEndingMedia(line);

            // 节点标记与效果
            if (!string.IsNullOrWhiteSpace(line.nodeId))
            {
                _currentNodeId = line.nodeId.Trim();
                _currentNodeTitle = string.IsNullOrWhiteSpace(line.nodeTitle) ? _currentNodeId : line.nodeTitle.Trim();
                state.Visit(_currentNodeId);
            }
            VNEffects.Apply(line.effects, state);

            _pendingChoices = line.choices != null && line.choices.Count > 0;
            if (choicePanel != null) choicePanel.Hide();

            // 分支点自动存档
            if (_pendingChoices && autoSaveOnChoice)
                AutoSave("分支点");

            // 到达结局行
            if (line.endGame)
                StartCoroutine(EndGameRoutine(line.endDelay));

            // 历史记录
            if (!string.IsNullOrWhiteSpace(line.text))
            {
                log.Add(new VNLogEntry
                {
                    name = line.name ?? "",
                    text = StripPauseMarkers(line.text),
                    nodeTitle = _currentNodeTitle ?? "",
                });
                if (log.Count > MaxLogEntries) log.RemoveAt(0);
            }

            ApplyTextStyle(line);
            ApplySpeaker(line);
            ApplySpeakerHighlight(line);
            PlayTransition(line);

            // 失明滤镜
            if (line.useBlind)
            {
                _blindTarget = Mathf.Clamp01(line.blind);
                _blindFadeTime = Mathf.Max(0.05f, line.blindFade);
            }

            SetIndicatorVisible(false);
            _fullText = line.text ?? "";
            _textComplete = false;
            _textStarted = false;

            if (_typeRoutine != null) StopCoroutine(_typeRoutine);
            if (_stepRoutine != null) StopCoroutine(_stepRoutine);
            _stepsRunning = false;
            _autoTimer = line.autoDelay;

            if (audioManager != null) audioManager.StopVoice();
            ResolveVoice(line);

            if (line.steps != null && line.steps.Count > 0)
            {
                // 演出顺序表模式：舞台和文字都由序列驱动
                bodyText.text = "";
                _typing = false;
                _stepsLine = line;
                _stepRoutine = StartCoroutine(RunSteps(line));
            }
            else
            {
                StartTypewriter(line);

                // 默认流程：多音轨可以按 at 自行排时间
                if (line.audio != null && line.audio.Count > 0)
                    StartCoroutine(ScheduleAudioCues(line));
            }
        }

        /// <summary>开始打字（含 stageTiming = wait 的延迟）。</summary>
        private void StartTypewriter(VNLine line)
        {
            _textStarted = true;

            if (_typeRoutine != null) StopCoroutine(_typeRoutine);

            if (string.IsNullOrEmpty(_fullText) || line.style.instant || line.style.charsPerSecond <= 0f)
            {
                bodyText.text = StripPauseMarkers(_fullText);
                OnTypingFinished(line);
                return;
            }

            bodyText.text = "";
            _typing = true;

            bool useStageWait = (line.stageTiming ?? "").Trim().ToLowerInvariant() == "wait"
                                && (line.steps == null || line.steps.Count == 0);

            float stageDelay = useStageWait ? EstimateStageDuration(line) : 0f;
            _typeRoutine = StartCoroutine(TypeText(line, stageDelay));
        }

        // ---------------- 演出顺序表 ----------------

        private bool _stepsRunning;
        private VNLine _stepsLine;
        private Coroutine _stepRoutine;
        private bool _textStarted;

        // 说话音（Undertale 风格）
        private string _voicePath = "";
        private string _voiceMode = "char";
        private float _voiceVolume = 0.75f;
        private float _voicePitchRandom = 0.06f;
        private int _voiceEveryN = 1;
        private int _voiceCharCounter;

        [Tooltip("全局默认说话音：角色没单独配语音时用这个（可选）")]
        public string defaultVoice = "";

        [Header("角色卡")]
        [Tooltip("角色卡 JSON（立绘 / 说话音 / 字体一次配好）；留空则不使用")]
        public string characterSetPath = "Assets/VN/Resources/VN/Data/characters.json";

        private VNCharacterSet _characters;
        private bool _charactersLoaded;

        private void LoadCharacterSet()
        {
            if (_charactersLoaded) return;
            _charactersLoaded = true;

            if (string.IsNullOrWhiteSpace(characterSetPath)) return;

            var text = VNAssetLoader.LoadText(characterSetPath);
            if (text == null)
            {
                Debug.LogWarning($"[VN] 找不到角色卡：{characterSetPath}");
                return;
            }

            _characters = JsonUtility.FromJson<VNCharacterSet>(text.text);
        }

        /// <summary>按显示名或 id 查角色卡。</summary>
        public VNCharacter FindCharacter(string nameOrId)
        {
            LoadCharacterSet();
            if (_characters == null || _characters.characters == null) return null;

            var key = (nameOrId ?? "").Trim();
            if (key.Length == 0) return null;

            foreach (var c in _characters.characters)
            {
                if (c == null) continue;
                if (c.id == key || c.displayName == key) return c;
            }

            foreach (var c in _characters.characters)
            {
                if (c == null) continue;
                if (!string.IsNullOrWhiteSpace(c.displayName) && (key.Contains(c.displayName) || c.displayName.Contains(key)))
                    return c;
                if (!string.IsNullOrWhiteSpace(c.id) && key.Contains(c.id))
                    return c;
            }

            return null;
        }

        private void ResolveVoice(VNLine line)
        {
            _voicePath = "";
            _voiceMode = "char";
            _voiceVolume = 0.75f;
            _voicePitchRandom = 0.06f;
            _voiceEveryN = 1;
            _voiceCharCounter = 0;

            // 1) 行级覆盖
            if (!string.IsNullOrWhiteSpace(line.voice))
            {
                _voicePath = line.voice.Trim();
                if (!string.IsNullOrWhiteSpace(line.voiceMode) && line.voiceMode != "keep")
                    _voiceMode = line.voiceMode.Trim().ToLowerInvariant();
                if (line.voiceVolume > 0f) _voiceVolume = line.voiceVolume;
                if (line.voiceEveryNChars > 0) _voiceEveryN = line.voiceEveryNChars;
                return;
            }

            // 2) 角色卡（按说话人名字查）
            var character = FindCharacter(line.name);
            if (character != null)
            {
                var cardMode = (character.voiceMode ?? "").Trim().ToLowerInvariant();

                // 角色卡明确写了 none：这个角色就是不出声
                if (cardMode == "none")
                {
                    ApplyLineVoiceOverrides(line);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(character.voice))
                {
                    _voicePath = character.voice.Trim();
                    _voiceMode = cardMode.Length > 0 ? cardMode : "char";
                    _voiceVolume = character.voiceVolume;
                    _voicePitchRandom = character.voicePitchRandom;
                    _voiceEveryN = Mathf.Max(1, character.voiceEveryNChars);
                    ApplyLineVoiceOverrides(line);
                    return;
                }
            }

            // 3) 场内角色
            var actor = ResolveSpeakerActor(line);
            if (actor != null && !string.IsNullOrWhiteSpace(actor.voice))
            {
                ApplyActorVoice(actor, line);
                return;
            }

            // 4) 全局默认
            if (!string.IsNullOrWhiteSpace(defaultVoice))
            {
                _voicePath = defaultVoice.Trim();
                if (line.voiceVolume > 0f) _voiceVolume = line.voiceVolume;
            }
        }

        private void ApplyLineVoiceOverrides(VNLine line)
        {
            if (line.voiceVolume > 0f) _voiceVolume = line.voiceVolume;
            if (line.voiceEveryNChars > 0) _voiceEveryN = line.voiceEveryNChars;
        }

        private void ApplyActorVoice(VNActor actor, VNLine line)
        {
            _voicePath = actor.voice.Trim();
            _voiceMode = string.IsNullOrWhiteSpace(actor.voiceMode)
                ? "char"
                : actor.voiceMode.Trim().ToLowerInvariant();
            _voiceVolume = actor.voiceVolume;
            _voicePitchRandom = actor.voicePitchRandom;
            _voiceEveryN = Mathf.Max(1, actor.voiceEveryNChars);

            if (line.voiceVolume > 0f) _voiceVolume = line.voiceVolume;
            if (line.voiceEveryNChars > 0) _voiceEveryN = line.voiceEveryNChars;
        }

        /// <summary>
        /// 找出这一行到底是哪个角色在说话。
        /// 旁白（没有名字也没有指定槽位）视为无人说话，不返回角色——否则会错误沿用画面上的角色语音。
        /// </summary>
        private VNActor ResolveSpeakerActor(VNLine line)
        {
            bool hasSlot = !string.IsNullOrWhiteSpace(line.speakerSlot);
            bool hasName = !string.IsNullOrWhiteSpace(line.name);

            if (!hasSlot && !hasName) return null; // 旁白

            var slot = ParseSlot(line.speakerSlot);
            if (hasSlot && _curActors.TryGetValue(slot, out var bySlot) && bySlot != null)
                return bySlot;

            if (hasName)
            {
                var name = line.name.Trim();
                foreach (var kv in _curActors)
                {
                    var a = kv.Value;
                    if (a == null) continue;

                    var id = (a.character ?? "").Trim();
                    if (id.Length == 0) continue;

                    // 双向包含：角色 id 叫 "凛" 或 "lin" 都能对上说话人「凛」
                    if (name.Contains(id) || id.Contains(name)) return a;
                }
            }

            // 名字对不上任何在场角色 → 当作没有说话人（走默认语音或静音）。
            // 这里不能再「场上只有一个配音角色就认定是他」，否则第一人称/旁白会串音。
            return null;
        }

        private IEnumerator RunSteps(VNLine line)
        {
            _stepsRunning = true;

            var ordered = new List<VNStageStep>();
            foreach (var s in line.steps)
                if (s != null) ordered.Add(s);
            ordered.Sort((a, b) => a.at.CompareTo(b.at));

            float elapsed = 0f;

            foreach (var step in ordered)
            {
                while (elapsed < step.at)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                ApplyStep(line, step);
            }

            // 序列里没有「文字」步骤时兜底，避免卡住
            if (!_textStarted) StartTypewriter(line);

            _stepsRunning = false;
            _stepRoutine = null;
        }

        private void ApplyStep(VNLine line, VNStageStep step)
        {
            if (step.background) ApplyBackground(line);
            if (step.actors) ApplyActors(line);
            if (step.camera) ApplyCamera(line);
            if (step.transition) PlayTransition(line);
            if (step.bgm) ApplyBgm(line);
            if (step.audio) ApplyAudioCues(line);
            if (step.sfx) ApplySfx(line);
            if (step.text) StartTypewriter(line);
        }

        /// <summary>点击时跳过剩余演出，立刻把这一行的舞台和文字都补上。</summary>
        public void SkipSteps()
        {
            if (!_stepsRunning || _stepsLine == null) return;

            if (_stepRoutine != null)
            {
                StopCoroutine(_stepRoutine);
                _stepRoutine = null;
            }
            _stepsRunning = false;

            var line = _stepsLine;
            ApplyBackground(line);
            ApplyActors(line);
            ApplyBgm(line);
            ApplySfx(line);
            ApplyAudioCues(line);
            ApplyCamera(line);
            PlayTransition(line);

            if (!_textStarted) StartTypewriter(line);
        }

        private struct TypeChar
        {
            public char c;
            public float pauseAfter;
        }

        /// <summary>
        /// 把台词解析成「字符 + 停顿」序列。支持 [p] / [p:0.8] 标记：
        /// 停 0.5 秒 / 停 0.8 秒，紧跟在当前字符之后。
        /// </summary>
        private static List<TypeChar> ParseTypeChars(string raw)
        {
            var list = new List<TypeChar>();
            if (string.IsNullOrEmpty(raw)) return list;

            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == '[')
                {
                    int close = raw.IndexOf(']', i);
                    if (close > i)
                    {
                        var inner = raw.Substring(i + 1, close - i - 1).Trim();
                        if (inner == "p" || inner.StartsWith("p:"))
                        {
                            float pause = 0.5f;
                            if (inner.Length > 2)
                            {
                                float.TryParse(inner.Substring(2),
                                    System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out pause);
                            }
                            pause = Mathf.Clamp(pause, 0f, 10f);

                            if (list.Count > 0)
                            {
                                var last = list[list.Count - 1];
                                last.pauseAfter += pause;
                                list[list.Count - 1] = last;
                            }
                            else
                            {
                                // 标记在最前面：先等着再开始
                                list.Add(new TypeChar { c = '\0', pauseAfter = pause });
                            }

                            i = close;
                            continue;
                        }
                    }
                }

                list.Add(new TypeChar { c = raw[i] });
            }

            return list;
        }

        /// <summary>去掉 [p:...] 标记，用于瞬间显示、历史记录。</summary>
        public static string StripPauseMarkers(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw.IndexOf('[') < 0) return raw;

            var chars = ParseTypeChars(raw);
            var sb = new System.Text.StringBuilder(chars.Count);
            foreach (var c in chars)
            {
                if (c.c != '\0') sb.Append(c.c);
            }
            return sb.ToString();
        }

        /// <summary>估算这行舞台动画要播多久，用于 stageTiming = wait。</summary>
        private float EstimateStageDuration(VNLine line)
        {
            float d = 0f;

            if (!string.IsNullOrWhiteSpace(line.transition) &&
                line.transition.Trim().ToLowerInvariant() != "none")
            {
                d = Mathf.Max(d, line.transitionDuration);
            }

            if (line.useCamera && line.camera != null)
            {
                d = Mathf.Max(d, line.camera.duration);
                if (!string.IsNullOrWhiteSpace(line.camera.shake) &&
                    line.camera.shake.Trim().ToLowerInvariant() != "none")
                {
                    d = Mathf.Max(d, line.camera.shakeDuration);
                }
            }

            if (line.actors != null)
            {
                foreach (var a in line.actors)
                {
                    if (a == null || a.hide) continue;
                    d = Mathf.Max(d, a.fadeDuration);
                }
            }

            return d;
        }

        private IEnumerator TypeText(VNLine line, float initialDelay = 0f)
        {
            float cps = line.style.charsPerSecond > 0f ? line.style.charsPerSecond : defaultCharsPerSecond;
            if (cps <= 0f) cps = 30f;
            cps *= Mathf.Clamp(textSpeedMultiplier, 0.1f, 5f);

            // stageTiming = wait：先让立绘 / 镜头 / 转场播完再开口
            if (initialDelay > 0f)
            {
                float wait = 0f;
                while (wait < initialDelay)
                {
                    wait += Time.unscaledDeltaTime;
                    if (ctrlToSpeedUp && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
                        break;
                    yield return null;
                }
            }

            var chars = ParseTypeChars(_fullText);
            var sb = new System.Text.StringBuilder(chars.Count);
            int index = 0;
            float acc = 0f;
            bool dirty = false;

            bool loopVoice = _voiceMode == "loop" && !string.IsNullOrEmpty(_voicePath);
            if (loopVoice && audioManager != null)
                audioManager.StartVoiceLoop(_voicePath, _voiceVolume);

            // 开头的停顿标记
            if (chars.Count > 0 && chars[0].c == '\0')
            {
                yield return WaitPause(chars[0].pauseAfter);
                index = 1;
            }

            while (index < chars.Count)
            {
                float speedMul = 1f;
                if (ctrlToSpeedUp && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
                    speedMul = 8f;

                acc += cps * speedMul * Time.deltaTime;

                while (acc >= 1f && index < chars.Count)
                {
                    acc -= 1f;

                    var tc = chars[index];
                    sb.Append(tc.c);
                    index++;
                    dirty = true;

                    // 逐字语音
                    if (!loopVoice && !string.IsNullOrEmpty(_voicePath) && tc.c != '\0' && audioManager != null)
                    {
                        _voiceCharCounter++;
                        if (_voiceCharCounter >= _voiceEveryN)
                        {
                            _voiceCharCounter = 0;
                            audioManager.PlayVoiceOnce(_voicePath, _voiceVolume, _voicePitchRandom);
                        }
                    }

                    if (tc.pauseAfter > 0f)
                    {
                        if (dirty) { bodyText.text = sb.ToString(); dirty = false; }
                        acc = 0f;
                        yield return WaitPause(tc.pauseAfter);
                    }
                }

                if (dirty)
                {
                    bodyText.text = sb.ToString();
                    dirty = false;
                }

                yield return null;
            }

            bodyText.text = StripPauseMarkers(_fullText);
            if (audioManager != null) audioManager.StopVoice();
            _typing = false;
            _typeRoutine = null;
            OnTypingFinished(line);
        }

        private IEnumerator WaitPause(float seconds)
        {
            if (seconds <= 0f) yield break;

            bool loopVoice = _voiceMode == "loop" && !string.IsNullOrEmpty(_voicePath);
            if (loopVoice && audioManager != null) audioManager.PauseVoice();

            float t = 0f;
            while (t < seconds)
            {
                if (ctrlToSpeedUp && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
                    break;

                t += Time.unscaledDeltaTime;
                yield return null;
            }

            if (loopVoice && audioManager != null) audioManager.ResumeVoice();
        }

        private void CompleteTypingImmediately()
        {
            if (_typeRoutine != null)
            {
                StopCoroutine(_typeRoutine);
                _typeRoutine = null;
            }
            _typing = false;
            if (audioManager != null) audioManager.StopVoice();
            bodyText.text = StripPauseMarkers(_fullText);
            OnTypingFinished(_script.lines[_index]);
        }

        private void OnTypingFinished(VNLine line)
        {
            _textComplete = true;

            if (_pendingChoices)
            {
                ShowChoices(line);
                return;
            }

            SetIndicatorVisible(true);

            if (line.autoDelay > 0f) _autoTimer = line.autoDelay;
        }

        // ---------------- 选项 ----------------

        private void ShowChoices(VNLine line)
        {
            if (choicePanel == null) choicePanel = GetComponentInChildren<VNChoicePanel>(true);
            if (choicePanel == null)
            {
                var go = new GameObject("VN Choice Panel");
                choicePanel = go.AddComponent<VNChoicePanel>();
                choicePanel.Init(dialogueFont);
            }

            var items = new List<VNChoiceItem>();
            for (int i = 0; i < line.choices.Count; i++)
            {
                var c = line.choices[i];
                if (c == null) continue;

                bool ok = VNConditions.Evaluate(c.condition, state);
                if (!ok && !c.showWhenLocked) continue;

                items.Add(new VNChoiceItem
                {
                    text = ok
                        ? c.text
                        : (string.IsNullOrWhiteSpace(c.lockedText) ? c.text : c.lockedText),
                    enabled = ok,
                    index = i,
                });
            }

            if (items.Count == 0)
            {
                // 没有任何可选项：不能卡死，直接往下走
                Debug.LogWarning("[VN] 本行选项全部不满足条件，自动跳过");
                _pendingChoices = false;
                Advance();
                return;
            }

            SetIndicatorVisible(false);
            choicePanel.Show(items, OnChoicePicked);
        }

        private void OnChoicePicked(int index)
        {
            if (_script == null || _index < 0 || _index >= _script.lines.Count) return;

            var line = _script.lines[_index];
            if (line.choices == null || index < 0 || index >= line.choices.Count) return;

            var choice = line.choices[index];

            choiceHistory.Add(new VNChoiceRecord
            {
                nodeId = _currentNodeId,
                nodeTitle = _currentNodeTitle,
                choiceText = choice.text,
                targetNode = choice.targetNode,
                targetTitle = GetNodeTitle(choice.targetNode),
                lineIndex = _index,
                time = System.DateTime.Now.ToString("HH:mm:ss"),
            });

            VNEffects.Apply(choice.effects, state);
            _pendingChoices = false;
            JumpTo(choice);
        }

        private void JumpTo(VNChoice choice)
        {
            // 章节跳转优先
            if (!string.IsNullOrWhiteSpace(choice.targetChapter))
            {
                if (EnterChapter(choice.targetChapter, Mathf.Max(0, choice.targetChapterLine))) return;
                Debug.LogWarning($"[VN] 章节切换失败：{choice.targetChapter}");
            }

            if (!string.IsNullOrWhiteSpace(choice.targetNode))
            {
                int idx = FindLineByNode(choice.targetNode);
                if (idx >= 0)
                {
                    _index = idx - 1;
                    Advance();
                    return;
                }
                Debug.LogWarning($"[VN] 找不到节点：{choice.targetNode}");
            }

            if (choice.targetLine >= 0)
            {
                _index = Mathf.Clamp(choice.targetLine - 1, -1, _script.lines.Count - 1);
                Advance();
                return;
            }

            Advance();
        }

        public int FindLineByNode(string nodeId)
        {
            if (_script == null || _script.lines == null || string.IsNullOrWhiteSpace(nodeId)) return -1;
            for (int i = 0; i < _script.lines.Count; i++)
            {
                var l = _script.lines[i];
                if (l != null && l.nodeId == nodeId.Trim()) return i;
            }
            return -1;
        }

        public string GetNodeTitle(string nodeId)
        {
            int idx = FindLineByNode(nodeId);
            if (idx < 0) return nodeId;
            var l = _script.lines[idx];
            return string.IsNullOrWhiteSpace(l.nodeTitle) ? l.nodeId : l.nodeTitle;
        }

        public string CurrentNodeTitle => _currentNodeTitle;

        private void SetIndicatorVisible(bool visible)
        {
            if (nextIndicator != null) nextIndicator.SetActive(visible);
            if (dialogueRoot != null) dialogueRoot.SetActive(true);
        }

        private void ApplySpeaker(VNLine line)
        {
            bool hasName = !string.IsNullOrWhiteSpace(line.name);
            if (namePlate != null) namePlate.SetActive(hasName);
            if (nameText != null) nameText.text = hasName ? line.name : "";
        }

        private void ApplyTextStyle(VNLine line)
        {
            var style = line.style ?? new VNTextStyle();

            if (bodyText != null)
            {
                var font = ResolveFont(line);
                if (font != null) bodyText.font = font;

                bodyText.fontSize = Mathf.Max(8, style.fontSize);
                bodyText.color = VNColors.Parse(style.color, Color.white);
                bodyText.supportRichText = true;

                var outline = bodyText.GetComponent<Outline>();
                if (outline != null) outline.enabled = style.outline;
            }

            if (bodyEffect != null)
                bodyEffect.SetMode(style.effect, style.effectStrength, style.effectSpeed);
        }

        /// <summary>字体优先级：行级 → 说话角色 → 播放器默认。</summary>
        private Font ResolveFont(VNLine line)
        {
            // 1) 行级
            if (line.style != null && !string.IsNullOrWhiteSpace(line.style.font))
            {
                var f = VNAssetLoader.LoadFont(line.style.font);
                if (f != null) return f;
            }

            // 2) 角色卡
            var character = FindCharacter(line.name);
            if (character != null && !string.IsNullOrWhiteSpace(character.font))
            {
                var f = VNAssetLoader.LoadFont(character.font);
                if (f != null) return f;
            }

            // 3) 场内角色
            var actor = ResolveSpeakerActor(line);
            if (actor != null && !string.IsNullOrWhiteSpace(actor.font))
            {
                var f = VNAssetLoader.LoadFont(actor.font);
                if (f != null) return f;
            }

            return dialogueFont;
        }

        private void ApplyBackground(VNLine line)
        {
            if (string.IsNullOrWhiteSpace(line.background)) return;
            var sprite = VNAssetLoader.LoadSprite(line.background);
            if (sprite == null)
            {
                Debug.LogWarning($"[VN] 找不到背景：{line.background}");
                return;
            }

            if (backgroundA == null || backgroundB == null)
            {
                if (backgroundA != null) backgroundA.sprite = sprite;
                _curBackground = line.background;
                return;
            }

            _curBackground = line.background;

            // 第一次上背景：直接铺满，不做淡入（否则会先闪一帧白块）
            if (backgroundA.sprite == null && backgroundB.sprite == null)
            {
                backgroundA.sprite = sprite;
                backgroundA.color = Color.white;
                ApplyAspect(backgroundA, sprite);
                backgroundA.gameObject.SetActive(true);
                backgroundB.gameObject.SetActive(false);
                backgroundB.color = new Color(1f, 1f, 1f, 0f);
                return;
            }

            // 交接：把当前可见层当作 A，新图放 B 并淡入
            var front = backgroundB.color.a >= backgroundA.color.a ? backgroundB : backgroundA;
            var back = ReferenceEquals(front, backgroundA) ? backgroundB : backgroundA;

            back.sprite = sprite;
            back.color = new Color(1f, 1f, 1f, 0f);
            back.gameObject.SetActive(true);
            ApplyAspect(back, sprite);

            StartCoroutine(CrossFadeImages(back, front, Mathf.Max(0.01f, line.backgroundFade)));
        }

        /// <summary>让背景保持原始宽高比（EnvelopeParent 会裁切而不是拉伸）。</summary>
        private static void ApplyAspect(Image image, Sprite sprite)
        {
            if (image == null || sprite == null) return;
            var fitter = image.GetComponent<AspectRatioFitter>();
            if (fitter == null) return;
            fitter.aspectRatio = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
        }

        private static IEnumerator CrossFadeImages(Image fadeIn, Image fadeOut, float duration)
        {
            float t = 0f;
            Color inColor = fadeIn.color;
            Color outColor = fadeOut != null ? fadeOut.color : Color.clear;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                inColor.a = k;
                fadeIn.color = inColor;
                if (fadeOut != null)
                {
                    fadeOut.color = new Color(outColor.r, outColor.g, outColor.b, 1f - k);
                }
                yield return null;
            }

            fadeIn.color = new Color(inColor.r, inColor.g, inColor.b, 1f);
            if (fadeOut != null)
            {
                fadeOut.color = new Color(outColor.r, outColor.g, outColor.b, 0f);
                fadeOut.gameObject.SetActive(false);
            }
        }

        private void ApplyAudio(VNLine line)
        {
            ApplyBgm(line);
            ApplySfx(line);
        }

        private void ApplyBgm(VNLine line)
        {
            if (audioManager == null) return;

            var action = (line.bgmAction ?? "").Trim().ToLowerInvariant();
            float fade = Mathf.Max(0.01f, line.bgmFade);

            if (!string.IsNullOrWhiteSpace(line.bgm))
            {
                // 播放：淡入时长可以单独设；可选从上次进度继续；可指定起始音调
                float fadeIn = line.bgmFadeIn > 0f ? line.bgmFadeIn : fade;
                audioManager.PlayBgm(line.bgm, line.bgmVolume, fadeIn, line.bgmResume, line.bgmPitchStart);
                _curBgmVolume = line.bgmVolume;
            }
            else if (action == "stop")
            {
                // 停止：可淡出、可边淡出边变调
                audioManager.StopBgm(fade, line.bgmPitch, line.bgmPitchDuration);
                _curBgmVolume = 0f;
            }
            else if (action == "pitch")
            {
                // 只改音调，不停
                audioManager.TweenPitch(line.bgmPitch, line.bgmPitchDuration);
            }
            else if (action == "fade")
            {
                // 渐变到指定音量（不停止），可同时变调
                audioManager.FadeBgmTo(line.bgmVolume, fade, line.bgmPitch, line.bgmPitchDuration);
                _curBgmVolume = line.bgmVolume;
            }
            else if (action == "restart")
            {
                audioManager.RestartBgm(fade);
            }
        }

        private void ApplySfx(VNLine line)
        {
            if (audioManager == null || string.IsNullOrWhiteSpace(line.sfx)) return;
            audioManager.PlaySfx(line.sfx, line.sfxVolume);
        }

        // ---------------- 多音轨 ----------------

        /// <summary>把这一行所有的音轨指令立即应用（不等待 at 时间）。</summary>
        private void ApplyAudioCues(VNLine line)
        {
            if (audioManager == null || line.audio == null || line.audio.Count == 0) return;
            foreach (var cue in line.audio) ApplyAudioCue(cue);
        }

        private void ApplyAudioCue(VNAudioCue cue)
        {
            if (cue == null || audioManager == null) return;

            var track = string.IsNullOrWhiteSpace(cue.track) ? "bgm" : cue.track.Trim().ToLowerInvariant();
            var action = string.IsNullOrWhiteSpace(cue.action) ? "play" : cue.action.Trim().ToLowerInvariant();
            float fade = Mathf.Max(0.01f, cue.fade);

            if (track == "bgm")
            {
                if (action == "stop") audioManager.StopBgm(fade, cue.pitch, cue.pitchDuration);
                else if (action == "pitch") audioManager.TweenPitch(cue.pitch, cue.pitchDuration);
                else if (!string.IsNullOrWhiteSpace(cue.clip)) audioManager.PlayBgm(cue.clip, cue.volume, fade);
                return;
            }

            if (action == "stop") audioManager.StopTrack(track, fade, cue.pitch, cue.pitchDuration);
            else if (action == "pitch") audioManager.SetTrackPitch(track, cue.pitch, cue.pitchDuration);
            else if (!string.IsNullOrWhiteSpace(cue.clip))
                audioManager.PlayTrack(track, cue.clip, cue.volume, fade, cue.loop, cue.pitch);
        }

        /// <summary>按每条 cue 的 at 时间依次触发（用于默认流程，没有演出顺序表时）。</summary>
        private IEnumerator ScheduleAudioCues(VNLine line)
        {
            if (line.audio == null || line.audio.Count == 0) yield break;

            var ordered = new List<VNAudioCue>();
            foreach (var c in line.audio)
                if (c != null) ordered.Add(c);
            ordered.Sort((a, b) => a.at.CompareTo(b.at));

            float elapsed = 0f;
            foreach (var cue in ordered)
            {
                while (elapsed < cue.at)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                ApplyAudioCue(cue);
            }
        }

        private VNEndingScreen _endingScreen;

        private VNEndingScreen EnsureEndingScreen()
        {
            if (_endingScreen == null)
            {
                var go = new GameObject("VN Ending Screen");
                go.transform.SetParent(transform, false);
                _endingScreen = go.AddComponent<VNEndingScreen>();
                _endingScreen.font = dialogueFont;
            }
            return _endingScreen;
        }

        /// <summary>显示结局 CG 或视频，并隐藏对话框。</summary>
        private void ShowEndingMedia(VNLine line)
        {
            var screen = EnsureEndingScreen();

            if (!string.IsNullOrWhiteSpace(line.endingVideo))
            {
                screen.PlayVideo(line.endingVideo, line.endingLoop, null);
            }
            else
            {
                screen.ShowCg(line.endingCg, line.endingLoop, null);
            }

            if (dialogueRoot != null) dialogueRoot.SetActive(false);
        }

        private IEnumerator EndGameRoutine(float delay)
        {
            // 等这一行打完字再开始计时，免得台词还没显示完就退了
            while (_typing) yield return null;

            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            if (audioManager != null) audioManager.StopBgm(1.5f);
            yield return new WaitForSecondsRealtime(1.5f);

            if (endingReturnToTitle)
            {
                EndToTitle();
            }
            else
            {
                QuitGame();
            }
        }

        /// <summary>结局收尾：清掉结局画面，回到主菜单。</summary>
        public void EndToTitle()
        {
            if (_endingScreen != null) _endingScreen.Hide();

            // 复位剧情状态，免得下次「新游戏」还停在上一次的位置
            _index = -1;
            _script = null;
            _pendingChoices = false;
            state = new VNState();
            choiceHistory.Clear();
            log.Clear();
            _curActors.Clear();
            HideAllActors();
            ApplyBackgroundImmediate("");
            ResetCamera();

            if (choicePanel != null) choicePanel.Hide();
            if (bodyText != null) bodyText.text = "";
            SetIndicatorVisible(false);
            if (namePlate != null) namePlate.SetActive(false);

            if (dialogueRoot != null) dialogueRoot.SetActive(false);
            if (savePanel != null) savePanel.gameObject.SetActive(false);

            ShowTitleScreen();
        }

        private void ApplyActors(VNLine line)
        {
            // 本行不显示立绘（结局 / CG 段）
            if (line.suppressActors)
            {
                HideAllActors();
                return;
            }

            // 全体退场
            if (line.clearActors)
            {
                HideAllActors();
            }

            // 指定角色退场
            ApplyCharacterExits(line);

            if (line.actors == null || line.actors.Count == 0) return;

            foreach (var actor in line.actors)
            {
                if (actor == null) continue;

                // 空条目（既没图也没角色）直接跳过，避免占位和后续误判
                if (string.IsNullOrWhiteSpace(actor.sprite) && string.IsNullOrWhiteSpace(actor.character))
                    continue;

                ApplyActor(actor);
            }
        }

        /// <summary>让指定角色退场：隐藏并移除他在任意槽位的立绘。</summary>
        private void ApplyCharacterExits(VNLine line)
        {
            if (line == null || line.exitCharacters == null || line.exitCharacters.Count == 0) return;

            EnsureActorMap();

            foreach (var raw in line.exitCharacters)
            {
                var id = (raw ?? "").Trim();
                if (id.Length == 0) continue;

                var slots = new List<VNSlot>();
                foreach (var kv in _curActors)
                {
                    if (kv.Value != null && kv.Value.character == id) slots.Add(kv.Key);
                }

                foreach (var slot in slots)
                {
                    if (_actorRoutines.TryGetValue(slot, out var run) && run != null) StopCoroutine(run);

                    if (_actors.TryGetValue(slot, out var img) && img != null)
                    {
                        var cg = img.GetComponent<CanvasGroup>();
                        if (cg != null) cg.alpha = 0f;
                        img.gameObject.SetActive(false);
                    }

                    _curActors.Remove(slot);
                }
            }
        }

        /// <summary>这一行是否要求某个角色退场。</summary>
        private static bool WantsExit(VNLine line, string characterId)
        {
            if (line == null || line.exitCharacters == null || string.IsNullOrWhiteSpace(characterId)) return false;

            foreach (var raw in line.exitCharacters)
            {
                if ((raw ?? "").Trim() == characterId) return true;
            }
            return false;
        }

        private void HideAllActors()
        {
            EnsureActorMap();

            foreach (var kv in _actors)
            {
                if (kv.Value == null) continue;
                if (_actorRoutines.TryGetValue(kv.Key, out var run) && run != null) StopCoroutine(run);

                var cg = kv.Value.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 0f;
                kv.Value.gameObject.SetActive(false);
            }

            _curActors.Clear();
        }

        /// <summary>
        /// 说话人匹配到角色卡时，自动按角色卡配置出图（替代手配立绘栏）。
        /// 行级 actors 优先，手配了就不走这里。
        /// </summary>
        private void ApplyCardPortrait(VNLine line)
        {
            if (line == null || line.suppressActors) return;
            if (line.actors != null && line.actors.Count > 0) return;

            var card = FindCharacter(line.name);
            if (card == null) return;
            if (!card.portraitEnabled) return;
            if (string.IsNullOrWhiteSpace(card.portrait)) return;

            // 这一行要求他退场 → 不自动出图
            if (WantsExit(line, card.id)) return;

            var portrait = card.portrait.Trim();

            // 这个角色已经在任意槽位显示了 → 不重复出图（防止剧本手配 + 角色卡自动叠成两个）
            foreach (var kv in _curActors)
            {
                var existing = kv.Value;
                if (existing == null || existing.hide) continue;
                if (existing.sprite == portrait) return;
            }

            var actor = new VNActor
            {
                slot = string.IsNullOrWhiteSpace(card.portraitSlot) ? "center" : card.portraitSlot.Trim().ToLowerInvariant(),
                sprite = portrait,
                character = card.id,
                motion = "fade",
            };

            ApplyActor(actor);
        }

        private void ApplyActor(VNActor actor)
        {
            var slot = ParseSlot(actor.slot);
            if (!_actors.TryGetValue(slot, out var image) || image == null) return;

            if (actor.hide || string.IsNullOrWhiteSpace(actor.sprite))
            {
                if (actor.hide)
                {
                    if (_actorRoutines.TryGetValue(slot, out var run) && run != null) StopCoroutine(run);
                    var cg = image.GetComponent<CanvasGroup>();
                    if (cg != null) cg.alpha = 0f;
                    image.gameObject.SetActive(false);
                }
                return;
            }

            var sprite = ResolveActorSprite(actor);
            if (sprite == null)
            {
                Debug.LogWarning($"[VN] 找不到立绘：{actor.character}/{actor.expression} 或 {actor.sprite}");
                return;
            }

            // 同一张图、槽位已经在显示：只改位置/大小，这时做平移缩放补间而不是重播入场
            bool sameSprite = image.gameObject.activeSelf
                              && image.sprite != null
                              && image.sprite.name == sprite.name;

            // 同一个角色的不同表情：直接换图，也不要播入场动画
            bool sameCharacter = image.gameObject.activeSelf
                                 && !string.IsNullOrWhiteSpace(actor.character)
                                 && _slotCharacter.TryGetValue(slot, out var prevChar)
                                 && prevChar == actor.character.Trim();

            image.sprite = sprite;
            image.color = VNColors.Parse(actor.tint, Color.white);
            image.preserveAspect = true;
            image.gameObject.SetActive(true);
            _curActors[slot] = CloneActor(actor);
            _slotCharacter[slot] = string.IsNullOrWhiteSpace(actor.character) ? "" : actor.character.Trim();

            var rect = (RectTransform)image.transform;
            var basePos = _actorBasePos.TryGetValue(slot, out var bp) ? bp : rect.anchoredPosition;
            var targetPos = basePos + new Vector2(actor.offsetX, actor.offsetY);
            float targetScale = Mathf.Max(0.01f, actor.scale);

            if (_actorRoutines.TryGetValue(slot, out var old) && old != null) StopCoroutine(old);

            if (sameSprite)
            {
                _actorRoutines[slot] = StartCoroutine(TweenActorTo(image, rect, targetPos, targetScale, Mathf.Max(0.01f, actor.moveDuration)));
            }
            else if (sameCharacter)
            {
                // 换表情：图已经换好，只补间位置/缩放，给一点点过渡感
                _actorRoutines[slot] = StartCoroutine(TweenActorTo(image, rect, targetPos, targetScale, 0.12f));
            }
            else
            {
                _actorRoutines[slot] = StartCoroutine(PlayActorMotion(image, rect, targetPos, targetScale, actor, slot));
            }
        }

        /// <summary>同一张立绘只改位置/缩放时的平滑补间。</summary>
        private IEnumerator TweenActorTo(Image image, RectTransform rect, Vector2 targetPos, float targetScale, float duration)
        {
            var cg = image.GetComponent<CanvasGroup>();
            if (cg == null) cg = image.gameObject.AddComponent<CanvasGroup>();
            if (cg.alpha < 0.999f) cg.alpha = 1f;

            Vector2 startPos = rect.anchoredPosition;
            float startScale = rect.localScale.x;

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                rect.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, k);
                rect.localScale = Vector3.one * Mathf.LerpUnclamped(startScale, targetScale, k);
                yield return null;
            }

            rect.anchoredPosition = targetPos;
            rect.localScale = Vector3.one * targetScale;
        }

        public static VNSlot ParseSlot(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return VNSlot.Center;
            switch (value.Trim().ToLowerInvariant())
            {
                case "left":
                case "左":
                    return VNSlot.Left;
                case "right":
                case "右":
                    return VNSlot.Right;
                default:
                    return VNSlot.Center;
            }
        }

        private IEnumerator PlayActorMotion(Image image, RectTransform rect, Vector2 targetPos, float targetScale, VNActor actor, VNSlot slot)
        {
            var cg = image.GetComponent<CanvasGroup>();
            if (cg == null) cg = image.gameObject.AddComponent<CanvasGroup>();

            string motion = string.IsNullOrWhiteSpace(actor.motion) ? "fade" : actor.motion.Trim().ToLowerInvariant();
            float dur = Mathf.Max(0.01f, actor.fadeDuration);

            // 起始状态
            Vector2 startPos = targetPos;
            float startScale = targetScale;
            float startAlpha = 0f;

            switch (motion)
            {
                case "slideup":
                    startPos = targetPos + new Vector2(0f, -160f);
                    cg.alpha = 1f;
                    startAlpha = 1f;
                    break;
                case "slideside":
                    startPos = targetPos + new Vector2(slot == VNSlot.Right ? 160f : -160f, 0f);
                    cg.alpha = 1f;
                    startAlpha = 1f;
                    break;
                case "bounce":
                case "pop":
                    startScale = targetScale * 0.82f;
                    cg.alpha = 0.2f;
                    startAlpha = 0.2f;
                    break;
                case "none":
                    dur = 0.01f;
                    startAlpha = 1f;
                    break;
                default: // fade
                    cg.alpha = 0f;
                    startAlpha = 0f;
                    break;
            }

            rect.anchoredPosition = startPos;
            rect.localScale = Vector3.one * startScale;

            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                float e = Mathf.SmoothStep(0f, 1f, k);

                rect.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, e);
                rect.localScale = Vector3.one * Mathf.LerpUnclamped(startScale, targetScale, e);
                cg.alpha = Mathf.LerpUnclamped(startAlpha, 1f, e);
                yield return null;
            }

            rect.anchoredPosition = targetPos;
            rect.localScale = Vector3.one * targetScale;
            cg.alpha = 1f;
            _actorRoutines[slot] = null;
        }

        /// <summary>供编辑器预览使用：直接显示某一行而不推进。</summary>
        public void PreviewLine(int index)
        {
            if (_script == null || _script.lines == null) return;
            if (index < 0 || index >= _script.lines.Count) return;
            _index = index;
            ShowLine(_script.lines[index]);
        }

        // ---------------- 表情 / 高亮 / 转场 ----------------

        /// <summary>优先按「角色 + 表情」找图，找不到再退回 sprite 整路径。</summary>
        private static Sprite ResolveActorSprite(VNActor actor)
        {
            if (actor == null) return null;

            if (!string.IsNullOrWhiteSpace(actor.character))
            {
                var dir = "Assets/VN/Resources/VN/Char/" + actor.character.Trim();

                if (!string.IsNullOrWhiteSpace(actor.expression))
                {
                    var stem = dir + "/" + actor.expression.Trim();
                    var s = VNAssetLoader.LoadSprite(stem + ".png");
                    if (s == null) s = VNAssetLoader.LoadSprite(stem + ".jpg");
                    if (s != null) return s;
                }

                var single = VNAssetLoader.LoadSprite(dir + ".png");
                if (single == null) single = VNAssetLoader.LoadSprite(dir + ".jpg");
                if (single != null) return single;
            }

            return VNAssetLoader.LoadSprite(actor.sprite);
        }

        /// <summary>
        /// 说话者高亮：当前说话的角色保持原色，其他角色压暗。
        /// 优先用 line.speakerSlot，否则拿 line.name 去匹配 actor.character。
        /// </summary>
        private void ApplySpeakerHighlight(VNLine line)
        {
            bool hasSlot = !string.IsNullOrWhiteSpace(line.speakerSlot);
            var speaker = hasSlot ? ParseSlot(line.speakerSlot) : VNSlot.Center;

            bool anyVisible = false;
            foreach (var kv in _actors)
            {
                if (kv.Value != null && kv.Value.gameObject.activeSelf) { anyVisible = true; break; }
            }
            if (!anyVisible) return;

            bool hasNameMatch = false;
            if (!hasSlot && !string.IsNullOrWhiteSpace(line.name))
            {
                foreach (var kv in _slotCharacter)
                    if (!string.IsNullOrEmpty(kv.Value) && line.name.Contains(kv.Value)) { hasNameMatch = true; break; }
            }

            // 没有可判断的信息时不压暗，避免单人场景自己变暗
            if (!hasSlot && !hasNameMatch)
            {
                foreach (var kv in _actors)
                    if (kv.Value != null) RestoreActorTint(kv.Key, kv.Value);
                return;
            }

            foreach (var kv in _actors)
            {
                var img = kv.Value;
                if (img == null || !img.gameObject.activeSelf) continue;

                bool isSpeaker = hasSlot
                    ? kv.Key == speaker
                    : (_slotCharacter.TryGetValue(kv.Key, out var ch) && !string.IsNullOrEmpty(ch) && line.name.Contains(ch));

                if (isSpeaker) RestoreActorTint(kv.Key, img);
                else DimActor(img);
            }
        }

        private void RestoreActorTint(VNSlot slot, Image img)
        {
            var tint = _curActors.TryGetValue(slot, out var a) && a != null
                ? VNColors.Parse(a.tint, Color.white)
                : Color.white;
            img.color = tint;
        }

        private static void DimActor(Image img)
        {
            var c = img.color;
            img.color = new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, c.a);
        }

        private void PlayTransition(VNLine line)
        {
            if (string.IsNullOrWhiteSpace(line.transition)) return;
            var mode = line.transition.Trim().ToLowerInvariant();
            if (mode == "none") return;

            EnsureTransitionOverlay();
            if (transitionOverlay == null) return;

            if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);
            _transitionRoutine = StartCoroutine(TransitionRoutine(mode, Mathf.Max(0.05f, line.transitionDuration)));
        }

        private void EnsureTransitionOverlay()
        {
            if (transitionOverlay != null || dialogueRoot == null) return;

            var canvas = dialogueRoot.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var go = new GameObject("Transition Overlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas.transform, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = false;
            go.SetActive(false);

            transitionOverlay = img;
        }

        private IEnumerator TransitionRoutine(string mode, float duration)
        {
            var img = transitionOverlay;
            if (img == null) yield break;

            var baseColor = (mode == "fadewhite" || mode == "white" || mode == "flash") ? Color.white : Color.black;
            float peak = mode == "flash" ? 0.85f : 1f;

            img.gameObject.SetActive(true);
            img.transform.SetAsLastSibling();

            float half = duration * 0.5f;
            float t = 0f;

            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / half);
                img.color = new Color(baseColor.r, baseColor.g, baseColor.b, peak * k);
                yield return null;
            }

            img.color = new Color(baseColor.r, baseColor.g, baseColor.b, peak);

            t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Clamp01(t / half);
                img.color = new Color(baseColor.r, baseColor.g, baseColor.b, peak * k);
                yield return null;
            }

            img.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
            img.gameObject.SetActive(false);
            _transitionRoutine = null;
        }

        // ---------------- 镜头 ----------------

        /// <summary>按行应用镜头：平移 / 缩放，可选晃动。只动 stageRoot，不影响对话框和按钮。</summary>
        private void ApplyCamera(VNLine line)
        {
            if (stageRoot == null) return;
            if (!line.useCamera) return;

            var cam = line.camera ?? new VNCamera();

            if (_cameraMoveRoutine != null) StopCoroutine(_cameraMoveRoutine);
            _cameraMoveRoutine = StartCoroutine(MoveCamera(
                new Vector2(cam.offsetX, cam.offsetY),
                Mathf.Max(0.1f, cam.zoom),
                Mathf.Max(0.01f, cam.duration)));

            if (!string.IsNullOrWhiteSpace(cam.shake) && cam.shake.Trim().ToLowerInvariant() != "none")
                StartShake(cam);
        }

        private IEnumerator MoveCamera(Vector2 targetPos, float targetZoom, float duration)
        {
            Vector2 startPos = _cameraBasePos;
            float startZoom = _cameraBaseZoom;
            float t = 0f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                _cameraBasePos = Vector2.LerpUnclamped(startPos, targetPos, k);
                _cameraBaseZoom = Mathf.LerpUnclamped(startZoom, targetZoom, k);
                if (_cameraShakeRoutine == null) ApplyStageTransform();
                yield return null;
            }

            _cameraBasePos = targetPos;
            _cameraBaseZoom = targetZoom;
            if (_cameraShakeRoutine == null) ApplyStageTransform();
            _cameraMoveRoutine = null;
        }

        private void StartShake(VNCamera cam)
        {
            if (_cameraShakeRoutine != null) StopCoroutine(_cameraShakeRoutine);
            _cameraShakeRoutine = StartCoroutine(ShakeCamera(cam));
        }

        private IEnumerator ShakeCamera(VNCamera cam)
        {
            float strength = Mathf.Max(0f, cam.shakeStrength);
            switch ((cam.shake ?? "").Trim().ToLowerInvariant())
            {
                case "light": strength *= 0.5f; break;
                case "strong": strength *= 2f; break;
            }

            float duration = Mathf.Max(0.01f, cam.shakeDuration);
            float t = 0f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float fade = 1f - Mathf.Clamp01(t / duration);
                var offset = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * strength * fade;
                ApplyStageTransform(offset);
                yield return null;
            }

            ApplyStageTransform();
            _cameraShakeRoutine = null;
        }

        private void ApplyStageTransform(Vector2 shakeOffset = default)
        {
            if (stageRoot == null) return;
            stageRoot.anchoredPosition = _cameraBasePos + shakeOffset;
            stageRoot.localScale = Vector3.one * _cameraBaseZoom;
        }

        public void ResetCamera()
        {
            if (_cameraMoveRoutine != null) { StopCoroutine(_cameraMoveRoutine); _cameraMoveRoutine = null; }
            if (_cameraShakeRoutine != null) { StopCoroutine(_cameraShakeRoutine); _cameraShakeRoutine = null; }
            _cameraBasePos = Vector2.zero;
            _cameraBaseZoom = 1f;
            ApplyStageTransform();
        }

        // ---------------- 编辑器预览 ----------------

        /// <summary>
        /// 不进 Play 模式也能预览某一行的舞台效果（编辑器窗口用）。
        /// 从第 0 行累积到目标行，保证「留空沿用上一行」的结果正确。
        /// </summary>
        public void PreviewInEditor(int index)
        {
            if (Application.isPlaying)
            {
                PreviewLine(index);
                return;
            }

            if (!LoadScript(scriptAssetPath)) return;
            if (_script.lines == null || _script.lines.Count == 0) return;

            index = Mathf.Clamp(index, 0, _script.lines.Count - 1);
            EnsureActorMap();

            // 先清空立绘
            foreach (var kv in _actors)
            {
                if (kv.Value == null) continue;
                var cg = kv.Value.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
                kv.Value.gameObject.SetActive(false);
            }
            _curActors.Clear();

            VNCamera lastCamera = null;

            for (int i = 0; i <= index; i++)
            {
                var l = _script.lines[i];
                if (l == null) continue;

                if (!string.IsNullOrWhiteSpace(l.background))
                    ApplyBackgroundImmediate(l.background);

                if (l.useCamera) lastCamera = l.camera;

                if (l.actors == null) continue;
                foreach (var a in l.actors)
                {
                    if (a == null) continue;
                    if (a.hide)
                    {
                        var slot = ParseSlot(a.slot);
                        if (_actors.TryGetValue(slot, out var img) && img != null) img.gameObject.SetActive(false);
                        _curActors.Remove(slot);
                    }
                    else
                    {
                        ApplyActorImmediate(a);
                    }
                }
            }

            // 镜头
            if (stageRoot != null)
            {
                if (lastCamera != null)
                {
                    _cameraBasePos = new Vector2(lastCamera.offsetX, lastCamera.offsetY);
                    _cameraBaseZoom = Mathf.Max(0.1f, lastCamera.zoom);
                }
                else
                {
                    _cameraBasePos = Vector2.zero;
                    _cameraBaseZoom = 1f;
                }
                ApplyStageTransform();
            }

            var line = _script.lines[index];
            _index = index;
            ApplyTextStyle(line);
            ApplySpeaker(line);
            if (bodyText != null) bodyText.text = StripPauseMarkers(line.text ?? "");
            SetIndicatorVisible(true);
            _typing = false;
            _textComplete = true;
        }

        // ---------------- 存档 / 读档 ----------------

        public VNSaveSlot CaptureState(int slot)
        {
            var data = new VNSaveSlot
            {
                slot = slot,
                scriptPath = scriptAssetPath,
                lineIndex = Mathf.Max(0, _index),
                background = _curBackground,
                bgm = audioManager != null ? audioManager.CurrentBgm : "",
                bgmVolume = _curBgmVolume,
                actors = new List<VNActor>(),
                preview = BuildPreview(),
                state = state.Clone(),
                history = new List<VNChoiceRecord>(choiceHistory),
            };

            foreach (var kv in _curActors)
            {
                if (kv.Value == null) continue;
                data.actors.Add(CloneActor(kv.Value));
            }

            return data;
        }

        public bool QuickSave(int slot)
        {
            var data = CaptureState(slot);
            bool ok = VNSaveSystem.Write(data);
            if (ok)
            {
                // 等这一帧渲染完再截图，否则可能拿到黑屏
                if (Application.isPlaying) StartCoroutine(CaptureThumbDelayed(slot));
                else VNSaveSystem.SaveThumbnail(slot);

                PlaySystemSfx(saveSfx);
            }
            ShowToast(ok ? $"已保存到槽位 {slot + 1}" : "保存失败");
            return ok;
        }

        private IEnumerator CaptureThumbDelayed(int slot)
        {
            yield return new WaitForEndOfFrame();
            VNSaveSystem.SaveThumbnail(slot);
        }

        public bool LoadSlot(int slot)
        {
            var data = VNSaveSystem.Read(slot);
            if (data == null)
            {
                ShowToast($"槽位 {slot + 1} 是空的");
                return false;
            }

            bool ok = RestoreState(data);
            if (ok) PlaySystemSfx(loadSfx);

            ShowToast(ok ? $"已读取槽位 {slot + 1}" : "读档失败");
            return ok;
        }

        public bool QuickLoad(int slot)
        {
            return LoadSlot(slot);
        }

        public void DeleteSlot(int slot)
        {
            VNSaveSystem.Delete(slot);
            VNSaveSystem.DeleteThumbnail(slot);

            PlaySystemSfx(string.IsNullOrWhiteSpace(deleteSfx) ? saveSfx : deleteSfx);
            ShowToast($"已删除槽位 {slot + 1}");
        }

        private void PlaySystemSfx(string path)
        {
            if (audioManager == null || string.IsNullOrWhiteSpace(path)) return;
            audioManager.PlaySfx(path, systemSfxVolume);
        }

        [Header("自动存档")]
        [Tooltip("遇到分支（有选项的行）时自动保存一份")]
        public bool autoSaveOnChoice = true;

        /// <summary>自动存档：写到独立的 auto 槽，不占玩家的 6 个手动槽。</summary>
        public bool AutoSave(string reason = "")
        {
            try
            {
                var data = CaptureState(VNSaveSystem.AutoSlot);
                if (!VNSaveSystem.Write(data)) return false;

                if (Application.isPlaying) StartCoroutine(CaptureThumbDelayed(VNSaveSystem.AutoSlot));
                else VNSaveSystem.SaveThumbnail(VNSaveSystem.AutoSlot);

                Debug.Log("[VN] 自动存档" + (string.IsNullOrEmpty(reason) ? "" : "（" + reason + "）"));
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[VN] 自动存档失败：" + ex.Message);
                return false;
            }
        }

        public bool HasAutoSave()
        {
            return VNSaveSystem.Exists(VNSaveSystem.AutoSlot);
        }

        public bool LoadAutoSave()
        {
            return LoadSlot(VNSaveSystem.AutoSlot);
        }

        public bool RestoreState(VNSaveSlot data)
        {
            if (data == null) return false;
            if (!LoadScript(data.scriptPath)) return false;

            ApplyBackgroundImmediate(data.background);

            // 先清空所有槽位，再按存档重建，避免残留上一局的立绘
            foreach (var kv in _actors)
            {
                if (kv.Value == null) continue;
                var cg = kv.Value.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 0f;
                kv.Value.gameObject.SetActive(false);
            }
            _curActors.Clear();

            if (data.actors != null)
            {
                foreach (var a in data.actors)
                    ApplyActorImmediate(a);
            }

            _curBgmVolume = data.bgmVolume;
            if (audioManager != null)
            {
                if (!string.IsNullOrWhiteSpace(data.bgm))
                    audioManager.PlayBgm(data.bgm, data.bgmVolume, 0.25f);
            }

            _index = Mathf.Clamp(data.lineIndex, 0, _script.lines.Count - 1);

            // 恢复数值与分支历史
            state = data.state != null ? data.state.Clone() : new VNState();
            choiceHistory = data.history != null
                ? new List<VNChoiceRecord>(data.history)
                : new List<VNChoiceRecord>();
            _pendingChoices = false;
            if (choicePanel != null) choicePanel.Hide();

            ShowLine(_script.lines[_index], applyStage: false);
            return true;
        }

        private void ApplyBackgroundImmediate(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            var sprite = VNAssetLoader.LoadSprite(path);
            if (sprite == null) return;

            if (backgroundA != null)
            {
                backgroundA.sprite = sprite;
                backgroundA.color = Color.white;
                ApplyAspect(backgroundA, sprite);
                backgroundA.gameObject.SetActive(true);
            }

            if (backgroundB != null)
            {
                backgroundB.color = new Color(1f, 1f, 1f, 0f);
                backgroundB.gameObject.SetActive(false);
            }

            _curBackground = path;
        }

        private void ApplyActorImmediate(VNActor actor)
        {
            if (actor == null) return;
            var slot = ParseSlot(actor.slot);
            if (!_actors.TryGetValue(slot, out var image) || image == null) return;

            if (actor.hide || string.IsNullOrWhiteSpace(actor.sprite))
            {
                image.gameObject.SetActive(false);
                _curActors.Remove(slot);
                return;
            }

            var sprite = VNAssetLoader.LoadSprite(actor.sprite);
            if (sprite == null) return;

            image.sprite = sprite;
            image.color = VNColors.Parse(actor.tint, Color.white);
            image.preserveAspect = true;
            image.gameObject.SetActive(true);

            var rect = (RectTransform)image.transform;
            var basePos = _actorBasePos.TryGetValue(slot, out var bp) ? bp : rect.anchoredPosition;
            rect.anchoredPosition = basePos + new Vector2(actor.offsetX, actor.offsetY);
            rect.localScale = Vector3.one * Mathf.Max(0.01f, actor.scale);

            var cg = image.GetComponent<CanvasGroup>();
            if (cg == null) cg = image.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

            _curActors[slot] = CloneActor(actor);
            _slotCharacter[slot] = string.IsNullOrWhiteSpace(actor.character) ? "" : actor.character.Trim();
        }

        private string BuildPreview()
        {
            if (_script == null || _script.lines == null || _index < 0 || _index >= _script.lines.Count)
                return "";

            var line = _script.lines[_index];
            var text = (line.text ?? "").Replace("\n", " ");
            if (text.Length > 24) text = text.Substring(0, 24) + "…";
            return string.IsNullOrWhiteSpace(line.name) ? text : $"{line.name}：{text}";
        }

        private static VNActor CloneActor(VNActor src)
        {
            if (src == null) return null;
            return JsonUtility.FromJson<VNActor>(JsonUtility.ToJson(src));
        }

        public void ToggleSavePanel()
        {
            EnsureSystemPanel();
            savePanel.Toggle();
        }

        public void SetTextSpeed(float multiplier)
        {
            textSpeedMultiplier = Mathf.Clamp(multiplier, 0.25f, 3f);
            PlayerPrefs.SetFloat(TextSpeedKey, textSpeedMultiplier);
            PlayerPrefs.Save();
        }

        public void ShowToast(string message)
        {
            _toast = message;
            _toastUntil = Time.unscaledTime + 2f;
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_toast) || Time.unscaledTime > _toastUntil) return;

            if (_toastStyle == null)
            {
                _toastStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 24,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                };
            }

            // IMGUI 默认字体没有中文字形，必须换成支持中文的字体，否则提示会变成方块
            var toastFont = dialogueFont;
            if (toastFont == null)
            {
                if (_fallbackUiFont == null)
                {
                    _fallbackUiFont = Font.CreateDynamicFontFromOSFont(
                        new[] { "Microsoft YaHei", "SimHei", "Microsoft JhengHei", "Arial" }, 24);
                }
                toastFont = _fallbackUiFont;
            }
            if (toastFont != null) _toastStyle.font = toastFont;

            var rect = new Rect(Screen.width * 0.5f - 220f, Screen.height * 0.12f, 440f, 60f);
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(rect, _toast, _toastStyle);
            GUI.color = old;
        }
    }
}
