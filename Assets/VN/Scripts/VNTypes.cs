using System;
using System.Collections.Generic;
using UnityEngine;

namespace VN
{
    /// <summary>立绘槽位。</summary>
    public enum VNSlot
    {
        Left = 0,
        Center = 1,
        Right = 2,
    }

    /// <summary>一个可选项。条件不满足时可以隐藏，也可以显示为灰色不可点。</summary>
    [Serializable]
    public class VNChoice
    {
        [Tooltip("选项文字")]
        public string text = "";

        [Tooltip("选择后跳到的节点 id；留空则用 targetLine 或下一行")]
        public string targetNode = "";

        [Tooltip("选择后跳到的行号（0 起）；-1 = 不用")]
        public int targetLine = -1;

        [Tooltip("显示条件，如 affection >= 3 && !visited(route_a)；留空 = 总是显示")]
        public string condition = "";

        [Tooltip("选择后执行的效果，如 affection += 1; visit(route_a)")]
        public string effects = "";

        [Tooltip("条件不满足时是否仍然显示（灰色不可点）")]
        public bool showWhenLocked = false;

        [Tooltip("条件不满足时显示的文字；留空则用原文字")]
        public string lockedText = "";
    }

    /// <summary>一条剧本指令：一行台词 + 可选的舞台变化。</summary>
    [Serializable]
    public class VNLine
    {
        [Tooltip("说话人；留空则视为旁白，不显示名牌")]
        public string name = "";

        [TextArea(2, 6)]
        public string text = "";

        [Tooltip("背景资源；留空 = 保持不变")]
        public string background = "";

        [Tooltip("背景切换的淡入时长（秒）")]
        public float backgroundFade = 0.45f;

        [Tooltip("背景音乐；留空 = 保持不变。同级名字会做淡入淡出切换")]
        public string bgm = "";

        [Range(0f, 1f)]
        public float bgmVolume = 0.7f;

        [Tooltip("一次性音效；留空 = 不播放")]
        public string sfx = "";

        [Range(0f, 1f)]
        public float sfxVolume = 1f;

        [Tooltip("本行要改变的立绘；为空则沿用上一行的立绘")]
        public List<VNActor> actors = new List<VNActor>();

        [Tooltip("是否应用镜头指令（平移 / 缩放 / 晃动）")]
        public bool useCamera = false;

        public VNCamera camera = new VNCamera();

        public VNTextStyle style = new VNTextStyle();

        [Tooltip("大于 0 时，文字播完后自动等待该秒数再跳到下一行")]
        public float autoDelay = 0f;

        [Tooltip("节点 ID：唯一标识，选项和跳转都靠它定位。留空 = 这一行不是节点")]
        public string nodeId = "";

        [Tooltip("节点显示名：分支树里看到的名字，一般写人话（如「门口 · 第一次选择」）")]
        public string nodeTitle = "";

        [Tooltip("经过这行时执行的效果，如 affection += 1; visit(ch1_start)")]
        public string effects = "";

        [Tooltip("本行的选项：填了就停下来等玩家选，不会自动推进")]
        public List<VNChoice> choices = new List<VNChoice>();

        [Tooltip("高亮人物：这句台词由 left / center / right 哪个立绘来说。该角色保持原色，其他角色自动变暗。留空 = 不压暗任何人")]
        public string speakerSlot = "";

        [Tooltip("转场效果：none（无）/ fadeBlack（黑场）/ fadeWhite（白场）/ flash（白闪）")]
        public string transition = "none";

        [Tooltip("转场持续时间（秒），建议 0.2 ~ 0.8")]
        public float transitionDuration = 0.35f;

        [Tooltip("播放完这行后自动跳到某个节点（留空 = 顺序往下）")]
        public string gotoNode = "";

        [Tooltip("播放完这行后自动跳到某一行（从 0 开始，-1 = 不使用）")]
        public int gotoLine = -1;

        [TextArea(1, 3)]
        [Tooltip("备注，不进游戏，只给你自己看")]
        public string note = "";
    }

    /// <summary>单个槽位的立绘状态。</summary>
    [Serializable]
    public class VNActor
    {
        [Tooltip("槽位：left / center / right")]
        public string slot = "center";

        [Tooltip("立绘资源；留空且 hide=true 时用于隐藏该槽位")]
        public string sprite = "";

        [Tooltip("true = 清空该槽位的立绘")]
        public bool hide = false;

        [Tooltip("入场方式：none / fade / slideUp / slideSide / bounce / pop")]
        public string motion = "fade";

        [Tooltip("相对槽位锚点的像素偏移")]
        public float offsetX = 0f;
        public float offsetY = 0f;

        [Tooltip("缩放倍率")]
        public float scale = 1f;

        [Tooltip("着色，十六进制，如 #FFFFFF")]
        public string tint = "#FFFFFF";

        [Tooltip("淡入时长（秒）")]
        public float fadeDuration = 0.35f;

        [Tooltip("同一张图只改位置/大小时的平移补间时长（秒）")]
        public float moveDuration = 0.4f;

        [Tooltip("角色 id；配合 expression 会自动找 Char/角色/表情.png")]
        public string character = "";

        [Tooltip("表情名；有 character 时生效，留空则用 sprite 字段的整张图")]
        public string expression = "";
    }

    /// <summary>镜头指令：整体平移 / 缩放 / 晃动，只影响背景与立绘，不影响对话框和按钮。</summary>
    [Serializable]
    public class VNCamera
    {
        [Tooltip("横向平移（设计像素，正数向右）")]
        public float offsetX = 0f;

        [Tooltip("纵向平移（设计像素，正数向上）")]
        public float offsetY = 0f;

        [Tooltip("缩放倍率；1 = 原始大小")]
        public float zoom = 1f;

        [Tooltip("平移到目标位置所需时间（秒）")]
        public float duration = 0.6f;

        [Tooltip("晃动强度：none / light / medium / strong")]
        public string shake = "none";

        [Tooltip("晃动持续时间（秒）")]
        public float shakeDuration = 0.5f;

        [Tooltip("晃动幅度（像素）")]
        public float shakeStrength = 18f;
    }

    /// <summary>一行台词的排版与演出参数。</summary>
    [Serializable]
    public class VNTextStyle
    {
        [Tooltip("字号。像素字体建议 12 / 24 / 36 / 48")]
        public int fontSize = 36;

        [Tooltip("每秒显示字符数；0 = 瞬间显示")]
        public float charsPerSecond = 30f;

        [Tooltip("文字演出：none / shake / wave / pulse / glitch")]
        public string effect = "none";

        [Tooltip("演出强度（像素）")]
        public float effectStrength = 3f;

        [Tooltip("演出速度倍率")]
        public float effectSpeed = 1f;

        [Tooltip("文字颜色，十六进制")]
        public string color = "#FFFFFF";

        [Tooltip("是否显示描边（帮助像素字在浅色背景上可读）")]
        public bool outline = true;

        [Tooltip("true = 跳过打字机，直接整段显示")]
        public bool instant = false;
    }

    /// <summary>一个章节的剧本。</summary>
    [Serializable]
    public class VNScript
    {
        public string title = "未命名章节";
        public List<VNLine> lines = new List<VNLine>();
    }

    /// <summary>一条已显示的台词，历史记录用。</summary>
    [Serializable]
    public class VNLogEntry
    {
        public string name = "";
        public string text = "";
        public string nodeTitle = "";
    }

    public static class VNColors
    {
        public static Color Parse(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            var s = hex.Trim();
            if (!s.StartsWith("#")) s = "#" + s;
            return ColorUtility.TryParseHtmlString(s, out var c) ? c : fallback;
        }
    }
}
