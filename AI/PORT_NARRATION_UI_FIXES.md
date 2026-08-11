# Перенос исправлений: озвучка, навигация по страницам, наложение экранов

Гайд для переноса в проект-близнец (общая кодовая база Bala Stories / Islam Stories).
Все правки — в `Assets/Scripts/FairyTales/`. Изменений в сцене и префабах **не требуется**.

Порядок важен: **Фикс 1 обязателен**, он один чинит три видимых симптома. Остальные —
устойчивость и диагностика.

---

## Как понять, что баги есть в вашем проекте

| Симптом | Виновник |
|---|---|
| Текст и картинка листаются, а счётчик `1/44` стоит на месте | Фикс 1 |
| Кнопка «назад» всегда неактивна | Фикс 1 |
| Озвучка не играет вообще, в логах тишина | Фикс 1 |
| Экран прогресса накладывается на экран сказки, кнопки просвечивают | Фикс 3 |
| Озвучка молчит только в iOS-сборке, в Editor работает | Фикс 2 |
| После генерации озвучки ничего не происходит, пока не нажмёшь «Готово» | Фикс 4 |

Быстрая проверка Фикса 1: открыть сказку и полистать. Если картинка меняется,
а счётчик страниц — нет, баг есть.

---

## Фикс 1 — КРИТИЧНЫЙ. `ArgumentNullException` убивает `ReadingScreen.OnShown()`

### Причина

`ReadingScreen.OnShown()` падал на `recordingOverlay.Deactivate()`, и весь код после этой
строки не выполнялся никогда:

```csharp
recordingOverlay.Deactivate();                 // ← ArgumentNullException
pageNavigator.OnPageChanged += OnPageChanged;  // подписки нет
PlayNarration(_startPage);                     // озвучка не запускается
```

Стектрейс:

```
at AssetCache.HasAnyParentNarration (taleId, lang)   AssetCache.cs
at RecordingOverlay.HasAnyRecording ()
at RecordingOverlay.RefreshUi ()
at RecordingOverlay.StopPreview ()
at RecordingOverlay.Deactivate ()
at ReadingScreen.OnShown ()
at BaseScreen+<>c__DisplayClass21_0.<Show>b__0 ()
```

`_taleId` и `_lang` в `RecordingOverlay` заполняются **только** в `Activate()`. При обычном
чтении оверлей не активируют — поля остаются `null`, а `OnShown` всё равно зовёт
`Deactivate()` без проверки. Дальше `Path.Combine(Root, "parent-narration", null, null)`
бросает исключение.

В `OnHidden` проверка `IsActive` есть, в `OnShown` её забыли — отсюда асимметрия.

### Почему симптомов три

`UpdatePageCounter()` и `UpdateNavButtons()` вызываются в `OnPrepare()` один раз, а дальше
только из обработчика `OnPageChanged`, на который подписка не состоялась. Текст и картинка
листаются, потому что это делает сам `PageNavigator` — ему подписка не нужна.

### Правка A — `UI/Reading/RecordingOverlay.cs`

```csharp
public void Deactivate()
{
    // Tearing down an overlay that was never activated has nothing to undo, and its
    // tale/lang are still null — walking the teardown path would throw and abort
    // whatever called us (ReadingScreen.OnShown does, on every normal read).
    if (!IsActive) return;

    if (_recording) FinalizeSegment();
    // ...остальное без изменений
}
```

### Правка B — `Cache/AssetCache.cs`

```csharp
public static bool HasAnyParentNarration(string taleId, string lang)
{
    // No tale/language means nothing was ever recorded — Path.Combine would throw.
    if (string.IsNullOrEmpty(taleId) || string.IsNullOrEmpty(lang)) return false;

    var dir = Path.Combine(Root, "parent-narration", taleId, lang);
    return Directory.Exists(dir) && Directory.GetFiles(dir, "page_*.wav").Length > 0;
}

public static void ClearParentNarration(string taleId, string lang)
{
    if (string.IsNullOrEmpty(taleId) || string.IsNullOrEmpty(lang)) return;

    var dir = Path.Combine(Root, "parent-narration", taleId, lang);
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
}
```

Нужны обе правки: A убирает причину, B защищает остальных вызывающих.

---

## Фикс 2 — беззвучная озвучка на iOS (MP3 + streaming)

Проявляется **только в сборке под iOS**. В Editor на Windows MP3 декодируется нормально,
поэтому баг там не виден.

### Причина

Сервер отдаёт озвучку в MP3 (`Content-Type: audio/mpeg`, сигнатура `FF F3`, MPEG-2 Layer III,
24 kHz, 48 kbps, моно). `NarrationPlayer` включал потоковое декодирование для любого формата.
Unity не поддерживает streaming `AudioType.MPEG` на iOS: `GetContent` возвращает клип нулевой
длины, проверка `clip.length < 0.01f` молча выходит — тишина без единого следа.

### Правка — `Audio/NarrationPlayer.cs`, метод `LoadAndPlay`

```csharp
// Stream so playback starts before full decode finishes — but only for
// formats where Unity actually supports it. Streaming an MPEG clip on iOS
// yields a zero-length clip that never plays (server narration is MP3).
var stream = audioType != AudioType.MPEG;
((DownloadHandlerAudioClip)req.downloadHandler).streamAudio = stream;
```

Было: `streamAudio = true;` безусловно.

Записи родителя (OGG/WAV) продолжают стримиться как раньше — регрессии нет.

> Статус: на устройстве не подтверждён. Возможно, в iOS-сборке причиной была та же
> `ArgumentNullException` из Фикса 1. Правка безвредна и оставлена.

---

## Фикс 3 — наложение экранов

Экран прогресса рисовался поверх экрана сказки, кнопки под ним оставались кликабельными.

### Причина

У `NarrationProgressScreen` нет собственного фона (дети: `SafeArea`, `StatusText`,
`ProgressBar`, `PagesText`, `BtnDone`) — фон в проекте общий (`sharedBackground`), поэтому
любой нескрытый экран просвечивает насквозь.

Предыдущий экран скрывался **только внутри `OnComplete` твина** — во всех путях
(`CoordinatedSlide`, `Hide()`, `ShowOver`). Прерванный твин или второй переход поверх
незавершённого — и экран оставался с `alpha=1` и `Canvas.enabled=true` навсегда.

Дыра: в `Show<T>()` fallback-ветка не выставляла `_transitioning`:

```csharp
if (prev != null) prev.Hide();
next.Show();          // _transitioning НЕ выставлялся
```

Этой веткой ходят экраны с `slideFromBottom = 0` (у нас `ReadingScreen`). Плюс
`if (_transitioning) return` молча **терял** навигацию, хотя `_current` уже был переставлен.

### Правка A — `UI/Core/BaseScreen.cs`

Экран хранит свою `Sequence` и умеет её завершать.

```csharp
private Sequence _seq;

/// <summary>True while this screen is actually drawn.</summary>
public bool IsVisible => Canvas.enabled;

/// <summary>
/// Run any in-flight show/hide animation straight to its end, callbacks included.
/// Screens are only hidden inside a tween's OnComplete, so an interrupted tween
/// would otherwise leave this screen visible on top of the next one forever.
/// </summary>
public void CompleteAnimation()
{
    var seq = _seq;
    _seq = null;
    if (seq != null && seq.IsActive()) seq.Complete(true);
}

/// <summary>Drop an in-flight animation without running its callbacks — for the
/// immediate/finalize paths that set the end state themselves.</summary>
private void KillAnimation()
{
    var seq = _seq;
    _seq = null;
    if (seq != null && seq.IsActive()) seq.Kill();
}
```

В `Show()` и `Hide()`: первой строкой `CompleteAnimation();`, в `seq.OnComplete` первой
строкой `_seq = null;`, после `seq.OnComplete(...)` — `_seq = seq;`.

Первой строкой `KillAnimation();` добавить в: `PrepareBelow()`, `FinalizeShown()`,
`FinalizeHidden()`, `ShowImmediate()`, `HideImmediate()`.

### Правка B — `UI/Core/ScreenManager.cs`

Заменить поле `_transitionStart` на `_slideSeq`:

```csharp
private Sequence _slideSeq;
```

Добавить два метода:

```csharp
/// <summary>
/// Run any transition still in flight straight to its end, callbacks included, so
/// the screens are in a settled state before a new one starts. Dropping the request
/// instead (the old behaviour) lost the navigation and could strand the outgoing
/// screen visible on top of the incoming one, since screens are only hidden inside
/// a tween's OnComplete.
/// </summary>
private void SettleTransition()
{
    if (!_transitioning) return;

    var seq = _slideSeq;
    _slideSeq = null;
    if (seq != null && seq.IsActive()) seq.Complete(true);

    // Independent-animation path: no shared sequence, finish each screen's own.
    foreach (var screen in _screens.Values) screen.CompleteAnimation();

    _transitioning = false;
}

/// <summary>
/// Last-resort invariant: exactly one screen is drawn. Guards against a screen left
/// visible by a path that never completed.
/// </summary>
private void HideAllExcept(BaseScreen keep)
{
    foreach (var screen in _screens.Values)
        if (screen != keep && screen.IsVisible) screen.HideImmediate();
}
```

В `Show<T>()`, `Show(Type)`, `ShowOver<T>()`, `ShowImmediate<T>()` — **удалить** блок
force-reset залипшего флага (`Time.unscaledTime - _transitionStart > 3f` с
`Debug.LogWarning`) и `if (_transitioning) return;`, поставив вместо них
`SettleTransition();` первой строкой. Тот force-reset был костылём именно под эту гонку.

Обе ветки перехода свести в один метод:

```csharp
var prev = _current;
_current = next;
BeginTransition(prev, next);
```

```csharp
/// <summary>
/// Single entry point for a screen swap. Both paths mark the manager as
/// transitioning and end by asserting that only the incoming screen is drawn.
/// </summary>
private void BeginTransition(BaseScreen prev, BaseScreen next)
{
    if (prev != null && prev.SlideEnabled && next.SlideEnabled)
    {
        // Both slide-enabled → coordinated swipe-up (move as one unit)
        CoordinatedSlide(prev, next);
        return;
    }

    // Fallback: independent animations. This path used to leave _transitioning
    // unset, so a second Show() could start on top of it and strand both screens.
    _transitioning = true;
    prev?.Hide();
    next.Show(onComplete: () =>
    {
        _transitioning = false;
        HideAllExcept(next);
    });
}
```

В `CoordinatedSlide` — убрать `_transitionStart`, сохранить последовательность и закрепить
инвариант:

```csharp
seq.OnComplete(() =>
{
    _slideSeq = null;
    prev.FinalizeHidden();
    next.FinalizeShown();
    _transitioning = false;
    HideAllExcept(next);
});
_slideSeq = seq;
```

В `ShowOver<T>()` — в `onComplete` добавить `HideAllExcept(next);`.
В `ShowImmediate<T>()` — `HideAllExcept(next);` в конце.

### Правка C — `UI/Narration/NarrationProgressScreen.cs`

Завершённый переход теперь может доставить `OnShown` повторно:

```csharp
protected override void OnShown()
{
    // Never stack pollers: a settled transition can deliver OnShown again.
    if (_polling != null) StopCoroutine(_polling);
    _polling = StartCoroutine(PollAndDownload());
}
```

---

## Фикс 4 — озвучка не стартует сама после генерации

`PollAndDownload` по завершении лишь показывал кнопку «Готово». Пользователь ждал звука,
видел заполненный прогресс — и ничего.

### Правка — `UI/Narration/NarrationProgressScreen.cs`

В ветке `if (done)`, после `btnDone.gameObject.SetActive(true)`:

```csharp
// Go straight to reading. Waiting for a tap on "Done" left the user on
// a finished progress screen with nothing playing — which is exactly
// what "narration does not work" looked like. The button stays as the
// manual fallback for when the detail never loaded.
OpenReading();
yield break;
```

`OnDone` переписать через общий метод:

```csharp
private void OnDone()
{
    if (OpenReading()) return;
    _screens.Show<TaleDetailScreen>();
}

/// <summary>
/// Opens the reading screen with the freshly generated AI narration.
/// Returns false when there is nothing to open, so the caller can fall back.
/// </summary>
private bool OpenReading()
{
    var reading = _detail != null ? _screens.Get<ReadingScreen>() : null;
    if (reading == null)
    {
        RemoteLog.Event("narration_open_reading_failed",
            $"tale={_tale?.id}; detail={_detail != null}; " +
            $"pages={_detail?.pages?.Length ?? -1}");
        return false;
    }

    reading.SetTale(_detail, NarrationMode.AI);
    _screens.Show<ReadingScreen>();
    return true;
}
```

---

## Фикс 5 — телеметрия озвучки

Именно она нашла Фикс 1. Все пути отказа возвращались молча, поэтому баг был невидим.
Канал тот же, что у IAP: `RemoteLog.Event(...)` → `POST /api/debug/log`. Настраивать ничего
не нужно, `RemoteLog` самозагружается. В Editor всё дублируется в консоль как
`[REMOTE-LOG] ...`, то есть попадает и в `Editor.log`.

Важно: `RemoteLog` зеркалит только `Error`/`Exception`/`Assert`. `Debug.LogWarning`
**не уезжает с устройства** — поэтому предупреждения заменены на явные события.

Добавить `using FairyTales.Diagnostics;` там, где его нет (`NarrationPlayer.cs`,
`NarrationProgressScreen.cs`).

### `Audio/NarrationPlayer.cs`

| Событие | Когда |
|---|---|
| `narration_playing` | звук реально пошёл: `fmt`, `bytes`, `len`, `stream` |
| `narration_empty_clip` | декод «успешен», клип пустой — режим отказа iOS/MP3 |
| `narration_decode_fail` | Unity не разобрал аудио |
| `narration_not_playing` | клип есть, `AudioSource` молчит — шлёт `vol`, `AudioListener.volume`, `AudioListener.pause` |
| `narration_no_data` | пришло 0 байт |

`narration_not_playing` отличает проблему аудио-сессии / mute-переключателя от проблемы
декодирования.

### `UI/Reading/ReadingScreen.cs`

`narration_session` (один раз на открытие книги: `mode`, `lang`, есть ли player и service),
`narration_ai_cache_hit`, `narration_ai_download`, `narration_ai_download_fail`,
`narration_ai_no_service`, `narration_tts_request`, `narration_tts_fail`,
`narration_parent_missing`.

Плюс ранний выход в `OnShown` — это была слепая зона, экран открывался и молча выходил:

```csharp
protected override void OnShown()
{
    if (_tale == null || _tale.pages == null)
    {
        // Silent dead end: the screen opens but nothing renders and nothing plays.
        RemoteLog.Event("reading_open_empty",
            $"tale={_tale?.id}; pages={_tale?.pages?.Length ?? -1}; mode={_mode}");
        return;
    }
    // ...
}
```

Ошибку TTS перестать глотать:

```csharp
onError: e => RemoteLog.Event("narration_tts_fail",
    $"tale={_tale.id}; page={page}; lang={lang}; err={e}"),
```

Было: `onError: e => { }`.

### `UI/Narration/NarrationProgressScreen.cs`

`narration_poll_fail`, `narration_generation_error`, `narration_page_download_fail`,
`narration_generation_done` с `downloaded` vs `total`.

Короткий `downloaded` — отдельный скрытый дефект: мета не сохраняется, кэш считается
невалидным, следующее «Слушать» гоняет генерацию заново.

### `UI/Library/TaleDetailScreen.cs`

`narration_start` (`voice`, `narratorGender`, `lang`, `bundled`, `pages`, предыдущий
`status`) и `narration_start_fail`.

### Как читать

Здоровый сеанс:

```
narration_start           | tale=...; voice=narrator; lang=ru
narration_generation_done | downloaded=44; total=44
narration_session         | tale=...; mode=AI; player=True; service=True
narration_ai_cache_hit    | page=0; bytes=26208
narration_playing         | fmt=mp3; len=6.5; stream=False
```

Отсутствие `narration_session` при наличии `narration_generation_done` = Фикс 1.

---

## Что НЕ является багом клиента (проверено на живом сервере)

Бэкенд по озвучке исправен, проверено end-to-end:

| Эндпоинт | Результат |
|---|---|
| `POST /api/tales/{id}/narrate` | 200, валидный MP3 |
| `POST /api/tales/{id}/narrate-all` | 200, `{jobId, status:"processing"}` |
| `GET /api/tales/{id}/narration-status` | `processing 20/94` → `done 94/94` (~60 сек) |
| `GET /api/tales/{id}/narration/{page}` | 200, MP3 |
| `POST /api/debug/log` | 200 |

404 на `narration-status` до первого `narrate-all` — норма, джобы ещё нет.

Отдельно, к сведению — это архитектурные факты, а не регрессии:

1. **`GET /api/tales/{id}/default-narration` отдаёт 404**, и в `/api/tales` нет поля
   `hasDefaultNarration`. Из-за этого блок предзагрузки в `TaleDownloadService` не
   выполняется никогда — офлайн-озвучки не будет.
2. **`Assets/Audio/Default` пустая** и лежит вне `Resources/`, поэтому
   `Resources.Load<AudioClip>("Audio/Default/...")` всегда `null`. Локального фолбэка нет.
3. **Кнопка «Читать» открывает с `NarrationMode.None`** (`TaleDetailScreen.OnRead`) —
   тишина там задумана. Звук только через «Слушать».
4. **Клиент нигде не читает `contentVersion`** — обновлённые тексты сами не доедут ни для
   одной сказки, включая bundled.
5. Сервер отдаёт **только MP3**, ни query-параметр, ни `Accept` формат не меняют. Если
   iOS-декодер окажется виноват — вопрос решается на бэкенде (WAV/OGG), не на клиенте.

---

## Порядок применения и проверка

1. Фикс 1 (A + B) — обязательно.
2. Фикс 5 — телеметрия, ставить рано: она покажет, что осталось.
3. Фиксы 3 и 4.
4. Фикс 2 — только если собираете под iOS.

Проверка в Editor:

- открыть сказку, полистать: счётчик идёт `1/44 → 2/44`, «назад» становится активной;
- «Слушать» → выбрать голос → дождаться генерации: само уезжает в чтение и звучит;
- в консоли по фильтру `[REMOTE-LOG] narration` — `narration_session`, затем
  `narration_ai_cache_hit` и `narration_playing` на каждой странице;
- переходы: библиотека → сказка → чтение, оплата и возврат, «назад» с экрана прогресса —
  ни на одном шаге два экрана не видны одновременно.

Фикс 3 затрагивает навигацию целиком, поэтому переходы стоит прощёлкать отдельно.
Фикс 2 проверяется только на реальном устройстве.
