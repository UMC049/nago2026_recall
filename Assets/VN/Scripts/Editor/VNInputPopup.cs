using UnityEditor;
using UnityEngine;

namespace VN.EditorTools
{
    /// <summary>通用单行输入弹窗：改项目名、章节名都用它。</summary>
    public class VNInputPopup : EditorWindow
    {
        private string _title = "";
        private string _label = "";
        private string _value = "";
        private System.Action<string> _onOk;

        public static void Show(string title, string label, string initial, System.Action<string> onOk)
        {
            var win = CreateInstance<VNInputPopup>();
            win._title = title;
            win._label = label;
            win._value = initial ?? "";
            win._onOk = onOk;

            win.titleContent = new GUIContent(title);
            win.minSize = new Vector2(400, 150);
            win.maxSize = new Vector2(400, 150);
            win.ShowModalUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField(_label, EditorStyles.miniLabel);
            GUI.SetNextControlName("VNInputField");
            _value = EditorGUILayout.TextField(_value);

            if (Event.current.type == EventType.Repaint &&
                GUI.GetNameOfFocusedControl() != "VNInputField")
            {
                GUI.FocusControl("VNInputField");
            }

            EditorGUILayout.Space(6);

            var trimmed = (_value ?? "").Trim();
            bool valid = IsValid(trimmed);

            if (trimmed.Length > 0 && !valid)
                EditorGUILayout.HelpBox("不能包含： / \\ : * ? \" < > |", MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("取消", GUILayout.Width(76))) Close();

            using (new EditorGUI.DisabledScope(!valid))
            {
                if (GUILayout.Button("确定", GUILayout.Width(76)))
                    Confirm();
            }

            EditorGUILayout.EndHorizontal();

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && valid)
                Confirm();
        }

        private void Confirm()
        {
            var v = (_value ?? "").Trim();
            if (!IsValid(v)) return;

            var cb = _onOk;
            Close();
            cb?.Invoke(v);
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
