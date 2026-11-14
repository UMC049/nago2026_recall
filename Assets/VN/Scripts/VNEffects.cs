using System;
using UnityEngine;

namespace VN
{
    /// <summary>
    /// 效果语句执行。语法：
    ///   affection += 1
    ///   trust -= 2
    ///   flag_met = 1
    ///   visit(route_a)
    /// 多条用分号分隔：affection += 1; flag_met = 1
    /// </summary>
    public static class VNEffects
    {
        public static void Apply(string effects, VNState state)
        {
            if (string.IsNullOrWhiteSpace(effects) || state == null) return;

            foreach (var raw in effects.Split(';'))
            {
                var stmt = raw.Trim();
                if (stmt.Length == 0) continue;

                try
                {
                    ApplyOne(stmt, state);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[VN] 效果语句有问题：'{stmt}' -> {ex.Message}");
                }
            }
        }

        private static void ApplyOne(string stmt, VNState state)
        {
            // visit(node)
            if (stmt.StartsWith("visit", StringComparison.OrdinalIgnoreCase))
            {
                int l = stmt.IndexOf('(');
                int r = stmt.LastIndexOf(')');
                if (l < 0 || r <= l) throw new Exception("写法应为 visit(节点id)");
                var node = stmt.Substring(l + 1, r - l - 1).Trim().Trim('"', '\'');
                state.Visit(node);
                return;
            }

            // += / -= / =
            int plus = stmt.IndexOf("+=", StringComparison.Ordinal);
            if (plus > 0)
            {
                state.Add(stmt.Substring(0, plus).Trim(), ParseValue(stmt.Substring(plus + 2)));
                return;
            }

            int minus = stmt.IndexOf("-=", StringComparison.Ordinal);
            if (minus > 0)
            {
                state.Add(stmt.Substring(0, minus).Trim(), -ParseValue(stmt.Substring(minus + 2)));
                return;
            }

            int eq = stmt.IndexOf('=');
            if (eq > 0)
            {
                var name = stmt.Substring(0, eq).Trim();
                var value = ParseValue(stmt.Substring(eq + 1));
                state.Set(name, value);
                return;
            }

            throw new Exception("无法识别的语句");
        }

        private static float ParseValue(string raw)
        {
            var v = raw.Trim();

            if (v.Equals("true", StringComparison.OrdinalIgnoreCase)) return 1f;
            if (v.Equals("false", StringComparison.OrdinalIgnoreCase)) return 0f;

            if (float.TryParse(v, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var f))
                return f;

            throw new Exception($"'{v}' 不是数字");
        }
    }
}
