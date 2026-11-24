using System.Collections.Generic;
using FairyTales.UI.Core;

namespace FairyTales.Lessons
{
    /// <summary>
    /// Interface texts of the lesson player. Lessons exist only in ru, kz and en
    /// (<see cref="LessonLang"/>), so the table holds just those three and lives here
    /// instead of in Loc, which is shared with Bala word for word.
    ///
    /// Texts may carry {childName} and {m:…|f:…}: <see cref="T"/> resolves them like
    /// tale text. Kazakh needs a native speaker's pass before release.
    /// </summary>
    public static class LessonStrings
    {
        private static readonly Dictionary<string, (string ru, string kz, string en)> Table = new()
        {
            ["next"] = ("Дальше", "Әрі қарай", "Next"),
            ["done"] = ("Готово", "Дайын", "Done"),
            ["retry"] = ("Повторить", "Қайталау", "Try again"),
            ["back"] = ("Назад", "Артқа", "Back"),

            ["prepare"] = ("Готовим урок…", "Сабақты дайындап жатырмыз…", "Getting the lesson ready…"),
            ["fail_network"] = ("Нет интернета. Проверьте подключение и попробуйте ещё раз",
                "Интернет жоқ. Байланысты тексеріп, қайта көріңіз",
                "No internet. Check the connection and try again"),
            ["fail_locked"] = ("Этот урок откроется с подпиской", "Бұл сабақ жазылыммен ашылады",
                "This lesson opens with a subscription"),
            ["fail_missing"] = ("Урок пока недоступен", "Сабақ әзірге қолжетімсіз", "This lesson isn't available yet"),

            ["exit_title"] = ("Выйти из урока?", "Сабақтан шығасың ба?", "Leave the lesson?"),
            ["exit_note"] = ("Мы запомним, где ты {m:остановился|f:остановилась}",
                "Қай жерде тоқтағаныңды есте сақтаймыз", "We'll remember where you stopped"),
            ["stay"] = ("Остаться", "Қалу", "Stay"),
            ["leave"] = ("Выйти", "Шығу", "Leave"),

            ["resume_title"] = ("Продолжим урок?", "Сабақты жалғастырамыз ба?", "Shall we go on?"),
            ["resume_note"] = ("Ты {m:остановился|f:остановилась} на шаге {0} из {1}",
                "Сен {1} қадамның {0}-қадамында тоқтадың", "You stopped at step {0} of {1}"),
            ["resume"] = ("Продолжить", "Жалғастыру", "Continue"),
            ["restart"] = ("Сначала", "Басынан", "Start over"),

            ["repeat_bubble"] = ("Скажи вместе со мной!", "Менімен бірге айт!", "Say it with me!"),
            ["repeat_said"] = ("Я {m:сказал|f:сказала}!", "Мен айттым!", "I said it!"),

            ["quiz_correct"] = ("Машаллах, правильно!", "Машаллаһ, дұрыс!", "MashaAllah, that's right!"),
            ["quiz_try"] = ("Попробуй ещё", "Тағы байқап көр", "Try again"),

            ["deed_header"] = ("ЗАДАНИЕ С МАМОЙ", "АНАМЕН БІРГЕ ТАПСЫРМА", "A TASK WITH MUM"),
            ["deed_check"] = ("Сделали вместе", "Бірге жасадық", "We did it together"),
            ["deed_who"] = ("отмечает мама", "анасы белгілейді", "mum ticks it"),
            ["deed_marked"] = ("Мама отметила!", "Анасы белгіледі!", "Mum ticked it!"),

            ["reward_title"] = ("Машаллах, {childName}!", "Машаллаһ, {childName}!", "MashaAllah, {childName}!"),
            ["reward_again"] = ("Звезда за этот урок сегодня уже есть. Приходи завтра!",
                "Бұл сабақтың бүгінгі жұлдызы алынған. Ертең кел!",
                "Today's star for this lesson is already yours. Come back tomorrow!"),
            ["reward_tree"] = ("Посмотреть дерево", "Ағашты көру", "See the tree"),
            ["reward_home"] = ("На главную", "Басты бетке", "Home"),
        };

        /// <summary>Content language of the player texts: ru | kz | en (English as the fallback).</summary>
        public static string Lang => LessonLang.ContentLang(Loc.Lang) ?? "en";

        /// <summary>The text in the lesson language, name and gender forms resolved.</summary>
        public static string T(string key)
        {
            if (!Table.TryGetValue(key, out var t)) return key;
            var s = Lang switch { "ru" => t.ru, "kz" => t.kz, _ => t.en };
            return TaleText.Resolve(s);
        }

        public static string T(string key, params object[] args) => string.Format(T(key), args);

        /// <summary>"+1 звезда", "+2 звезды", "+2 жұлдыз", "+2 stars".</summary>
        public static string Stars(int n) => "+" + n + " " + Lang switch
        {
            "ru" => RuPlural(n, "звезда", "звезды", "звёзд"),
            "kz" => "жұлдыз",
            _ => n == 1 ? "star" : "stars",
        };

        /// <summary>"+1 листок", "+2 листка", "+2 жапырақ", "+2 leaves".</summary>
        public static string Leaves(int n) => "+" + n + " " + Lang switch
        {
            "ru" => RuPlural(n, "листок", "листка", "листков"),
            "kz" => "жапырақ",
            _ => n == 1 ? "leaf" : "leaves",
        };

        private static string RuPlural(int n, string one, string few, string many)
        {
            int m10 = n % 10, m100 = n % 100;
            if (m10 == 1 && m100 != 11) return one;
            if (m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14)) return few;
            return many;
        }
    }
}
