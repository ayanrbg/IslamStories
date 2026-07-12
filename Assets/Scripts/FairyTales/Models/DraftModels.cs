using System;

namespace FairyTales.Models
{
    [Serializable]
    public class Draft
    {
        public int id;
        public string narratorName;
        public string taleId;
        public int lastPage;
        public string voiceId;
        public string createdAt;
    }

    [Serializable]
    public class DraftCreateRequest
    {
        public string narratorName;
        public string taleId;
    }

    [Serializable]
    public class DraftCreateResponse
    {
        public Draft draft;
    }

    [Serializable]
    public class DraftUpdateRequest
    {
        public string voiceId;
    }

    [Serializable]
    public class DraftDeleteResponse
    {
        public string status;
    }
}
