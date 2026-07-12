using System;
using System.Collections.Generic;

namespace FairyTales.UI.Core
{
    /// <summary>
    /// Holds a pending deep-link route delivered by a push notification
    /// (CLIENT_TICKET_PUSH §4). The push payload's <c>data</c> dictionary is stored
    /// here by <see cref="Diagnostics.PushService"/>; <c>LibraryScreen</c> consumes it
    /// once the catalog is loaded (it owns the ScreenManager + tale list needed to
    /// navigate). Kept UI-agnostic on purpose so the messaging layer never references
    /// screens directly.
    ///
    /// data contract: { type: "tale"|"paywall"|"url"|"home", taleId?, url? }.
    /// </summary>
    public static class DeepLink
    {
        /// <summary>Last unconsumed push route, or null. Cleared by the consumer.</summary>
        public static IDictionary<string, string> Pending;

        /// <summary>Raised when a new route arrives so a live screen can act immediately.</summary>
        public static event Action OnRoute;

        public static void Set(IDictionary<string, string> data)
        {
            if (data == null) return;
            Pending = data;
            try { OnRoute?.Invoke(); } catch { /* routing must never break messaging */ }
        }
    }
}
