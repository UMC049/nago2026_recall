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
        [Tooltip("剧本 JSON 的工程路径，例如 Assets/VN/Resources/VN/Data/chapter01.json")]
        public string scriptAssetPath = "Assets/VN/Resources/VN/Data/chapter01.json";
        public bool playOnStart = true;

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
            if (playOnStart) LoadAndPlay(scriptAssetPath);
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

            _index = Mathf.Clamp(startLineIndex - 1, -1, _script.lines.Count - 1);
            Advance();
        }

        /// <summary>只加载剧本，不开始播放。返回是否成功。</summary>
        public bool LoadScript(string assetPath)
        {
            scriptAssetPath = assetPath;
            var textAsset = VNAssetLoader.LoadText(assetPath);            if (textAsset == null)
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

        private void Update()
        {
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
                Debug.Log("[VN] 剧本结束");
                return;
            }
            _index++;
            ShowLine(_script.lines[_index]);
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
                ApplyAudio(line);
                ApplyCamera(line);
            }

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

            // 历史记录
            if (!string.IsNullOrWhiteSpace(line.text))
            {
                log.Add(new VNLogEntry
                {
                    name = line.name ?? "",
                    text = line.text,
                    nodeTitle = _currentNodeTitle ?? "",
                });
                if (log.Count > MaxLogEntries) log.RemoveAt(0);
            }

            ApplyTextStyle(line);
            ApplySpeaker(line);
            ApplySpeakerHighlight(line);
            PlayTransition(line);

            SetIndicatorVisible(false);
            _fullText = line.text ?? "";
            _textComplete = false;

            if (_typeRoutine != null) StopCoroutine(_typeRoutine);
            _autoTimer = line.autoDelay;

            if (string.IsNullOrEmpty(_fullText) || line.style.instant || line.style.charsPerSecond <= 0f)
            {
                bodyText.text = _fullText;
                OnTypingFinished(line);
            }
            else
            {
                bodyText.text = "";
                _typing = true;
                _typeRoutine = StartCoroutine(TypeText(line));
            }
        }

        private IEnumerator TypeText(VNLine line)
        {
            float cps = line.style.charsPerSecond > 0f ? line.style.charsPerSecond : defaultCharsPerSecond;
            if (cps <= 0f) cps = 30f;
            cps *= Mathf.Clamp(textSpeedMultiplier, 0.1f, 5f);

            float shown = 0f;
            int len = _fullText.Length;
            int lastCount = -1;

            while (shown < len)
            {
                float speedMul = 1f;
                if (ctrlToSpeedUp && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
                    speedMul = 8f;

                shown += cps * speedMul * Time.deltaTime;
                int count = Mathf.Clamp(Mathf.FloorToInt(shown), 0, len);
                if (count != lastCount)
                {
                    bodyText.text = _fullText.Substring(0, count);
                    lastCount = count;
                }
                yield return null;
            }

            bodyText.text = _fullText;
            _typing = false;
            _typeRoutine = null;
            OnTypingFinished(line);
        }

        private void CompleteTypingImmediately()
        {
            if (_typeRoutine != null)
            {
                StopCoroutine(_typeRoutine);
                _typeRoutine = null;
            }
            _typing = false;
            bodyText.text = _fullText;
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
                if (dialogueFont != null) bodyText.font = dialogueFont;
                bodyText.fontSize = Mathf.Max(8, style.fontSize);
                bodyText.color = VNColors.Parse(style.color, Color.white);
                bodyText.supportRichText = true;

                var outline = bodyText.GetComponent<Outline>();
                if (outline != null) outline.enabled = style.outline;
            }

            if (bodyEffect != null)
                bodyEffect.SetMode(style.effect, style.effectStrength, style.effectSpeed);
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
            if (audioManager == null) return;
            if (!string.IsNullOrWhiteSpace(line.bgm))
            {
                audioManager.PlayBgm(line.bgm, line.bgmVolume, 0.6f);
                _curBgmVolume = line.bgmVolume;
            }
            if (!string.IsNullOrWhiteSpace(line.sfx))
                audioManager.PlaySfx(line.sfx, line.sfxVolume);
        }

        private void ApplyActors(VNLine line)
        {
            if (line.actors == null || line.actors.Count == 0) return;

            foreach (var actor in line.actors)
            {
                if (actor == null) continue;
                ApplyActor(actor);
            }
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
            if (bodyText != null) bodyText.text = line.text ?? "";
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
            ShowToast($"已删除槽位 {slot + 1}");
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
