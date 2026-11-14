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

        private static string Root => Path.Combine(Application.persistentDataPath, "vn_saves");

        public static string PathFor(int slot)
        {
            return Path.Combine(Root, $"slot_{slot}.json");
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
    }
}
