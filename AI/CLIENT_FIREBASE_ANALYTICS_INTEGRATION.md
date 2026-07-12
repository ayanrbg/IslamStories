# Firebase GA4 — что сделано в клиенте и что осталось руками

Реализация тикета `CLIENT_TICKET_ANALYTICS.md` по таксономии `ANALYTICS_SPEC.md` (v1).

## Что уже в коде (компилируется и работает СЕЙЧАС)

- **`Assets/Scripts/FairyTales/Diagnostics/Analytics.cs`** — единый фасад аналитики.
  Паттерн как у `RemoteLog`: самобутстрап (`[RuntimeInitializeOnLoadMethod]`), не кидает
  исключения, не блокирует геймплей. Два стока:
  1. **GA4 (Firebase)** — вызовы компилируются ТОЛЬКО при заданном скрипт-дефайне
     `FT_FIREBASE` (т.е. после импорта Firebase Unity SDK). Без дефайна GA4-вызовы —
     no-op, проект всегда собирается. Инструментирование экранов при этом не меняется.
  2. **Бэкенд-зеркало** — `POST /api/analytics/event`, fire-and-forget, батч ≤ 50,
     флаш каждые 15 c / при 25 событиях / на паузе / выходе. Только для критичного
     вайтлиста (spec §5): `purchase_*`, `paywall_view/dismiss`, `promo_redeem`,
     `tale_complete`. `tale_page_view` в зеркало НЕ шлётся (объём — это для BigQuery).
- **Детский режим (spec Rule #1)** зашит: при инициализации Firebase ставится
  `allow_personalized_ads=false`, никакого Advertising ID / AdMob не подключается, PII
  (имя ребёнка, email) не уходит ни в одном событии/параметре.
- **User properties** (`RefreshUserProps`) читаются из PlayerPrefs и ставятся на старте
  и при изменениях: `is_premium`, `premium_source`, `app_language`, `child_gender`,
  `has_cloned_voice`. `SetUserId` = device-based `AuthService.GetOrCreateUserId()` (не PII).
- **Все события §2 инструментированы** (см. таблицу «Где что» ниже).

## Осталось сделать руками (задача №1 тикета — импорт SDK)

Импортировать бинарники Firebase сам агент надёжно не может: нужны конфиги-секреты из
консоли (`bala-stories-afb46`), редакторный импорт-пасс для нативных плагинов и EDM4U
Force Resolve. Runbook под **Unity 6000.3.7f1**:

1. Скачать **Firebase Unity SDK** (latest 13.x+ поддерживает Unity 6):
   https://firebase.google.com/download/unity → распаковать zip.
2. В открытом проекте: `Assets → Import Package → Custom Package` →
   **`dotnet4/FirebaseAnalytics.unitypackage`** (именно `dotnet4`, НЕ `dotnet3` — Unity 6
   на .NET Standard/4.x). Import All. Тянет за собой `FirebaseApp` (core) + EDM4U.
3. При первом импорте EDM4U спросит про авто-резолв Android → **Enable**. Затем
   `Assets → External Dependency Manager → Android Resolver → Force Resolve`.
4. Конфиги из консоли Firebase (проект `bala-stories-afb46`):
   - Android app `com.tokengc.balastories` → `google-services.json` → в `Assets/`
   - iOS app `com.mozz.fairyTales` → `GoogleService-Info.plist` → в `Assets/`
5. Скрипт-дефайн **`FT_FIREBASE`** для iOS+Android
   (`Project Settings → Player → Other Settings → Scripting Define Symbols`). После этого
   GA4-ветка в `Analytics.cs` компилируется — БЕЗ правок в экранах. (Агент может вписать
   его в ProjectSettings.asset, но только ПОСЛЕ импорта SDK, иначе не скомпилируется.)
6. Проверить в **DebugView**:
   - Android: `adb shell setprop debug.firebase.analytics.app com.tokengc.balastories`
   - iOS: launch arg `-FIRDebugEnabled` (Xcode → Scheme → Arguments).
   Firebase Console → Analytics → DebugView. Выключить: `… .none`.

Гочи Unity 6: min Android API у Firebase — 23+ (проверить Player Settings). Newtonsoft
уже стоит через UPM (`com.unity.nuget.newtonsoft-json`) — Analytics-пакет свой не тянет,
конфликта быть не должно; если EDM4U/Newtonsoft задублируется — удалить дубль из
`Assets/`. iOS собирается через Xcode с Pods (Podfile генерит EDM4U).

## Где что инструментировано

| Событие | Файл |
|---|---|
| `app_open`, user properties, SetUserId | `Diagnostics/Analytics.cs` (bootstrap) |
| `onboarding_start`, `onboarding_step` (language) | `UI/Onboarding/LanguageSelectScreen.cs` |
| `onboarding_step` (profile), `profile_set`, `onboarding_complete` | `UI/Onboarding/PersonalizationScreen.cs` |
| `library_view` | `UI/Library/LibraryScreen.cs` |
| `tale_card_click`, `tale_coming_soon_click`, `tale_download_start/complete/error` | `UI/Library/TaleCard.cs` |
| `tale_open`, `tale_page_view`, `tale_complete`, `tale_abandon`, `narration_play/complete` | `UI/Reading/ReadingScreen.cs` |
| `voice_clone_start/success/error` (+ `has_cloned_voice`) | `UI/Library/TaleDetailScreen.cs` |
| `paywall_view/dismiss`, `purchase_start/success`, `purchase` (revenue), `purchase_restore`, `promo_redeem` | `UI/Payment/PaymentScreen.cs` |
| `purchase_error` | `IAP/IAPManager.cs` |
| `premium_source` persistence + user-props refresh | `IAP/IAPManager.cs` |
| `app_language` update on lang change | `UI/Core/Loc.cs` |

## Принятые допущения / отклонения от спеки (флажки)

- **`purchase_success` — ровно 1 раз на покупку**: логируется в `PaymentScreen` в колбэке
  успешной пользовательской покупки, НЕ в `IAPManager.ProcessPurchase` — потому что на iOS
  restore переигрывает `ProcessPurchase` на каждый чек и дал бы дубли. Restore шлёт свой
  `purchase_restore`.
- **`voice_type`** в `narration_play`/complete: `NarrationMode.AI → "cloned"`, иначе
  `"default"`. Приближение: AI-режим может быть и «диктор» (не клон), точного разделения
  клон/диктор на этапе воспроизведения нет.
- **`app_language`/`language`** шлём в родных кодах приложения `ru/kz/en/uz` (в спеке пример
  `kk` вместо `kz`). Оставил как есть для консистентности со slug-подходом; при желании
  маппинг `kz→kk` — одна строка в `RefreshUserProps`/`tale_open`.
- **`paywall source`**: помимо enum спеки использую описательные `narrate`, `parent_voice`,
  `first_read` (spec допускает доп. значения source).
- **`premium_source`**: сервер отдаёт `apple/google/promo` в `Entitlement.source`; клиент
  кэширует в PlayerPrefs `ft_premium_source` (в `ApplyEntitlement` и на промокоде=`promo`).
  Для admin-грантов сервер должен вернуть `source="admin"`.

## Тест без Firebase SDK

Даже без `FT_FIREBASE` всё проверяемо: события пишутся в консоль (`[Analytics] <name> | ...`)
и критичные уходят в `POST /api/analytics/event` — читать через
`GET /api/analytics/summary` / `?events` (X-Admin-Key).
