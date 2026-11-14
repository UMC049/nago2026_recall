using System;
using System.Collections.Generic;
using UnityEngine;

namespace VN
{
    [Serializable]
    public class VNVarEntry
    {
        public string key;
        public float value;
    }

    /// <summary>玩家做过的选择（用于分支树展示）。</summary>
    [Serializable]
    public class VNChoiceRecord
    {
        public string nodeId;
        public string nodeTitle;
        public string choiceText;
        public string targetNode;
        public string targetTitle;
        public int lineIndex;
        public string time;
    }

    /// <summary>
    /// 玩家状态：数值变量 + 经过的节点。全部可 JSON 序列化（JsonUtility 不支持 Dictionary）。
    /// </summary>
    [Serializable]
    public class VNState
    {
        public List<VNVarEntry> numbers = new List<VNVarEntry>();
        public List<string> visited = new List<string>();

        public float Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0f;
            for (int i = 0; i < numbers.Count; i++)
                if (numbers[i].key == name) return numbers[i].value;
            return 0f;
        }

        public void Set(string name, float value)
        {
            if (string.IsNullOrEmpty(name)) return;
            for (int i = 0; i < numbers.Count; i++)
            {
                if (numbers[i].key == name)
                {
                    numbers[i].value = value;
                    return;
                }
            }
            numbers.Add(new VNVarEntry { key = name, value = value });
        }

        public void Add(string name, float delta)
        {
            Set(name, Get(name) + delta);
        }

        public bool IsVisited(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return false;
            return visited.Contains(nodeId);
        }

        public void Visit(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return;
            if (!visited.Contains(nodeId)) visited.Add(nodeId);
        }

        public void Clear()
        {
            numbers.Clear();
            visited.Clear();
        }

        public VNState Clone()
        {
            return JsonUtility.FromJson<VNState>(JsonUtility.ToJson(this)) ?? new VNState();
        }
    }

    /// <summary>
    /// 条件表达式求值。语法故意做得简单，创作者不用学编程：
    ///   数值比较   affection >= 3
    ///   布尔比较   met_alice == 1        （flag 就是 0/1 的数值）
    ///   节点判定   visited(route_a)
    ///   逻辑组合   a >= 3 &amp;&amp; (b &lt; 2 || visited(c))
    ///   取反       !visited(route_a)
    /// 空表达式视为「总是成立」。
    /// </summary>
    public static class VNConditions
    {
        public static bool Evaluate(string expression, VNState state)
        {
            if (string.IsNullOrWhiteSpace(expression)) return true;

            try
            {
                var parser = new Parser(expression, state ?? new VNState());
                bool result = parser.ParseOr();
                parser.EnsureEnd();
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VN] 条件表达式有问题：'{expression}' -> {ex.Message}（按 true 处理）");
                return true;
            }
        }

        private class Parser
        {
            private readonly string _src;
            private readonly VNState _state;
            private int _pos;

            public Parser(string src, VNState state)
            {
                _src = src;
                _state = state;
            }

            public bool ParseOr()
            {
                bool left = ParseAnd();
                while (TryConsume("||"))
                {
                    bool right = ParseAnd();
                    left = left || right;
                }
                return left;
            }

            private bool ParseAnd()
            {
                bool left = ParseUnary();
                while (TryConsume("&&"))
                {
                    bool right = ParseUnary();
                    left = left && right;
                }
                return left;
            }

            private bool ParseUnary()
            {
                if (TryConsume("!")) return !ParseUnary();
                return ParsePrimary();
            }

            private bool ParsePrimary()
            {
                SkipSpace();

                if (TryConsume("("))
                {
                    bool v = ParseOr();
                    Require(")");
                    return v;
                }

                // visited(node)
                if (TryConsumeWord("visited"))
                {
                    Require("(");
                    string node = ReadIdent();
                    Require(")");
                    return _state.IsVisited(node);
                }

                // 左操作数
                float left = ReadOperand();

                // 比较符
                string op = ReadOperator();
                if (op == null) return Math.Abs(left) > 0.0001f; // 单独一个变量：非 0 即真

                float right = ReadOperand();

                switch (op)
                {
                    case ">=": return left >= right;
                    case "<=": return left <= right;
                    case ">": return left > right;
                    case "<": return left < right;
                    case "!=": return Math.Abs(left - right) > 0.0001f;
                    default: return Math.Abs(left - right) < 0.0001f; // "=" 与 "=="
                }
            }

            private float ReadOperand()
            {
                SkipSpace();
                if (_pos >= _src.Length) throw new Exception("表达式结尾缺少值");

                // 数字
                if (char.IsDigit(_src[_pos]) || _src[_pos] == '-' || _src[_pos] == '.')
                {
                    int start = _pos;
                    if (_src[_pos] == '-') _pos++;
                    while (_pos < _src.Length && (char.IsDigit(_src[_pos]) || _src[_pos] == '.')) _pos++;
                    return float.Parse(_src.Substring(start, _pos - start));
                }

                // true / false
                SkipSpace();
                int wordStart = _pos;
                string word = ReadIdent();
                if (word.Equals("true", StringComparison.OrdinalIgnoreCase)) return 1f;
                if (word.Equals("false", StringComparison.OrdinalIgnoreCase)) return 0f;

                // 变量名
                return _state.Get(word);
            }

            private string ReadOperator()
            {
                SkipSpace();
                if (_pos >= _src.Length) return null;

                if (Match(">=")) return ">=";
                if (Match("<=")) return "<=";
                if (Match("!=")) return "!=";
                if (Match("==")) return "==";
                if (Match(">")) return ">";
                if (Match("<")) return "<";
                if (Match("=")) return "=";
                return null;
            }

            private string ReadIdent()
            {
                SkipSpace();
                int start = _pos;
                while (_pos < _src.Length)
                {
                    char c = _src[_pos];
                    if (char.IsLetterOrDigit(c) || c == '_' || c == '.') _pos++;
                    else break;
                }
                if (_pos == start) throw new Exception($"这里需要一个名字（位置 {_pos}）");
                return _src.Substring(start, _pos - start);
            }

            private bool TryConsume(string token)
            {
                SkipSpace();
                if (Match(token)) return true;
                return false;
            }

            private bool Match(string token)
            {
                if (_pos + token.Length > _src.Length) return false;
                if (string.CompareOrdinal(_src, _pos, token, 0, token.Length) != 0) return false;
                _pos += token.Length;
                return true;
            }

            private bool TryConsumeWord(string word)
            {
                SkipSpace();
                if (_pos + word.Length > _src.Length) return false;
                if (string.Compare(_src, _pos, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) != 0) return false;
                int next = _pos + word.Length;
                if (next < _src.Length && (char.IsLetterOrDigit(_src[next]) || _src[next] == '_')) return false;
                _pos = next;
                return true;
            }

            private void Require(string token)
            {
                SkipSpace();
                if (!Match(token)) throw new Exception($"缺少 '{token}'（位置 {_pos}）");
            }

            private void SkipSpace()
            {
                while (_pos < _src.Length && char.IsWhiteSpace(_src[_pos])) _pos++;
            }

            public void EnsureEnd()
            {
                SkipSpace();
                if (_pos < _src.Length) throw new Exception($"多余的内容：'{_src.Substring(_pos)}'");
            }
        }
    }
}
