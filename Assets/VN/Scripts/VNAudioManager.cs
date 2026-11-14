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

        [Tooltip("说话音（Undertale 风格的合成器音色）专用通道")]
        public AudioSource voice;

        [Header("主音量（系统页可调，PlayerPrefs 记住）")]
        [Range(0f, 1f)] public float globalBgmVolume = 1f;
        [Range(0f, 1f)] public float globalSfxVolume = 1f;

        private const string BgmVolumeKey = "vn.audio.bgm";
        private const string SfxVolumeKey = "vn.audio.sfx";

        private AudioSource _active;
        private float _lineBgmVolume = 0.7f;
        private Coroutine _bgmRoutine;
        private Coroutine _pitchRoutine;
        private string _voicePath = "";

        public string CurrentBgm { get; private set; } = "";

        /// <summary>当前 BGM 实际目标音量 = 剧本给的音量 × 主音量。</summary>
        public float CurrentBgmVolume => Mathf.Clamp01(_lineBgmVolume * globalBgmVolume);

        private void Awake()
        {
            Setup(bgmA);
            Setup(bgmB);
            if (sfx != null) sfx.loop = false;
            if (voice != null)
            {
                voice.loop = false;
                voice.playOnAwake = false;
            }
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
            StopBgm(fadeSeconds, 1f, 0f);
        }

        /// <summary>
        /// 停止 BGM。targetPitch != 1 时会边淡出边变调（「磁带慢慢停下」那种效果）。
        /// pitchDuration 是变调持续时长，0 表示跟着淡出一起走。
        /// </summary>
        public void StopBgm(float fadeSeconds, float targetPitch, float pitchDuration)
        {
            CurrentBgm = "";
            if (_bgmRoutine != null) StopCoroutine(_bgmRoutine);
            _bgmRoutine = StartCoroutine(FadeOutAll(
                Mathf.Max(0.01f, fadeSeconds),
                Mathf.Clamp(targetPitch, 0.05f, 4f),
                pitchDuration > 0f ? pitchDuration : -1f));
        }

        /// <summary>只改变当前 BGM 的音调（不停止）。</summary>
        public void TweenPitch(float targetPitch, float duration)
        {
            if (_active == null || !_active.isPlaying) return;
            targetPitch = Mathf.Clamp(targetPitch, 0.05f, 4f);

            if (duration <= 0f)
            {
                _active.pitch = targetPitch;
                return;
            }

            if (_pitchRoutine != null) StopCoroutine(_pitchRoutine);
            _pitchRoutine = StartCoroutine(TweenPitchRoutine(_active, targetPitch, duration));
        }

        private IEnumerator TweenPitchRoutine(AudioSource src, float target, float duration)
        {
            float start = src.pitch;
            float t = 0f;
            while (t < duration && src != null)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                src.pitch = Mathf.Lerp(start, target, k);
                yield return null;
            }
            if (src != null) src.pitch = target;
            _pitchRoutine = null;
        }

        /// <summary>重新播放当前曲目（从头开始）。</summary>
        public void RestartBgm(float fadeSeconds)
        {
            var path = CurrentBgm;
            if (string.IsNullOrEmpty(path)) return;
            CurrentBgm = "";
            PlayBgm(path, _lineBgmVolume, Mathf.Max(0.01f, fadeSeconds));
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
            to.pitch = 1f;
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

        private IEnumerator FadeOutAll(float fade, float targetPitch = 1f, float pitchDuration = -1f)
        {
            float a = bgmA != null ? bgmA.volume : 0f;
            float b = bgmB != null ? bgmB.volume : 0f;
            float pitchA = bgmA != null ? bgmA.pitch : 1f;
            float pitchB = bgmB != null ? bgmB.pitch : 1f;

            bool doPitch = Mathf.Abs(targetPitch - 1f) > 0.001f;
            float pd = pitchDuration < 0f ? fade : Mathf.Max(0.01f, pitchDuration);
            float total = Mathf.Max(fade, doPitch ? pd : 0f);

            float t = 0f;
            while (t < total)
            {
                t += Time.unscaledDeltaTime;

                float kVol = fade > 0f ? Mathf.Clamp01(t / fade) : 1f;
                if (bgmA != null) bgmA.volume = Mathf.Lerp(a, 0f, kVol);
                if (bgmB != null) bgmB.volume = Mathf.Lerp(b, 0f, kVol);

                if (doPitch)
                {
                    float kP = Mathf.Clamp01(t / pd);
                    if (bgmA != null) bgmA.pitch = Mathf.Lerp(pitchA, targetPitch, kP);
                    if (bgmB != null) bgmB.pitch = Mathf.Lerp(pitchB, targetPitch, kP);
                }

                yield return null;
            }

            if (bgmA != null) { bgmA.Stop(); bgmA.volume = 0f; bgmA.pitch = 1f; }
            if (bgmB != null) { bgmB.Stop(); bgmB.volume = 0f; bgmB.pitch = 1f; }
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

        // ---------------- 说话音 ----------------

        /// <summary>逐字模式：触发一次短音，带轻微音调抖动避免机械感。</summary>
        public void PlayVoiceOnce(string path, float volume, float pitchRandom)
        {
            if (string.IsNullOrWhiteSpace(path) || voice == null) return;

            var clip = VNAssetLoader.LoadClip(path);
            if (clip == null) return;

            float basePitch = 1f;
            if (pitchRandom > 0.0001f)
                basePitch += UnityEngine.Random.Range(-pitchRandom, pitchRandom);

            voice.pitch = Mathf.Clamp(basePitch, 0.5f, 2f);
            voice.PlayOneShot(clip, Mathf.Clamp01(volume) * Mathf.Clamp01(globalSfxVolume));
        }

        /// <summary>循环模式：打字期间持续播放。</summary>
        public void StartVoiceLoop(string path, float volume)
        {
            if (string.IsNullOrWhiteSpace(path) || voice == null) return;
            if (_voicePath == path && voice.isPlaying) return;

            var clip = VNAssetLoader.LoadClip(path);
            if (clip == null) return;

            _voicePath = path;
            voice.clip = clip;
            voice.loop = true;
            voice.pitch = 1f;
            voice.volume = Mathf.Clamp01(volume) * Mathf.Clamp01(globalSfxVolume);
            voice.Play();
        }

        public void PauseVoice()
        {
            if (voice != null && voice.isPlaying) voice.Pause();
        }

        public void ResumeVoice()
        {
            if (voice != null && voice.clip != null && !voice.isPlaying) voice.UnPause();
        }

        public void StopVoice()
        {
            if (voice == null) return;
            voice.Stop();
            voice.loop = false;
            voice.clip = null;
            _voicePath = "";
        }
    }
}
