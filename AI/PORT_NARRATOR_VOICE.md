# Перенос в Islam Stories: выбор голоса диктора доходит до сервера

Гайд для проекта-близнеца. Правки — в `Assets/Scripts/FairyTales/`, изменений в
сцене **не требуется** (кнопки голоса уже привязаны, проверьте по разделу 6).

**Файлы копировать нельзя.** Кодовые базы разошлись: в Islam Stories ещё старый
серверный флоу озвучки, мета кэша без языка, а `UI/Core/TaleText.cs` отсутствует.
Ниже — правки под ВАШУ версию файлов.

---

## Проблема

Выбор «мужской / женский» в панели голоса до сервера не доходил: по логам
`narratorGender` не пришёл ни разу — 49 запросов из 49. Сервер каждый раз брал
мужской по умолчанию, что бы ни выбрал пользователь.

Причин четыре, и первая же объясняет, почему единичные правки не помогали:

1. `NarrationService.NarratePage` кладёт тело **только если есть текст**. Без
   текста уходит пустой POST — ни голоса, ни имени.
2. `ReadingScreen.PlayDefaultNarration` не передаёт `narratorGender` вообще.
3. Выбор голоса нигде не сохраняется: он живёт внутри одного прохода генерации и
   теряется, как только пользователь просто открыл книгу.
4. `name` / `gender` в `/narrate` не шлются никогда — сервер молча берёт их из
   профиля, который обновляется только при запуске приложения.

Проверено на живом сервере (тот же текст, `voice=narrator`):

| Запрос | Ответ |
|---|---|
| `narratorGender: "male"` | 25 100 Б |
| `narratorGender: "female"` | 26 108 Б |
| поле не передано | 25 100 Б — ровно мужской |

Сервер поле уважает. Вопрос только в том, доходит ли оно.

---

## 1. Новый файл `Assets/Scripts/FairyTales/UserProfile.cs`

Единственный источник правды по трём значениям, от которых зависит аудио.

```csharp
using UnityEngine;

namespace FairyTales
{
    public static class UserProfile
    {
        public static string ChildName => PlayerPrefs.GetString("ft_childName", "");
        public static string ChildGender => PlayerPrefs.GetString("ft_gender", "male");

        /// <summary>"male" | "female" — голос диктора, выбранный в UI.</summary>
        public static string NarratorGender
        {
            get => PlayerPrefs.GetString("ft_narratorGender",
                PlayerPrefs.GetString("ft_narratorGenderDefault", "male"));
            set
            {
                if (string.IsNullOrEmpty(value)) return; // родительский голос — не диктор
                PlayerPrefs.SetString("ft_narratorGender", value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Дефолт с сервера — никогда не перебивает явный выбор.</summary>
        public static void SetNarratorDefault(string gender)
        {
            if (gender != "male" && gender != "female") return;
            if (PlayerPrefs.GetString("ft_narratorGenderDefault", "") == gender) return;
            PlayerPrefs.SetString("ft_narratorGenderDefault", gender);
            PlayerPrefs.Save();
        }

        /// <summary>Всё, от чего зависит звук, одним токеном для ключей кэша.</summary>
        public static string CacheTag => Hash($"{ChildName}|{ChildGender}|{NarratorGender}");

        private static string Hash(string s)
        {
            uint h = 2166136261;
            foreach (var c in s) h = (h ^ c) * 16777619;
            return h.ToString("x8");
        }
    }
}
```

`namespace FairyTales` — родительский для `FairyTales.Api` и `FairyTales.UI.*`,
поэтому `using` нигде добавлять не нужно.

## 2. `Api/NarrationService.cs` — тело запроса всегда

Заменить в `NarratePage` весь блок `if (!string.IsNullOrEmpty(text)) { … } else { … }` на:

```csharp
            var body = JsonConvert.SerializeObject(new
            {
                text,
                narratorGender = Narrator(narratorGender),
                name = Name(),
                gender = UserProfile.ChildGender
            }, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            // TTS-провайдер отваливается временно (502 от Edge/Fish, обрыв мобильной
            // сети). Одна страница, которой не повезло, молчала до очистки кэша.
            byte[] audio = null;
            string error = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                error = null;
                yield return _api.PostJsonForAudio(url, body,
                    data => audio = data, e => error = e);

                if (audio != null || IsClientError(error)) break;
                if (attempt == 0) yield return new WaitForSecondsRealtime(1.5f);
            }

            if (audio != null) onSuccess?.Invoke(audio);
            else onError?.Invoke(error);
        }

        private static string Narrator(string narratorGender)
            => string.IsNullOrEmpty(narratorGender)
                ? UserProfile.NarratorGender : narratorGender;

        private static string Name(string name = null)
            => string.IsNullOrEmpty(name) ? UserProfile.ChildName : name;

        // ApiClient форматирует ошибку как "[<код>] <error> — <body>".
        private static bool IsClientError(string error)
            => error != null && error.StartsWith("[4");
```

В `NarrateAll` подставить те же дефолты:

```csharp
                name = Name(childName),
                gender = string.IsNullOrEmpty(gender) ? UserProfile.ChildGender : gender,
                voice = voice,
                narratorGender = Narrator(narratorGender),
```

Смысл: **ни один вызывающий больше не может отправить запрос без голоса**, даже
если забудет параметр. Именно это делает фикс устойчивым к будущим правкам.

## 3. Сохранять выбор пользователя

`UI/Library/TaleDetailScreen.cs`, первой строкой в `OnVoiceSelected`:

```csharp
            UserProfile.NarratorGender = narratorGender;
```

`UI/Library/VoiceSelectScreen.cs`, первой строкой в `Select`:

```csharp
            UserProfile.NarratorGender = narratorGender;
```

## 4. `UI/Reading/ReadingScreen.cs` — голос в TTS-фолбэке

В `PlayDefaultNarration`, в вызов `NarratePage`, добавить последний аргумент:

```csharp
                voice: "narrator", lang: lang, text: pageText,
                narratorGender: UserProfile.NarratorGender);
```

Там же кэш TTS сейчас пишется в `AssetCache.NarrationKey` — тот же ключ, что и у
**скачанной** предзаписанной озвучки, без учёта голоса. Сменил диктора — играет
старое. Заведите отдельный ключ:

```csharp
        // AssetCache.cs
        public static string TtsNarrationKey(string taleId, string lang, int page,
            string profileTag)
            => $"tts-narration/{taleId}/{lang}/{profileTag}/page_{page}.mp3";
```

и в `PlayDefaultNarration` перед запросом читать `TtsNarrationKey(..., UserProfile.CacheTag)`,
а в `onSuccess` писать туда же. Тег включает имя ребёнка, поэтому смена имени
тоже вызывает переозвучку.

---

## 5. Чего этот тикет НЕ чинит у вас

В Islam Stories `PlayAiNarration` всё ещё тянет `GET /api/tales/{id}/narration/{page}`
(`DownloadNarratedPage`). У этого эндпоинта **нет ни языка, ни голоса** — он отдаёт
то, что сгенерировал последний серверный джоб. Пока это так, в AI-режиме женский
голос может не заиграть даже при идеально сформированном запросе, а после смены
языка вернётся аудио на предыдущем языке.

Лечится переходом на клиентскую генерацию (у нас это уже сделано): каждая
страница озвучивается через `NarratePage` с текстом, который реально на экране, и
кладётся в кэш по ключу с языком. Мета кэша при этом должна знать язык — у вас
`AiNarrationMetaKey(taleId)` без него, из-за чего казахская озвучка выглядит
валидной для русской. Это отдельная задача, делать её стоит сразу после этой.

## 6. Приёмка

1. Выбрать женский голос → озвучить страницу → голос женский.
2. Сменить имя ребёнка → озвучить → в аудио новое имя.
3. В логах сервера нет строк `Narrate: тело без name/gender → беру из профиля`.
4. В сцене у `TaleDetailScreen` заполнены `btnVoiceMale` и `btnVoiceFemale`
   (в инспекторе, не `None`) — иначе панель голоса просто не сработает.

## 7. Опционально: рычаг без релиза

У нас на сервер добавлены два поля в `GET /api/debug/config`, которые позволяют
чинить озвучку без выката в сторы: `narrationEpoch` (поднял число — все клиенты
выбросили сгенерированное аудио и озвучили заново) и `defaultNarratorGender`
(голос по умолчанию для тех, кто ни разу не выбирал). Клиентская часть — файл
`Diagnostics/NarrationConfig.cs` плюс одна строка в `RemoteLogMirror.ApplyConfig`.

Спецификация серверной части: `AI/SERVER_NARRATION_CONFIG.md`. У Islam Stories
свой бэкенд, так что поля нужно добавить и там — иначе клиентская часть просто
ничего не делает.

Важно: сброс кэша прилетает с каждым батчем логов (~4 с), поэтому он обязан
пропускаться во время генерации книги — иначе удалит страницы у работающего
джоба, и книга пометит себя завершённой с дырами тишины. У нас это статический
флаг `NarrationProgressScreen.IsGenerating`, который проверяет `NarrationConfig`.
