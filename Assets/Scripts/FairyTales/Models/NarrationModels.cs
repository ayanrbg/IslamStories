using System;

namespace FairyTales.Models
{
    [Serializable]
    public class NarrateAllResponse
    {
        public string jobId;
        public string status;
    }

    [Serializable]
    public class NarrationStatusResponse
    {
        public string status; // "processing", "done", "error"
        public int pagesReady;
        public int totalPages;
    }
}
