# Optimization Plan — Fairy Tales Game

## Цель
Минимальный APK, плавные 60 fps, 2-3 стартовые сказки в компактном формате.

---

## Phase 1: SDF-шрифты — 129 МБ → ~12 МБ [MANUAL — Unity Editor]

4 SDF-ассета по ~33 МБ — сгенерированы с полным Unicode. Нужна перегенерация.

**Window → TextMeshPro → Font Asset Creator**, для каждого из 4 шрифтов:
- `Assets/Fonts/G-Medium.asset`
- `Assets/Fonts/G-M-OUTLINESDF.asset`
- `Assets/Fonts/G-ORANGE.asset`
- `Assets/Fonts/Montserrat-Bold SDF.asset`

Настройки:
- Source Font: соответствующий TTF
- Atlas Resolution: **2048×2048**
- Character Set: **Custom Characters**:
```
ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz
АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя
ӘәҒғҚқҢңӨөҰұҮүІіҺһ
0123456789.,;:!?-–—'"«»()[]{}@#$%&*+=/\<>~₸₽$
```
- Render Mode: SDFAA (для outline — SDF)
- Generate → Save поверх существующего .asset
- Для `G-M-OUTLINESDF` — восстановить outline-материал после перегенерации

---

## Phase 2: Фикс анимаций и фризов [DONE]

- **2a. ButtonScaleEffect.cs** — `transform.DOKill()` перед каждым `DOScale`
- **2b. PageNavigator.cs** — `_activeTween?.Kill()` перед новым Sequence, сохранение `_activeTween = seq`
- **2c. AssetCache.cs** — in-memory `SpriteCache` (Dictionary), проверка перед декодированием, `ClearSpriteCache()`
- **2d. BaseScreen.cs** — `Canvas.ForceUpdateCanvases()` → `LayoutRebuilder.ForceRebuildLayoutImmediate(Rect)`
- **2e. LibraryScreen.cs** — пулинг карточек (`_cardPool`), переиспользование вместо destroy+instantiate
- **2f. PageNavigator.cs** — `static readonly Color Transparent = new(1,1,1,0)` вместо `new Color()` каждый раз
- **TaleCard.cs** — `button.onClick.RemoveAllListeners()` в начале `Init()`

---

## Phase 3: Удаление неиспользуемых пакетов [DONE]

Удалены из `manifest.json`:

Пакеты: visualscripting, timeline, coplay, 2d.animation, aseprite, psdimporter, spriteshape, tilemap, tilemap.extras, 2d.tooling, multiplayer.center, inputsystem

Модули: cloth, terrain, terrainphysics, vehicles, vr, xr, wind, umbra, physics (3D), particlesystem, vectorgraphics, video, adaptiveperformance, tilemap, ai, director, screencapture

**После открытия Unity**: пересобрать, убедиться что нет ошибок.

---

## Phase 4: Удаление дубликатов и мусора [MANUAL — Unity Editor]

| Что | Путь | Экономия |
|-----|------|----------|
| Дубли обложек | `Assets/Covers/` (дублирует `Resources/Covers/`) | ~1.8 МБ |
| Backup сцены | `Assets/_Recovery/` | ~725 КБ |
| Scene template | `Assets/Settings/Lit2DSceneTemplate.scenetemplate` | ~3.8 МБ |

Удалять через Unity (ПКМ → Delete) чтобы .meta-файлы удалились тоже.

---

## Phase 5: Оптимизация текстур [MANUAL — Unity Inspector]

Для всех PNG в `Assets/UI/` (~50 файлов):
- **Max Size:** 512 для иконок/кнопок, 1024 для фонов
- **Compression:** Android → ASTC 6×6, iOS → ASTC 6×6
- **Crunch Compression:** включить (quality 50)
- **Generate Mip Maps:** OFF
- **Read/Write:** OFF

Для обложек `Assets/Resources/Covers/`:
- **Max Size:** 512
- **Compression:** ASTC 6×6

---

## Phase 6: Вшивание стартовых сказок (StreamingAssets) [CODE DONE, CONTENT MANUAL]

Код готов:
- `BundledTaleLoader.cs` — загрузка tale.json, cover, pages из StreamingAssets через UnityWebRequest
- `IllustrationProvider.cs` — добавлен `GetPageAsync()` с fallback на StreamingAssets
- `CoverProvider.cs` — добавлен `GetAsync()` с fallback на StreamingAssets
- `TaleDownloadService.cs` — skip download для bundled tales
- `AssetCache.cs` — `IsTaleDownloaded` распознаёт bundled tales

**Нужно вручную**: наполнить `Assets/StreamingAssets/BundledTales/` контентом:
```
kolobok/
  tale.json        (~2 КБ — текст + метаданные)
  cover.jpg        (~30-50 КБ, quality 80)
  page_0.jpg       (~100-150 КБ, quality 80)
  ...page_N.jpg
teremok/
  tale.json
  cover.jpg
  page_0.jpg
  ...page_N.jpg
```

Формат `tale.json`:
```json
{
  "id": "kolobok",
  "title": "Колобок",
  "lang": "ru",
  "totalPages": 10,
  "pages": ["Текст страницы 0...", "Текст страницы 1...", ...]
}
```

---

## Phase 7: Build Settings [DONE]

Изменено в `ProjectSettings.asset`:
- **Managed Stripping Level:** High (Android + iOS)
- **IL2CPP Code Generation:** Faster (smaller) runtime (Android + iOS)
- **Texture Compression:** Android → ASTC (было ETC2)
- **ARM64 only:** уже было выставлено

---

## Ожидаемый результат

| Категория | До | После | Экономия |
|-----------|-----|-------|----------|
| SDF шрифты | 129 МБ | ~12 МБ | **~117 МБ** |
| Пакеты/модули | ~15 МБ | 0 | ~15 МБ |
| Дубликаты/мусор | ~6 МБ | 0 | ~6 МБ |
| UI текстуры | ~9.6 МБ | ~3 МБ | ~6.6 МБ |
| Stripping High | — | — | ~5-10 МБ |
| **Итого** | | | **~150+ МБ** |
| Стартовые сказки | 0 | ~3 МБ | +3 МБ в билде |

---

## Верификация

1. Пересобрать проект после каждой фазы → проверить отсутствие ошибок
2. Build Report → сравнить размеры
3. Профилировать на устройстве → отсутствие GC spikes при переходах
4. Стартовые сказки загружаются из StreamingAssets без интернета
5. Все 3 языка (RU/KZ/EN) — символы отображаются после перегенерации шрифтов
