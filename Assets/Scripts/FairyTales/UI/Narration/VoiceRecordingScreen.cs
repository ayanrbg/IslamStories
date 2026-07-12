using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FairyTales.Api;
using FairyTales.Audio;
using FairyTales.Models;
using FairyTales.UI.Core;
using System.Collections.Generic;

namespace FairyTales.UI.Narration
{
    public class VoiceRecordingScreen : BaseScreen
    {
        [Header("UI")]
        [SerializeField] private TMP_Text sampleText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button btnRecord;
        [SerializeField] private Button btnPlay;
        [SerializeField] private Button btnSend;
        [SerializeField] private Button btnBack;

        private static readonly Dictionary<string, string[]> SamplesByLang = new()
        {
            ["ru"] = new[]
            {
                "Жили-были дедушка да бабушка. Была у них внучка, которая очень любила сказки.",
                "Однажды солнечным утром они отправились в волшебный лес за грибами и ягодами.",
                "В лесу они встретили маленького зайчика, который заблудился и искал свою маму.",
                "Все вместе они нашли дорогу домой и с тех пор стали лучшими друзьями."
            },
            ["kz"] = new[]
            {
                "Баяғыда бір ата мен әже болыпты. Оларда ертегіні өте жақсы көретін немересі бар екен.",
                "Бір күні ашық күнде олар сиқырлы орманға саңырауқұлақ пен жидек теруге барыпты.",
                "Орманда олар жолын жоғалтып, анасын іздеп жүрген кішкентай қоянды кездестірді.",
                "Бәрі бірге үйге жол тауып, сол күннен бастап ең жақсы достар болды."
            },
            ["en"] = new[]
            {
                "Once upon a time there lived a grandfather and a grandmother. They had a granddaughter who loved fairy tales.",
                "One sunny morning they set off into the magical forest to pick mushrooms and berries.",
                "In the forest they met a little bunny who was lost and looking for his mother.",
                "Together they found the way home and from that day on they became the best of friends."
            },
            ["uz"] = new[]
            {
                "Qadim zamonda bir bobo va buvi yashar edi. Ularning ertaklarni juda yaxshi ko'radigan nevarasi bor edi.",
                "Bir kuni quyoshli tongda ular sehrli o'rmonga qo'ziqorin va mevalar terishga jo'nashdi.",
                "O'rmonda ular yo'lini yo'qotgan va onasini qidirayotgan kichkina quyonchani uchratishdi.",
                "Hammasi birgalikda uyga yo'l topishdi va o'sha kundan boshlab eng yaqin do'stlarga aylandi."
            }
        };

        private ScreenManager _screens;
        private MicRecorder _mic;
        private NarrationPlayer _player;
        private VoiceService _voice;
        private NarrationService _narration;
        private TaleSummary _tale;
        private Draft _draft;
        private byte[] _recordedAudio;

        private void Awake()
        {
            _screens = GetComponentInParent<ScreenManager>();
            var api = FindAnyObjectByType<ApiClient>();
            _voice = new VoiceService(api);
            _narration = new NarrationService(api);

            if (btnRecord) btnRecord.onClick.AddListener(OnRecord);
            if (btnPlay) btnPlay.onClick.AddListener(OnPlay);
            if (btnSend) btnSend.onClick.AddListener(OnSend);
            if (btnBack) btnBack.onClick.AddListener(OnBack);
        }

        public void SetContext(TaleSummary tale, Draft draft)
        {
            _tale = tale;
            _draft = draft;
            _recordedAudio = null;
        }

        protected override void OnPrepare()
        {
            _mic = FindAnyObjectByType<MicRecorder>();
            _player = FindAnyObjectByType<NarrationPlayer>();

            if (sampleText)
            {
                var lang = Loc.Lang;
                var samples = SamplesByLang.ContainsKey(lang) ? SamplesByLang[lang] : SamplesByLang["ru"];
                sampleText.text = string.Join("\n\n", samples);
            }

            UpdateButtons();
            if (timerText) timerText.text = "00:00";
            if (statusText) statusText.text = Loc.Get("rec_hint");
        }

        private void Update()
        {
            if (_mic == null || !_mic.IsRecording) return;

            var elapsed = _mic.ElapsedTime;
            if (timerText)
                timerText.text = $"{(int)elapsed / 60:00}:{(int)elapsed % 60:00}";
        }

        private void OnRecord()
        {
            if (_mic == null) return;

            if (_mic.IsRecording)
            {
                _mic.OnStopped -= OnRecordingStopped;
                _mic.OnStopped += OnRecordingStopped;
                _mic.StopRecording();
            }
            else
            {
                _recordedAudio = null;
                _mic.OnStopped -= OnRecordingStopped;
                _mic.OnStopped += OnRecordingStopped;
                _mic.StartRecording();
                if (statusText) statusText.text = Loc.Get("rec_recording");
                UpdateButtons();
            }
        }

        private void OnRecordingStopped(byte[] wavData)
        {
            _mic.OnStopped -= OnRecordingStopped;
            _recordedAudio = wavData;

            if (statusText)
                statusText.text = wavData != null
                    ? Loc.Get("rec_done_detail")
                    : Loc.Get("rec_error_detail");

            UpdateButtons();
        }

        private void OnPlay()
        {
            if (_player == null || _recordedAudio == null) return;
            _player.PlayFromBytes(_recordedAudio);
        }

        private void OnSend()
        {
            if (_recordedAudio == null) return;
            StartCoroutine(CloneAndNarrate());
        }

        private IEnumerator CloneAndNarrate()
        {
            if (statusText) statusText.text = Loc.Get("rec_cloning");
            SetInteractable(false);

            string voiceId = null;
            yield return _voice.CloneVoice(_recordedAudio, "voice_sample.wav",
                onSuccess: r => voiceId = r.voiceId,
                onError: e =>
                {
                    // RELEASE: Debug.LogError($"[VoiceRecording] Clone: {e}");
                    if (statusText) statusText.text = Loc.Format("error_with_msg", e);
                });

            if (voiceId == null)
            {
                SetInteractable(true);
                yield break;
            }

            PlayerPrefs.SetInt("ft_voiceCloned", 1);
            PlayerPrefs.Save();

            if (statusText) statusText.text = Loc.Get("rec_narrating");

            var childName = PlayerPrefs.GetString("ft_childName", "");
            var gender = PlayerPrefs.GetString("ft_gender", "male");
            yield return _narration.NarrateAll(_tale.id, childName, gender,
                onSuccess: r =>
                {
                    var progress = _screens.Get<NarrationProgressScreen>();
                    if (progress == null) return;

                    progress.SetContext(_tale);
                    _screens.Show<NarrationProgressScreen>();
                },
                onError: e =>
                {
                    // RELEASE: Debug.LogError($"[VoiceRecording] NarrateAll: {e}");
                    if (statusText) statusText.text = Loc.Format("error_with_msg", e);
                    SetInteractable(true);
                });
        }

        private void UpdateButtons()
        {
            bool recording = _mic != null && _mic.IsRecording;
            bool hasAudio = _recordedAudio != null;

            if (btnPlay) btnPlay.interactable = hasAudio && !recording;
            if (btnSend) btnSend.interactable = hasAudio && !recording;
        }

        private void SetInteractable(bool value)
        {
            if (btnRecord) btnRecord.interactable = value;
            if (btnPlay) btnPlay.interactable = value;
            if (btnSend) btnSend.interactable = value;
            if (btnBack) btnBack.interactable = value;
        }

        private void OnBack() => _screens.Show<NarrationSetupScreen>();

        protected override void OnHidden()
        {
            if (_mic != null && _mic.IsRecording)
                _mic.StopRecording();
            if (_player != null && _player.IsPlaying)
                _player.Stop();
        }
    }
}
