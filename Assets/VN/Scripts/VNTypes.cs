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
