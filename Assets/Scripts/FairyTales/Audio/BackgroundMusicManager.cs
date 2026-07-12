using FairyTales.UI.Core;
using UnityEngine;
using System.Collections.Generic;

namespace FairyTales.Audio
{
    /// <summary>
    /// Singleton background music player that shuffles through multiple tracks
    /// with crossfade transitions. Tracks never repeat back-to-back.
    /// Muting keeps playback running so music resumes from the same position.
    /// </summary>
    public class BackgroundMusicManager : MonoBehaviour
    {
        private static BackgroundMusicManager _instance;
        public static BackgroundMusicManager Instance => _instance;

        [SerializeField] private AudioClip[] menuTracks;
        [SerializeField] private float volume = 0.5f;
        [Tooltip("Crossfade duration in seconds between tracks")]
        [SerializeField] private float crossfadeDuration = 2f;

        private AudioSource _sourceA;
        private AudioSource _sourceB;
        private AudioSource _active;
        private AudioSource _next;

        private bool _crossfading;
        private float _crossfadeTimer;

        private List<int> _shuffledIndices = new List<int>();
        private int _shufflePos;
        private int _lastPlayedIndex = -1;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            _sourceA = gameObject.AddComponent<AudioSource>();
            _sourceB = gameObject.AddComponent<AudioSource>();

            ConfigureSource(_sourceA);
            ConfigureSource(_sourceB);

            if (PlayerPrefs.HasKey("ft_volume"))
                volume = ReadingState.LoadVolume();
            bool muted = ReadingState.LoadMuted();
            ApplyVolume(_sourceA, volume);
            ApplyVolume(_sourceB, volume);
            _sourceA.mute = muted;
            _sourceB.mute = muted;

            _active = _sourceA;
            _next = _sourceB;
        }

        private void Start()
        {
            PlayMenu();
        }

        private void ConfigureSource(AudioSource src)
        {
            src.loop = false;
            src.playOnAwake = false;
            src.priority = 0;
            src.spatialBlend = 0f; // pure 2D stereo
            src.bypassEffects = true;
            src.bypassReverbZones = true;
        }

        private void ApplyVolume(AudioSource src, float vol)
        {
            src.volume = vol;
        }

        private void RebuildShuffle()
        {
            _shuffledIndices.Clear();
            for (int i = 0; i < menuTracks.Length; i++)
                _shuffledIndices.Add(i);

            // Fisher-Yates shuffle
            for (int i = _shuffledIndices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_shuffledIndices[i], _shuffledIndices[j]) = (_shuffledIndices[j], _shuffledIndices[i]);
            }

            // If the first track in the new shuffle is the same as the last played, swap it
            if (_shuffledIndices.Count > 1 && _shuffledIndices[0] == _lastPlayedIndex)
            {
                int swapIdx = Random.Range(1, _shuffledIndices.Count);
                (_shuffledIndices[0], _shuffledIndices[swapIdx]) = (_shuffledIndices[swapIdx], _shuffledIndices[0]);
            }

            _shufflePos = 0;
        }

        private AudioClip GetNextTrack()
        {
            if (menuTracks == null || menuTracks.Length == 0) return null;
            if (menuTracks.Length == 1) return menuTracks[0];

            if (_shufflePos >= _shuffledIndices.Count)
                RebuildShuffle();

            int idx = _shuffledIndices[_shufflePos];
            _shufflePos++;
            _lastPlayedIndex = idx;
            return menuTracks[idx];
        }

        private void Update()
        {
            if (_active == null || _active.clip == null)
                return;

            // If active source finished and no crossfade is running, start next track
            if (!_active.isPlaying && !_crossfading)
            {
                PlayNextTrack();
                return;
            }

            // Start crossfade when active source is near the end
            if (!_crossfading && _active.isPlaying)
            {
                float timeLeft = _active.clip.length - _active.time;
                if (timeLeft <= crossfadeDuration && timeLeft > 0f)
                {
                    StartCrossfadeToNext();
                }
            }

            if (_crossfading)
            {
                UpdateCrossfade();
            }
        }

        private void StartCrossfadeToNext()
        {
            AudioClip nextClip = GetNextTrack();
            if (nextClip == null) return;

            _crossfading = true;
            _crossfadeTimer = 0f;

            _next.clip = nextClip;
            _next.volume = 0f;
            _next.Play();
        }

        private void UpdateCrossfade()
        {
            _crossfadeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_crossfadeTimer / crossfadeDuration);

            _active.volume = volume * (1f - t);
            _next.volume = volume * t;

            if (t >= 1f)
            {
                _active.Stop();
                _active.volume = volume;

                (_active, _next) = (_next, _active);
                _crossfading = false;
            }
        }

        private void PlayNextTrack()
        {
            AudioClip clip = GetNextTrack();
            if (clip == null) return;

            _active.clip = clip;
            _active.volume = volume;
            _active.Play();
        }

        public void PlayMenu()
        {
            if (menuTracks == null || menuTracks.Length == 0) return;

            RebuildShuffle();
            PlayNextTrack();
        }

        public void PlayForTale(string taleId)
        {
            var clip = Resources.Load<AudioClip>($"Music/{taleId}");
            if (clip != null) Play(clip);
        }

        public void Play(AudioClip clip)
        {
            if (_active.clip == clip && _active.isPlaying) return;

            if (_crossfading)
            {
                _next.Stop();
                _crossfading = false;
            }

            _active.clip = clip;
            _active.volume = volume;
            _active.Play();
        }

        public void Stop()
        {
            _active.Stop();
            _next.Stop();
            _crossfading = false;
        }

        public void SetVolume(float v)
        {
            volume = Mathf.Clamp01(v);
            if (!_crossfading)
                _active.volume = volume;
            ReadingState.SaveVolume(volume);
        }

        public void SetMuted(bool muted)
        {
            _sourceA.mute = muted;
            _sourceB.mute = muted;
            ReadingState.SaveMuted(muted);
        }

        public bool IsMuted => _sourceA != null && _sourceA.mute;
    }
}
