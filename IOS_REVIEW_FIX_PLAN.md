# Исправление rejection Apple — Bala Stories 1.0.0

## 1. Удалить NSLocalNetworkUsageDescription из Info.plist

Приложение не использует локальную сеть. Если Unity генерирует ключ заново при билде — добавить PostProcessBuild скрипт.

## 2. Ответить в App Store Connect → Resolution Center

**1. Does the app include third-party analytics?**

No. The app does not include any third-party analytics SDKs. There is no Firebase Analytics, Amplitude, Crashlytics, Unity Analytics, or any other telemetry framework integrated into the app. No usage metrics, session data, or behavioral data is collected.

**2. Does the app include third-party advertising?**

No. The app contains no advertising of any kind. There are no ad SDKs or ad networks integrated.

**3. Will the data be shared with any third parties?**

No. No data is shared with any third parties. All server communication is exclusively between the app and our own backend server operated by MOZZ. Voice recordings sent for AI narration processing are stored on our server only and are not shared with or accessible by any third party.

**4. Is the app collecting any user or device data for purposes beyond third-party analytics or third-party advertising?**

The app collects the following data, all of which is essential for core functionality:

**Stored locally on device only (never sent to any server):**
- Child's first name — used to personalize fairy tale text (e.g., inserting the child's name into the story)
- Child's gender — used to adjust grammatical forms in story text (gendered language support for Russian/Kazakh)
- Reading progress — current page per story
- Audio volume and mute preferences
- Interface language preference (Russian, Kazakh, English, Uzbek)

**Sent to our server (operated by MOZZ):**
- Child's first name and gender — sent during registration to enable server-side text personalization
- Voice recording (up to 30 seconds, 4 sample sentences) — sent to our server for AI voice cloning to generate personalized narration of fairy tales. The user explicitly initiates this action. Recordings are stored only on our server.
- In-App Purchase receipts — sent to our server for subscription validation

**The app works fully offline** for reading stories with pre-loaded content. Online connection is only required for: initial registration, AI voice cloning, AI-narrated audio download, and subscription validation.

No device identifiers (UDID, IDFA, IDFV), IP addresses, location data, contacts, photos, or any other personal data is collected. No data is collected passively or in the background.

## 3. Заполнить метаданные подписок (статус: "Метаданные отсутствуют")

- Локализованное название и описание (RU/EN/KZ)
- Цены для всех регионов
- Review screenshot

## 4. Добавить In-App Purchase capability в Xcode

Signing & Capabilities → In-App Purchase.

## 5. Протестировать покупку в Sandbox

1. Создать Sandbox Tester: App Store Connect → Users and Access → Sandbox
2. На устройстве: Settings → App Store → Sandbox Account → войти
3. Нажать кнопку подписки → должен появиться StoreKit purchase sheet
