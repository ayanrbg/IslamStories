# Серверу: `content_version` не растёт при перезаливке текста через `db/seed.js`

05.10.2026, клиент.

## Проблема

Клиент с этой сборки следит за `contentVersion` из `GET /api/tales`: если версия
сказки сменилась, закэшированный текст откладывается и перекачивается, а
сгенерированная по старому тексту озвучка удаляется.

Но `content_version` растёт только при правке через админку
(`routes/adminTales.js`). `db/seed.js` при перезаливке делает
`DO UPDATE SET title = …, pages = …` и версию не трогает. Поэтому после
коммита 284cf51 (warm_bread, стр. 33/35/36) у warm_bread на проде по-прежнему
`contentVersion: 1`, и по версии клиент правку не увидит.

Для warm_bread клиент это уже обошёл: текст, закэшированный до этой сборки,
перекачивается в любом случае, а старая озвучка warm_bread стирается один раз.
Но следующая правка через seed снова пройдёт незамеченной.

## Что сделать

1. В `db/seed.js` повышать версию, только если текст действительно изменился:

```sql
ON CONFLICT (slug, lang) DO UPDATE
SET title = EXCLUDED.title,
    pages = EXCLUDED.pages,
    content_version = CASE WHEN tales.pages IS DISTINCT FROM EXCLUDED.pages
                           THEN tales.content_version + 1
                           ELSE tales.content_version END
```

2. Один раз на проде, чтобы версия совпадала с уже выложенным текстом:

```sql
UPDATE tales SET content_version = content_version + 1 WHERE slug = 'warm_bread';
```

Клиенту это ничем не грозит: лишнее повышение версии стоит одной перекачки JSON.

## Строки интерфейса (п. 6 тикета)

Таблица лежит в `AI/UI_STRINGS_FOR_REVIEW.csv`: ключ, ru, kz, en и черновые
переводы клиента на id, tr, ms, fr, ar, ur, bn (uz был раньше). Нужна вычитка
носителями: правки вносить прямо в CSV, клиент перенесёт их в `Loc/Loc.*.cs` и
String Table. Ключи `StringTable` общие для en-US и en-GB.
