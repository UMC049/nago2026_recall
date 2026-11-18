using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VN.EditorTools
{
    /// <summary>一键打包：菜单「VN → 打包 Windows」，或由脚本调用 Schedule()。</summary>
    public static class VNBuilder
    {
        public const string ScenePath = "Assets/VN/Scenes/VN.unity";
        public const string OutputPath = "Build/nago2026_recall.exe";

        [MenuItem("VN/打包 Windows (x64)", false, 40)]
        public static void BuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog("打包 Windows", "输出到 " + OutputPath + "，开始？", "开始", "取消"))
                return;

            Schedule();
        }

        /// <summary>把打包排到下一帧，避免阻塞当前的操作（比如 MCP 调用）。</summary>
        public static void Schedule()
        {
            EditorApplication.delayCall += DoBuild;
        }

        private static void DoBuild()
        {
            var scenes = new[] { ScenePath };
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            if (!System.IO.Directory.Exists("Build"))
                System.IO.Directory.CreateDirectory("Build");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            Debug.Log("[VN] 开始打包：" + OutputPath);

            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;

            Debug.Log($"[VN] 打包结束：{s.result}，{s.totalSize / (1024 * 1024)}MB，错误 {s.totalErrors} 个，输出 {s.outputPath}");

            if (s.result == BuildResult.Succeeded)
                EditorUtility.RevealInFinder(OutputPath);
        }
    }
}
