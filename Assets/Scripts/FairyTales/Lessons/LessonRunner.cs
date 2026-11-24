using System.Collections;
using UnityEngine;

namespace FairyTales.Lessons
{
    /// <summary>
    /// Runs lesson coroutines that must outlive the screen that started them: the
    /// background download of the next lesson keeps going after the player closes,
    /// and a lesson download runs its narration request alongside the media.
    /// Created on first use, survives scene loads, invisible in the hierarchy.
    /// </summary>
    public sealed class LessonRunner : MonoBehaviour
    {
        private static LessonRunner _instance;

        public static Coroutine Run(IEnumerator routine)
        {
            if (_instance == null)
            {
                var go = new GameObject("[LessonRunner]") { hideFlags = HideFlags.HideInHierarchy };
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<LessonRunner>();
            }
            return _instance.StartCoroutine(routine);
        }
    }
}
