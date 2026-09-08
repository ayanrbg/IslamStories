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

        public static void SaveVolume(float volume)
        {
            PlayerPrefs.SetFloat("ft_volume", volume);
            PlayerPrefs.Save();
        }

        public static float LoadVolume()
        {
            return PlayerPrefs.GetFloat("ft_volume", 0.5f);
        }

        public static void SaveMuted(bool muted)
        {
            PlayerPrefs.SetInt("ft_muted", muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static bool LoadMuted()
        {
            return PlayerPrefs.GetInt("ft_muted", 0) == 1;
        }
    }
}
