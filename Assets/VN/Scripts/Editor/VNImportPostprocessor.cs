using UnityEditor;
using UnityEngine;

namespace VN.EditorTools
{
    /// <summary>
    /// 自动把视觉小说素材导入成正确的类型：
    ///   - Assets/Arts/** 与 Assets/VN/Resources/** 下的图片 → Sprite（Point 过滤、无压缩、无 mipmap）
    ///   - Assets/Sounds/** 下的音频        → 便于即时播放的设置
    /// 这样你把素材丢进目录就可用，不需要手动改 Importer。
    /// </summary>
    public class VNImportPostprocessor : AssetPostprocessor
    {
        private static readonly string[] ImageRoots =
        {
            "Assets/Arts/",
            "Assets/VN/Resources/",
        };

        private static readonly string[] AudioRoots =
        {
            "Assets/Sounds/",
            "Assets/VN/Resources/",
        };

        private void OnPreprocessTexture()
        {
            var path = assetPath.Replace('\\', '/');
            if (!StartsWithAny(path, ImageRoots)) return;

            var importer = assetImporter as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
        }

        private void OnPreprocessAudio()
        {
            var path = assetPath.Replace('\\', '/');
            if (!StartsWithAny(path, AudioRoots)) return;

            var importer = assetImporter as AudioImporter;
            if (importer == null) return;

            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
        }

        private static bool StartsWithAny(string path, string[] roots)
        {
            for (int i = 0; i < roots.Length; i++)
                if (path.StartsWith(roots[i], System.StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
