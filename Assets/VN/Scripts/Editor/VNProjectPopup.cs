using UnityEditor;
using UnityEngine;

namespace VN.EditorTools
{
    /// <summary>新建项目的小弹窗：输入项目名（= Data 下的文件夹名）。</summary>
    public class VNProjectPopup : EditorWindow
    {
        private string _name = "";
        private System.Action<string> _onCreate;

        public static void Show(System.Action<string> onCreate)
        {
            var win = CreateInstance<VNProjectPopup>();
            win._onCreate = onCreate;
            win.titleContent = new GUIContent("新建项目");
            win.minSize = new Vector2(380, 150);
            win.maxSize = new Vector2(380, 150);
            win.ShowModalUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("项目名（会成为 Data 下的文件夹名，支持中文）", EditorStyles.miniLabel);
            GUI.SetNextControlName("VNProjectNameField");
            _name = EditorGUILayout.TextField(_name);

            if (Event.current.type == EventType.Repaint)
            {
                if (GUI.GetNameOfFocusedControl() != "VNProjectNameField")
                    GUI.FocusControl("VNProjectNameField");
            }

            EditorGUILayout.Space(6);

            var trimmed = (_name ?? "").Trim();
            bool valid = IsValid(trimmed);
            if (trimmed.Length > 0 && !valid)
                EditorGUILayout.HelpBox("不能包含： / \\ : * ? \" < > |", MessageType.Warning);
            else if (trimmed.Length == 0)
                EditorGUILayout.HelpBox("例如：gameA、另一个游戏、demo2", MessageType.None);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("取消", GUILayout.Width(76))) Close();

            using (new EditorGUI.DisabledScope(!valid))
            {
                if (GUILayout.Button("创建", GUILayout.Width(76)))
                    Create();
            }

            EditorGUILayout.EndHorizontal();

            // 回车直接创建
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && valid)
                Create();
        }

        private void Create()
        {
            var name = (_name ?? "").Trim();
            if (!IsValid(name)) return;

            var callback = _onCreate;
            Close();

            callback?.Invoke(name);
        }

        private static bool IsValid(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            foreach (var c in name)
            {
                if (c == '/' || c == '\\' || c == ':' || c == '*' || c == '?' ||
                    c == '"' || c == '<' || c == '>' || c == '|')
                    return false;
            }

            return name != "." && name != "..";
        }
    }
}
