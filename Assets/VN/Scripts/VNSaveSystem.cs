using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VN
{
    /// <summary>一个存档槽的完整快照：剧本位置 + 舞台状态。</summary>
    [Serializable]
    public class VNSaveSlot
    {
        public int slot;
        public string scriptPath;
        public int lineIndex;

        public string background;
        public string bgm;
        public float bgmVolume = 0.7f;

        public List<VNActor> actors = new List<VNActor>();

        /// <summary>数值变量 + 经过的节点</summary>
        public VNState state = new VNState();

        /// <summary>玩家做过的选择（分支树）</summary>
        public List<VNChoiceRecord> history = new List<VNChoiceRecord>();

        public string savedAt;
        public string preview;

        public bool IsEmpty => string.IsNullOrEmpty(scriptPath);
    }

    /// <summary>
    /// 存档读写。文件落在 Application.persistentDataPath/vn_saves/slot_N.json，
    /// 每个工程互不干扰，打包后也能用。
    /// </summary>
    public static class VNSaveSystem
    {
        public const int SlotCount = 6;

        /// <summary>自动存档用的特殊槽位（不占 6 个手动槽）。</summary>
        public const int AutoSlot = -1;

        private static string Root => Path.Combine(Application.persistentDataPath, "vn_saves");

        public static string PathFor(int slot)
        {
            return slot < 0
                ? Path.Combine(Root, "auto.json")
                : Path.Combine(Root, $"slot_{slot}.json");
        }

        public static bool Exists(int slot)
        {
            return File.Exists(PathFor(slot));
        }

        public static VNSaveSlot Read(int slot)
        {
            var path = PathFor(slot);
            if (!File.Exists(path)) return null;

            try
            {
                var json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<VNSaveSlot>(json);
                if (data != null) data.slot = slot;
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VN] 读取存档 {slot} 失败：{ex.Message}");
                return null;
            }
        }

        public static bool Write(VNSaveSlot data)
        {
            if (data == null) return false;

            try
            {
                Directory.CreateDirectory(Root);
                data.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                File.WriteAllText(PathFor(data.slot), JsonUtility.ToJson(data, true), System.Text.Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VN] 写入存档 {data.slot} 失败：{ex.Message}");
                return false;
            }
        }

        public static void Delete(int slot)
        {
            try
            {
                var path = PathFor(slot);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VN] 删除存档 {slot} 失败：{ex.Message}");
            }
        }

        public static VNSaveSlot[] ReadAll()
        {
            var result = new VNSaveSlot[SlotCount];
            for (int i = 0; i < SlotCount; i++) result[i] = Read(i);
            return result;
        }

        public static bool HasAny()
        {
            for (int i = 0; i < SlotCount; i++)
                if (Exists(i)) return true;
            return false;
        }

        // ---------------- 缩略图 ----------------

        public static string ThumbPath(int slot)
        {
            return slot < 0
                ? Path.Combine(Root, "auto.png")
                : Path.Combine(Root, $"slot_{slot}.png");
        }

        public static void SaveThumbnail(int slot)
        {
            try
            {
                Directory.CreateDirectory(Root);
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                if (tex == null) return;
                File.WriteAllBytes(ThumbPath(slot), tex.EncodeToPNG());
                UnityEngine.Object.Destroy(tex);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VN] 保存缩略图失败：{ex.Message}");
            }
        }

        public static Texture2D LoadThumbnail(int slot)
        {
            var path = ThumbPath(slot);
            if (!File.Exists(path)) return null;

            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                return tex.LoadImage(File.ReadAllBytes(path)) ? tex : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VN] 读取缩略图失败：{ex.Message}");
                return null;
            }
        }

        public static void DeleteThumbnail(int slot)
        {
            try
            {
                var path = ThumbPath(slot);
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }
    }
}
