using System;

namespace FairyTales.Models
{
    [Serializable]
    public class CloneResponse
    {
        public string voiceId;
        public string status;
    }

    [Serializable]
    public class DeleteVoiceResponse
    {
        public string status;
    }
}
