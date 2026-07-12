# IAP — Инструкция для iOS-публикатора

Эта инструкция для человека, который публикует приложение в App Store через Mac/Xcode.

---

## 1. App Store Connect — Создать подписки

1. Открыть [App Store Connect](https://appstoreconnect.apple.com)
2. Выбрать приложение → **Subscriptions** (в боковом меню)
3. Создать **Subscription Group** — название: `Fairy Tales Premium`
4. Внутри группы создать **2 подписки**:

| Product ID | Длительность | Описание |
|---|---|---|
| `fairytales_monthly` | 1 Month | Ежемесячная подписка |
| `fairytales_yearly` | 1 Year | Годовая подписка |

> **ВАЖНО:** Product ID должны быть ТОЧНО такими — `fairytales_monthly` и `fairytales_yearly`. Они захардкожены в приложении.

5. Для каждой подписки заполнить:
   - **Reference Name** — внутреннее имя (видно только в консоли, любое)
   - **Subscription Price** — задать цену. Apple сам конвертирует в другие валюты, но можно скорректировать вручную
   - **Localizations** — добавить RU, KZ, EN:
     - Display Name (то, что увидит юзер в системном диалоге покупки)
     - Description (краткое описание подписки)

6. **Free Trial** (если нужен пробный период):
   - В подписке → **Introductory Offers** → Create
   - Тип: **Free Trial**
   - Длительность: **3 дня** (или сколько нужно)
   - Сделать для обеих подписок

---

## 2. Shared Secret

Это ключ, который нужен серверу для валидации покупок.

1. App Store Connect → приложение → **General** → **App Information**
2. Раздел **App-Specific Shared Secret** → нажать **Manage**
3. Сгенерировать или скопировать существующий
4. **Передать этот ключ серверному разработчику** — он добавит его в переменную окружения `APPLE_SHARED_SECRET`

---

## 3. Sandbox-тестирование

Позволяет тестировать покупки бесплатно.

1. App Store Connect → **Users and Access** → **Sandbox** → **Testers**
2. Нажать **+** → создать тестовый аккаунт:
   - Email (любой, можно несуществующий)
   - Пароль
   - Страна (влияет на валюту при тестировании)
3. На тестовом iPhone:
   - **Settings → App Store** → прокрутить вниз до **Sandbox Account**
   - Ввести созданный email и пароль
4. Теперь при запуске dev/TestFlight-билда покупки будут sandbox (деньги не списываются)

> Sandbox-подписки обновляются ускоренно: 1 месяц = 5 минут, 1 год = 1 час.

---

## 4. Xcode — Build Settings

1. Открыть проект в Xcode
2. Выбрать **Target** приложения
3. Вкладка **Signing & Capabilities**
4. Нажать **+ Capability** → добавить **In-App Purchase**
5. Проверить что **Bundle Identifier** совпадает с тем, что в App Store Connect

---

## 5. Перед отправкой на Review

Apple ОБЯЗАТЕЛЬНО проверяет:

### 5.1 Ссылки
В приложении есть кнопки "Terms" и "Privacy" на экране подписки. Сейчас ведут на `example.com` — нужно заменить на реальные URL. Передайте мне ссылки, я обновлю в коде:
- **Terms of Use** (Условия использования)
- **Privacy Policy** (Политика конфиденциальности)

### 5.2 Restore Purchases
Кнопка "Восстановить покупки" уже реализована в приложении. Apple ревьюер ВСЕГДА проверяет её наличие и работоспособность — без неё реджектнут.

### 5.3 Subscription Info
В описании приложения (App Store Connect → App Information) нужно указать:
- Какие подписки предлагаются (месячная, годовая)
- Цены
- Что подписка автоматически продлевается
- Как отменить подписку

---

## 6. Чеклист

- [ ] Subscription Group `Fairy Tales Premium` создана
- [ ] Product ID `fairytales_monthly` создан с ценой
- [ ] Product ID `fairytales_yearly` создан с ценой
- [ ] Localizations заполнены (RU, KZ, EN)
- [ ] Free Trial настроен (3 дня) — если нужен
- [ ] Shared Secret сгенерирован и передан серверному разработчику
- [ ] Sandbox-тестер создан
- [ ] In-App Purchase capability добавлена в Xcode
- [ ] Bundle ID совпадает
- [ ] Terms of Use URL — реальная ссылка
- [ ] Privacy Policy URL — реальная ссылка
- [ ] Тестовая покупка прошла в sandbox
- [ ] Восстановление покупок работает в sandbox
