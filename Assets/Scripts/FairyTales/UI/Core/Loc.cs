using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace FairyTales.UI.Core
{
    public static class Loc
    {
        private static readonly Dictionary<string, string> ToLocaleCode = new()
        {
            ["ru"] = "ru-RU",
            ["kz"] = "kk-KZ",
            ["en"] = "en",
            ["uz"] = "uz"
        };

        private static readonly Dictionary<string, string> FromLocaleCode = new()
        {
            ["ru-RU"] = "ru",
            ["ru"] = "ru",
            ["kk-KZ"] = "kz",
            ["kk"] = "kz",
            ["en"] = "en",
            ["uz"] = "uz"
        };
        private static readonly Dictionary<string, Dictionary<string, string>> Strings = new()
        {
            ["no_narration"] = new() { ["ru"] = "Нет озвучки. Нажмите «Озвучить»", ["kz"] = "Дыбыстау жоқ. «Озвучить» басыңыз", ["en"] = "No narration. Tap \"Narrate\"", ["uz"] = "Ovoz yo'q. \"Ovozlash\" tugmasini bosing" },
            ["narration_error"] = new() { ["ru"] = "Ошибка запуска озвучки", ["kz"] = "Дыбыстау қатесі", ["en"] = "Narration start failed", ["uz"] = "Ovozlashni ishga tushirishda xato" },
            ["read"] = new() { ["ru"] = "Читать", ["kz"] = "Оқу", ["en"] = "Read", ["uz"] = "O'qish" },
            ["listen"] = new() { ["ru"] = "Слушать", ["kz"] = "Тыңдау", ["en"] = "Listen", ["uz"] = "Tinglash" },
            ["narrate"] = new() { ["ru"] = "Озвучить", ["kz"] = "Дыбыстау", ["en"] = "Narrate", ["uz"] = "Ovozlash" },
            ["settings"] = new() { ["ru"] = "Настройки", ["kz"] = "Баптаулар", ["en"] = "Settings", ["uz"] = "Sozlamalar" },
            ["continue"] = new() { ["ru"] = "Продолжить", ["kz"] = "Жалғастыру", ["en"] = "Continue", ["uz"] = "Davom etish" },
            ["boy"] = new() { ["ru"] = "Мальчик", ["kz"] = "Ұл бала", ["en"] = "Boy", ["uz"] = "Bola" },
            ["girl"] = new() { ["ru"] = "Девочка", ["kz"] = "Қыз бала", ["en"] = "Girl", ["uz"] = "Qiz" },
            ["child_name"] = new() { ["ru"] = "Имя ребёнка", ["kz"] = "Баланың аты", ["en"] = "Child's name", ["uz"] = "Bola ismi" },
            ["unlock_all"] = new() { ["ru"] = "Открыть все истории", ["kz"] = "Барлық оқиғаларды ашу", ["en"] = "Unlock All Stories", ["uz"] = "Barcha hikoyalarni ochish" },
            ["premium_active_until"] = new() { ["ru"] = "Активно до {0}", ["kz"] = "{0} дейін жарамды", ["en"] = "Active until {0}", ["uz"] = "{0} gacha faol" },
            ["premium_active"] = new() { ["ru"] = "Премиум активен", ["kz"] = "Премиум жарамды", ["en"] = "Premium active", ["uz"] = "Premium faol" },
            ["pages"] = new() { ["ru"] = "стр.", ["kz"] = "бет", ["en"] = "pages", ["uz"] = "bet" },
            ["new_recording"] = new() { ["ru"] = "Новая запись", ["kz"] = "Жаңа жазба", ["en"] = "New recording", ["uz"] = "Yangi yozuv" },
            ["drafts"] = new() { ["ru"] = "Черновики", ["kz"] = "Жобалар", ["en"] = "Drafts", ["uz"] = "Qoralamalar" },
            ["narrator_name"] = new() { ["ru"] = "Имя рассказчика", ["kz"] = "Әңгімешінің аты", ["en"] = "Narrator name", ["uz"] = "Hikoyachi ismi" },
            ["start"] = new() { ["ru"] = "Начать", ["kz"] = "Бастау", ["en"] = "Start", ["uz"] = "Boshlash" },
            ["send"] = new() { ["ru"] = "Отправить", ["kz"] = "Жіберу", ["en"] = "Send", ["uz"] = "Yuborish" },
            ["done"] = new() { ["ru"] = "Готово", ["kz"] = "Дайын", ["en"] = "Done", ["uz"] = "Tayyor" },
            ["loading"] = new() { ["ru"] = "Загрузка...", ["kz"] = "Жүктелуде...", ["en"] = "Loading...", ["uz"] = "Yuklanmoqda..." },
            ["back"] = new() { ["ru"] = "Назад", ["kz"] = "Артқа", ["en"] = "Back", ["uz"] = "Orqaga" },
            ["unlock_coming_soon"] = new() { ["ru"] = "Скоро будет доступно!", ["kz"] = "Жақында қолжетімді болады!", ["en"] = "Coming soon!", ["uz"] = "Tez kunda!" },
            ["plan_monthly"] = new() { ["ru"] = "Ежемесячная подписка", ["kz"] = "Ай сайынғы жазылым", ["en"] = "Monthly plan", ["uz"] = "Oylik obuna" },
            ["plan_yearly"] = new() { ["ru"] = "Годовая подписка", ["kz"] = "Жылдық жазылым", ["en"] = "Yearly plan", ["uz"] = "Yillik obuna" },
            ["coming_soon"] = new() { ["ru"] = "Скоро!", ["kz"] = "Жақында!", ["en"] = "Coming soon!", ["uz"] = "Tez kunda!" },
            ["restore_coming_soon"] = new() { ["ru"] = "Восстановление покупок скоро!", ["kz"] = "Сатып алуды қалпына келтіру жақында!", ["en"] = "Restore purchases coming soon!", ["uz"] = "Xaridlarni tiklash tez kunda!" },
            ["rec_hint"] = new() { ["ru"] = "Нажмите запись и читайте вслух", ["kz"] = "Жазуды басып, дауыстап оқыңыз", ["en"] = "Tap record and read aloud", ["uz"] = "Yozuvni bosing va ovoz chiqarib o'qing" },
            ["rec_recording"] = new() { ["ru"] = "Запись...", ["kz"] = "Жазылуда...", ["en"] = "Recording...", ["uz"] = "Yozilmoqda..." },
            ["rec_done"] = new() { ["ru"] = "Запись завершена", ["kz"] = "Жазба аяқталды", ["en"] = "Recording complete", ["uz"] = "Yozuv tugadi" },
            ["rec_error"] = new() { ["ru"] = "Ошибка записи", ["kz"] = "Жазу қатесі", ["en"] = "Recording error", ["uz"] = "Yozuv xatosi" },
            ["rec_play_hint"] = new() { ["ru"] = "Готово. Нажмите, чтобы прослушать", ["kz"] = "Дайын. Тыңдау үшін басыңыз", ["en"] = "Done. Tap to listen back", ["uz"] = "Tayyor. Tinglash uchun bosing" },
            ["rec_playing"] = new() { ["ru"] = "Воспроизведение...", ["kz"] = "Ойнатылуда...", ["en"] = "Playing...", ["uz"] = "Ijro etilmoqda..." },
            ["record_voice_first"] = new() { ["ru"] = "Сначала запишите свой голос («Озвучить»)", ["kz"] = "Алдымен дауысыңызды жазыңыз («Дыбыстау»)", ["en"] = "Record your voice first (\"Narrate\")", ["uz"] = "Avval ovozingizni yozing (\"Ovozlash\")" },
            ["voice_saved"] = new() { ["ru"] = "Озвучка сохранена", ["kz"] = "Дыбыстау сақталды", ["en"] = "Narration saved", ["uz"] = "Ovoz saqlandi" },
            ["rec_cloning"] = new() { ["ru"] = "Клонирование голоса...", ["kz"] = "Дауысты клондау...", ["en"] = "Cloning voice...", ["uz"] = "Ovoz klonlanmoqda..." },
            ["rec_narrating"] = new() { ["ru"] = "Запуск озвучки...", ["kz"] = "Дыбыстау басталуда...", ["en"] = "Starting narration...", ["uz"] = "Ovozlash boshlanmoqda..." },
            ["record"] = new() { ["ru"] = "Запись", ["kz"] = "Жазу", ["en"] = "Record", ["uz"] = "Yozuv" },
            ["stop"] = new() { ["ru"] = "Стоп", ["kz"] = "Тоқта", ["en"] = "Stop", ["uz"] = "To'xtatish" },
            ["rerecord"] = new() { ["ru"] = "Перезаписать", ["kz"] = "Қайта жазу", ["en"] = "Re-record", ["uz"] = "Qayta yozish" },
            ["error"] = new() { ["ru"] = "Ошибка", ["kz"] = "Қате", ["en"] = "Error", ["uz"] = "Xato" },
            ["drafts_limit"] = new() { ["ru"] = "Максимум 3 черновика", ["kz"] = "Ең көбі 3 жоба", ["en"] = "Maximum 3 drafts", ["uz"] = "Maksimum 3 ta qoralama" },
            ["download.tales"] = new() { ["ru"] = "Загрузка сказок", ["kz"] = "Ертегілер жүктелуде", ["en"] = "Downloading tales", ["uz"] = "Ertaklar yuklanmoqda" },
            ["download.narration"] = new() { ["ru"] = "Загружаем озвучку...", ["kz"] = "Дыбыстау жүктелуде...", ["en"] = "Downloading narration...", ["uz"] = "Ovoz yuklanmoqda..." },
            ["per_month"] = new() { ["ru"] = "мес", ["kz"] = "ай", ["en"] = "mo", ["uz"] = "oy" },
            ["per_year"] = new() { ["ru"] = "год", ["kz"] = "жыл", ["en"] = "yr", ["uz"] = "yil" },
            ["start_trial"] = new() { ["ru"] = "Попробовать бесплатно", ["kz"] = "Тегін байқап көру", ["en"] = "Try for free", ["uz"] = "Bepul sinab ko'rish" },
            ["subscribe"] = new() { ["ru"] = "Подписаться", ["kz"] = "Жазылу", ["en"] = "Subscribe", ["uz"] = "Obuna bo'lish" },
            // Guideline 3.1.2(c): the billed amount must stay the most prominent pricing
            // element, so trial wording is confined to this subordinate note.
            ["trial_note"] = new() { ["ru"] = "Пробный период бесплатно, затем {0}", ["kz"] = "Сынақ кезеңі тегін, кейін {0}", ["en"] = "Free trial, then {0}", ["uz"] = "Sinov davri bepul, keyin {0}" },
            ["auto_renew_ios"] = new() { ["ru"] = "Подписка продлевается автоматически. Отменить можно в настройках Apple ID в любой момент.", ["kz"] = "Жазылым автоматты түрде жаңарады. Кез келген уақытта Apple ID баптауларынан бас тартуға болады.", ["en"] = "Subscription renews automatically. Cancel anytime in your Apple ID settings.", ["uz"] = "Obuna avtomatik yangilanadi. Istalgan vaqtda Apple ID sozlamalarida bekor qilish mumkin." },
            ["auto_renew_android"] = new() { ["ru"] = "Подписка продлевается автоматически. Отменить можно в настройках Google Play в любой момент.", ["kz"] = "Жазылым автоматты түрде жаңарады. Кез келген уақытта Google Play баптауларынан бас тартуға болады.", ["en"] = "Subscription renews automatically. Cancel anytime in your Google Play settings.", ["uz"] = "Obuna avtomatik yangilanadi. Istalgan vaqtda Google Play sozlamalarida bekor qilish mumkin." },
            ["iap_not_ready"] = new() { ["ru"] = "Магазин загружается...", ["kz"] = "Дүкен жүктелуде...", ["en"] = "Store loading...", ["uz"] = "Do'kon yuklanmoqda..." },
            ["iap_unavailable"] = new() { ["ru"] = "Магазин временно недоступен", ["kz"] = "Дүкен уақытша қолжетімсіз", ["en"] = "Store temporarily unavailable", ["uz"] = "Do'kon vaqtincha mavjud emas" },
            ["retry"] = new() { ["ru"] = "Повторить", ["kz"] = "Қайталау", ["en"] = "Retry", ["uz"] = "Qayta urinish" },
            ["purchase_success"] = new() { ["ru"] = "Подписка оформлена!", ["kz"] = "Жазылым рәсімделді!", ["en"] = "Subscription activated!", ["uz"] = "Obuna faollashtirildi!" },
            ["purchase_failed"] = new() { ["ru"] = "Ошибка покупки. Попробуйте ещё раз", ["kz"] = "Сатып алу қатесі. Қайталап көріңіз", ["en"] = "Purchase failed. Please try again", ["uz"] = "Xarid xatosi. Qaytadan urinib ko'ring" },
            ["purchase_not_activated"] = new() { ["ru"] = "Магазин принял оплату, но подписка не подтвердилась. Нажмите «Восстановить покупки»", ["kz"] = "Дүкен төлемді қабылдады, бірақ жазылым расталмады. «Сатып алуларды қалпына келтіру» басыңыз", ["en"] = "The store took the payment but the subscription was not confirmed. Tap \"Restore purchases\"", ["uz"] = "Do'kon to'lovni qabul qildi, lekin obuna tasdiqlanmadi. \"Xaridlarni tiklash\" tugmasini bosing" },
            ["restore_success"] = new() { ["ru"] = "Покупки восстановлены!", ["kz"] = "Сатып алулар қалпына келтірілді!", ["en"] = "Purchases restored!", ["uz"] = "Xaridlar tiklandi!" },
            ["restore_none"] = new() { ["ru"] = "Нет покупок для восстановления", ["kz"] = "Қалпына келтіретін сатып алу жоқ", ["en"] = "No purchases to restore", ["uz"] = "Tiklanadigan xarid yo'q" },
            ["email_subject"] = new() { ["ru"] = "Обращение из приложения Сказки", ["kz"] = "Ертегілер қосымшасынан хабарлама", ["en"] = "Message from Fairy Tales app", ["uz"] = "Ertaklar ilovasidan xabar" },
            ["write_email"] = new() { ["ru"] = "Написать на почту", ["kz"] = "Хат жазу", ["en"] = "Write email", ["uz"] = "Xat yozish" },
            ["solve_problem"] = new() { ["ru"] = "Решите пример", ["kz"] = "Есепті шешіңіз", ["en"] = "Solve the problem", ["uz"] = "Misolni yeching" },
            ["narrator_male"] = new() { ["ru"] = "Диктор муж.", ["kz"] = "Ер диктор", ["en"] = "Male narrator", ["uz"] = "Erkak diktor" },
            ["narrator_female"] = new() { ["ru"] = "Диктор жен.", ["kz"] = "Әйел диктор", ["en"] = "Female narrator", ["uz"] = "Ayol diktor" },
            ["voice_parent"] = new() { ["ru"] = "Голос родителя", ["kz"] = "Ата-ана дауысы", ["en"] = "Parent voice", ["uz"] = "Ota-ona ovozi" },
            ["narrate_again"] = new() { ["ru"] = "Озвучить заново", ["kz"] = "Қайта дыбыстау", ["en"] = "Re-narrate", ["uz"] = "Qayta ovozlash" },
            ["choose_voice"] = new() { ["ru"] = "Выберите голос диктора", ["kz"] = "Диктор дауысын таңдаңыз", ["en"] = "Choose narrator voice", ["uz"] = "Diktor ovozini tanlang" },
            ["deleting_voice"] = new() { ["ru"] = "Удаление голоса...", ["kz"] = "Дауыс жойылуда...", ["en"] = "Deleting voice...", ["uz"] = "Ovoz o'chirilmoqda..." },
            ["preparing_library"] = new() { ["ru"] = "Подготавливаем библиотеку...", ["kz"] = "Кітапхана дайындалуда...", ["en"] = "Preparing library...", ["uz"] = "Kutubxona tayyorlanmoqda..." },
            ["done_excl"] = new() { ["ru"] = "Готово!", ["kz"] = "Дайын!", ["en"] = "Done!", ["uz"] = "Tayyor!" },
            ["rec_done_detail"] = new() { ["ru"] = "Запись завершена. Прослушайте или отправьте.", ["kz"] = "Жазба аяқталды. Тыңдаңыз немесе жіберіңіз.", ["en"] = "Recording complete. Listen or send.", ["uz"] = "Yozuv tugadi. Tinglang yoki yuboring." },
            ["rec_error_detail"] = new() { ["ru"] = "Ошибка записи. Попробуйте ещё раз.", ["kz"] = "Жазу қатесі. Қайталап көріңіз.", ["en"] = "Recording error. Please try again.", ["uz"] = "Yozuv xatosi. Qaytadan urinib ko'ring." },
            ["error_with_msg"] = new() { ["ru"] = "Ошибка: {0}", ["kz"] = "Қате: {0}", ["en"] = "Error: {0}", ["uz"] = "Xato: {0}" },
            ["digit_0"] = new() { ["ru"] = "ноль", ["kz"] = "нөл", ["en"] = "zero", ["uz"] = "nol" },
            ["digit_1"] = new() { ["ru"] = "один", ["kz"] = "бір", ["en"] = "one", ["uz"] = "bir" },
            ["digit_2"] = new() { ["ru"] = "два", ["kz"] = "екі", ["en"] = "two", ["uz"] = "ikki" },
            ["digit_3"] = new() { ["ru"] = "три", ["kz"] = "үш", ["en"] = "three", ["uz"] = "uch" },
            ["digit_4"] = new() { ["ru"] = "четыре", ["kz"] = "төрт", ["en"] = "four", ["uz"] = "to'rt" },
            ["digit_5"] = new() { ["ru"] = "пять", ["kz"] = "бес", ["en"] = "five", ["uz"] = "besh" },
            ["digit_6"] = new() { ["ru"] = "шесть", ["kz"] = "алты", ["en"] = "six", ["uz"] = "olti" },
            ["digit_7"] = new() { ["ru"] = "семь", ["kz"] = "жеті", ["en"] = "seven", ["uz"] = "yetti" },
            ["digit_8"] = new() { ["ru"] = "восемь", ["kz"] = "сегіз", ["en"] = "eight", ["uz"] = "sakkiz" },
            ["digit_9"] = new() { ["ru"] = "девять", ["kz"] = "тоғыз", ["en"] = "nine", ["uz"] = "to'qqiz" },
            ["promo_code"] = new() { ["ru"] = "Промокод", ["kz"] = "Промокод", ["en"] = "Promo code", ["uz"] = "Promokod" },
            ["promo_placeholder"] = new() { ["ru"] = "Введите промокод", ["kz"] = "Промокодты енгізіңіз", ["en"] = "Enter promo code", ["uz"] = "Promokodni kiriting" },
            ["promo_apply"] = new() { ["ru"] = "Подтвердить", ["kz"] = "Растау", ["en"] = "Apply", ["uz"] = "Tasdiqlash" },
            ["promo_not_found"] = new() { ["ru"] = "Промокод не найден", ["kz"] = "Промокод табылмады", ["en"] = "Promo code not found", ["uz"] = "Promokod topilmadi" },
            ["promo_already_used"] = new() { ["ru"] = "Промокод уже использован", ["kz"] = "Промокод бұрын қолданылған", ["en"] = "Promo code already used", ["uz"] = "Promokod allaqachon ishlatilgan" },
            ["promo_no_connection"] = new() { ["ru"] = "Нет соединения", ["kz"] = "Байланыс жоқ", ["en"] = "No connection", ["uz"] = "Aloqa yo'q" },
            ["promo_exhausted"] = new() { ["ru"] = "Промокод больше не действует", ["kz"] = "Промокод енді жарамсыз", ["en"] = "This promo code is no longer valid", ["uz"] = "Promokod endi amal qilmaydi" },
            ["promo_tales_unlocked"] = new() { ["ru"] = "Промокод применён — открыто сказок: {0}", ["kz"] = "Промокод қолданылды — ашылған ертегілер: {0}", ["en"] = "Promo code applied — tales unlocked: {0}", ["uz"] = "Promokod qo'llanildi — ochilgan ertaklar: {0}" },
            ["promo_self"] = new() { ["ru"] = "Это ваш собственный промокод", ["kz"] = "Бұл сіздің жеке промокодыңыз", ["en"] = "That's your own promo code", ["uz"] = "Bu sizning shaxsiy promokodingiz" },
            // Has to say the BOOK WENT TO THE FRIEND. "Promo code accepted" on its own
            // sends the person hunting for a new book in their own library.
            ["promo_referral_ok"] = new() { ["ru"] = "Спасибо! Ваш друг получил книгу в подарок.", ["kz"] = "Рахмет! Досыңыз сыйлыққа кітап алды.", ["en"] = "Thank you! Your friend got a book as a gift.", ["uz"] = "Rahmat! Do'stingiz sovg'aga kitob oldi." },
            ["promo_too_many"] = new() { ["ru"] = "Слишком много попыток, попробуйте позже", ["kz"] = "Тым көп әрекет, кейінірек қайталаңыз", ["en"] = "Too many attempts, try again later", ["uz"] = "Juda ko'p urinish, keyinroq qayta urinib ko'ring" },
            ["promo_service_down"] = new() { ["ru"] = "Сервис временно недоступен", ["kz"] = "Қызмет уақытша қолжетімсіз", ["en"] = "Service temporarily unavailable", ["uz"] = "Xizmat vaqtincha mavjud emas" },
            // "Book as a gift" — invite a friend, the friend enters your code, the tale opens for you.
            ["gift_badge"] = new() { ["ru"] = "Книга в подарок!", ["kz"] = "Сыйлыққа кітап!", ["en"] = "Free book!", ["uz"] = "Kitob sovg'aga!" },
            ["gift_title"] = new() { ["ru"] = "Получите книгу бесплатно!", ["kz"] = "Кітапты тегін алыңыз!", ["en"] = "Get this book for free!", ["uz"] = "Kitobni bepul oling!" },
            // {0} = the "unlock all" button, {1} = the promo button. Substituted from their
            // own Loc keys so the quoted names always match what the friend actually sees.
            // <nobr> keeps a button name on one line: "«Открыть все / истории»" split across
            // two lines reads as two different buttons.
            ["gift_instruction"] = new() { ["ru"] = "Пригласите друга и отправьте ему ваш промокод.\nДругу нужно нажать <b><nobr>«{0}»</nobr></b>, а затем <b><nobr>«{1}»</nobr></b> в правом верхнем углу.", ["kz"] = "Досыңызды шақырып, оған промокодыңызды жіберіңіз.\nДосыңыз <b><nobr>«{0}»</nobr></b> түймесін, содан кейін оң жақ жоғарғы бұрыштағы <b><nobr>«{1}»</nobr></b> түймесін басуы керек.", ["en"] = "Invite a friend and send them your promo code.\nThey need to tap <b><nobr>\"{0}\"</nobr></b>, then <b><nobr>\"{1}\"</nobr></b> in the top right corner.", ["uz"] = "Do'stingizni taklif qiling va unga promokodingizni yuboring.\nDo'stingiz <b><nobr>\"{0}\"</nobr></b> ni ochib, o'ng yuqori burchakdagi <b><nobr>\"{1}\"</nobr></b> tugmasini bosishi kerak." },
            ["gift_your_code"] = new() { ["ru"] = "Ваш промокод", ["kz"] = "Сіздің промокодыңыз", ["en"] = "Your promo code", ["uz"] = "Sizning promokodingiz" },
            ["copy"] = new() { ["ru"] = "Копировать", ["kz"] = "Көшіру", ["en"] = "Copy", ["uz"] = "Nusxalash" },
            ["gift_code_copied"] = new() { ["ru"] = "Промокод скопирован", ["kz"] = "Промокод көшірілді", ["en"] = "Promo code copied", ["uz"] = "Promokod nusxalandi" },
            ["gift_code_error"] = new() { ["ru"] = "Код недоступен", ["kz"] = "Код қолжетімсіз", ["en"] = "Code unavailable", ["uz"] = "Kod mavjud emas" },
            ["gift_earned"] = new() { ["ru"] = "Друг ввёл ваш промокод — книга открыта!", ["kz"] = "Досыңыз промокодыңызды енгізді — кітап ашылды!", ["en"] = "A friend entered your promo code — the book is yours!", ["uz"] = "Do'stingiz promokodingizni kiritdi — kitob ochildi!" },
            // First-run intro screens, shown around the profile step. The highlighted words
            // travel inside the translation, as in paywall_headline.
            ["intro_values_title"] = new() { ["ru"] = "Помогите ребёнку стать ближе <color=#FFC93C>к Аллаху</color>", ["kz"] = "Балаңызға <color=#FFC93C>Аллаһқа</color> жақындауға көмектесіңіз", ["en"] = "Help your child grow closer <color=#FFC93C>to Allah</color>", ["uz"] = "Farzandingizga <color=#FFC93C>Allohga</color> yaqinlashishga yordam bering" },
            ["intro_values_sub"] = new() { ["ru"] = "Через добрые исламские истории мы помогаем детям\nрасти с верой и правильными ценностями", ["kz"] = "Мейірімді исламдық оқиғалар арқылы балалардың\nиманмен және дұрыс құндылықтармен өсуіне көмектесеміз", ["en"] = "Through kind Islamic stories we help children\ngrow up with faith and good values", ["uz"] = "Mehrli islomiy hikoyalar orqali bolalarga\niymon va to'g'ri qadriyatlar bilan ulg'ayishga yordam beramiz" },
            ["intro_values_quote"] = new() { ["ru"] = "«Вели своей семье совершать молитву и будь терпелив в ней».", ["kz"] = "«Отбасыңа намаз оқуды бұйыр және оған сабырлы бол».", ["en"] = "“Enjoin prayer upon your family and be steadfast therein.”", ["uz"] = "«Ahlingizni namozga buyuring va unga sabrli bo'ling»." },
            ["intro_values_quote_source"] = new() { ["ru"] = "Коран 20:132", ["kz"] = "Құран 20:132", ["en"] = "Quran 20:132", ["uz"] = "Qur'on 20:132" },
            ["intro_formats_title"] = new() { ["ru"] = "Читайте или слушайте — как\nудобно вашей <color=#FFC93C>семье</color>", ["kz"] = "Оқыңыз немесе тыңдаңыз —\n<color=#FFC93C>отбасыңызға</color> ыңғайлы түрде", ["en"] = "Read or listen — whatever\nsuits your <color=#FFC93C>family</color>", ["uz"] = "O'qing yoki tinglang —\n<color=#FFC93C>oilangizga</color> qanday qulay bo'lsa" },
            ["intro_formats_card_title"] = new() { ["ru"] = "4 удобных формата", ["kz"] = "4 ыңғайлы формат", ["en"] = "4 easy formats", ["uz"] = "4 ta qulay format" },
            ["intro_formats_card_sub"] = new() { ["ru"] = "Чтение · Ваш голос · Диктор · Персональная озвучка", ["kz"] = "Оқу · Сіздің дауысыңыз · Диктор · Жеке дыбыстау", ["en"] = "Reading · Your voice · Narrator · Personal narration", ["uz"] = "O'qish · Sizning ovozingiz · Diktor · Shaxsiy ovozlash" },
            // Library header. {0} is the child's name already put into the case the phrase
            // needs (ChildNameForms): genitive in Russian, dative in Kazakh.
            ["library_for_child"] = new() { ["ru"] = "История для {0}", ["kz"] = "{0} арналған оқиға", ["en"] = "A Story for {0}", ["uz"] = "{0} uchun hikoya" },
            ["library_for_child_sub_male"] = new() { ["ru"] = "Подобрано специально для него", ["kz"] = "Ол үшін арнайы таңдалды", ["en"] = "Picked especially for him", ["uz"] = "Aynan u uchun tanlandi" },
            ["library_for_child_sub_female"] = new() { ["ru"] = "Подобрано специально для неё", ["kz"] = "Ол үшін арнайы таңдалды", ["en"] = "Picked especially for her", ["uz"] = "Aynan u uchun tanlandi" },
        };

        /// <summary>All supported app languages — used to download every translation.</summary>
        public static readonly string[] AllLangs = { "ru", "kz", "en", "uz" };

        public static event Action OnLanguageChanged;

        public static string Lang
        {
            get => PlayerPrefs.GetString("ft_lang", "ru");
            set
            {
                PlayerPrefs.SetString("ft_lang", value);
                PlayerPrefs.Save();
                ApplyLocale(value);
                Diagnostics.Analytics.SetUserProperty("app_language", value);
                OnLanguageChanged?.Invoke();
            }
        }

        public static string Get(string key)
        {
            if (!Strings.TryGetValue(key, out var translations)) return key;
            if (translations.TryGetValue(Lang, out var text)) return text;
            if (translations.TryGetValue("ru", out var fallback)) return fallback;
            return key;
        }

        public static string Format(string key, params object[] args)
        {
            return string.Format(Get(key), args);
        }

        /// <summary>
        /// Detect device language and return matching app lang code.
        /// </summary>
        public static string DetectSystemLang()
        {
            return Application.systemLanguage switch
            {
                SystemLanguage.Russian => "ru",
                SystemLanguage.English => "en",
                SystemLanguage.Unknown => "uz", // Uzbek not in Unity enum — detected via locale
                _ => "ru"
            };
        }

        /// <summary>
        /// Set Unity Localization SelectedLocale to match our lang code.
        /// </summary>
        public static void ApplyLocale(string lang)
        {
            if (!LocalizationSettings.InitializationOperation.IsDone) return;
            if (!ToLocaleCode.TryGetValue(lang, out var code)) code = "ru-RU";
            var locale = LocalizationSettings.AvailableLocales.Locales
                .FirstOrDefault(l => l.Identifier.Code == code);
            if (locale != null)
                LocalizationSettings.SelectedLocale = locale;
        }

        /// <summary>
        /// Convert Unity Locale code (e.g. "ru-RU") to our short code ("ru").
        /// </summary>
        public static string FromLocale(Locale locale)
        {
            if (locale == null) return "ru";
            return FromLocaleCode.TryGetValue(locale.Identifier.Code, out var lang) ? lang : "ru";
        }
    }
}
