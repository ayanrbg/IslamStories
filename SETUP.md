# SETUP — что нужно сделать (Islam Stories)

Клонирование завершено. Код, структура и бэкенд-контракты идентичны оригиналу.
**Ничего больше копировать не нужно** — осталось только подставить свои идентификаторы
и положить свои конфиг-файлы. Ниже — по шагам, с текущими значениями.

Полное описание с пояснениями — в `MIGRATION.md`. Здесь — короткий рабочий чек-лист.

---

## ✅ Уже сделано мной
- [x] Клон проекта создан и запушен: `https://github.com/ayanrbg/IslamStories.git` (ветка `main`)
- [x] Имя продукта: `productName = Islam Stories` (+ текст запроса микрофона)
- [x] Большие бинарники Firebase переведены в Git LFS
- [x] Старые Firebase-конфиги исключены из репозитория (`.gitignore`)

## ⬜ Что нужно сделать тебе

### 0. Первый запуск
- [ ] Установить **Git LFS** на машине, где будешь клонировать (`git lfs install`) — иначе
      Firebase `.so`/`.bundle` подтянутся как текстовые заглушки.
- [ ] Открыть проект в Unity **6000.3.7f1** (ровно эта версия). `Library/` пересоберётся сама.

### 1. Bundle ID и имя (Player Settings)
Edit → Project Settings → Player:
- [ ] Android `applicationIdentifier`: `com.tokengc.balastories` → **свой** `com.<company>.<app>`
- [ ] iOS `applicationIdentifier`: `com.mozz.fairyTales` → **свой** `com.<company>.<app>`
- [ ] Company Name: `TokenGC` → своё
- [ ] Version: `1.7.2` → `1.0.0`, iOS build number: `25` → `1`
- [ ] Иконка и splash

### 2. Firebase (заменить файлы + новый проект)
Текущий Firebase-проект: `bala-stories-afb46` (number `118383655401`) — НЕ твой.
- [ ] Создать новый проект в Firebase Console
- [ ] Добавить Android-приложение (новый package из шага 1) и iOS-приложение (новый bundle)
- [ ] Скачать и положить (перезаписать) файлы:
      - `Assets/google-services.json`
      - `Assets/GoogleService-Info.plist`
- [ ] Включить в проекте **Analytics (GA4)** и **Cloud Messaging (FCM)**
- [ ] После импорта SDK выставить scripting defines `FT_FIREBASE` и `FT_FCM`

### 3. Backend
- [ ] `Assets/Scripts/FairyTales/Api/ApiClient.cs` (строка ~11):
      `baseUrl = "https://bala-stories.apiapp.kz:3000"` → **твой домен**
- [ ] Проверить значение поля в сцене `Assets/Scenes/SampleScene.unity` (объект с `ApiClient`,
      `[SerializeField]` мог быть переопределён в инспекторе)
- [ ] Развернуть аналогичный бэкенд на своём домене (эндпоинты те же)

### 4. IAP (подписки)
- [ ] `Assets/Scripts/FairyTales/IAP/IAPManager.cs` (строки ~13–14):
      `fairytales_monthly` / `fairytales_yearly` → свои SKU
- [ ] Создать эти же SKU в **App Store Connect** и **Google Play Console**

### 5. Android manifest
- [ ] `Assets/Plugins/Android/AndroidManifest.xml`: проверить push-канал `fairytales_default`,
      deep-link `intent-filter` (хост/схема), FileProvider `authorities`

### 6. Юридические ссылки
- [ ] `Assets/Scripts/FairyTales/UI/Payment/PaymentScreen.cs` (строки ~237, 243):
      ссылки Privacy/Terms на Google Docs → свои

### 7. Перед релизом
- [ ] Android: свой keystore (Player → Publishing Settings)
- [ ] iOS: свой Team ID / provisioning profile

---

## Проверка dev-билда
- [ ] Собрать dev-билд
- [ ] Analytics шлёт события в **новый** Firebase-проект
- [ ] Push-токен регистрируется на бэкенде
- [ ] API отвечает с нового домена

> Хочешь — часть этого (Bundle ID, backend URL, IAP SKU) можно проставить сразу в коде.
> Firebase-конфиги и keystore придётся положить руками — их автоматически взять неоткуда.
