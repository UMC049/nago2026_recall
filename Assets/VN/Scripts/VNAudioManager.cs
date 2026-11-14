using System.Collections;
using UnityEngine;

namespace VN
{
    /// <summary>BGM 交叉淡入淡出 + 音效播放。</summary>
    public class VNAudioManager : MonoBehaviour
    {
        [Header("由场景构建器自动赋值")]
        public AudioSource bgmA;
        public AudioSource bgmB;
        public AudioSource sfx;

        [Header("主音量（系统页可调，PlayerPrefs 记住）")]
        [Range(0f, 1f)] public float globalBgmVolume = 1f;
        [Range(0f, 1f)] public float globalSfxVolume = 1f;

        private const string BgmVolumeKey = "vn.audio.bgm";
        private const string SfxVolumeKey = "vn.audio.sfx";

        private AudioSource _active;
        private float _lineBgmVolume = 0.7f;
        private Coroutine _bgmRoutine;

        public string CurrentBgm { get; private set; } = "";

        /// <summary>当前 BGM 实际目标音量 = 剧本给的音量 × 主音量。</summary>
        public float CurrentBgmVolume => Mathf.Clamp01(_lineBgmVolume * globalBgmVolume);

        private void Awake()
        {
            Setup(bgmA);
            Setup(bgmB);
            if (sfx != null) sfx.loop = false;
            _active = bgmA;

            globalBgmVolume = PlayerPrefs.GetFloat(BgmVolumeKey, 1f);
            globalSfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
        }

        public void SetGlobalBgmVolume(float value)
        {
            globalBgmVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(BgmVolumeKey, globalBgmVolume);
            PlayerPrefs.Save();

            if (_bgmRoutine == null && _active != null && _active.isPlaying)
                _active.volume = CurrentBgmVolume;
        }

        public void SetGlobalSfxVolume(float value)
        {
            globalSfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxVolumeKey, globalSfxVolume);
            PlayerPrefs.Save();
        }

        private static void Setup(AudioSource src)
        {
            if (src == null) return;
            src.loop = true;
            src.playOnAwake = false;
            src.volume = 0f;
        }

        /// <summary>播放 BGM；与当前相同则不打断。</summary>
        public void PlayBgm(string path, float volume, float fadeSeconds)
        {
            _lineBgmVolume = Mathf.Clamp01(volume);
            if (string.IsNullOrWhiteSpace(path))
            {
                if (!string.IsNullOrEmpty(CurrentBgm)) StopBgm(fadeSeconds);
                return;
            }

            if (path == CurrentBgm)
            {
                if (_bgmRoutine == null && _active != null && !_active.isPlaying)
                    _active.Play();
                return;
            }

            var clip = VNAssetLoader.LoadClip(path);
            if (clip == null)
            {
                Debug.LogWarning($"[VN] 找不到 BGM：{path}");
                return;
            }

            CurrentBgm = path;
            SwapTo(clip, Mathf.Max(0.01f, fadeSeconds));
        }

        public void StopBgm(float fadeSeconds)
        {
            CurrentBgm = "";
            if (_bgmRoutine != null) StopCoroutine(_bgmRoutine);
            _bgmRoutine = StartCoroutine(FadeOutAll(Mathf.Max(0.01f, fadeSeconds)));
        }

        private void SwapTo(AudioClip clip, float fade)
        {
            if (_bgmRoutine != null) StopCoroutine(_bgmRoutine);
            _bgmRoutine = StartCoroutine(CrossFade(clip, fade));
        }

        private IEnumerator CrossFade(AudioClip clip, float fade)
        {
            var from = _active;
            var to = ReferenceEquals(_active, bgmA) ? bgmB : bgmA;
            if (to == null) to = from;

            to.clip = clip;
            to.volume = 0f;
            to.Play();

            float startFrom = from != null ? from.volume : 0f;
            float t = 0f;
            while (t < fade)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / fade);
                if (from != null) from.volume = Mathf.Lerp(startFrom, 0f, k);
                to.volume = Mathf.Lerp(0f, CurrentBgmVolume, k);
                yield return null;
            }

            to.volume = CurrentBgmVolume;
            if (from != null)
            {
                from.Stop();
                from.volume = 0f;
            }

            _active = to;
            _bgmRoutine = null;
        }

        private IEnumerator FadeOutAll(float fade)
        {
            float a = bgmA != null ? bgmA.volume : 0f;
            float b = bgmB != null ? bgmB.volume : 0f;
            float t = 0f;
            while (t < fade)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / fade);
                if (bgmA != null) bgmA.volume = Mathf.Lerp(a, 0f, k);
                if (bgmB != null) bgmB.volume = Mathf.Lerp(b, 0f, k);
                yield return null;
            }

            if (bgmA != null) { bgmA.Stop(); bgmA.volume = 0f; }
            if (bgmB != null) { bgmB.Stop(); bgmB.volume = 0f; }
            _bgmRoutine = null;
        }

        public void PlaySfx(string path, float volume)
        {
            if (string.IsNullOrWhiteSpace(path) || sfx == null) return;
            var clip = VNAssetLoader.LoadClip(path);
            if (clip == null)
            {
                Debug.LogWarning($"[VN] 找不到音效：{path}");
                return;
            }
            sfx.PlayOneShot(clip, Mathf.Clamp01(volume) * Mathf.Clamp01(globalSfxVolume));
        }
    }
}
