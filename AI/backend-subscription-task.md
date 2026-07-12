# Задача для бэкенда: Подписки (Phase 10A)

## Что нужно сделать

Добавить систему подписок: 3 новых эндпоинта + 1 таблица в БД.
Пользователь покупает подписку в мобильном приложении (Google Play / App Store), приложение отправляет чек (receipt) на сервер, сервер валидирует и сохраняет.

---

## 1. Таблица `subscriptions`

| Колонка     | Тип      | Описание                                |
|-------------|----------|-----------------------------------------|
| `userId`    | string   | FK на users, уникальный (один юзер = одна подписка) |
| `store`     | string   | `"google"` или `"apple"`                |
| `productId` | string   | ID продукта в сторе (см. ниже)          |
| `plan`      | string   | `"monthly"` или `"yearly"`              |
| `receiptId` | string   | Уникальный ID транзакции из стора       |
| `expiresAt` | datetime | Когда подписка истекает                 |
| `createdAt` | datetime | Время первой покупки                    |
| `updatedAt` | datetime | Время последней валидации               |

**Product IDs в сторах:**
```
com.fairytales.premium.monthly   → plan: "monthly"
com.fairytales.premium.yearly    → plan: "yearly"
```

---

## 2. Эндпоинты

### 2.1. `POST /api/subscription/verify` — Валидация покупки

Приложение отправляет чек после покупки. Сервер валидирует, сохраняет, возвращает статус.

**Запрос:**
```
POST /api/subscription/verify
Authorization: Bearer <token>
Content-Type: application/json

{
  "receipt": "<строка чека из стора>",
  "store": "google",
  "productId": "com.fairytales.premium.yearly"
}
```

**Параметры тела:**

| Поле        | Тип    | Обязательный | Описание                              |
|-------------|--------|--------------|---------------------------------------|
| `receipt`   | string | да           | Чек/токен покупки из стора            |
| `store`     | string | да           | `"google"` или `"apple"`             |
| `productId` | string | да           | ID продукта (см. список выше)         |

**Ответ (200) — подписка активна:**
```json
{
  "premium": true,
  "plan": "yearly",
  "expiresAt": "2027-03-12T00:00:00.000Z"
}
```

**Ошибки:**
```json
// 400 — не все поля переданы
{ "error": "receipt, store and productId are required" }

// 400 — невалидный store
{ "error": "store must be 'google' or 'apple'" }

// 400 — невалидный productId
{ "error": "Unknown productId" }

// 400 — чек невалидный (стор отклонил)
{ "error": "Invalid receipt" }

// 502 — стор недоступен
{ "error": "Receipt validation failed" }
```

**Логика:**
1. Проверить обязательные поля
2. Валидировать чек через Store API (см. раздел «Валидация чеков»)
3. Определить `plan` по `productId`
4. Определить `expiresAt` из ответа стора
5. Upsert в таблицу `subscriptions` (по `userId`)
6. Вернуть `{ premium: true, plan, expiresAt }`

---

### 2.2. `GET /api/subscription/status` — Текущий статус подписки

Приложение вызывает при каждом запуске, чтобы проверить активна ли подписка.

**Запрос:**
```
GET /api/subscription/status
Authorization: Bearer <token>
```

**Ответ (200) — подписка активна:**
```json
{
  "premium": true,
  "plan": "yearly",
  "expiresAt": "2027-03-12T00:00:00.000Z"
}
```

**Ответ (200) — подписки нет или истекла:**
```json
{
  "premium": false,
  "plan": null,
  "expiresAt": null
}
```

**Логика:**
1. Найти запись в `subscriptions` по `userId`
2. Если нет записи → `{ premium: false, plan: null, expiresAt: null }`
3. Если `expiresAt < now` → `{ premium: false, plan: null, expiresAt: null }`
4. Иначе → `{ premium: true, plan, expiresAt }`

---

### 2.3. `POST /api/subscription/restore` — Восстановление покупок

Пользователь нажимает «Восстановить покупки» в приложении. Логика идентична `verify` — принимает чек, валидирует, сохраняет.

**Запрос:**
```
POST /api/subscription/restore
Authorization: Bearer <token>
Content-Type: application/json

{
  "receipt": "<строка чека из стора>",
  "store": "apple",
  "productId": "com.fairytales.premium.monthly"
}
```

**Ответ и ошибки:** точно такие же, как у `/verify`.

> Это отдельный эндпоинт для семантики (аналитика, логи), но внутри можно вызывать ту же функцию валидации.

---

## 3. Валидация чеков — ПЕРВЫЙ ЭТАП (заглушка)

На первом этапе **НЕ нужна** настоящая валидация через Store API. Достаточно заглушки:

```javascript
// Заглушка — принимает любой чек как валидный
async function validateReceipt(receipt, store, productId) {
  // TODO: реальная валидация через Google Play / App Store API
  return {
    valid: true,
    expiresAt: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000), // +30 дней
    transactionId: `stub_${Date.now()}`
  };
}
```

Это позволит протестировать весь flow в приложении до подключения реальных сторов.

### Потом (когда будут аккаунты в сторах):

**Google Play:**
- Библиотека: `google-auth-library` + `googleapis`
- Вызов: `androidpublisher.purchases.subscriptions.get`
- Нужен service account JSON от Google Play Console

**Apple App Store:**
- Библиотека: `app-store-server-library` (от Apple)
- Или: `POST https://buy.itunes.apple.com/verifyReceipt` (legacy, но проще)
- Нужен shared secret от App Store Connect

---

## 4. Миграция базы данных

SQL (если используется SQL):
```sql
CREATE TABLE subscriptions (
  userId      VARCHAR(255) PRIMARY KEY,
  store       VARCHAR(10)  NOT NULL,
  productId   VARCHAR(255) NOT NULL,
  plan        VARCHAR(10)  NOT NULL,
  receiptId   VARCHAR(255) NOT NULL,
  expiresAt   DATETIME     NOT NULL,
  createdAt   DATETIME     DEFAULT CURRENT_TIMESTAMP,
  updatedAt   DATETIME     DEFAULT CURRENT_TIMESTAMP
);
```

Если MongoDB / JSON-файл — аналогичная структура, ключ `userId`.

---

## 5. Тестирование

### curl примеры

**Verify (покупка):**
```bash
curl -X POST http://localhost:3000/api/subscription/verify \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"receipt":"test_receipt_123","store":"google","productId":"com.fairytales.premium.yearly"}'
```

Ожидаемый ответ:
```json
{ "premium": true, "plan": "yearly", "expiresAt": "2027-04-11T..." }
```

**Status (проверка):**
```bash
curl http://localhost:3000/api/subscription/status \
  -H "Authorization: Bearer <token>"
```

Ожидаемый ответ:
```json
{ "premium": true, "plan": "yearly", "expiresAt": "2027-04-11T..." }
```

**Restore (восстановление):**
```bash
curl -X POST http://localhost:3000/api/subscription/restore \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"receipt":"test_receipt_123","store":"apple","productId":"com.fairytales.premium.monthly"}'
```

---

## 6. Чеклист

- [ ] Таблица `subscriptions` создана
- [ ] `POST /api/subscription/verify` — принимает чек, сохраняет, возвращает статус
- [ ] `GET /api/subscription/status` — возвращает текущий статус (или `premium: false`)
- [ ] `POST /api/subscription/restore` — работает как verify
- [ ] Заглушка валидации (любой чек = валидный, +30 дней)
- [ ] Все эндпоинты требуют `Authorization: Bearer <token>`
- [ ] Ошибки возвращаются в формате `{ "error": "..." }` (как в остальных эндпоинтах)

---

## 7. Обновлённая таблица эндпоинтов

После реализации — всего 21 эндпоинт:

| # | Endpoint | Method | Status |
|---|----------|--------|--------|
| 1–18 | (все существующие) | — | ✅ |
| 19 | `/api/subscription/verify` | POST | ⬜ |
| 20 | `/api/subscription/status` | GET | ⬜ |
| 21 | `/api/subscription/restore` | POST | ⬜ |
