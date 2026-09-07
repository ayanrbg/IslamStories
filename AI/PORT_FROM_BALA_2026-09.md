# Порт функционала Bala Stories → Islam Stories (сентябрь 2026)

Документ-передача. Составлен из сессии, запиненной на проект Bala, поэтому
Unity-редактор Islam Stories отсюда недоступен. **Выполнять из сессии,
запущенной в `E:\UnityProjects\Fairy Tales\IslamStories`.**

- Исходник: `E:\UnityProjects\Fairy Tales\Fairy-Tales-Game` (Bala), ветка `device-based-userid`
- Приёмник: этот репозиторий, ветка `port/from-bala-2026-09` (создана, baseline `b58fd5d`)
- Последняя точка синхронизации: `79f8827` (11 августа 2026)

## Главное, что нужно понять до начала

**Это не односторонний порт, а двусторонний merge.**

Репозитории разошлись в обе стороны. Islam Stories ушёл вперёд в платёжном
и телеметрическом слое — там лежит работа под ревью App Store, которой в Bala
нет вообще:

| Коммит Islam Stories | Что добавил |
|---|---|
| `b51dcbd` | Fix App Review rejections: Apple IAP product ids, промокоды скрыты на iOS |
| `36f4925` | StoreSpecificIds — маппинг product id по сторам |
| `9fe4744` | Harden IAP for App Review, релиз 1.2.3 (build 11) |
| `47f6404` | Fix silent narration on iOS + удалённая телеметрия озвучки |
| `79f8827` | Fix narration crash, screen overlap, зависший экран прогресса |

Bala ушёл вперёд в навигации, кэше, библиотеке и в подарочно-реферальной фиче.

Замер расхождения (по 23 расходящимся `.cs`, без учёта переносов строк):

| Файл | строк только в Bala | строк только в Islam |
|---|---:|---:|
| `UI/Payment/PaymentScreen.cs` | 120 | **303** |
| `IAP/IAPManager.cs` | 55 | **272** |
| `Diagnostics/RemoteLog.cs` | 7 | **115** |
| `Editor/PaymentSetup.cs` | 2 | **57** |
| `UI/Core/Toast.cs` | 18 | 34 |
| `UI/Reading/IllustrationProvider.cs` | 62 | 53 |
| `UI/Core/ReadingState.cs` | 0 | 17 |
| `UI/Reading/PageNavigator.cs` | 4 | 11 |
| `UI/Core/Loc.cs` | 16 | 9 |
| `AppConfig.cs` | 7 | 8 |
| `Cache/AssetCache.cs` | 20 | 20 |
| `UI/Onboarding/LoadingScreen.cs` | 8 | 7 |
| `UI/Core/ScreenManager.cs` | **84** | 7 |
| `UI/Library/LibraryScreen.cs` | **59** | 5 |
| `UI/Reading/ReadingScreen.cs` | 58 | 30 |
| `Cache/TaleDownloadService.cs` | 50 | 10 |
| `Cache/BundledTaleLoader.cs` | 40 | 19 |
| `UI/Library/TaleCard.cs` | 18 | 3 |
| `UI/Core/BaseScreen.cs` | 7 | 0 |
| `Models/TaleModels.cs` | 6 | 0 |
| `Diagnostics/Analytics.cs` | 3 | 0 |
| `Api/PromoService.cs` | 2 | 1 |
| `UI/Library/VoiceSelectScreen.cs` | 0 | 3 |

`UI/Library/TaleDetailScreen.cs` уже идентичен — расходятся только переносы строк.

### Почему копировать файл целиком опасно

`Loc.cs` — наглядный пример расхождения в обе стороны. Только в Islam Stories есть:

```
["trial_note"]        // Guideline 3.1.2(c): сумма списания должна оставаться
["auto_renew_ios"]    // самым заметным элементом; формулировки про триал и
["auto_renew_android"]// автопродление вынесены сюда именно под ревью Apple
["iap_unavailable"], ["retry"], ["preparing_library"], ["done_excl"]
```

Копирование `Loc.cs` из Bala удалит их и вернёт приложение к состоянию, за
которое его уже отклоняли.

## Порядок работы

### Шаг 0. Пин MCP

```
list_engine_instances → выбрать "unity:Islam Stories"
select_engine_instance(instance_id=...)
```

Пин выставляется от рабочей папки, поэтому сессия должна быть запущена в
`IslamStories`. Проверить, что выбран правильный инстанс, **до** любых правок
сцены — иначе изменения уедут в чужой проект.

### Шаг 1. Категория A — чистое копирование

`AppConfig.cs` фиксирует контракт: это единственный файл, которому разрешено
различаться между репозиториями. Для этих файлов Islam Stories не содержит
ничего своего, копирование безопасно:

- `Assets/Scripts/FairyTales/Diagnostics/Analytics.cs` — добавляет в whitelist события
  `gift_popup_view`, `gift_code_copied`, `gift_earned`, `promo_entered`
- `Assets/Scripts/FairyTales/Models/TaleModels.cs`
- `Assets/Scripts/FairyTales/UI/Core/BaseScreen.cs`

Новые файлы (в Islam Stories отсутствуют полностью, копировать вместе с `.meta`):

- `Assets/Scripts/FairyTales/Api/ReferralService.cs`
- `Assets/Scripts/FairyTales/UI/Library/GiftPopup.cs`
- `Assets/Scripts/FairyTales/Editor/GiftUISetup.cs`

### Шаг 2. Категория B — merge, ведущая сторона Bala

У Islam Stories здесь мало своего, но проверять каждый его блок нужно:
он может оказаться фиксом из `79f8827`, а не устаревшим кодом.

- `UI/Core/ScreenManager.cs` (+84) — фикс залипающего `_transitioning`, когда
  DOTween-последовательность прерывается; «оседание» переходов
- `UI/Library/LibraryScreen.cs` (+59) — подпись срока действия премиума на кнопке
- `Cache/TaleDownloadService.cs` (+50), `Cache/BundledTaleLoader.cs` (+40) —
  гендерные страницы, готовность `IsTaleDownloaded`
- `UI/Reading/ReadingScreen.cs` (58/30) — автооткрытие чтения; у Islam Stories
  тут может быть часть фикса overlap из `79f8827`
- `UI/Library/TaleCard.cs` (+18) — бейдж «Книга в подарок»
- `UI/Core/Loc.cs` — **только добавить** ключи из Bala (`purchase_not_activated`,
  `promo_self`, `promo_referral_ok`, `gift_*`, `copy`), ничего не удаляя
- `Api/PromoService.cs` — `case 403: "promo_self"` и тип `referral` в комментарии

### Шаг 3. Категория C — merge, ведущая сторона Islam Stories

Здесь база — версия Islam Stories, из Bala переносятся **точечные** правки.
Каждую правку прикладывать отдельно и сверять с тем, что она не ломает
compliance-логику.

- `IAP/IAPManager.cs` — из Bala: причина отказа покупки, тестируемость покупок
  в редакторе. Сохранить: StoreSpecificIds, App Review hardening
- `UI/Payment/PaymentScreen.cs` — из Bala: годовая скидка из живых цен стора,
  CTA «Try for free», скрытый триал. Сохранить: `trial_note`, автопродление,
  иерархию цен по Guideline 3.1.2(c)
- `Editor/PaymentSetup.cs` — база Islam Stories
- `Diagnostics/RemoteLog.cs` — база Islam Stories (телеметрия озвучки на iOS)
- `UI/Core/Toast.cs`, `UI/Core/ReadingState.cs`, `UI/Reading/PageNavigator.cs`,
  `UI/Reading/IllustrationProvider.cs`, `Cache/AssetCache.cs`,
  `UI/Onboarding/LoadingScreen.cs`, `UI/Library/VoiceSelectScreen.cs`

### Шаг 4. Не трогать

`Assets/Scripts/FairyTales/AppConfig.cs` — backend URL, Apple product id
(`com.mozz.islamStories*`), ссылки на оферту и политику. Это единственный файл,
который обязан отличаться.

Также не переносить: `ProjectSettings/`, bundle id, версию, контент сказок,
`google-services*`.

### Шаг 5. Сцена и префаб — через MCP

В Bala изменения сцены большие (`SampleScene.unity` +1748 строк,
`TaleCard.prefab` +261). Копировать YAML между проектами нельзя — GUID'ы
ассетов не совпадают.

Хорошая новость: `GiftUISetup.cs` — идемпотентный editor-скрипт, который строит
UI сам, забирая спрайты и шрифты из существующих попапов:

```
Меню Unity: FairyTales → Setup Gift UI
```

Он создаёт `GiftPopup` под `[UICanvas]`, подпись на кнопке промокода и бейдж
подарка внутри `TaleCard.prefab`. Повторный запуск ничего не ломает — только
дозаполняет недостающее и перевязывает ссылки. После запуска сохранить сцену.

Остальную часть диффа сцены разобрать через MCP-инструменты
(`gameobject-find`, `gameobject-component-modify`), сверяясь с Bala.

### Шаг 6. Проверка

- `console-get-logs` с `logTypeFilter=Error` — компиляция без ошибок
- Прогнать: библиотека → карточка с бейджем → попап подарка → копирование кода
- Проверить, что экран оплаты не потерял `trial_note` и дисклеймер автопродления

## Вопросы, которые были открыты — и чем закончились

1. **`app = "BALA_STORIES"` в `PromoService.cs` — ложная тревога, поле удалено.**
   Оба бэкенда (`routes/promo.js`, строка 73 и 258) разбирают из тела только
   `{ code }` и дальше сами подставляют свой идентификатор при вызове внешнего
   промо-сервиса: Bala — `BALA_STORIES`, Islam Stories — `ISLAMIC_TALES`.
   Клиентское поле не читал никто, промокоды всё это время работали правильно.
   Поле убрано из запроса, чтобы литерал чужого бренда больше не выглядел багом
   при сравнении репозиториев.

2. **Промокоды на iOS — снова показываются** (`ece2adc`). Были скрыты в
   `b51dcbd` после отклонения по Guideline 3.1.1. Решение владельца: у них уже
   есть выложенное приложение с полем ввода на iOS, и сборки не должны
   отличаться от Android. Подарочная механика без поля ввода нерабочая — другу
   на iOS было некуда вводить код, а попап при этом ссылался на кнопку, которой
   в его сборке нет.

3. **Триал — оставлен как в Islam Stories.** В Bala CTA всегда «Try for free»;
   здесь формулировки намеренно приглушены под Guideline 3.1.2(c), и `trial_note`
   с дисклеймером автопродления сохранены. Из Bala взяты только годовая скидка
   по живым ценам и разбор причины отказа покупки.

## Что уже сделано

- Незакоммиченная работа Islam Stories зафиксирована в `b58fd5d` на `main`
  (порт озвучки, `AppConfig`, `TaleText`, `UserProfile`, локализация, `link.xml`,
  UPM-пакет MCP)
- `.gitignore` дополнен: `.claude/`, `.mcp.json`, `Assets/Plugins/NuGet/`
  (18 МБ DLL, которые UPM-пакет MCP разрешает при импорте)
- Ветка `port/from-bala-2026-09` создана и выбрана
