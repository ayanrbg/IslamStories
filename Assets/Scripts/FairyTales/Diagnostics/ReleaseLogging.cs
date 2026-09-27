using UnityEngine;

namespace FairyTales.Diagnostics
{
    /// <summary>
    /// Store builds run with Unity logging off. Every Debug.Log collected a managed stack
    /// trace (Player Settings keep one for every log type) and the whole stream was
    /// mirrored to the server by <see cref="RemoteLogMirror"/> — steady work on the main
    /// thread for output nobody reads on a customer's phone.
    ///
    /// The editor and Development Builds keep everything.
    /// </summary>
    public static class ReleaseLogging
    {
        /// <summary>True in store builds, where logging is switched off.</summary>
        public static bool Silenced => !Debug.isDebugBuild;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Apply()
        {
            if (Silenced) Debug.unityLogger.logEnabled = false;
        }
    }
}
