using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VN
{
    /// <summary>
    /// 自定义舞台效果插件。
    ///
    /// 用法：在 Assets 下任意位置新建一个 C# 脚本，实现这个接口即可，
    /// 不用改框架代码。剧本行里填 Id 就能调用。
    ///
    /// <code>
    /// public class MyShake : IVNStageEffect
    /// {
    ///     public string Id => "my_shake";
    ///     public string DisplayName => "我的摇晃";
    ///
    ///     public IEnumerator Play(VNEffectContext ctx)
    ///     {
    ///         var rect = ctx.dialogueRoot;
    ///         var start = rect.anchoredPosition;
    ///         float t = 0f;
    ///         while (t &lt; 0.5f)
    ///         {
    ///             t += Time.deltaTime;
    ///             rect.anchoredPosition = start + new Vector2(Mathf.Sin(t * 60f) * 8f, 0f);
    ///             yield return null;
    ///         }
    ///         rect.anchoredPosition = start;
    ///     }
    /// }
    /// </code>
    /// </summary>
    public interface IVNStageEffect
    {
        /// <summary>剧本里填的名字（唯一，建议英文小写下划线，如 my_shake）。</summary>
        string Id { get; }

        /// <summary>编辑器下拉里显示的名字（写人话，如「我的摇晃」）。</summary>
        string DisplayName { get; }

        /// <summary>
        /// 执行效果。返回的协程播放期间，玩家仍然可以点击推进（不阻塞）。
        /// 想阻塞的话自己在效果里把玩家输入关掉，或者拖长协程。
        /// </summary>
        IEnumerator Play(VNEffectContext ctx);
    }

    /// <summary>传给插件效果的上下文：常用的节点都取好了，直接改就行。</summary>
    public class VNEffectContext
    {
        /// <summary>播放器本体，可以访问公开字段和方法。</summary>
        public VNPlayer player;

        /// <summary>当前这一行（可以读对话文字、角色名等）。</summary>
        public VNLine line;

        /// <summary>当前行号（0 起）。</summary>
        public int lineIndex;

        /// <summary>剧本里给这个效果填的参数（自由字符串，自己解析）。</summary>
        public string parameters;

        /// <summary>镜头作用的容器（背景 + 立绘都在里面）。</summary>
        public RectTransform stageRoot;

        /// <summary>背景两层（交叉淡入用）。</summary>
        public Image backgroundA;
        public Image backgroundB;

        /// <summary>三个立绘槽位。</summary>
        public Image actorLeft;
        public Image actorCenter;
        public Image actorRight;

        /// <summary>对话框根节点（可以整体位移 / 缩放 / 淡出）。</summary>
        public RectTransform dialogueRoot;

        /// <summary>用来 StartCoroutine 的宿主。</summary>
        public MonoBehaviour host;

        /// <summary>按槽位拿立绘，slot 写 left / center / right。</summary>
        public Image GetActor(string slot)
        {
            switch ((slot ?? "").Trim().ToLowerInvariant())
            {
                case "left":
                case "左":
                    return actorLeft;
                case "right":
                case "右":
                    return actorRight;
                default:
                    return actorCenter;
            }
        }

        /// <summary>解析参数里的 key=value（用 ; 分隔），例如 "dist=8;time=0.5"。</summary>
        public string GetParam(string key, string fallback = "")
        {
            if (string.IsNullOrWhiteSpace(parameters) || string.IsNullOrWhiteSpace(key)) return fallback;

            foreach (var part in parameters.Split(';'))
            {
                var kv = part.Split('=');
                if (kv.Length < 2) continue;
                if (kv[0].Trim() == key.Trim()) return kv[1].Trim();
            }

            return fallback;
        }

        /// <summary>参数里的 float，带默认值。</summary>
        public float GetParamFloat(string key, float fallback)
        {
            return float.TryParse(GetParam(key), out var v) ? v : fallback;
        }
    }

    /// <summary>效果注册表：自动扫描所有实现了 IVNStageEffect 的类型。</summary>
    public static class VNStageEffectRegistry
    {
        private static Dictionary<string, IVNStageEffect> _byId;

        public static void EnsureLoaded()
        {
            if (_byId != null) return;

            _byId = new Dictionary<string, IVNStageEffect>(StringComparer.OrdinalIgnoreCase);

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                // 跳过 Unity / 系统程序集，省点时间。
                // 注意：工程路径含中文时 GetName() 可能抛底层异常，拿不到名字就继续扫。
                try
                {
                    var asmName = asm.GetName().Name;
                    if (!string.IsNullOrEmpty(asmName) &&
                        (asmName.StartsWith("Unity") || asmName.StartsWith("System") ||
                         asmName.StartsWith("mscorlib") || asmName.StartsWith("netstandard")))
                        continue;
                }
                catch
                {
                    // 忽略，继续
                }

                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract || t.IsInterface) continue;
                    if (!typeof(IVNStageEffect).IsAssignableFrom(t)) continue;

                    try
                    {
                        var inst = (IVNStageEffect)Activator.CreateInstance(t);
                        if (inst == null || string.IsNullOrWhiteSpace(inst.Id)) continue;

                        var id = inst.Id.Trim();
                        if (_byId.ContainsKey(id))
                        {
                            Debug.LogWarning($"[VN] 自定义效果 Id 冲突：{id}（{t.Name} 被忽略）");
                            continue;
                        }

                        _byId[id] = inst;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[VN] 自定义效果加载失败：{t.Name} - {ex.Message}");
                    }
                }
            }
        }

        public static IVNStageEffect Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            EnsureLoaded();
            return _byId.TryGetValue(id.Trim(), out var inst) ? inst : null;
        }

        /// <summary>所有已注册的效果（编辑器下拉用）。</summary>
        public static List<IVNStageEffect> All()
        {
            EnsureLoaded();
            return new List<IVNStageEffect>(_byId.Values);
        }

        /// <summary>强制重扫（编辑器里新加了脚本后调用）。</summary>
        public static void Reload()
        {
            _byId = null;
            EnsureLoaded();
        }
    }
}
