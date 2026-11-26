using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace FairyTales.Lessons
{
    /// <summary>
    /// Narration of the lesson player: the lesson's mp3 files from the cache and the
    /// shared phrases bundled per language («Машаллах, правильно!», «Попробуй ещё»…).
    /// One voice at a time; a new clip cuts the previous one.
    /// </summary>
    public class LessonVoice
    {
        /// <summary>Bundled phrases: Resources/LessonVoice/&lt;ru|kz|en&gt;/&lt;key&gt;. Missing ones are silent.</summary>
        public const string Correct = "correct";
        public const string TryAgain = "try_again";
        /// <summary>«Послушай себя!» before the child's recording.</summary>
        public const string ListenYourself = "listen_yourself";
        /// <summary>«Скажи вместе с другом!» after a quiet try.</summary>
        public const string SayWithFriend = "say_with_friend";

        private readonly MonoBehaviour _host;
        private readonly AudioSource _source;
        private readonly Dictionary<string, AudioClip> _clips = new();
        private Coroutine _routine;
        private int _ticket;

        public bool Paused { get; private set; }

        public LessonVoice(MonoBehaviour host)
        {
            _host = host;
            _source = host.gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
        }

        /// <summary>
        /// Plays a cached mp3. <paramref name="done"/> gets true once it played to the end,
        /// false when there was nothing to play or it would not load; it is not called
        /// when another clip or <see cref="Stop"/> cut this one off.
        /// </summary>
        public void PlayFile(string path, Action<bool> done = null)
        {
            Stop();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) { done?.Invoke(false); return; }
            _routine = _host.StartCoroutine(Run(path, null, ++_ticket, done));
        }

        public void PlayBundled(string key, Action<bool> done = null)
        {
            Stop();
            var clip = Resources.Load<AudioClip>($"LessonVoice/{LessonStrings.Lang}/{key}");
            if (clip == null) { done?.Invoke(false); return; }
            _routine = _host.StartCoroutine(Run(null, clip, ++_ticket, done));
        }

        /// <summary>Plays a clip made at runtime (the child's own voice); the voice does not own it.</summary>
        public void PlayClip(AudioClip clip, Action<bool> done = null)
        {
            Stop();
            if (clip == null) { done?.Invoke(false); return; }
            _routine = _host.StartCoroutine(Run(null, clip, ++_ticket, done));
        }

        public bool IsPlaying => _source != null && _source.isPlaying;

        public void Stop()
        {
            _ticket++;
            if (_routine != null) _host.StopCoroutine(_routine);
            _routine = null;
            Paused = false;
            if (_source != null) _source.Stop();
        }

        public void Pause(bool pause)
        {
            if (_source == null || Paused == pause) return;
            Paused = pause;
            if (pause) _source.Pause();
            else _source.UnPause();
        }

        /// <summary>Unloads the clips of the lesson that just closed.</summary>
        public void Release()
        {
            Stop();
            if (_source != null) _source.clip = null;
            foreach (var c in _clips.Values) if (c != null) UnityEngine.Object.Destroy(c);
            _clips.Clear();
        }

        private IEnumerator Run(string path, AudioClip clip, int ticket, Action<bool> done)
        {
            if (clip == null && !_clips.TryGetValue(path, out clip))
            {
                // Not streamed: an MPEG clip streamed on iOS comes out empty (NarrationPlayer).
                using var req = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.MPEG);
                ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio = false;
                yield return req.SendWebRequest();
                if (ticket != _ticket) yield break;
                if (req.result == UnityWebRequest.Result.Success) clip = DownloadHandlerAudioClip.GetContent(req);
                if (clip == null || clip.length < 0.05f)
                {
                    Debug.LogWarning($"[LessonVoice] can't play {Path.GetFileName(path)}: {req.error}");
                    _routine = null;
                    done?.Invoke(false);
                    yield break;
                }
                _clips[path] = clip;
            }

            _source.clip = clip;
            _source.Play();
            while (ticket == _ticket && (Paused || _source.isPlaying)) yield return null;
            if (ticket != _ticket) yield break;
            _routine = null;
            done?.Invoke(true);
        }
    }

    /// <summary>
    /// Pictures of the open lesson, decoded from the cache on demand. A 2048 px picture
    /// takes ~11 MB as a texture, so a step's pictures are released when the step ends.
    /// </summary>
    public class LessonImages
    {
        private readonly Dictionary<string, Sprite> _sprites = new();

        /// <summary>Sprite of a media key for this child's gender, or null (no file, not an image).</summary>
        public Sprite Get(LessonPackage package, string key)
        {
            var path = package?.MediaPath(key, UserProfile.ChildGender);
            if (path == null) return null;
            if (_sprites.TryGetValue(path, out var s) && s != null) return s;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            byte[] bytes;
            try { bytes = File.ReadAllBytes(path); }
            catch (Exception e) { Debug.LogWarning($"[LessonImages] {e.Message}"); UnityEngine.Object.Destroy(tex); return null; }
            // JPG and PNG only (the server stores JPEG); a WebP would fail here.
            if (!tex.LoadImage(bytes, true))
            {
                Debug.LogWarning($"[LessonImages] can't decode {Path.GetFileName(path)}");
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            _sprites[path] = s;
            return s;
        }

        public void Release()
        {
            foreach (var s in _sprites.Values)
            {
                if (s == null) continue;
                UnityEngine.Object.Destroy(s.texture);
                UnityEngine.Object.Destroy(s);
            }
            _sprites.Clear();
        }
    }
}
