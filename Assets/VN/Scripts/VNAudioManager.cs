using System.Collections;
using System.Collections.Generic;
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
        private string _lastBgmPath = "";
        private float _lastBgmTime = 0f;

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
            PlayBgm(path, volume, fadeSeconds, false, 1f);
        }

        /// <summary>
        /// 播放 BGM。
        /// resume = true 且和上次停掉的曲目相同时，从上次的位置继续。
        /// initialPitch 是这首曲子的起始音调（1 = 原调）。
        /// </summary>
        public void PlayBgm(string path, float volume, float fadeSeconds, bool resume, float initialPitch)
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

            float startTime = 0f;
            if (resume && path == _lastBgmPath && _lastBgmTime > 0f)
            {
                startTime = Mathf.Min(_lastBgmTime, Mathf.Max(0f, clip.length - 0.05f));
            }

            CurrentBgm = path;
            SwapTo(clip, Mathf.Max(0.01f, fadeSeconds), startTime, Mathf.Clamp(initialPitch, 0.05f, 4f));
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
            // 记录进度，方便以后 resume
            if (_active != null && _active.isPlaying && !string.IsNullOrEmpty(CurrentBgm))
            {
                _lastBgmPath = CurrentBgm;
                _lastBgmTime = _active.time;
            }

            CurrentBgm = "";
            if (_bgmRoutine != null) StopCoroutine(_bgmRoutine);
            _bgmRoutine = StartCoroutine(FadeOutAll(
                Mathf.Max(0.01f, fadeSeconds),
                Mathf.Clamp(targetPitch, 0.05f, 4f),
                pitchDuration > 0f ? pitchDuration : -1f));
        }

        /// <summary>
        /// 把当前 BGM 渐变到指定音量（不停止），可以同时变调。
        /// 适合「音乐渐渐变弱变低」这种氛围演出。
        /// </summary>
        public void FadeBgmTo(float targetVolume, float duration, float targetPitch, float pitchDuration)
        {
            if (_active == null || !_active.isPlaying) return;

            _lineBgmVolume = Mathf.Clamp01(targetVolume);

            if (_bgmRoutine != null) StopCoroutine(_bgmRoutine);
            _bgmRoutine = StartCoroutine(FadeBgmRoutine(
                _active,
                CurrentBgmVolume,
                Mathf.Max(0.01f, duration),
                Mathf.Clamp(targetPitch, 0.05f, 4f),
                pitchDuration > 0f ? pitchDuration : -1f));
        }

        private IEnumerator FadeBgmRoutine(AudioSource src, float targetVol, float duration, float targetPitch, float pitchDuration)
        {
            float startVol = src.volume;
            float startPitch = src.pitch;
            bool doPitch = Mathf.Abs(targetPitch - 1f) > 0.001f;
            float pd = pitchDuration > 0f ? pitchDuration : duration;
            float total = Mathf.Max(duration, doPitch ? pd : 0f);

            float t = 0f;
            while (t < total && src != null)
            {
                t += Time.unscaledDeltaTime;
                src.volume = Mathf.Lerp(startVol, targetVol, Mathf.Clamp01(t / duration));
                if (doPitch) src.pitch = Mathf.Lerp(startPitch, targetPitch, Mathf.Clamp01(t / pd));
                yield return null;
            }

            if (src != null)
            {
                src.volume = targetVol;
                if (doPitch) src.pitch = targetPitch;
            }
            _bgmRoutine = null;
        }

        /// <summary>只改变当前 BGM 的音调（不停止）。</summary>
        public void TweenPitch(float targetPitch, float duration)        {
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

        private void SwapTo(AudioClip clip, float fade, float startTime, float pitch)
        {
            if (_bgmRoutine != null) StopCoroutine(_bgmRoutine);
            _bgmRoutine = StartCoroutine(CrossFade(clip, fade, startTime, pitch));
        }

        private IEnumerator CrossFade(AudioClip clip, float fade, float startTime, float pitch)
        {
            var from = _active;
            var to = ReferenceEquals(_active, bgmA) ? bgmB : bgmA;
            if (to == null) to = from;

            to.clip = clip;
            to.pitch = pitch;
            to.time = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, clip.length - 0.05f));
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

        // ---------------- 多音轨 ----------------

        private readonly Dictionary<string, AudioSource> _tracks = new Dictionary<string, AudioSource>();
        private readonly Dictionary<string, Coroutine> _trackRoutines = new Dictionary<string, Coroutine>();
        private readonly Dictionary<string, float> _trackVolume = new Dictionary<string, float>();

        private AudioSource GetTrack(string track)
        {
            var key = string.IsNullOrWhiteSpace(track) ? "ambient" : track.Trim().ToLowerInvariant();

            if (_tracks.TryGetValue(key, out var src) && src != null) return src;

            var go = new GameObject("Track_" + key, typeof(AudioSource));
            go.transform.SetParent(transform, false);

            src = go.GetComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.volume = 0f;

            _tracks[key] = src;
            return src;
        }

        /// <summary>在指定音轨上播放（不同音轨可以同时出声）。</summary>
        public void PlayTrack(string track, string path, float volume, float fade, bool loop, float pitch)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            var clip = VNAssetLoader.LoadClip(path);
            if (clip == null)
            {
                Debug.LogWarning($"[VN] 找不到音频：{path}");
                return;
            }

            var key = string.IsNullOrWhiteSpace(track) ? "ambient" : track.Trim().ToLowerInvariant();
            var src = GetTrack(key);

            src.clip = clip;
            src.loop = loop;
            src.pitch = Mathf.Clamp(pitch, 0.1f, 4f);
            src.time = 0f;

            float target = Mathf.Clamp01(volume) * Mathf.Clamp01(globalBgmVolume);
            _trackVolume[key] = target;

            if (_trackRoutines.TryGetValue(key, out var run) && run != null) StopCoroutine(run);
            _trackRoutines[key] = StartCoroutine(FadeTrackIn(src, target, Mathf.Max(0.01f, fade)));

            src.Play();
        }

        public void StopTrack(string track, float fade, float pitch = 1f, float pitchDuration = 0f)
        {
            var key = string.IsNullOrWhiteSpace(track) ? "ambient" : track.Trim().ToLowerInvariant();
            if (!_tracks.TryGetValue(key, out var src) || src == null) return;

            if (_trackRoutines.TryGetValue(key, out var run) && run != null) StopCoroutine(run);
            _trackRoutines[key] = StartCoroutine(FadeTrackOut(src, Mathf.Max(0.01f, fade), pitch, pitchDuration));
        }

        public void SetTrackPitch(string track, float pitch, float duration)
        {
            var key = string.IsNullOrWhiteSpace(track) ? "ambient" : track.Trim().ToLowerInvariant();
            if (!_tracks.TryGetValue(key, out var src) || src == null) return;

            pitch = Mathf.Clamp(pitch, 0.1f, 4f);

            if (duration <= 0f)
            {
                src.pitch = pitch;
                return;
            }

            if (_trackRoutines.TryGetValue(key, out var run) && run != null) StopCoroutine(run);
            _trackRoutines[key] = StartCoroutine(TweenPitchRoutine(src, pitch, duration));
        }

        public void SetTrackVolume(string track, float volume)
        {
            var key = string.IsNullOrWhiteSpace(track) ? "ambient" : track.Trim().ToLowerInvariant();
            if (!_tracks.TryGetValue(key, out var src) || src == null) return;

            float v = Mathf.Clamp01(volume) * Mathf.Clamp01(globalBgmVolume);
            _trackVolume[key] = v;
            src.volume = v;
        }

        private IEnumerator FadeTrackIn(AudioSource src, float target, float duration)
        {
            float start = src.volume;
            float t = 0f;
            while (t < duration && src != null)
            {
                t += Time.unscaledDeltaTime;
                src.volume = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
                yield return null;
            }
            if (src != null) src.volume = target;
        }

        private IEnumerator FadeTrackOut(AudioSource src, float duration, float targetPitch, float pitchDuration)
        {
            float startVol = src.volume;
            float startPitch = src.pitch;
            bool doPitch = Mathf.Abs(targetPitch - 1f) > 0.001f;
            float pd = pitchDuration > 0f ? pitchDuration : duration;
            float total = Mathf.Max(duration, doPitch ? pd : 0f);

            float t = 0f;
            while (t < total && src != null)
            {
                t += Time.unscaledDeltaTime;
                src.volume = Mathf.Lerp(startVol, 0f, Mathf.Clamp01(t / duration));
                if (doPitch) src.pitch = Mathf.Lerp(startPitch, targetPitch, Mathf.Clamp01(t / pd));
                yield return null;
            }

            if (src == null) yield break;
            src.Stop();
            src.volume = 0f;
            src.pitch = 1f;
            src.clip = null;
        }
    }
}
