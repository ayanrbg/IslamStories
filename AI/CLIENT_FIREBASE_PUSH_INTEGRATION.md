# Push (FCM) — что сделано в коде и что осталось руками

Реализация тикета `AI/CLIENT_TICKET_PUSH.md`. Клиентская логика полностью в C#, но
Firebase-вызовы скомпилируются только после ручных нативных шагов (аналогично тому, как
аналитика гейтится дефайном `FT_FIREBASE`).

## Новые/изменённые файлы
- `Assets/Scripts/FairyTales/Diagnostics/PushService.cs` — самобутстрап (как `Analytics`/
  `RemoteLog`). Разрешение → токен → `POST /api/push/register` (старт, refresh токена, смена
  языка). Тап по пушу → парсит `data` → `DeepLink`. `PushService.Unregister()` шлёт
  `/api/push/unregister`. Опционально `/api/push/opened` (если в `data` есть `campaignId`) +
  GA4-событие `push_open`. **Всё за дефайном `FT_FCM`** кроме HTTP/deep-link (те компилируются
  всегда). Токен регистрируется под тем же device-based `AuthService.GetOrCreateUserId()`.
- `Assets/Scripts/FairyTales/UI/Core/DeepLink.cs` — UI-агностик держатель маршрута из пуша
  (`Pending` + событие `OnRoute`). Messaging-слой не знает про экраны.
- `LibraryScreen.cs` — подписка на `DeepLink.OnRoute` + `HandleDeepLink()`: `tale` → открыть
  сказку (через тот же пейвол-гейт, если заблокирована), `paywall` → `PaymentScreen.Open`,
  `url` → `Application.OpenURL`, `home`/неизвестное → ничего. Резолв `taleId` требует
  загруженного каталога — если пуш пришёл раньше, маршрут ждёт и применяется после `LoadTales`.
- `AuthService.Logout()` → `PushService.Unregister()` перед сбросом userId.
- `Assets/Plugins/Android/AndroidManifest.xml` — `POST_NOTIFICATIONS` + default notification
  channel `fairytales_default` (иконка status-bar — опционально, см. коммент в манифесте).

## Осталось руками (нельзя из C#)
1. **Импортировать Firebase Messaging** Unity-модуль (`FirebaseMessaging.unitypackage`) — тот же
   SDK, что и аналитика, добавляется модуль Messaging (появится `Firebase.Messaging.dll`).
2. **Добавить scripting define `FT_FCM`** (Player Settings → Scripting Define Symbols, iOS+Android)
   ПОСЛЕ импорта. До этого проект собирается, push-код no-op.
3. **iOS**: capability **Push Notifications** + **Background Modes → Remote notifications**.
   APNs-ключ (.p8) заливает бэкенд в Firebase Console. Проверить, что `GoogleService-Info.plist`
   в проекте (есть).
4. **Android**: `google-services.json` уже подтянут через Firebase-плагин. Иконка пуша —
   опционально положить белый силуэт в `res/drawable` и раскомментить meta-data в манифесте.

## Контракт `data` (согласовать с бэкендом)
`{ type: "tale"|"paywall"|"url"|"home", taleId?, url?, campaignId? }`. Тап роутится только при
`NotificationOpened` (запуск из фона/закрытого) — foreground-доставку не перехватываем, чтобы не
выдёргивать пользователя с экрана.

## Проверка (чек-лист тикета)
- [ ] Токен уходит в `/api/push/register` при старте / refresh / смене языка.
- [ ] Тестовый пуш приходит на iOS и Android.
- [ ] Тап открывает нужный экран для `tale/paywall/url/home`.
- [ ] Пуш на языке пользователя (перерегистрация по `Loc.OnLanguageChanged`).
- [ ] Выключение/логаут → `/api/push/unregister`.
