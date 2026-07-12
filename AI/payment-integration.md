# Payment Integration Plan (Phase 10)

## Overview
- Unity IAP — Google Play + App Store
- Auto-renewable subscriptions (monthly / yearly) with store-native trial
- Server-side receipt validation
- Premium = доступ ко всем сказкам

## Architecture

```
Client (Unity IAP)            Server (Node.js)
──────────────────           ─────────────────
Purchase / Restore
       │
       ▼
Store (Google/Apple)
       │
       ▼
Receipt (чек)
       │
       ├──► POST /api/subscription/verify ──► валидация через Store API
       │                                          │
       │                                          ▼
       │                                    Save to DB
       │                                    (userId, plan, expiresAt, store, receiptId)
       │                                          │
       ◄──────────────────────────────────────────┘
       │
       ▼
Update UI (premium = true)
```

---

## Phase 10A — Server (3 endpoints)

### Endpoints

#### `POST /api/subscription/verify`
Validate receipt from store.
```
Authorization: Bearer <token>
Content-Type: application/json

{
  "receipt": "<store receipt string>",
  "store": "google" | "apple",
  "productId": "com.fairytales.premium.monthly"
}
```

Response (200):
```json
{
  "premium": true,
  "plan": "yearly",
  "expiresAt": "2026-04-10T00:00:00.000Z"
}
```

Errors:
```json
{ "error": "Invalid receipt" }        // 400
{ "error": "Receipt validation failed" } // 502
```

#### `GET /api/subscription/status`
Current subscription status.
```
Authorization: Bearer <token>
```

Response (200):
```json
{
  "premium": true,
  "plan": "yearly",
  "expiresAt": "2026-04-10T00:00:00.000Z"
}
```

No subscription:
```json
{
  "premium": false,
  "plan": null,
  "expiresAt": null
}
```

#### `POST /api/subscription/restore`
Restore purchases (same as verify, semantically separate).
```
Authorization: Bearer <token>
Content-Type: application/json

{
  "receipt": "<store receipt string>",
  "store": "google" | "apple",
  "productId": "com.fairytales.premium.yearly"
}
```

Response: same as verify.

### DB Table — `subscriptions`

| Column    | Type     | Description                        |
|-----------|----------|------------------------------------|
| userId    | string   | FK to users                        |
| store     | string   | "google" or "apple"                |
| productId | string   | Store product ID                   |
| plan      | string   | "monthly" or "yearly"              |
| receiptId | string   | Unique receipt/transaction ID      |
| expiresAt | datetime | Subscription expiration            |
| createdAt | datetime | First purchase time                |
| updatedAt | datetime | Last validation time               |

---

## Phase 10B — Client (Unity)

### New Files

| File | Purpose |
|------|---------|
| `Models/SubscriptionModels.cs` | VerifyRequest, SubscriptionStatus |
| `Api/SubscriptionService.cs` | Verify, GetStatus, Restore — wrappers over endpoints |
| `IAP/IAPManager.cs` | Unity IAP: init, purchase, get receipt |
| `IAP/PremiumState.cs` | Singleton: `bool IsPremium`, cache in PlayerPrefs + server check |

### Modified Files

| File | Changes |
|------|---------|
| `PaymentScreen.cs` | OnTrial → IAPManager.Purchase(); OnRestore → IAPManager.Restore() |
| `LibraryScreen.cs` | Check PremiumState on show, hide "Unlock All" if premium |
| `TaleCard.cs` | Show lock icon on premium tales (when server marks them) |

---

## Flows

### Purchase Flow
```
1. User taps "Попробовать бесплатно" (trial CTA)
2. IAPManager.Purchase(productId) → native store dialog
3. Store returns receipt
4. POST /api/subscription/verify { receipt, store, productId }
5. Server validates → saves → returns { premium: true }
6. PremiumState.IsPremium = true
7. PaymentScreen closes → LibraryScreen updates
```

### Restore Flow
```
1. User taps "Восстановить покупки"
2. IAPManager.RestorePurchases() → store returns all active receipts
3. For each → POST /api/subscription/verify
4. If active subscription found → PremiumState.IsPremium = true
```

### App Launch Flow
```
1. GET /api/subscription/status
2. PremiumState.IsPremium = response.premium
3. If expired → PremiumState.IsPremium = false
4. Show PaymentScreen when user tries to open premium tale
```

---

## Product IDs

```
com.fairytales.premium.monthly
com.fairytales.premium.yearly
```

Both are auto-renewable subscriptions with trial period (configured in store console).

---

## Implementation Order

1. Server — endpoints + DB table (start with stub `premium: true`, no real store validation)
2. Client — SubscriptionModels, SubscriptionService, PremiumState
3. Client — IAPManager (Unity IAP package)
4. Client — Wire PaymentScreen to IAPManager
5. Client — PremiumState checks in LibraryScreen / TaleCard
6. Store consoles — configure products in Google Play Console / App Store Connect
7. Server — real validation via Store APIs

---

## TODO (later)
- Mark specific tales as premium on server side
- Free tales vs premium tales distinction in API response
- Grace period handling for expired subscriptions
- Subscription webhook from stores (real-time status updates)
