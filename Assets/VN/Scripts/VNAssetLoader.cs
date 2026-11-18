using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VN
{
    /// <summary>
    /// 资源加载器。优先按工程内路径（Assets/...）取，这样素材放在任意目录都能用；
    /// 打包后没有 AssetDatabase，则回退到 Resources（把路径改成 Resources 下的相对名即可）。
    /// </summary>
    public static class VNAssetLoader
    {
        public static Sprite LoadSprite(string path)
        {            if (string.IsNullOrWhiteSpace(path)) return null;

#if UNITY_EDITOR
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;

            // 素材还没被导入成 Sprite 时，临时从贴图造一个，避免空白
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null)
            {
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
#endif
            return Resources.Load<Sprite>(ToResourcesPath(path));
        }

        public static AudioClip LoadClip(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

#if UNITY_EDITOR
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null) return clip;
#endif
            return Resources.Load<AudioClip>(ToResourcesPath(path));
        }

        public static Font LoadFont(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

#if UNITY_EDITOR
            var font = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (font != null) return font;
#endif
            return Resources.Load<Font>(ToResourcesPath(path));
        }

        /// <summary>加载结局视频（需要把视频导入成 VideoClip）。</summary>
        public static UnityEngine.Video.VideoClip LoadVideo(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

#if UNITY_EDITOR
            var clip = AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>(path);
            if (clip != null) return clip;
#endif
            return Resources.Load<UnityEngine.Video.VideoClip>(ToResourcesPath(path));
        }

        public static TextAsset LoadText(string path)        {
            if (string.IsNullOrWhiteSpace(path)) return null;

#if UNITY_EDITOR
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (text != null) return text;
#endif
            return Resources.Load<TextAsset>(ToResourcesPath(path));
        }

        /// <summary>把 "Assets/xxx/yyy.png" 变成 Resources 可用的 "xxx/yyy"。</summary>
        public static string ToResourcesPath(string path)        {
            if (string.IsNullOrWhiteSpace(path)) return path;
            var p = path.Replace('\\', '/');

            const string marker = "/Resources/";
            var idx = p.IndexOf(marker, System.StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                p = p.Substring(idx + marker.Length);
            }
            else
            {
                if (p.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                    p = p.Substring("Assets/".Length);
            }

            var dot = p.LastIndexOf('.');
            if (dot > p.LastIndexOf('/')) p = p.Substring(0, dot);
            return p;
        }

        /// <summary>编辑器里把资源路径转成可展示的短名。</summary>
        public static string ShortName(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "(无)";
            var p = path.Replace('\\', '/');
            var cut = p.LastIndexOf('/');
            return cut >= 0 ? p.Substring(cut + 1) : p;
        }
    }
}
