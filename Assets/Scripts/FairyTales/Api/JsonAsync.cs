using System;
using System.Collections;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace FairyTales.Api
{
    /// <summary>
    /// JSON parsing off the main thread. A tale is tens of kilobytes of text, and the
    /// first Newtonsoft parse of a type also builds its contract by reflection — slow
    /// on IL2CPP. Done inline it landed in the frame of a tap or mid-transition.
    /// </summary>
    public static class JsonAsync
    {
        /// <summary>Parses <paramref name="json"/> on a worker thread; <paramref name="done"/>
        /// runs back on the main thread with the result, or default on malformed input.</summary>
        public static IEnumerator Parse<T>(string json, Action<T> done)
        {
            yield return Run(() => JsonConvert.DeserializeObject<T>(json), done);
        }

        /// <summary>Runs <paramref name="work"/> on a worker thread and waits for it frame by
        /// frame. <paramref name="done"/> gets default when the work throws.</summary>
        public static IEnumerator Run<T>(Func<T> work, Action<T> done)
        {
            var task = Task.Run(work);
            while (!task.IsCompleted) yield return null;
            done?.Invoke(task.Status == TaskStatus.RanToCompletion ? task.Result : default);
        }
    }
}
