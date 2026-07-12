using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace FairyTales.Audio
{
    public class NarrationPlayer : MonoBehaviour
    {
        private AudioSource _source;
        private Coroutine _playRoutine;
        private UnityWebRequest _activeRequest;

        public bool IsPlaying => _source != null && _source.isPlaying;
        public float Progress => _source != null && _source.clip != null
            ? _source.time / _source.clip.length : 0f;

        public event Action OnFinished;

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
        }

        public void PlayFromBytes(byte[] audioData)
        {
            Stop();
            _playRoutine = StartCoroutine(LoadAndPlay(audioData));
        }

        public void PlayClip(AudioClip clip)
        {
            Stop();
            _source.clip = clip;
            _source.Play();
            _playRoutine = StartCoroutine(WaitForEnd());
        }

        public void Pause() => _source.Pause();
        public void Resume() => _source.UnPause();

        public void Stop()
        {
            if (_playRoutine != null)
            {
                StopCoroutine(_playRoutine);
                _playRoutine = null;
            }
            _source.Stop();
            _source.clip = null;
            DisposeRequest();
        }

        private void DisposeRequest()
        {
            if (_activeRequest != null)
            {
                _activeRequest.Dispose();
                _activeRequest = null;
            }
        }

        private IEnumerator LoadAndPlay(byte[] data)
        {
            if (data == null || data.Length == 0) yield break;

            var (ext, audioType) = DetectFormat(data);
            var path = Path.Combine(Application.temporaryCachePath, $"narration.{ext}");
            File.WriteAllBytes(path, data);

            var uri = "file:///" + path.Replace("\\", "/");

            DisposeRequest();
            // Keep a local handle: a newer PlayFromBytes/Stop (e.g. fired from our own
            // OnFinished when chaining clips) may replace/null _activeRequest while we're
            // suspended. Reading the field back after a yield would then NRE.
            var req = UnityWebRequestMultimedia.GetAudioClip(uri, audioType);
            _activeRequest = req;
            // Enable streaming so playback starts before full decode finishes
            ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio = true;
            yield return req.SendWebRequest();

            // Superseded while loading → abandon this request quietly.
            if (_activeRequest != req)
            {
                req.Dispose();
                yield break;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                DisposeRequest();
                yield break;
            }

            var clip = DownloadHandlerAudioClip.GetContent(req);
            if (clip == null || clip.length < 0.01f)
            {
                DisposeRequest();
                yield break;
            }

            _source.clip = clip;
            _source.Play();

            if (!_source.isPlaying)
            {
                DisposeRequest();
                yield break;
            }

            yield return WaitForEnd();

            // Only clean up if still current — WaitForEnd's OnFinished may have started
            // a new clip that now owns _activeRequest.
            if (_activeRequest == req)
                DisposeRequest();
        }

        private static (string ext, AudioType type) DetectFormat(byte[] data)
        {
            if (data.Length < 4) return ("wav", AudioType.WAV);

            // OGG: "OggS"
            if (data[0] == 'O' && data[1] == 'g' && data[2] == 'g' && data[3] == 'S')
                return ("ogg", AudioType.OGGVORBIS);

            // WAV: "RIFF"
            if (data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F')
                return ("wav", AudioType.WAV);

            // MP3: ID3 tag or sync word (0xFF 0xFB/0xF3/0xF2)
            if ((data[0] == 'I' && data[1] == 'D' && data[2] == '3') ||
                (data[0] == 0xFF && (data[1] & 0xE0) == 0xE0))
                return ("mp3", AudioType.MPEG);

            return ("wav", AudioType.WAV);
        }

        private IEnumerator WaitForEnd()
        {
            while (_source.isPlaying)
                yield return null;

            _playRoutine = null;
            OnFinished?.Invoke();
        }
    }
}
