using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace VN
{
    /// <summary>
    /// 结局画面：显示一张 CG 或播放一段视频，全屏覆盖。
    /// 到达结局后可以在这里停住等玩家看完，再交给 VNPlayer 退出。
    /// </summary>
    public class VNEndingScreen : MonoBehaviour
    {
        public Font font;

        private Canvas _canvas;
        private GameObject _root;
        private RawImage _screen;
        private VideoPlayer _video;
        private RenderTexture _rt;
        private Coroutine _playRoutine;

        public bool IsPlaying { get; private set; }

        public void ShowCg(string assetPath, bool loop, Action onFinished)
        {
            EnsureBuilt();
            StopVideo();

            var sprite = VNAssetLoader.LoadSprite(assetPath);
            if (sprite == null)
            {
                Debug.LogWarning("[VN] 找不到结局 CG：" + assetPath);
                onFinished?.Invoke();
                return;
            }

            _root.SetActive(true);
            _screen.texture = null;
            _screen.color = Color.white;
            _screen.texture = sprite.texture;

            var rt = _screen.rectTransform;
            var fitter = _root.GetComponentInChildren<AspectRatioFitter>();
            if (fitter != null)
            {
                fitter.aspectRatio = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            }

            IsPlaying = true;

            if (_playRoutine != null) StopCoroutine(_playRoutine);
            _playRoutine = StartCoroutine(CgRoutine(loop, onFinished));
        }

        public void PlayVideo(string assetPath, bool loop, Action onFinished)
        {
            EnsureBuilt();
            StopVideo();

            var clip = VNAssetLoader.LoadVideo(assetPath);
            if (clip == null)
            {
                Debug.LogWarning("[VN] 找不到结局视频：" + assetPath + "（如果是 .mp4，需要放进 Resources 并导入为 VideoClip）");
                onFinished?.Invoke();
                return;
            }

            _root.SetActive(true);

            if (_rt == null)
            {
                _rt = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
                _rt.Create();
            }

            _screen.texture = _rt;
            _screen.color = Color.white;

            var fitter = _root.GetComponentInChildren<AspectRatioFitter>();
            if (fitter != null)
            {
                fitter.aspectRatio = clip.width > 0 ? (float)clip.width / Mathf.Max(1, clip.height) : 16f / 9f;
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            }

            if (_video == null)
            {
                var go = new GameObject("Video", typeof(VideoPlayer));
                go.transform.SetParent(transform, false);
                _video = go.GetComponent<VideoPlayer>();
                _video.playOnAwake = false;
                _video.audioOutputMode = VideoAudioOutputMode.Direct;
            }

            _video.clip = clip;
            _video.source = VideoSource.VideoClip;
            _video.renderMode = VideoRenderMode.RenderTexture;
            _video.targetTexture = _rt;
            _video.isLooping = loop;
            _video.SetDirectAudioMute(0, false);

            IsPlaying = true;

            if (_playRoutine != null) StopCoroutine(_playRoutine);
            _playRoutine = StartCoroutine(VideoRoutine(loop, onFinished));
        }

        public void Hide()
        {
            IsPlaying = false;

            if (_playRoutine != null)
            {
                StopCoroutine(_playRoutine);
                _playRoutine = null;
            }

            StopVideo();
            if (_root != null) _root.SetActive(false);
        }

        private void StopVideo()
        {
            if (_video != null)
            {
                _video.Stop();
                _video.clip = null;
            }
        }

        private IEnumerator CgRoutine(bool loop, Action onFinished)
        {
            // CG 是静态图，等玩家点一下或者直接回调
            float t = 0f;
            while (t < 3f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            onFinished?.Invoke();
            _playRoutine = null;
        }

        private IEnumerator VideoRoutine(bool loop, Action onFinished)
        {
            _video.Prepare();

            float wait = 0f;
            while (!_video.isPrepared && wait < 5f)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!_video.isPrepared)
            {
                Debug.LogWarning("[VN] 结局视频准备超时");
                IsPlaying = false;
                onFinished?.Invoke();
                _playRoutine = null;
                yield break;
            }

            _video.Play();

            while (_video.isPlaying || _video.isPaused)
            {
                if (!loop && _video.frameCount > 0 && _video.frame >= (long)_video.frameCount - 1)
                    break;

                yield return null;
            }

            if (loop)
            {
                // 循环模式：一直开着，不回调
                while (true) yield return null;
            }

            IsPlaying = false;
            onFinished?.Invoke();
            _playRoutine = null;
        }

        private void EnsureBuilt()
        {
            if (_canvas != null) return;

            var go = new GameObject("VN Ending Canvas",
                typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);

            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 240;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            _root = go;

            var bg = new GameObject("Black", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(go.transform, false);
            var bgImg = bg.GetComponent<Image>();
            bgImg.color = Color.black;
            bgImg.raycastTarget = false;

            var br = bg.GetComponent<RectTransform>();
            br.anchorMin = Vector2.zero;
            br.anchorMax = Vector2.one;
            br.offsetMin = Vector2.zero;
            br.offsetMax = Vector2.zero;

            var screenGo = new GameObject("Media", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            screenGo.transform.SetParent(go.transform, false);

            _screen = screenGo.GetComponent<RawImage>();
            _screen.raycastTarget = false;

            var sr = screenGo.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(0.5f, 0.5f);
            sr.anchorMax = new Vector2(0.5f, 0.5f);
            sr.pivot = new Vector2(0.5f, 0.5f);
            sr.anchoredPosition = Vector2.zero;
            sr.sizeDelta = new Vector2(1920, 1080);

            var fitter = screenGo.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;

            _root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
            }
        }
    }
}
