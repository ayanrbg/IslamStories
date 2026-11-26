using System;
using System.Collections;
using UnityEngine;

namespace FairyTales.Lessons
{
    public enum MicAttempt
    {
        /// <summary>The child spoke; the recording is ready in <see cref="LessonMic.Recording"/>.</summary>
        Voice,
        /// <summary>Nothing above the room noise within the listening time.</summary>
        Silent,
        /// <summary>No microphone, no permission, or it would not start.</summary>
        Failed,
    }

    /// <summary>
    /// The "repeat after the friend" microphone (spec §4): listens by itself for up to 5 s,
    /// finds the voice by a threshold over the room noise measured in the first 300 ms,
    /// stops after 0.8 s of silence and hands back the clip to play straight away.
    ///
    /// The sound lives only in memory until the step ends — nothing is written to disk or
    /// sent anywhere. Separate from the tales' MicRecorder (shared with Bala byte for byte,
    /// WAV files for the mum's voice): this one needs the live level and the voice search.
    /// </summary>
    public sealed class LessonMic : IDisposable
    {
        /// <summary>Waiting for the voice to start.</summary>
        public float ListenSeconds = 5f;
        /// <summary>Room noise is measured over this much at the start.</summary>
        public float CalibrateSeconds = 0.3f;
        /// <summary>Speech above the threshold, in total, that counts as a voice.</summary>
        public float VoiceSeconds = 0.4f;
        /// <summary>Silence after the voice that ends the recording.</summary>
        public float TailSeconds = 0.8f;
        /// <summary>Longest recording kept (a child singing on and on).</summary>
        public float MaxSeconds = 8f;
        /// <summary>Kept before the voice was found, so the first syllable isn't clipped.</summary>
        public float PreRollSeconds = 0.3f;

        private const int RingSeconds = 12;
        private const int Window = 1024;

        private string _device;
        private AudioClip _ring;
        private int _rate;
        private float[] _scratch = new float[Window];

        /// <summary>Loudness 0..1 of the last moment heard (drives the mask and the mouth).</summary>
        public float Level { get; private set; }
        /// <summary>True while the microphone is open.</summary>
        public bool Listening { get; private set; }
        /// <summary>The last voice, trimmed; owned by the mic (destroyed on the next attempt and on Dispose).</summary>
        public AudioClip Recording { get; private set; }
        /// <summary>Recent loudness of <see cref="Recording"/> for the "listen to yourself" bar (0..1 each).</summary>
        public float[] Envelope { get; private set; } = Array.Empty<float>();

        public static bool HasDevice => Microphone.devices != null && Microphone.devices.Length > 0;

        /// <summary>
        /// One attempt. Calls <paramref name="done"/> with the outcome; <see cref="Stop"/> or the
        /// caller stopping the coroutine ends it early (no callback then).
        /// </summary>
        public IEnumerator Listen(Func<bool> paused, Action<MicAttempt> done)
        {
            DropRecording();
            if (!LessonPermissions.MicGranted || !HasDevice) { done(MicAttempt.Failed); yield break; }

            _device = Microphone.devices[0];
            Microphone.GetDeviceCaps(_device, out int minRate, out int maxRate);
            // 0/0 means "any rate". 22 kHz is plenty for a voice and keeps the copy small.
            _rate = maxRate == 0 ? 22050 : Mathf.Clamp(22050, minRate == 0 ? 8000 : minRate, maxRate);
            _ring = Microphone.Start(_device, true, RingSeconds, _rate);
            if (_ring == null) { done(MicAttempt.Failed); yield break; }

            float wait = 0;
            while (Microphone.GetPosition(_device) <= 0 && wait < 1.5f) { wait += Time.unscaledDeltaTime; yield return null; }
            if (Microphone.GetPosition(_device) <= 0) { Stop(); done(MicAttempt.Failed); yield break; }
            Listening = true;
            _rate = _ring.frequency;
            int channels = _ring.channels;
            int ringLen = _ring.samples;

            int start = Microphone.GetPosition(_device);
            int read = start;          // next ring sample to analyse
            var detector = new VoiceDetector(this, _rate);

            while (!detector.Finished)
            {
                if (paused != null && paused()) { yield return null; continue; }
                int pos = Microphone.GetPosition(_device);
                int available = (pos - read + ringLen) % ringLen;
                while (available >= Window && !detector.Finished)
                {
                    float rms = Rms(read, Window, ringLen, channels);
                    read = (read + Window) % ringLen;
                    available -= Window;
                    detector.Feed(rms, Window);
                    Level = detector.Level;
                    // The ring is 12 s: stop before it wraps over the start.
                    if (detector.Heard >= (RingSeconds - 1) * (long)_rate) detector.Close();
                }
                if (!detector.Finished) yield return null;
            }

            if (detector.HasVoice)
            {
                Recording = Cut(start, detector.VoiceStart, detector.VoiceEnd, ringLen, channels);
                Envelope = Resample(detector.Envelope, 40);
            }
            Stop();
            done(Recording != null ? MicAttempt.Voice : MicAttempt.Silent);
        }

        /// <summary>
        /// The voice search on a stream of loudness windows, apart from the microphone so it can
        /// be checked with made-up sound. Positions are in samples from the start of listening.
        /// </summary>
        public sealed class VoiceDetector
        {
            private readonly LessonMic _cfg;
            private readonly int _rate;
            private float _noise;
            private int _noiseWindows;
            private float _voiced, _silence, _idle;

            public float Threshold { get; private set; } = 0.02f;
            public float Level { get; private set; }
            public long Heard { get; private set; }
            public long VoiceStart { get; private set; } = -1;
            public long VoiceEnd { get; private set; } = -1;
            public bool Finished { get; private set; }
            public bool HasVoice => VoiceStart >= 0 && VoiceEnd > VoiceStart;
            public readonly System.Collections.Generic.List<float> Envelope = new();

            public VoiceDetector(LessonMic cfg, int rate)
            {
                _cfg = cfg;
                _rate = rate;
            }

            public void Feed(float rms, int samples)
            {
                if (Finished) return;
                Heard += samples;
                float dt = (float)samples / _rate;
                float t = (float)Heard / _rate;

                if (t <= _cfg.CalibrateSeconds)
                {
                    _noise += rms; _noiseWindows++;
                    // A quiet room gives ~0.002, a voice up close 0.05–0.3. Capped, so a child who
                    // starts talking at once doesn't push the threshold out of reach.
                    Threshold = Mathf.Clamp(_noise / _noiseWindows * 3f, 0.015f, 0.05f);
                    Level = 0;
                    return;
                }

                Level = Mathf.Clamp01((rms - Threshold * 0.5f) / (Threshold * 6f));
                bool loud = rms > Threshold;
                if (loud)
                {
                    _voiced += dt;
                    _silence = 0;
                    if (VoiceStart < 0) VoiceStart = Math.Max(0, Heard - samples - (long)(_cfg.PreRollSeconds * _rate));
                }
                else if (VoiceStart >= 0) _silence += dt;
                if (VoiceStart >= 0) Envelope.Add(Mathf.Clamp01(rms / (Threshold * 8f)));
                else _idle += dt;

                bool isVoice = _voiced >= _cfg.VoiceSeconds;
                // A short noise (a cough, a bump) that never became a voice: forget it after a pause.
                if (!isVoice && VoiceStart >= 0 && _silence >= _cfg.TailSeconds)
                {
                    VoiceStart = -1; _voiced = 0; _silence = 0; Envelope.Clear();
                }
                if (isVoice && (_silence >= _cfg.TailSeconds || Heard - VoiceStart >= _cfg.MaxSeconds * _rate))
                {
                    VoiceEnd = Heard - (long)(Mathf.Max(0, _silence - 0.25f) * _rate);
                    Finished = true;
                }
                if (VoiceStart < 0 && _idle >= _cfg.ListenSeconds) Finished = true;
            }

            /// <summary>No more sound: keep a voice that is already long enough.</summary>
            public void Close()
            {
                if (Finished) return;
                if (VoiceStart >= 0 && _voiced >= _cfg.VoiceSeconds) VoiceEnd = Heard;
                else VoiceStart = -1;
                Finished = true;
            }
        }

        public void Stop()
        {
            if (Listening || (_device != null && Microphone.IsRecording(_device))) Microphone.End(_device);
            Listening = false;
            Level = 0;
            if (_ring != null) { UnityEngine.Object.Destroy(_ring); _ring = null; }
        }

        public void DropRecording()
        {
            if (Recording != null) { UnityEngine.Object.Destroy(Recording); Recording = null; }
            Envelope = Array.Empty<float>();
        }

        public void Dispose()
        {
            Stop();
            DropRecording();
        }

        // ─────────────────────────── samples ───────────────────────────

        private float Rms(int from, int count, int ringLen, int channels)
        {
            if (_scratch.Length < count * channels) _scratch = new float[count * channels];
            // GetData wraps around the end of a looping clip by itself.
            _ring.GetData(_scratch, from);
            double sum = 0;
            int n = count * channels;
            for (int i = 0; i < n; i++) sum += _scratch[i] * _scratch[i];
            return (float)Math.Sqrt(sum / n);
        }

        private AudioClip Cut(int start, long from, long to, int ringLen, int channels)
        {
            int length = (int)Math.Min(to - from, (long)ringLen - 1);
            if (length <= _rate / 10) return null;
            var data = new float[length * channels];
            _ring.GetData(data, (int)((start + from) % ringLen));
            // Normalise softly: a quiet child should still hear themselves.
            float peak = 0;
            foreach (var s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
            float gain = peak > 0.001f ? Mathf.Min(4f, 0.9f / peak) : 1f;
            // Short fades so the clip doesn't click.
            int fade = Mathf.Min(length / 4, _rate / 50);
            for (int i = 0; i < length; i++)
            {
                float f = 1f;
                if (i < fade) f = (float)i / fade;
                else if (i > length - fade) f = (float)(length - i) / fade;
                for (int c = 0; c < channels; c++) data[i * channels + c] *= gain * f;
            }
            var clip = AudioClip.Create("lesson_voice", length, channels, _rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float[] Resample(System.Collections.Generic.List<float> src, int n)
        {
            var dst = new float[n];
            if (src.Count == 0) return dst;
            for (int i = 0; i < n; i++)
            {
                int a = i * src.Count / n, b = Mathf.Max(a + 1, (i + 1) * src.Count / n);
                float m = 0;
                for (int j = a; j < b && j < src.Count; j++) m = Mathf.Max(m, src[j]);
                dst[i] = m;
            }
            return dst;
        }
    }
}
