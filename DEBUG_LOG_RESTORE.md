# Debug.Log — Restore Guide

All `Debug.Log/LogWarning/LogError` in runtime scripts were commented out for release.
Marker: `// RELEASE:` (line comments) and `/* RELEASE: ... */` (inline in lambdas).

## How to restore

### Quick: uncomment all at once (bash/PowerShell)
```bash
# In project root:
grep -rln "RELEASE:" Assets/Scripts/FairyTales/ --include="*.cs" | grep -v "/Editor/" | xargs sed -i 's|// RELEASE: ||g; s|/\* RELEASE: \(.*\) \*/|\1|g'
```

### Manual: search `RELEASE:` in IDE, uncomment individually.

## Lambda pattern
Some callbacks were changed from:
```csharp
onError: e => Debug.LogWarning($"...")
```
to:
```csharp
onError: e => { } /* RELEASE: Debug.LogWarning($"...") */
```
To restore, replace with:
```csharp
onError: e => Debug.LogWarning($"...")
```

## Affected files (55 lines total)

| File | Lines | Type |
|------|-------|------|
| Api/NarrationService.cs | 41 | Log |
| Audio/BackgroundMusicManager.cs | 43 | else LogWarning |
| Audio/DefaultNarrationProvider.cs | 33 | if+LogWarning |
| Audio/MicRecorder.cs | 29 | LogError |
| Audio/NarrationPlayer.cs | 66 | LogError |
| Cache/AssetCache.cs | 25, 36, 83 | Log, Log, LogWarning |
| Cache/BundledTaleLoader.cs | 64 | LogWarning |
| Cache/TaleDownloadService.cs | 99, 131, 149 | lambda, LogWarning, LogWarning |
| IAP/IAPManager.cs | 49, 55, 60, 65, 82, 90, 102, 171, 192 | mixed |
| IAP/SubscriptionService.cs | 45, 50, 69 | Log, LogWarning×2 |
| UI/Core/ChildGatePopup.cs | 48 | LogWarning |
| UI/Core/ScreenManager.cs | 72 | LogError |
| UI/Core/Toast.cs | 28 | Log |
| UI/Library/LibraryScreen.cs | 88, 109 | LogWarning, lambda |
| UI/Library/TaleDetailScreen.cs | 118, 212, 236, 244, 249 | lambda, LogError, Log×2, LogError |
| UI/Narration/NarrationProgressScreen.cs | 101, 146 | LogError×2 |
| UI/Narration/NarrationSetupScreen.cs | 103, 125, 136 | lambda, LogWarning, lambda |
| UI/Narration/VoiceRecordingScreen.cs | 138, 166 | LogError×2 |
| UI/Onboarding/DownloadScreen.cs | 114 | if+LogError |
| UI/Onboarding/LoadingScreen.cs | 55, 62, 79 | lambda×3 |
| UI/Onboarding/PersonalizationScreen.cs | 82, 102, 108 | LogWarning, lambda×2 |
| UI/Reading/ReadingScreen.cs | 134, 161, 165, 170 | Log×3, LogWarning |
| UI/Reading/RecordingOverlay.cs | 170, 189, 214 | LogError, lambda, LogError |

**Editor/ scripts NOT touched** — they don't ship in builds.
