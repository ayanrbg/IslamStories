using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace FairyTales.Lessons
{
    /// <summary>
    /// What the lessons home needs to draw itself without the network: the last catalog
    /// of each language and the lesson covers, kept in persistentDataPath/lessons_home.
    /// </summary>
    public static class LessonHomeCache
    {
        private static string Dir => Path.Combine(Application.persistentDataPath, "lessons_home");

        private static readonly Dictionary<string, Sprite> Covers = new();
        private static readonly HashSet<string> Loading = new();

        // ─────────────────────────── catalog ───────────────────────────

        private static string CatalogPath(string contentLang) => Path.Combine(Dir, $"catalog_{contentLang}.json");

        public static LessonCatalog LoadCatalog(string appLang)
        {
            var lang = LessonLang.ContentLang(appLang);
            if (lang == null) return null;
            try
            {
                var path = CatalogPath(lang);
                if (!File.Exists(path)) return null;
                var catalog = JsonConvert.DeserializeObject<LessonCatalog>(File.ReadAllText(path));
                if (catalog == null) return null;
                catalog.topics ??= Array.Empty<LessonTopic>();
                catalog.lessons ??= Array.Empty<LessonSummary>();
                return catalog;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LessonHome] cached catalog: {e.Message}");
                return null;
            }
        }

        public static void SaveCatalog(string appLang, LessonCatalog catalog)
        {
            var lang = LessonLang.ContentLang(appLang);
            if (lang == null || catalog == null) return;
            try
            {
                Directory.CreateDirectory(Dir);
                var path = CatalogPath(lang);
                File.WriteAllText(path + ".tmp", JsonConvert.SerializeObject(catalog));
                if (File.Exists(path)) File.Delete(path);
                File.Move(path + ".tmp", path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LessonHome] save catalog: {e.Message}");
            }
        }

        // ─────────────────────────── covers ───────────────────────────

        /// <summary>A cover already in memory, or null.</summary>
        public static Sprite CoverNow(string url)
            => !string.IsNullOrEmpty(url) && Covers.TryGetValue(url, out var s) ? s : null;

        /// <summary>
        /// The cover of a lesson: memory, then disk, then the network (saved for next
        /// time). <paramref name="done"/> gets null when there is none to be had.
        /// </summary>
        public static IEnumerator Cover(string url, Action<Sprite> done)
        {
            if (string.IsNullOrEmpty(url)) { done?.Invoke(null); yield break; }
            while (Loading.Contains(url)) yield return null;
            if (Covers.TryGetValue(url, out var cached)) { done?.Invoke(cached); yield break; }

            Loading.Add(url);
            try
            {
                var path = Path.Combine(Dir, "covers", Hash(url));
                byte[] bytes = null;
                try { if (File.Exists(path)) bytes = File.ReadAllBytes(path); }
                catch (Exception) { bytes = null; }

                if (bytes == null)
                {
                    using var req = UnityWebRequest.Get(url);
                    req.timeout = 20;
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        bytes = req.downloadHandler.data;
                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                            File.WriteAllBytes(path, bytes);
                        }
                        catch (Exception e) { Debug.LogWarning($"[LessonHome] save cover: {e.Message}"); }
                    }
                }

                Sprite sprite = null;
                if (bytes != null)
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(bytes, true))
                    {
                        tex.wrapMode = TextureWrapMode.Clamp;
                        sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                        Covers[url] = sprite;
                    }
                    else UnityEngine.Object.Destroy(tex);
                }
                done?.Invoke(sprite);
            }
            finally
            {
                Loading.Remove(url);
            }
        }

        private static string Hash(string s)
        {
            using var sha = SHA1.Create();
            var b = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
            var sb = new StringBuilder(b.Length * 2);
            foreach (var x in b) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }
    }
}
