using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FairyTales.Models;
using FairyTales.UI.Core;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace FairyTales.Cache
{
    public static class BundledTaleLoader
    {
        private const string Folder = "BundledTales";

        // Set of tale IDs bundled in StreamingAssets, loaded from manifest.json at startup.
        // Used on Android where File.Exists can't probe inside the APK.
        private static HashSet<string> _bundledIds;

        private static string TaleFileName(string lang) => $"tale_{lang}.json";

        /// <summary>
        /// Loads the set of bundled tale IDs from manifest.json. Call once at startup
        /// so <see cref="IsBundled"/> can answer correctly on Android (inside the APK).
        /// </summary>
        public static IEnumerator InitBundledSet()
        {
            var path = Path.Combine(Application.streamingAssetsPath, Folder, "manifest.json");
#if UNITY_ANDROID && !UNITY_EDITOR
            var url = path;
#else
            var url = "file://" + path;
#endif
            using var req = UnityWebRequest.Get(url);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                _bundledIds = new HashSet<string>();
                yield break;
            }

            var set = new HashSet<string>();
            var entries = JsonConvert.DeserializeObject<ManifestEntry[]>(req.downloadHandler.text);
            if (entries != null)
                foreach (var e in entries)
                    if (e.bundled && !string.IsNullOrEmpty(e.id))
                        set.Add(e.id);

            _bundledIds = set;
        }

        public static bool IsBundled(string taleId)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // StreamingAssets is inside the APK — File.Exists can't probe it, so rely on
            // the manifest set. Until it loads, treat as NOT bundled so a tale isn't
            // wrongly shown as already downloaded (manifest loads early at startup).
            return _bundledIds != null && _bundledIds.Contains(taleId);
#else
            var path = Path.Combine(Application.streamingAssetsPath, Folder, taleId, TaleFileName(Loc.Lang));
            return File.Exists(path);
#endif
        }

        public static IEnumerator LoadTaleJson(string taleId, Action<TaleDetail> cb)
        {
            var url = GetUrl(taleId, TaleFileName(Loc.Lang));
            using var req = UnityWebRequest.Get(url);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                cb?.Invoke(null);
                yield break;
            }

            var detail = JsonConvert.DeserializeObject<TaleDetail>(req.downloadHandler.text);
            if (detail != null && detail.totalPages == 0 && detail.pages != null)
                detail.totalPages = detail.pages.Length;
            cb?.Invoke(detail);
        }

        /// <summary>
        /// Load manifest.json → TaleSummary[] for offline library fallback.
        /// </summary>
        public static IEnumerator LoadManifest(string lang, Action<TaleSummary[]> cb)
        {
            var path = Path.Combine(Application.streamingAssetsPath, Folder, "manifest.json");
#if UNITY_ANDROID && !UNITY_EDITOR
            var url = path;
#else
            var url = "file://" + path;
#endif
            using var req = UnityWebRequest.Get(url);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                // RELEASE: Debug.LogWarning($"[BundledTaleLoader] manifest.json not found: {req.error}");
                cb?.Invoke(null);
                yield break;
            }

            var entries = JsonConvert.DeserializeObject<ManifestEntry[]>(req.downloadHandler.text);
            if (entries == null)
            {
                cb?.Invoke(null);
                yield break;
            }

            var list = new List<TaleSummary>();
            foreach (var e in entries)
            {
                string title = e.titles != null && e.titles.TryGetValue(lang, out var t)
                    ? t
                    : e.titles != null && e.titles.TryGetValue("ru", out var fb) ? fb : e.id;

                list.Add(new TaleSummary
                {
                    id = e.id,
                    title = title,
                    lang = lang,
                    free = e.free,
                    bundled = e.bundled,
                    titles = e.titles
                });
            }

            cb?.Invoke(list.ToArray());
        }

        public static IEnumerator LoadSprite(string taleId, string fileName, Action<Sprite> cb)
        {
            var url = GetUrl(taleId, fileName);
            using var req = UnityWebRequestTexture.GetTexture(url, nonReadable: true);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                cb?.Invoke(null);
                yield break;
            }

            var tex = DownloadHandlerTexture.GetContent(req);
            var sprite = Sprite.Create(tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect);
            cb?.Invoke(sprite);
        }

        public static IEnumerator LoadCover(string taleId, Action<Sprite> cb)
        {
            yield return LoadSprite(taleId, "cover.jpg", cb);
        }

        public static IEnumerator LoadPage(string taleId, int page, Action<Sprite> cb)
        {
            yield return LoadSprite(taleId, $"page_{page}.jpg", cb);
        }

        public static IEnumerator LoadPageGendered(string taleId, int page,
            string gender, Action<Sprite> cb)
        {
            Sprite result = null;
            yield return LoadSprite(taleId, $"page_{page}_{gender}.jpg", s => result = s);
            if (result != null) { cb?.Invoke(result); yield break; }
            yield return LoadSprite(taleId, $"page_{page}.jpg", cb);
        }

        private static string GetUrl(string taleId, string file)
        {
            var path = Path.Combine(Application.streamingAssetsPath, Folder, taleId, file);
#if UNITY_ANDROID && !UNITY_EDITOR
            return path; // Already a jar:// URL on Android
#else
            return "file://" + path;
#endif
        }

        [Serializable]
        private class ManifestEntry
        {
            public string id;
            public bool free;
            public bool bundled;
            public Dictionary<string, string> titles;
        }
    }
}
