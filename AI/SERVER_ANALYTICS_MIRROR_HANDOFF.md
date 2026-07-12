# Handoff серверу: что клиент шлёт в зеркало аналитики

Клиентская аналитика Firebase GA4 реализована (см. `CLIENT_FIREBASE_ANALYTICS_INTEGRATION.md`).
**GA4 — источник правды.** Наш backend получает только **зеркало критичных событий**
(spec §5) как независимую копию для дебага/быстрого просмотра. Ниже — точный контракт того,
что реально отправляет клиент, чтобы вы подключили приём и просмотр.

## Endpoint

```
POST {baseUrl}/api/analytics/event
baseUrl = https://bala-stories.apiapp.kz:3000   (тот же ApiClient, что и все запросы)
Content-Type: application/json
Authorization: Bearer <JWT>   // прикрепляется, если пользователь уже залогинен/зарегистрирован
```

- **Fire-and-forget**, ответ клиент игнорирует → отвечайте всегда `200` (даже на мусор).
- **userId в теле НЕТ.** Берите userId из **JWT** (тот же токен, что в остальных ручках).
  Ранние события (`app_open` до регистрации) могут прийти **без** токена — тогда считайте
  анонимом или группируйте по `session`. Не падайте на отсутствии userId.
- Это НЕ `/api/debug/log` (там отдельный IAP-дебаг-логгер `RemoteLog`, не путать).

## Тело запроса (всегда батч)

```json
{
  "session": "a1b2c3d4e5f6",        // id одного запуска приложения (12 hex), группирует события
  "platform": "ios",                // ios | android | editor
  "appVersion": "1.4.0",            // Application.version
  "events": [
    { "name": "paywall_view",    "ts": 1720099200000, "params": { "source": "tale_locked" } },
    { "name": "purchase_start",  "ts": 1720099210000, "params": { "product_id": "fairytales_yearly", "source": "tale_locked" } },
    { "name": "purchase_success","ts": 1720099235000, "params": { "product_id": "fairytales_yearly", "price": 9.99, "currency": "USD", "is_trial": true } }
  ]
}
```

- `ts` — **Unix epoch в миллисекундах** (long).
- Батч ≤ **50** событий на запрос. Флаш у клиента: каждые ~15 c, при накоплении ≥25,
  на паузе приложения и на выходе.
- `params` — плоский объект, типы значений зависят от события (string / bool / number).

## Какие события приходят в зеркало (вайтлист)

Только эти 8 имён. Всё остальное (`tale_open`, `tale_page_view`, `narration_*`, onboarding,
`library_view`, `purchase` revenue-событие и т.д.) идёт **только в GA4**, в зеркало НЕ шлётся.

| event | params |
|---|---|
| `paywall_view` | `source` (string) |
| `paywall_dismiss` | `source` (string) |
| `purchase_start` | `product_id` (string), `source` (string) |
| `purchase_success` | `product_id` (string), `price` (number), `currency` (string), `is_trial` (bool) |
| `purchase_error` | `product_id` (string), `error_code` (string), `error_message` (string, опц.) |
| `purchase_restore` | `restored` (bool) |
| `promo_redeem` | `success` (bool) |
| `tale_complete` | `tale_id` (string, серверный slug), `total_pages` (number), `duration_ms` (number) |

Значения `source`: `tale_locked` | `library` | `narrate` | `parent_voice` | `first_read`.
`product_id`: `fairytales_monthly` | `fairytales_yearly`.

## Что нужно от сервера (для просмотра)

Приёмник, судя по тикету, уже задеплоен. Для дебаг-просмотра нужно (spec §5, `X-Admin-Key`):

- `GET /api/analytics/events?name=&userId=&session=&since=&limit=` — сырые события.
- `GET /api/analytics/summary?since=` — счётчики по имени события (быстрый sanity-check).

Хранение — 30 дней (это копия, долгую аналитику держит BigQuery через GA4).

## Правила (детское приложение)

PII не приходит и не должно логироваться: имя ребёнка, email, точная геолокация — никогда.
Пол — только косвенно (в зеркало вообще не попадает; в GA4 как `child_gender` boy/girl/unset).

## Как быстро проверить, что клиент шлёт

Собрать клиент, пройти пейволл/покупку/дочитать сказку — в течение ~15 c прилетит POST.
Параллельно клиент дублирует те же события в Unity-консоль строкой `[Analytics] <name> | …`,
так что расхождение клиент/сервер видно сразу.
