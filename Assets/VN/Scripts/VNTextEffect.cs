using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VN
{
    /// <summary>
    /// 逐字文字演出：抖动 / 波浪 / 呼吸 / 故障。
    /// 挂在 Text 上，按顶点位移实现，不影响排版和换行。
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class VNTextEffect : BaseMeshEffect
    {
        public enum Mode
        {
            None = 0,
            Shake = 1,
            Wave = 2,
            Pulse = 3,
            Glitch = 4,
        }

        [SerializeField] private Mode mode = Mode.None;
        [SerializeField] private float strength = 3f;
        [SerializeField] private float speed = 1f;

        private readonly List<UIVertex> _verts = new List<UIVertex>();
        private float _time;

        public Mode CurrentMode => mode;

        public void SetMode(string name, float effectStrength, float effectSpeed)
        {
            mode = Parse(name);
            strength = Mathf.Max(0f, effectStrength);
            speed = Mathf.Max(0.01f, effectSpeed);
            MarkDirty();
        }

        public static Mode Parse(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return Mode.None;
            switch (name.Trim().ToLowerInvariant())
            {
                case "shake":
                case "抖动":
                    return Mode.Shake;
                case "wave":
                case "波浪":
                    return Mode.Wave;
                case "pulse":
                case "呼吸":
                    return Mode.Pulse;
                case "glitch":
                case "故障":
                    return Mode.Glitch;
                default:
                    return Mode.None;
            }
        }

        private void MarkDirty()
        {
            if (graphic != null) graphic.SetVerticesDirty();
        }

        private void Update()
        {
            if (mode == Mode.None) return;
            _time += Time.unscaledDeltaTime * speed;
            MarkDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            _verts.Clear();
            vh.GetUIVertexStream(_verts);

            // UGUI Text 展开成三角形后，每个字符固定 6 个顶点
            const int vertsPerChar = 6;

            for (int i = 0; i < _verts.Count; i++)
            {
                var v = _verts[i];
                int charIndex = i / vertsPerChar;
                Apply(ref v.position, charIndex, ref v.color);
                _verts[i] = v;
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(_verts);
        }

        private void Apply(ref Vector3 pos, int charIndex, ref Color32 color)
        {
            float t = _time;
            // 强度按字号缩放：36 号字时 strength 就是像素数，字越大位移越大
            float k = FontScale;
            switch (mode)
            {
                case Mode.Shake:
                {
                    // 用字符索引做确定性相位，避免整行同向抖动
                    float nx = Mathf.PerlinNoise(charIndex * 0.35f, t * 1.7f) - 0.5f;
                    float ny = Mathf.PerlinNoise(charIndex * 0.35f + 11.3f, t * 1.7f) - 0.5f;
                    pos.x += nx * strength * 2f * k;
                    pos.y += ny * strength * 2f * k;
                    break;
                }
                case Mode.Wave:
                {
                    pos.y += Mathf.Sin(t * 2.2f + charIndex * 0.55f) * strength * k;
                    break;
                }
                case Mode.Pulse:
                {
                    float p = (Mathf.Sin(t * 2f + charIndex * 0.18f) + 1f) * 0.5f;
                    color.a = (byte)Mathf.Lerp(color.a * 0.45f, color.a, p);
                    break;
                }
                case Mode.Glitch:
                {
                    float seed = Mathf.PerlinNoise(charIndex * 0.7f, t * 3.1f);
                    if (seed > 0.72f)
                    {
                        pos.x += (seed - 0.72f) * strength * 12f * k;
                        color.a = (byte)Mathf.Clamp(color.a * 0.75f, 0, 255);
                    }
                    break;
                }
            }
        }

        private float FontScale
        {
            get
            {
                var text = graphic as Text;
                return text != null ? Mathf.Max(0.5f, text.fontSize / 36f) : 1f;
            }
        }
    }
}
