# MIGRATION — перенастройка клона под новый проект

Это точная копия проекта **Bala Stories** (Unity 6000.3.7f1). Код/структура/бэкенд-контракты
идентичны оригиналу. Ниже — всё, что нужно поменять под новый продукт. Пункты 1–6 обязательны
для запуска, 7–9 — перед публикацией в сторах.

> Открывай проект в Unity **ровно той же версии — 6000.3.7f1**. Папка `Library/` не копировалась,
> она пересоберётся при первом открытии (это нормально, займёт несколько минут).

---

## 1. Идентификаторы приложения (Bundle ID)

Edit → Project Settings → **Player** (или напрямую в `ProjectSettings/ProjectSettings.asset`):

| Поле | Текущее значение | Заменить на |
|---|---|---|
| Android `applicationIdentifier` | `com.tokengc.balastories` | `com.<company>.<app>` |
| iOS `applicationIdentifier` | `com.mozz.fairyTales` | `com.<company>.<app>` |
| Product Name | `Bala Stories: Bedtime Stories for Kids` | новое имя |
| Company Name | `TokenGC` | новая компания |
| Version (`bundleVersion`) | `1.7.2` | `1.0.0` |
| iOS build number | `25` | `1` |

Меняй через UI Unity, а не руками — в .asset есть связки. Также обнови иконку и splash.

## 2. Firebase (полная замена — самое важное)

Текущий Firebase-проект: **`bala-stories-afb46`** (project number `118383655401`).
Старые ключи в конфигах НЕ валидны для нового bundle id.

1. Создай новый проект в Firebase Console.
2. Добавь Android-приложение с **новым** package name (из п.1) и iOS-приложение с новым bundle id.
3. Скачай и **замени файлы**:
   - `Assets/google-services.json`  (Android)
   - `Assets/GoogleService-Info.plist`  (iOS)
4. Включи в новом проекте: **Analytics (GA4)**, **Cloud Messaging (FCM)**.
5. Scripting defines `FT_FIREBASE` и `FT_FCM` ставятся ПОСЛЕ импорта Firebase SDK
   (SDK уже лежит в `Assets/Firebase/`). Без них проект собирается, но GA4/Push — no-op.

> ⚠️ Старые `google-services.json` / `GoogleService-Info.plist` содержат API-ключи прежнего
> проекта. Удали их сразу после замены и не коммить чужие ключи в публичный репозиторий.

## 3. Бэкенд

- `Assets/Scripts/FairyTales/Api/ApiClient.cs` (строка ~11):
  ```
  baseUrl = "https://bala-stories.apiapp.kz:3000"   →   новый домен API
  ```
  Это `[SerializeField]` — значение могло быть переопределено в сцене на объекте с ApiClient.
  Проверь инспектор в `Assets/Scenes/SampleScene.unity` и поменяй там тоже.

- Бэкенд аналогичный: эндпоинты те же (`/api/subscription/*`, `/api/push/register`,
  `/api/analytics/event`, `/api/tales/*`). Разверни свою копию сервера на новом домене.

## 4. IAP (подписки)

`Assets/Scripts/FairyTales/IAP/IAPManager.cs` (строки ~13–14):

| Константа | Текущее | Заменить на свой SKU |
|---|---|---|
| `ProductMonthly` | `fairytales_monthly` | новый product id |
| `ProductYearly` | `fairytales_yearly` | новый product id |

Затем создай эти же SKU в **App Store Connect** и **Google Play Console** под новым приложением.

## 5. Android manifest

`Assets/Plugins/Android/AndroidManifest.xml`:
- Push-канал `com.google.firebase.messaging.default_notification_channel_id` = `fairytales_default` —
  можно оставить или переименовать (должен совпадать с каналом, который создаёт `PushService`).
- Проверь `intent-filter` (deep links) — если там прописан старый хост/схема, поменяй.
- `package` подставляется из Bundle ID автоматически, но проверь FileProvider `authorities`.

## 6. Юридические ссылки

`Assets/Scripts/FairyTales/UI/Payment/PaymentScreen.cs` (строки ~237, 243) — Privacy Policy и
Terms ведут на Google Docs старого проекта. Заменить на свои.

## 7. PlayerPrefs (изоляция данных на устройстве)

Ключи с префиксом `ft_` (`ft_userId`, `ft_premium`, `ft_lang`, `ft_gender`, `ft_childName` и т.д.).
Если оба приложения будут стоять на одном тест-устройстве — данные не пересекутся (разные bundle id
= разные PlayerPrefs). Менять префикс НЕ обязательно.

## 8. Подпись (перед релизом)

- **Android:** создай новый keystore, пропиши в Player → Publishing Settings. (В проекте keystore
  нет — его и не должно быть в репозитории.)
- **iOS:** свой Team ID / provisioning profile.

## 9. Ассеты и контент

Сказки-контент грузятся с бэкенда; в `Assets/StreamingAssets/BundledTales` лежит бандл `komar`
(бесплатная сказка, читается офлайн до первого скачивания), обложка — `Resources/Covers/komar.png`.
Если у нового продукта другой каталог — обнови bundled-манифест и Resources/Covers.

`tale_{lang}.json` в бандле должны содержать `totalPages` и `genderedPages`: сервер считает их
на лету (`routes/tales.js`), а до бандла он не дотягивается. `genderedPages` бери из имён файлов
иллюстраций (`page_N_boy/_girl`), а не из маркеров `{m:|f:}` в тексте — иначе наборы ru и kz
разъедутся и слайды не привяжутся (см. `AI/CLIENT_TICKET_KZ_BUNDLE_SLIDES.md`).
Id бандла обязан совпадать с `BUNDLED_TALES` в `services/talesService.js` на бэкенде.

---

## Быстрый чек-лист первого запуска
- [ ] Открыть в Unity 6000.3.7f1, дождаться реимпорта
- [ ] Сменить Bundle ID (Android+iOS), Product/Company name, version
- [ ] Заменить `google-services.json` + `GoogleService-Info.plist` (новый Firebase)
- [ ] Поменять `baseUrl` в ApiClient (+ в сцене)
- [ ] Новые IAP SKU в коде и в консолях сторов
- [ ] Проверить deep-link/authorities в AndroidManifest
- [ ] Собрать dev-билд: проверить, что Analytics шлёт в НОВЫЙ Firebase, push регистрируется, API отвечает
