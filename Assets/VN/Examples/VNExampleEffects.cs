using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace VN.Examples
{
    // ============================================================
    //  自定义效果示例
    //
    //  把这类脚本放在 Assets 下任意位置，框架会自动扫描到。
    //  剧本行里填 Id 就能调用，不需要改框架代码。
    //
    //  参数从 ctx.GetParam("key", "默认值") 读，
    //  剧本里写成 "key=value;key2=value2"。
    // ============================================================

    /// <summary>对话框左右晃动。参数：dist=8;time=0.5</summary>
    public class VNExampleDialogueShake : IVNStageEffect
    {
        public string Id => "dialogue_shake";
        public string DisplayName => "对话框晃动";

        public IEnumerator Play(VNEffectContext ctx)
        {
            if (ctx.dialogueRoot == null) yield break;

            float dist = ctx.GetParamFloat("dist", 8f);
            float time = Mathf.Max(0.05f, ctx.GetParamFloat("time", 0.45f));

            var rect = ctx.dialogueRoot;
            var origin = rect.anchoredPosition;

            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                float damp = 1f - (t / time);
                rect.anchoredPosition = origin + new Vector2(
                    Mathf.Sin(t * 70f) * dist * damp,
                    Mathf.Cos(t * 50f) * dist * 0.35f * damp);

                yield return null;
            }

            rect.anchoredPosition = origin;
        }
    }

    /// <summary>指定立绘上下弹一下。参数：slot=center;height=40;time=0.35</summary>
    public class VNExampleActorBounce : IVNStageEffect
    {
        public string Id => "actor_bounce";
        public string DisplayName => "立绘弹跳";

        public IEnumerator Play(VNEffectContext ctx)
        {
            var img = ctx.GetActor(ctx.GetParam("slot", "center"));
            if (img == null) yield break;

            float height = ctx.GetParamFloat("height", 40f);
            float time = Mathf.Max(0.1f, ctx.GetParamFloat("time", 0.35f));

            var rect = img.rectTransform;
            var origin = rect.anchoredPosition;

            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(t / time) * Mathf.PI);
                rect.anchoredPosition = origin + new Vector2(0f, k * height);

                yield return null;
            }

            rect.anchoredPosition = origin;
        }
    }

    /// <summary>镜头推近一下再回来。参数：zoom=1.15;time=0.8</summary>
    public class VNExampleCameraKick : IVNStageEffect
    {
        public string Id => "camera_kick";
        public string DisplayName => "镜头推近";

        public IEnumerator Play(VNEffectContext ctx)
        {
            if (ctx.stageRoot == null) yield break;

            float zoom = ctx.GetParamFloat("zoom", 1.12f);
            float time = Mathf.Max(0.2f, ctx.GetParamFloat("time", 0.7f));

            var rect = ctx.stageRoot;
            var origin = rect.localScale;

            float half = time * 0.45f;

            // 推近
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                rect.localScale = Vector3.Lerp(origin, origin * zoom, t / half);
                yield return null;
            }

            // 拉回
            t = 0f;
            while (t < time - half)
            {
                t += Time.deltaTime;
                rect.localScale = Vector3.Lerp(origin * zoom, origin, t / (time - half));
                yield return null;
            }

            rect.localScale = origin;
        }
    }

    /// <summary>整屏淡红再淡出（受伤 / 冲击）。参数：color=#FF3060;time=0.4</summary>
    public class VNExampleFlash : IVNStageEffect
    {
        public string Id => "screen_flash";
        public string DisplayName => "全屏闪色";

        public IEnumerator Play(VNEffectContext ctx)
        {
            var hex = ctx.GetParam("color", "#FFFFFF");
            float time = Mathf.Max(0.1f, ctx.GetParamFloat("time", 0.35f));

            if (!ColorUtility.TryParseHtmlString(hex, out var color)) color = Color.white;

            // 在立绘父级建一个临时全屏图
            var parent = ctx.stageRoot != null ? ctx.stageRoot.parent : null;
            if (parent == null) yield break;

            var go = new GameObject("VN_Flash", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;

            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                var c = color;
                c.a = 1f - (t / time);
                img.color = c;
                yield return null;
            }

            Object.Destroy(go);
        }
    }
}
