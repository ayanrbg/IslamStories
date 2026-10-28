using UnityEngine;

namespace FairyTales.UI.Core
{
    public static class ReadingState
    {
        public static void SavePage(string taleId, int page)
        {
            PlayerPrefs.SetInt($"ft_page_{taleId}", page);
        }

        public static int LoadPage(string taleId)
        {
            return PlayerPrefs.GetInt($"ft_page_{taleId}", 0);
        }

        // A dragged slider calls these every frame, and PlayerPrefs.Save() writes a file
        // each time. Pass flush: false while dragging and call Flush() once at the end;
        // Unity also flushes on its own when the app is backgrounded or quits.
        public static void SaveVolume(float volume, bool flush = true)
        {
            PlayerPrefs.SetFloat("ft_volume", volume);
            if (flush) PlayerPrefs.Save();
        }

        public static float LoadVolume()
        {
            return PlayerPrefs.GetFloat("ft_volume", 0.5f);
        }

        public static void SaveMuted(bool muted, bool flush = true)
        {
            PlayerPrefs.SetInt("ft_muted", muted ? 1 : 0);
            if (flush) PlayerPrefs.Save();
        }

        public static void Flush()
        {
            PlayerPrefs.Save();
        }

        public static bool LoadMuted()
        {
            return PlayerPrefs.GetInt("ft_muted", 0) == 1;
        }
    }
}
