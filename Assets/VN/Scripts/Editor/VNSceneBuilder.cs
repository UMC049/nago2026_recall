using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VN.EditorTools
{
    /// <summary>
    /// 一键生成视觉小说的完整场景（相机 / Canvas / 背景层 / 立绘层 / 对话框 / 音频）。
    /// 菜单：VN → 构建场景
    /// </summary>
    public static class VNSceneBuilder
    {
        private const string ScenePath = "Assets/VN/Scenes/VN.unity";
        private const string FontPath = "Assets/VN/Fonts/fusion-pixel-12px-zh_hans.ttf";
        private const string DefaultProject = "demo";
        private const string DefaultScript = "Assets/VN/Resources/VN/Data/demo/story01.json";

        // 横屏全屏设计基准（16:9，跟电脑显示器一致）
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        [MenuItem("VN/构建场景（或重建） %#v", priority = 1)]
        public static void BuildScene()
        {
            if (!EditorUtility.DisplayDialog("构建 VN 场景",
                    $"将新建一个场景并保存到：\n{ScenePath}\n\n当前未保存的场景改动会丢失。继续？",
                    "构建", "取消"))
                return;

            BuildSceneSilent();
        }

        /// <summary>不弹确认框，供自动化调用。</summary>
        public static void BuildSceneSilent()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);

            CreateCamera();
            CreateEventSystem();

            var canvasGO = CreateCanvas();
            var canvasRT = (RectTransform)canvasGO.transform;
            var frame = CreateFrame(canvasRT);
            var stage = CreateStage(frame);

            var (bgA, bgB) = CreateBackground(stage);
            var (actorLeft, actorCenter, actorRight) = CreateActors(stage);
            var (dialogueRoot, namePlate, nameText, bodyText, bodyEffect, nextIndicator) =
                CreateDialogue(frame, font);

            var (vnRoot, player, audioManager) = CreatePlayerRoot(
                stage, bgA, bgB, actorLeft, actorCenter, actorRight,
                dialogueRoot, namePlate, nameText, bodyText, bodyEffect, nextIndicator, font);

            // 记录到构建列表
            AddToBuildSettings(ScenePath);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Selection.activeGameObject = vnRoot;
            EditorGUIUtility.PingObject(vnRoot);
            Debug.Log($"[VN] 场景已构建：{ScenePath}（剧本：{DefaultScript}）");
        }

        private static void CreateCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);
            var cam = go.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.06f, 1f);
        }

        private static void CreateEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static GameObject CreateCanvas()
        {
            var go = new GameObject("VN Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            // Expand：无论窗口是什么比例，竖屏内容都完整可见（两侧留黑边）
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            go.GetComponent<GraphicRaycaster>().enabled = true;
            return go;
        }

        /// <summary>
        /// 镜头容器：只有背景和立绘挂在这里，镜头平移/缩放/晃动不会碰到对话框和右上角按钮。
        /// </summary>
        private static RectTransform CreateStage(RectTransform frame)
        {
            var stage = NewUI("Stage", frame);
            StretchFull(stage);
            return stage;
        }

        /// <summary>
        /// 锁定 3:4 的内容区。屏幕不是竖屏时，UI 保持原比例居中，两侧 / 上下留黑边，
        /// 而不是被横向拉宽。
        /// </summary>
        private static RectTransform CreateFrame(RectTransform canvas)
        {
            var frame = NewUI("VN Frame", canvas);
            StretchFull(frame);

            var fitter = frame.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = ReferenceResolution.x / ReferenceResolution.y;
            return frame;
        }

        private static (Image, Image) CreateBackground(RectTransform canvas)
        {
            var root = NewUI("Background", canvas);
            StretchFull(root);

            var a = NewImage("BG_A", root);
            StretchFull(a.rectTransform);
            a.color = new Color(1, 1, 1, 1);
            a.raycastTarget = false;
            AddEnvelopeFitter(a);

            var b = NewImage("BG_B", root);
            StretchFull(b.rectTransform);
            b.color = new Color(1, 1, 1, 0);
            b.raycastTarget = false;
            AddEnvelopeFitter(b);
            b.gameObject.SetActive(false);

            return (a, b);
        }

        /// <summary>背景按 sprite 比例铺满并裁切（等比，不拉伸）。</summary>
        private static void AddEnvelopeFitter(Image image)
        {
            var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 1f;
        }

        private static (Image, Image, Image) CreateActors(RectTransform canvas)
        {
            var root = NewUI("Actors", canvas);
            StretchFull(root);

            var left = NewActor("Actor_Left", root, new Vector2(0.24f, 0f), new Vector2(0f, 30f));
            var center = NewActor("Actor_Center", root, new Vector2(0.5f, 0f), new Vector2(0f, 30f));
            var right = NewActor("Actor_Right", root, new Vector2(0.76f, 0f), new Vector2(0f, 30f));
            return (left, center, right);
        }

        private static Image NewActor(string name, RectTransform parent, Vector2 anchor, Vector2 offset)
        {
            var img = NewImage(name, parent);
            var rt = img.rectTransform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(720f, 980f);
            rt.anchoredPosition = offset;

            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = new Color(1, 1, 1, 1);
            img.gameObject.AddComponent<CanvasGroup>();

            img.gameObject.SetActive(false);
            return img;
        }

        private static (GameObject, GameObject, Text, Text, VNTextEffect, GameObject) CreateDialogue(
            RectTransform canvas, Font font)
        {
            var root = NewUI("Dialogue", canvas);
            StretchFull(root);

            // 对话框底板
            var panel = NewImage("Panel", root);
            var panelRT = panel.rectTransform;
            panelRT.anchorMin = new Vector2(0f, 0f);
            panelRT.anchorMax = new Vector2(1f, 0f);
            panelRT.pivot = new Vector2(0.5f, 0f);
            panelRT.sizeDelta = new Vector2(0f, 320f);
            panelRT.anchoredPosition = new Vector2(0f, 40f);
            panelRT.offsetMin = new Vector2(80f, 40f);
            panelRT.offsetMax = new Vector2(-80f, 360f);
            panel.color = new Color(0.06f, 0.07f, 0.11f, 0.94f);
            panel.raycastTarget = false;

            // 名牌
            var plate = NewImage("NamePlate", panelRT);
            var plateRT = plate.rectTransform;
            plateRT.anchorMin = new Vector2(0f, 1f);
            plateRT.anchorMax = new Vector2(0f, 1f);
            plateRT.pivot = new Vector2(0f, 0f);
            plateRT.sizeDelta = new Vector2(300f, 68f);
            plateRT.anchoredPosition = new Vector2(30f, 2f);
            plate.color = new Color(0.86f, 0.32f, 0.45f, 1f);
            plate.raycastTarget = false;

            var nameText = NewText("NameText", plateRT, font, 30, TextAnchor.MiddleCenter);
            nameText.text = "角色名";
            nameText.raycastTarget = false;

            // 正文
            var body = NewText("BodyText", panelRT, font, 40, TextAnchor.UpperLeft);
            var bodyRT = body.rectTransform;
            bodyRT.anchorMin = Vector2.zero;
            bodyRT.anchorMax = Vector2.one;
            bodyRT.offsetMin = new Vector2(48f, 30f);
            bodyRT.offsetMax = new Vector2(-48f, -80f);
            body.text = "在这里显示台词。";
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            body.lineSpacing = 1.15f;
            body.raycastTarget = false;

            var effect = body.gameObject.AddComponent<VNTextEffect>();

            var outline = body.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;

            // 继续指示器
            var indicator = NewText("NextIndicator", panelRT, font, 32, TextAnchor.LowerRight);
            var indRT = indicator.rectTransform;
            indRT.anchorMin = new Vector2(1f, 0f);
            indRT.anchorMax = new Vector2(1f, 0f);
            indRT.pivot = new Vector2(1f, 0f);
            indRT.sizeDelta = new Vector2(60f, 60f);
            indRT.anchoredPosition = new Vector2(-24f, 16f);
            indicator.text = "▼";
            indicator.color = new Color(1f, 0.85f, 0.5f, 1f);
            indicator.raycastTarget = false;

            return (root.gameObject, plate.gameObject, nameText, body, effect, indicator.gameObject);
        }

        private static (GameObject, VNPlayer, VNAudioManager) CreatePlayerRoot(
            RectTransform stage, Image bgA, Image bgB, Image actorLeft, Image actorCenter, Image actorRight,
            GameObject dialogueRoot, GameObject namePlate, Text nameText, Text bodyText,
            VNTextEffect bodyEffect, GameObject nextIndicator, Font font)
        {
            var root = new GameObject("VN Root");

            var audioManager = root.AddComponent<VNAudioManager>();
            audioManager.bgmA = CreateAudioSource(root.transform, "BGM_A", true);
            audioManager.bgmB = CreateAudioSource(root.transform, "BGM_B", true);
            audioManager.sfx = CreateAudioSource(root.transform, "SFX", false);
            audioManager.voice = CreateAudioSource(root.transform, "Voice", false);

            var player = root.AddComponent<VNPlayer>();
            player.projectId = DefaultProject;
            player.startChapterFile = "story01.json";
            player.characterSetFile = "characters.json";
            player.scriptAssetPath = DefaultScript;
            player.stageRoot = stage;
            player.backgroundA = bgA;
            player.backgroundB = bgB;
            player.actorLeft = actorLeft;
            player.actorCenter = actorCenter;
            player.actorRight = actorRight;
            player.dialogueRoot = dialogueRoot;
            player.namePlate = namePlate;
            player.nameText = nameText;
            player.bodyText = bodyText;
            player.bodyEffect = bodyEffect;
            player.nextIndicator = nextIndicator;
            player.dialogueFont = font;
            player.audioManager = audioManager;

            return (root, player, audioManager);
        }

        private static AudioSource CreateAudioSource(Transform parent, string name, bool loop)
        {
            var go = new GameObject(name, typeof(AudioSource));
            go.transform.SetParent(parent, false);
            var src = go.GetComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f;
            src.volume = 0.7f;
            return src;
        }

        // ---------- 小工具 ----------

        private static RectTransform NewUI(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Image>();
        }

        private static Text NewText(string name, RectTransform parent, Font font, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rt = text.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(10f, 6f);
            rt.offsetMax = new Vector2(-10f, -6f);
            return text;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void AddToBuildSettings(string path)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in list)
                if (s.path == path) return;

            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
