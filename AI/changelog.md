# Changelog

## Session 1 — 2026-03-09

### Phase 0 — Infrastructure ✅

**Packages:**
- Added `com.unity.nuget.newtonsoft-json` (3.2.1) to `Packages/manifest.json`
- DOTween installed via Asset Store (by user)

**Project setup:**
- Renamed `CLAUDE.md1` → `CLAUDE.md`
- Created `AI/` folder for all dev documentation

**Folder structure created in Assets:**
```
Assets/
├── Scripts/FairyTales/
│   ├── Api/
│   ├── Models/
│   ├── Audio/
│   └── UI/
│       ├── Core/
│       ├── Onboarding/
│       ├── Library/
│       ├── Reading/
│       └── Narration/
├── Audio/
│   ├── Default/
│   └── Music/
├── Prefabs/
│   ├── UI/
│   └── Screens/
└── Sprites/
    ├── UI/
    └── Tales/
```

**Documentation created:**
- `AI/architecture.md` — full architecture, folder structure, screen flow, development phases
- `AI/api-requirements.md` — existing + missing server endpoints for backend developer
- `AI/ui-flow.md` — all screens and navigation description based on UI mockups
- `AI/changelog.md` — this file

### Phase 1 — Core (API + Data) ✅

**Models** (5 files in `Scripts/FairyTales/Models/`):
- `AuthModels.cs` — LoginRequest, RegisterRequest, LoginResponse, RegisterResponse, ProfileData, ProfileUpdateRequest/Response
- `TaleModels.cs` — TaleSummary, TaleDetail, PersonalizeRequest/Response
- `VoiceModels.cs` — CloneResponse, DeleteVoiceResponse
- `NarrationModels.cs` — NarrateAllResponse, NarrationStatusResponse
- `DraftModels.cs` — Draft, DraftCreateRequest/Response, DraftDeleteResponse

**API Client** (`Scripts/FairyTales/Api/ApiClient.cs`):
- MonoBehaviour with configurable baseUrl
- Methods: Get, GetAudio, PostJson, Post, PostForAudio, PostMultipart, PutJson, Delete
- Auto JWT auth via Bearer token
- Unified error formatting

**Services** (4 files in `Scripts/FairyTales/Api/`):
- `AuthService.cs` — Login, Register, GetProfile, UpdateProfile, TryRestoreSession, Logout
- `TalesService.cs` — GetTales, GetTale, Personalize
- `VoiceService.cs` — CloneVoice, DeleteVoice, GetDrafts, CreateDraft, GetDraft, DeleteDraft
- `NarrationService.cs` — NarratePage, NarrateAll, GetNarrationStatus, DownloadNarratedPage

---

## Session 2 — 2026-03-09

### Phase 2 — Audio ✅

**Files created** (4 files in `Scripts/FairyTales/Audio/`):

- `MicRecorder.cs` — MonoBehaviour, запись с микрофона → WAV byte[]
  - Auto-stop по maxDuration (30s), events: OnStarted, OnStopped(byte[])
  - WAV encoding (PCM 16-bit, 44100 Hz)

- `NarrationPlayer.cs` — MonoBehaviour, воспроизведение аудио
  - PlayFromBytes(byte[]) — temp file → UnityWebRequestMultimedia (WAV/MP3/OGG)
  - PlayClip(AudioClip) — для предзаписанного аудио
  - Pause/Resume/Stop, Progress, OnFinished event

- `BackgroundMusicManager.cs` — Singleton, DontDestroyOnLoad
  - PlayMenu() — музыка меню (SerializeField)
  - PlayForTale(taleId) — загрузка из Resources/Music/{taleId}
  - SetVolume(), SetMuted(), Stop()

- `DefaultNarrationProvider.cs` — обычный класс (не MonoBehaviour)
  - GetPage(taleId, page) — Resources/Audio/Default/{taleId}/page_{N}
  - HasNarration(), HasAnyNarration()

**Resource paths:**
```
Assets/Resources/Audio/Default/{taleId}/page_0.wav
Assets/Resources/Music/{taleId}.wav
```

### Phase 3 — UI Framework ✅

**Files created** (2 files in `Scripts/FairyTales/UI/Core/`):

- `BaseScreen.cs` — abstract MonoBehaviour, RequireComponent(CanvasGroup)
  - Show()/Hide() — DOTween fade (CanvasGroup alpha)
  - ShowImmediate()/HideImmediate() — без анимации
  - Virtual hooks: OnShown(), OnHidden()

- `ScreenManager.cs` — MonoBehaviour, управление экранами
  - Auto-registers all BaseScreen children in Awake
  - Show<T>() — fade out current → fade in next
  - ShowImmediate<T>(), Get<T>(), Current
  - initialScreen — стартовый экран через Inspector

### Editor Tooling

- `Scripts/FairyTales/Editor/SceneSetup.cs` — menu: FairyTales → Setup Scene
  - Creates [Services] (ApiClient)
  - Creates [BackgroundMusic] (BackgroundMusicManager)
  - Creates [Audio] (MicRecorder + NarrationPlayer)
  - Creates [UICanvas] (Canvas 1080×1920 + ScreenManager + 9 screen stubs)
  - Creates EventSystem if missing
  - Idempotent — safe to run multiple times
  - All operations support Undo

---

## Session 3 — 2026-03-09

### Phase 4 — Onboarding Screens ✅

**Files created** (3 files in `Scripts/FairyTales/UI/Onboarding/`):

- `LanguageSelectScreen.cs` — выбор языка (RU/KZ/EN)
  - SerializeField кнопки + визуальные индикаторы выбора
  - Сохраняет `ft_lang` в PlayerPrefs
  - Навигация → PersonalizationScreen

- `PersonalizationScreen.cs` — имя ребёнка + пол
  - TMP_InputField для имени, 2 кнопки пола
  - Кнопка смены языка → назад к LanguageSelectScreen
  - Валидация: не даёт продолжить с пустым именем
  - Сохраняет `ft_childName`, `ft_gender` в PlayerPrefs
  - Навигация → LoadingScreen

- `LoadingScreen.cs` — регистрация + персонализация
  - Slider прогресс-бар + TMP_Text статус
  - Полный flow: Register → GetTales → Personalize каждую сказку
  - Прогресс обновляется по шагам
  - Event OnLoadingComplete для внешней подписки
  - Сохраняет `ft_onboarded` флаг
  - Генерирует userId (GUID) если не существует

**Updated:**
- `Editor/SceneSetup.cs` — теперь добавляет реальные компоненты
  - CreateScreen<T>() — generic метод для экранов с компонентами
  - LanguageSelectScreen как initialScreen в ScreenManager

**PlayerPrefs keys (onboarding):**
- `ft_lang` — язык (ru/kz/en)
- `ft_childName` — имя ребёнка
- `ft_gender` — пол (male/female)
- `ft_userId` — GUID пользователя
- `ft_onboarded` — флаг завершения онбординга (1)

---

## Session 4 — 2026-03-10

### Bugfix — Scene Setup

- `Editor/SceneSetup.cs` — теперь вызывает `OnboardingSetup.Setup()` после создания Canvas, чтобы UI-элементы онбординга создавались автоматически
- `Editor/OnboardingSetup.cs` + `Editor/SceneSetup.cs` — все `new GameObject(...)` теперь создаются с `typeof(RectTransform)` для корректной работы на Canvas (ранее создавались с обычным Transform)

**Next step:** Phase 5 — Library

---

## Session 5 — 2026-03-10

### Phase 5 — Library ✅

**Files created** (4 files in `Scripts/FairyTales/UI/Library/`):

- `LibraryScreen.cs` — грид карточек сказок
  - Загрузка через TalesService.GetTales()
  - GridLayoutGroup 4 колонки, ScrollRect
  - Кнопки: Settings, Music toggle, Unlock All
  - Клик по карточке → TaleDetailScreen

- `TaleCard.cs` — компонент карточки
  - Init(tale, isLocked, onClick) — обложка + название + замок
  - CoverProvider для загрузки обложек

- `TaleDetailScreen.cs` — детали сказки
  - Обложка, название, кол-во страниц
  - 3 кнопки: Читать, Слушать, Озвучить
  - LoadDetail() — GetTale + Personalize + GetNarrationStatus

- `CoverProvider.cs` — статический хелпер
  - Resources/Covers/{taleId}

**Editor:**
- `Editor/LibrarySetup.cs` — menu: FairyTales → Setup Library UI
  - SetupLibraryScreen, SetupTaleDetailScreen, CreateCardPrefab
  - Prefab: `Assets/Prefabs/UI/TaleCard.prefab`

**Bugfixes:**
- `LibrarySetup.cs` — добавлен `using FairyTales.UI.Core` (CS0246)
- `FindOrAddComponent` — сначала `FindAnyObjectByType<T>(FindObjectsInactive.Include)`
- `CreateCardPrefab` — вынесен `AssignCardPrefab()` для назначения при повторном запуске
- ScrollView: заменён `Mask` на `RectMask2D` (Stencil не работает в URP 2D)

### Phase 6 — Reading ✅

**Files created** (4 files in `Scripts/FairyTales/UI/Reading/`):

- `ReadingScreen.cs` — экран чтения
  - Fullscreen иллюстрация + scrollable текстовая панель (30% снизу)
  - Кнопки: Home, TOC, Music (top), Prev/Next (bottom)
  - Авто-озвучка через DefaultNarrationProvider + NarrationPlayer
  - Home → LibraryScreen

- `PageNavigator.cs` — перелистывание страниц
  - DOTween fade только на illustrationImage + pageText (кнопки остаются)
  - ResolveGender() — regex парсер `{m:...|f:...}` плейсхолдеров
  - Init/NextPage/PrevPage/GoToPage + OnPageChanged event

- `TableOfContentsPopup.cs` — popup оглавление
  - CanvasGroup fade in/out
  - Горизонтальный ScrollRect с миниатюрами
  - Текущая страница — cyan border
  - Клик по миниатюре → GoToPage + close

- `IllustrationProvider.cs` — статический хелпер
  - Resources/Illustrations/{taleId}/page_{page}

**Editor:**
- `Editor/ReadingSetup.cs` — menu: FairyTales → Setup Reading UI
  - SetupReadingScreen, CreateTocPopup, CreateThumbnailPrefab
  - Prefab: `Assets/Prefabs/UI/TocThumbnail.prefab`

**Updated:**
- `TaleDetailScreen.cs` — OnRead() навигирует на ReadingScreen с _detail
- `TaleDetailScreen.cs` — LoadDetail() вызывает Personalize() для подмены pages
- `SceneSetup.cs` — ReadingScreen как реальный компонент вместо stub

**Resource paths:**
```
Assets/Resources/Illustrations/{taleId}/page_0.png
Assets/Resources/Illustrations/{taleId}/page_1.png
```

### Flow Change — убран LoadingScreen из основного flow

**Новый flow:**
```
LanguageSelect → Personalization → Library
                                    ↓ (Settings)
                              Personalization
```

**Изменения:**

- `PersonalizationScreen.cs` — OnContinue() теперь → LibraryScreen (вместо LoadingScreen)
- `LibraryScreen.cs` — полный рефактор:
  - Lazy Register: при первом OnShown вызывает AuthService.Register()
  - btnSettings → PersonalizationScreen
  - Всегда перезагружает сказки при OnShown (имя/пол могли измениться)
  - GetOrCreateUserId() перенесён из LoadingScreen
- LoadingScreen остаётся в кодовой базе, но не используется в основном flow

**Next step:** Phase 7 — Narration

---

## Session 6 — 2026-03-10

### Auto-skip Onboarding

**ScreenManager.cs** — добавлено поле `onboardedScreen`:
- В Start() проверяет `PlayerPrefs.GetString("ft_childName")`
- Если имя есть → показывает `onboardedScreen` (LibraryScreen) вместо `initialScreen`
- SceneSetup назначает LibraryScreen в `onboardedScreen`

### Phase 7 — Narration ✅

**Files created** (3 files in `Scripts/FairyTales/UI/Narration/`):

- `NarrationSetupScreen.cs` — экран настройки озвучки
  - Обложка + название сказки
  - 2 вкладки: "Новая запись" / "Черновики"
  - Новая запись: имя рассказчика + кнопка "Начать" → CreateDraft → VoiceRecordingScreen
  - Черновики: список drafts → клик → VoiceRecordingScreen

- `VoiceRecordingScreen.cs` — запись голоса
  - 4 sample sentences для чтения вслух (хардкод)
  - Record/Stop (через MicRecorder), таймер в Update
  - Play (через NarrationPlayer)
  - Send → CloneVoice → NarrateAll → NarrationProgressScreen

- `NarrationProgressScreen.cs` — прогресс AI озвучки
  - Polling narration-status каждые 3 сек
  - Slider progressBar + pagesReady/totalPages
  - Когда done → кнопка "Готово" → TaleDetailScreen

**Editor:**
- `Editor/NarrationSetup.cs` — menu: FairyTales → Setup Narration UI
  - SetupNarrationSetupScreen, SetupVoiceRecordingScreen, SetupNarrationProgressScreen
  - Prefab: `Assets/Prefabs/UI/DraftItem.prefab`

**Updated:**
- `TaleDetailScreen.cs` — OnNarrate() → NarrationSetupScreen.SetTale + Show
- `SceneSetup.cs` — stubs заменены на CreateScreen<T> для narration экранов, onboardedScreen assigned
- `ScreenManager.cs` — onboardedScreen для автоскипа онбординга

**Full narration flow:**
```
TaleDetailScreen → "Озвучить" → NarrationSetupScreen
  → "Начать" → CreateDraft → VoiceRecordingScreen
    → Record → CloneVoice → NarrateAll → NarrationProgressScreen
      → Poll → Done → TaleDetailScreen (re-checks narration status)
```

**Known limitation:**
- ~~Кнопка "Слушать" в TaleDetailScreen — пока Debug.Log заглушка.~~ Реализовано в Phase 8.

---

## Session 7 — 2026-03-10

### Phase 8 — Listen & Polish ✅

#### 32. "Слушать" — Real Playback

**ReadingScreen.cs** — добавлен `NarrationMode` enum (None/Default/AI):
- `SetTale(tale, mode)` — принимает режим озвучки
- AI mode: скачивает аудио через `NarrationService.DownloadNarratedPage` → `NarrationPlayer.PlayFromBytes`
- Default mode: `DefaultNarrationProvider.GetPage` → `NarrationPlayer.PlayClip`
- AI fallback: при ошибке загрузки AI → автоматически пробует Default
- Авто-воспроизведение при смене страницы

**TaleDetailScreen.cs** — "Слушать" теперь реально работает:
- AI narration ready → ReadingScreen с NarrationMode.AI
- Default narration available → ReadingScreen с NarrationMode.Default
- Нет озвучки → Toast "Нет озвучки. Нажмите «Озвучить»"

#### 33. DOTween Animations

**ButtonScaleEffect.cs** — `Scripts/FairyTales/UI/Core/`
- IPointerDown/Up — scale down (0.9) / scale back (OutBack)
- Добавляется на любую кнопку как компонент

**LibraryScreen.cs** — staggered scale-in анимация карточек:
- Каждая карточка появляется с задержкой (i * 0.05s)
- Ease.OutBack для "пружинящего" эффекта

#### 34. Localization (RU/KZ/EN)

**Loc.cs** — `Scripts/FairyTales/UI/Core/`
- Статический класс, Dictionary<key, Dictionary<lang, text>>
- `Loc.Lang` — читает/пишет `ft_lang` из PlayerPrefs
- `Loc.Get(key)` — возвращает строку на текущем языке, fallback на "ru"
- 20 ключей: UI кнопки, toast-сообщения, labels

#### 35. State Persistence

**ReadingState.cs** — `Scripts/FairyTales/UI/Core/`
- `SavePage/LoadPage` — последняя страница per tale (`ft_page_{taleId}`)
- `SaveVolume/LoadVolume` — громкость музыки (`ft_volume`)
- `SaveMuted/LoadMuted` — mute состояние (`ft_muted`)

**Интеграции:**
- ReadingScreen — resume from last page, save on page change и OnHidden
- BackgroundMusicManager — restore volume/mute на старте, save при изменении

#### 36. Safe Area

**SafeAreaFitter.cs** — `Scripts/FairyTales/UI/Core/`
- RectTransform anchors подгоняются под Screen.safeArea
- Update() отслеживает изменения (поворот экрана)

#### 37. Monetization Placeholder

**UnlockPopup.cs** — `Scripts/FairyTales/UI/Core/`
- Fade in/out popup
- Кнопки Close и Unlock
- Unlock → Toast "Скоро будет доступно!"

**LibraryScreen.cs** — btnUnlockAll → UnlockPopup.Show() или Toast fallback

#### Toast System

**Toast.cs** — `Scripts/FairyTales/UI/Core/`
- Singleton, static Show(message)
- DOTween: fade in → display → fade out
- Editor setup: автоматически создаётся в SceneSetup

#### Editor

**SceneSetup.cs** — обновлён:
- Создаёт SafeArea (RectTransform + SafeAreaFitter)
- Создаёт Toast (Image bg + TMP_Text label + CanvasGroup + Toast component)

#### New Files
```
Scripts/FairyTales/UI/Core/Toast.cs
Scripts/FairyTales/UI/Core/Loc.cs
Scripts/FairyTales/UI/Core/ReadingState.cs
Scripts/FairyTales/UI/Core/SafeAreaFitter.cs
Scripts/FairyTales/UI/Core/UnlockPopup.cs
Scripts/FairyTales/UI/Core/ButtonScaleEffect.cs
```

#### Modified Files
```
Scripts/FairyTales/UI/Reading/ReadingScreen.cs — NarrationMode, AI playback, state persistence
Scripts/FairyTales/UI/Library/TaleDetailScreen.cs — real OnListen(), Toast, Loc
Scripts/FairyTales/UI/Library/LibraryScreen.cs — card animations, UnlockAll
Scripts/FairyTales/Audio/BackgroundMusicManager.cs — persist volume/mute
Scripts/FairyTales/Editor/SceneSetup.cs — Toast + SafeArea creation
```

---

## Session 8 — 2026-03-10

### Bugfixes & Improvements

#### Bugfix: narration status check
- **TaleDetailScreen.cs** — `_hasAiNarration` проверял `status == "ready"`, но сервер возвращает `"done"`. Исправлено: `status == "done" || status == "ready"`.

#### Bugfix: FMOD audio format detection
- **NarrationPlayer.cs** — сервер отдаёт MP3, но файл сохранялся как `.wav` → Unity не мог определить формат через `AudioType.UNKNOWN`.
- Добавлен `DetectFormat(byte[])` — определяет формат по magic bytes (OGG: `OggS`, WAV: `RIFF`, MP3: `ID3`/`0xFF 0xE0+`).
- Файл теперь сохраняется с правильным расширением (`.mp3`, `.ogg`, `.wav`) и передаётся правильный `AudioType`.

#### Bugfix: AI narration reads raw placeholders
- **NarrationService.cs** — `NarrateAll()` теперь принимает `childName` и `gender`, отправляет JSON body `{ "name": "...", "gender": "..." }` через `PostJson` вместо пустого `Post`.
- **VoiceRecordingScreen.cs** — передаёт `ft_childName` и `ft_gender` из PlayerPrefs в `NarrateAll()`.
- **API.md** — обновлена документация endpoint `POST /api/tales/{id}/narrate-all`: описан JSON body с `name`/`gender`, логика персонализации на сервере, новая ошибка 400.

#### Feature: persistent voice across tales
Голос клонируется один раз и используется для всех сказок без повторной записи.

- **VoiceRecordingScreen.cs** — после успешного `CloneVoice` сохраняет `ft_voiceCloned = 1` в PlayerPrefs.
- **NarrationSetupScreen.cs** — полный рефактор:
  - При открытии проверяет `ft_voiceCloned`
  - Если голос есть → показывает `panelQuickNarrate`:
    - **"Озвучить этим голосом"** → сразу `NarrateAll` → `NarrationProgressScreen`
    - **"Перезаписать голос"** → переключает на tab "Новая запись"
  - Если голоса нет → обычный flow (запись → clone → narrate)
  - Добавлены поля: `panelQuickNarrate`, `btnNarrateNow`, `btnRerecord`, `NarrationService`
- **NarrationSetup.cs** (Editor) — создаёт UI для `panelQuickNarrate` (label + 2 кнопки)

**Flow:**
```
Первая сказка:  Озвучить → Новая запись → Clone → NarrateAll → Done
Вторая сказка:  Озвучить → "Озвучить этим голосом" → NarrateAll → Done
```

#### UX: clear stale UI on screen transition
- **TaleDetailScreen.cs** — `SetTale()` очищает title, pageCount, cover (`ClearUI()`) до начала fade-in, чтобы не мелькали данные предыдущей сказки.
- **NarrationSetupScreen.cs** — `SetTale()` очищает title и cover аналогично.

#### PlayerPrefs keys (new)
- `ft_voiceCloned` — флаг: голос уже клонирован (1)

#### Modified Files
```
Scripts/FairyTales/Audio/NarrationPlayer.cs — DetectFormat(), правильный AudioType
Scripts/FairyTales/Api/NarrationService.cs — NarrateAll(taleId, childName, gender)
Scripts/FairyTales/UI/Narration/VoiceRecordingScreen.cs — save ft_voiceCloned, pass name/gender
Scripts/FairyTales/UI/Narration/NarrationSetupScreen.cs — quick narrate panel, ClearUI
Scripts/FairyTales/UI/Library/TaleDetailScreen.cs — status fix, ClearUI
Scripts/FairyTales/Editor/NarrationSetup.cs — panelQuickNarrate UI
AI/API.md — narrate-all documentation updated
```

---

## Session 9 — 2026-03-10

### Phase 9 — Screen Transitions & Payment ✅

#### Coordinated Slide-Up Transitions

Все экраны теперь появляются снизу экрана (slide up) вместо простого fade. При переходе между экранами — эффект свайпа вверх: оба экрана двигаются как единое целое (предыдущий уезжает вверх, новый приезжает снизу).

**BaseScreen.cs** — полный рефактор анимаций:
- `slideFromBottom` (SerializeField, default true) — включает slide-анимацию
- `Show()` — slide up from `-Screen.height` + fade in (0.4s, OutCubic)
- `Hide()` — slide up to `+Screen.height` + fade out (0.4s, InCubic)
- `PrepareBelow()` / `FinalizeShown()` / `FinalizeHidden()` — для координированных переходов
- `Rect`, `SlideEnabled`, `AnimDuration` — публичные свойства для ScreenManager
- ReadingScreen: `slideFromBottom = false` (fullscreen illustration, обычный fade)

**ScreenManager.cs** — координированные переходы:
- `sharedBackground` (Image) — общий фон за всеми экранами
- `CoordinatedSlide(prev, next)` — один DOTween Sequence двигает оба экрана с одинаковым easing (InOutCubic) и длительностью → движутся как цельный лист
- `_transitioning` флаг — блокирует переходы во время анимации
- Fallback: для экранов с `slideFromBottom = false` — независимые анимации

#### Shared Background

Один общий фон (`SharedBackground` Image) за всеми экранами, вместо отдельного Background в каждом экране.

**SceneSetup.cs** — создаёт `SharedBackground` как первый child канваса:
- Цвет: `(0.12, 0.08, 0.18)` — тёмно-фиолетовый
- Привязан к `ScreenManager.sharedBackground`

**Все Editor Setup скрипты** — убраны `AddBackground(root)`:
- `OnboardingSetup.cs` — удалены 3 вызова + метод + неиспользуемый `BgColor`
- `LibrarySetup.cs` — удалены 2 вызова + метод + неиспользуемый `BgColor`
- `NarrationSetup.cs` — удалены 3 вызова + метод + неиспользуемый `BgColor`
- `ReadingSetup.cs` — `slideFromBottom = false` для ReadingScreen (BgColor сохранён для illustration)

#### PaymentScreen

Экран подписки с выбором тарифа, кнопка в LibraryScreen (`btnUnlockAll`).

**PaymentScreen.cs** — `Scripts/FairyTales/UI/Payment/`:
- Заголовок "Получите доступ ко всем сказкам!"
- Кнопка закрытия (✕) → LibraryScreen
- 2 карточки тарифов: 449 ₽/мес и 2 490 ₽/год
  - Годовой: бейдж "-55%", зачёркнутая старая цена "5388 ₽/год"
  - Selection outline при выборе (по умолчанию — годовой)
- CTA кнопка "Попробовать 3 дня бесплатно" (золотая)
- Нижние ссылки: Условия / Восстановить покупку / Конфиденциальность
- TODO: IAP integration

**PaymentSetup.cs** — `Scripts/FairyTales/Editor/`:
- Menu: `FairyTales/Setup Payment UI`
- Plan cards с бейджем скидки и зачёркнутой ценой
- CTA кнопка (золотой цвет), link buttons (фиолетовые)

**LibraryScreen.cs** — `OnUnlockAll()` → `ScreenManager.Show<PaymentScreen>()` (вместо UnlockPopup)

**SceneSetup.cs** — зарегистрирован `PaymentScreen`

**Loc.cs** — новые ключи:
- `plan_monthly` — "Ежемесячная подписка" / "Ай сайынғы жазылым" / "Monthly plan"
- `plan_yearly` — "Годовая подписка" / "Жылдық жазылым" / "Yearly plan"
- `coming_soon` — "Скоро!" / "Жақында!" / "Coming soon!"
- `restore_coming_soon` — "Восстановление покупок скоро!" / ...

#### New Files
```
Scripts/FairyTales/UI/Payment/PaymentScreen.cs
Scripts/FairyTales/Editor/PaymentSetup.cs
```

#### Modified Files
```
Scripts/FairyTales/UI/Core/BaseScreen.cs — slide-up animations, coordinated transition API
Scripts/FairyTales/UI/Core/ScreenManager.cs — CoordinatedSlide, sharedBackground, _transitioning
Scripts/FairyTales/UI/Core/Loc.cs — payment localization keys
Scripts/FairyTales/UI/Library/LibraryScreen.cs — OnUnlockAll → PaymentScreen
Scripts/FairyTales/Editor/SceneSetup.cs — SharedBackground, PaymentScreen registration
Scripts/FairyTales/Editor/OnboardingSetup.cs — removed per-screen backgrounds
Scripts/FairyTales/Editor/LibrarySetup.cs — removed per-screen backgrounds
Scripts/FairyTales/Editor/NarrationSetup.cs — removed per-screen backgrounds
Scripts/FairyTales/Editor/ReadingSetup.cs — slideFromBottom=false for ReadingScreen
AI/architecture.md — Payment folder, updated flow, Phase 9
AI/changelog.md — this entry
```
