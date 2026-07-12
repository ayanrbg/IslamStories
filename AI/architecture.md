# Architecture & Development Plan

## Folder Structure
```
Assets/
├── Scripts/
│   └── FairyTales/
│       ├── Api/
│       │   ├── ApiClient.cs
│       │   ├── AuthService.cs
│       │   ├── VoiceService.cs
│       │   ├── TalesService.cs
│       │   └── NarrationService.cs
│       ├── Models/
│       │   ├── AuthModels.cs
│       │   ├── VoiceModels.cs
│       │   ├── TaleModels.cs
│       │   └── DraftModels.cs
│       ├── Audio/
│       │   ├── MicRecorder.cs
│       │   ├── NarrationPlayer.cs
│       │   ├── BackgroundMusicManager.cs
│       │   └── DefaultNarrationProvider.cs
│       ├── UI/
│       │   ├── Core/
│       │   │   ├── ScreenManager.cs
│       │   │   ├── BaseScreen.cs
│       │   │   ├── Loc.cs
│       │   │   ├── Toast.cs
│       │   │   ├── ReadingState.cs
│       │   │   ├── SafeAreaFitter.cs
│       │   │   ├── UnlockPopup.cs
│       │   │   └── ButtonScaleEffect.cs
│       │   ├── Onboarding/
│       │   │   ├── LanguageSelectScreen.cs
│       │   │   ├── PersonalizationScreen.cs
│       │   │   └── LoadingScreen.cs
│       │   ├── Library/
│       │   │   ├── LibraryScreen.cs
│       │   │   ├── TaleCard.cs
│       │   │   └── TaleDetailScreen.cs
│       │   ├── Payment/
│       │   │   └── PaymentScreen.cs
│       │   ├── Reading/
│       │   │   ├── ReadingScreen.cs
│       │   │   ├── PageNavigator.cs
│       │   │   └── TableOfContentsPopup.cs
│       │   └── Narration/
│       │       ├── NarrationSetupScreen.cs
│       │       ├── VoiceRecordingScreen.cs
│       │       └── NarrationProgressScreen.cs
│       └── FairyTaleManager.cs
├── Audio/
│   ├── Default/          # Pre-baked narration per tale/page
│   └── Music/            # Background music tracks
├── Prefabs/
│   ├── UI/
│   └── Screens/
├── Sprites/
│   ├── UI/
│   └── Tales/            # Illustrations per tale
└── Scenes/
    └── MainScene.unity
```

## Screens Flow
```
LanguageSelect → Personalization → Library ←→ Personalization (Settings)
                                    ↓    ↘
                               TaleDetail  PaymentScreen
                              /    |     \
                        Reading  Listen  NarrationSetup
                          ↓                    ↓
                   TableOfContents    VoiceRecording
                                            ↓
                                   NarrationProgress
```

## Development Phases

### Phase 0 — Infrastructure ✅
1. DOTween installed
2. Newtonsoft Json added to manifest
3. Folder structure created

### Phase 1 — Core (API + Data) ✅
5. ApiClient.cs — HTTP client (GET/POST/PUT/DELETE/Multipart/Audio)
6. Models: AuthModels, TaleModels, VoiceModels, NarrationModels, DraftModels
7. AuthService.cs — Login, Register, GetProfile, UpdateProfile, TryRestoreSession, Logout
8. TalesService.cs — GetTales, GetTale, Personalize
9. VoiceService.cs — CloneVoice, DeleteVoice, GetDrafts, CreateDraft, GetDraft, DeleteDraft
10. NarrationService.cs — NarratePage, NarrateAll, GetNarrationStatus, DownloadNarratedPage

#### Service → Endpoint Mapping
```
AuthService:
  Login()           → POST /api/auth/login
  Register()        → POST /api/auth/register
  GetProfile()      → GET  /api/user/profile
  UpdateProfile()   → PUT  /api/user/profile

TalesService:
  GetTales()        → GET  /api/tales?lang=
  GetTale()         → GET  /api/tales/:id
  Personalize()     → POST /api/tales/:id/personalize

VoiceService:
  CloneVoice()      → POST   /api/voice/clone
  DeleteVoice()     → DELETE /api/voice
  GetDrafts()       → GET    /api/voice/drafts
  CreateDraft()     → POST   /api/voice/drafts
  GetDraft()        → GET    /api/voice/drafts/:id
  DeleteDraft()     → DELETE /api/voice/drafts/:id

NarrationService:
  NarratePage()          → POST /api/tales/:id/narrate?page=N
  NarrateAll()           → POST /api/tales/:id/narrate-all
  GetNarrationStatus()   → GET  /api/tales/:id/narration-status
  DownloadNarratedPage() → GET  /api/tales/:id/narration/:page
```

### Phase 2 — Audio ✅
11. MicRecorder.cs — mic recording → WAV
12. NarrationPlayer.cs — play MP3 from bytes
13. BackgroundMusicManager.cs — global music toggle, per-tale tracks
14. DefaultNarrationProvider.cs — play pre-baked audio from Assets

### Phase 3 — UI Framework ✅
15. ScreenManager.cs — screen switching with coordinated slide transitions
16. BaseScreen.cs — base class (slide-up from bottom + fade)
17. Shared background (one Image behind all screens)
18. Shared UI prefabs (buttons, panels, header) — в Unity Editor

### Phase 4 — Onboarding Screens ✅
18. LanguageSelectScreen
19. PersonalizationScreen
20. LoadingScreen (progress bar + cat animation)

### Phase 5 — Library
21. LibraryScreen — grid of tale cards
22. TaleCard component
23. TaleDetailScreen — 2 states (narrated / not narrated)

### Phase 6 — Reading
24. ReadingScreen — fullscreen illustrations + text
25. PageNavigator — page flip with DOTween fade
26. TableOfContentsPopup — horizontal scroll thumbnails
27. Default narration integration

### Phase 7 — Narration ✅
28. NarrationSetupScreen — new recording / drafts
29. VoiceRecordingScreen — record 4 sentences + timer
30. NarrationProgressScreen — waiting for AI generation
31. Full integration: clone → narrate-all → poll → ready

### Phase 8 — Listen & Polish ✅
32. **"Слушать" button — real playback integration**
    - AI narration: download pages via NarrationService.DownloadNarratedPage → NarrationPlayer
    - Default narration: DefaultNarrationProvider → NarrationPlayer
    - No narration: toast / suggest "Озвучить"
    - ReadingScreen with auto-play per page (AI or Default)
33. DOTween animations everywhere
34. Localization (RU/KZ/EN)
35. State persistence (PlayerPrefs)
36. Mobile adaptation (Safe Area, resolutions)
37. Monetization placeholder

### Phase 9 — Screen Transitions & Payment ✅
38. Coordinated slide-up transitions (swipe-up effect between screens)
39. Shared background behind all menu screens
40. PaymentScreen — subscription UI (monthly/yearly plans, trial CTA)
