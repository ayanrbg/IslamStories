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
            ["repeat_watch"] = ("Смотреть", "Көру", "Watch"),
            ["repeat_hint"] = ("Скажи вместе с другом!", "Досыңмен бірге айт!", "Say it with your friend!"),
            ["repeat_say_with"] = ("Скажи вместе с другом!", "Досыңмен бірге айт!", "Say it with your friend!"),
            ["repeat_next_time"] = ("В следующий раз попробуем вместе", "Келесі жолы бірге айтып көреміз",
                "Next time we'll try it together"),
            ["repeat_me"] = ("Это ты, {0}", "Бұл сен, {0}", "That's you, {0}"),
            ["repeat_me_noname"] = ("Это ты!", "Бұл сен!", "That's you!"),
            ["repeat_no_camera"] = ("Без камеры", "Камерасыз", "No camera"),
            ["repeat_mouth"] = ("Рот двигается, когда ты говоришь", "Сөйлегенде аузы қимылдайды",
                "The mouth moves when you talk"),
            ["repeat_listen"] = ("Послушай себя!", "Өзіңді тыңда!", "Listen to yourself!"),
            ["repeat_praise"] = ("Машаллах!", "Машаллаһ!", "MashaAllah!"),

            ["perm_for_parents"] = ("ДЛЯ РОДИТЕЛЕЙ", "АТА-АНАЛАРҒА", "FOR PARENTS"),
            ["perm_title"] = ("Камера и микрофон в уроках", "Сабақтағы камера мен микрофон", "Camera and microphone in lessons"),
            ["perm_camera"] = ("Камера", "Камера", "Camera"),
            ["perm_camera_note"] = ("Ребёнок видит себя на экране в весёлой маске и повторяет фразу за другом.",
                "Бала өзін экранда көңілді маскамен көріп, досынан кейін сөзді қайталайды.",
                "Your child sees themselves in a fun mask and repeats the phrase after a friend."),
            ["perm_mic"] = ("Микрофон", "Микрофон", "Microphone"),
            ["perm_mic_note"] = ("Записываем голос на несколько секунд и сразу даём послушать себя.",
                "Дауысты бірнеше секундқа жазып, бірден өзін тыңдатамыз.",
                "We record a few seconds of voice and play it straight back."),
            ["perm_privacy"] = ("Видео и голос нигде не сохраняются и никуда не отправляются",
                "Бейне мен дауыс еш жерде сақталмайды және ешқайда жіберілмейді",
                "Video and voice are never saved or sent anywhere"),
            ["perm_allow"] = ("Разрешить", "Рұқсат беру", "Allow"),
            ["perm_no_camera"] = ("Без камеры", "Камерасыз", "Without camera"),

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

            ["avatar_child_tab"] = ("Ребёнок", "Бала", "Child"),
            ["avatar_mom_tab"] = ("Мама", "Анасы", "Mum"),
            ["avatar_title_mom"] = ("Аватар мамы", "Ананың аватары", "Mum's avatar"),
            ["avatar_take"] = ("Сделать фото", "Суретке түсіру", "Take a photo"),
            ["avatar_gallery"] = ("Из галереи", "Галереядан", "From gallery"),
            ["avatar_or_face"] = ("или выбери нарисованную голову", "немесе суреттегі басты таңда", "or pick a drawn head"),
            ["avatar_mom_hint"] = ("Без фото мама остаётся нарисованной", "Суретсіз ана суретте салынғандай қалады",
                "Without a photo, mum stays drawn"),
            ["avatar_privacy"] = ("Фото остаётся только на этом телефоне", "Сурет тек осы телефонда қалады",
                "The photo stays on this phone only"),
            ["avatar_remove"] = ("Убрать фото", "Суретті алып тастау", "Remove photo"),
            ["avatar_fit"] = ("Смотри в камеру и улыбнись!", "Камераға қарап, күлімде!", "Look at the camera and smile!"),
            ["avatar_cutting"] = ("Вырезаем голову…", "Басты қиып жатырмыз…", "Cutting out the head…"),
            ["avatar_no_face"] = ("Лицо не нашлось. Сфотографируй ещё раз при хорошем свете", "Бет табылмады. Жарық жерде қайта түсір", "No face found. Take another photo in good light"),
            ["avatar_or_face_mom"] = ("или выбери нарисованную", "немесе суреттегісін таңда", "or pick a drawn one"),
            ["avatar_body_plain"] = ("Без хиджаба", "Хиджабсыз", "No hijab"),
            ["avatar_body_hijab"] = ("В хиджабе", "Хиджабпен", "With hijab"),
            ["avatar_fit_hint"] = ("Подвинь голову пальцем", "Басты саусақпен жылжыт", "Move the head with a finger"),
            ["avatar_retake"] = ("Заново", "Қайта", "Retake"),
            ["home_lesson_day"] = ("Урок дня", "Күн сабағы", "Today's lesson"),
            ["home_all"] = ("Все уроки", "Барлық сабақ", "All lessons"),
            ["home_tales"] = ("Сказки", "Ертегілер", "Stories"),
            ["home_tree"] = ("Дерево", "Ағаш", "Tree"),
            ["home_games"] = ("Игры", "Ойындар", "Games"),
            ["home_parents"] = ("Для родителей", "Ата-аналарға", "For parents"),
            ["home_soon"] = ("Скоро!", "Жақында!", "Coming soon!"),
            ["home_coloring"] = ("Раскраски", "Боямалар", "Coloring"),
            ["home_picks"] = ("{0}, это для тебя!", "{0}, бұл саған!", "Top picks for {0}"),
            ["home_picks_you"] = ("Это для тебя!", "Бұл саған!", "Top picks for you"),
            ["home_trace"] = ("Обведи", "Сызып шық", "Trace"),
            ["home_family"] = ("Моя семья", "Менің отбасым", "My family"),
            ["home_family_edit"] = ("Изменить", "Өзгерту", "Edit"),
            ["home_me"] = ("Я", "Мен", "Me"),
            ["home_mom"] = ("Мама", "Анам", "Mum"),
            ["home_dad"] = ("Папа", "Әкем", "Dad"),
            ["home_lessons"] = ("Уроки", "Сабақтар", "Lessons"),
            ["home_unlock"] = ("Открыть все уроки", "Барлық сабақты ашу", "Unlock all lessons"),
            ["coloring_trace_done"] = ("Машаллах! Теперь раскрась", "Машаллах! Енді боя", "MashaAllah! Now colour it in"),
            ["coloring_title"] = ("Раскраски", "Боямалар", "Coloring pages"),
            ["coloring_done"] = ("Машаллах, как красиво!", "Машаллах, қандай әдемі!", "MashaAllah, so beautiful!"),
            ["trace_done"] = ("Машаллах, получилось!", "Машаллах, жасадың!", "MashaAllah, you did it!"),
            ["coloring_broken"] = ("Эта раскраска не открылась", "Бұл бояма ашылмады", "This page didn't open"),
            ["home_tree_soon"] = ("Дерево добрых дел скоро вырастет здесь", "Игі істер ағашы жақында осында өседі",
                "The tree of good deeds will grow here soon"),
            ["home_no_lessons"] = ("Уроки загрузятся, когда появится интернет", "Интернет болғанда сабақтар жүктеледі",
                "Lessons will load once there is internet"),
            ["home_deeds"] = ("Ждёт отметки: {0}", "Белгі күтуде: {0}", "To tick: {0}"),
            ["home_avatar_title"] = ("Сделаем тебя героем!", "Сені кейіпкер етейік!", "Let's make you the hero!"),
            ["home_avatar_text"] = ("Выбери лицо или сфотографируйся — и ты появишься в уроках",
                "Бет таңда немесе суретке түс — сабақтарда өзің боласың",
                "Pick a face or take a photo, and you'll be in the lessons"),
            ["home_avatar_go"] = ("Сделать", "Жасау", "Let's go"),
            ["home_avatar_later"] = ("Потом", "Кейін", "Later"),
            ["tree_title"] = ("Дерево добрых дел", "Игі істер ағашы", "Tree of good deeds"),
            ["tree_waiting"] = ("Ждут отметки мамы", "Анасының белгісін күтеді", "Waiting for mum's tick"),
            ["tree_mark"] = ("Отметить", "Белгілеу", "Tick"),
            ["tree_leaves"] = ("Листки", "Жапырақтар", "Leaves"),
            ["tree_empty"] = ("Пройди урок — и на дереве появится новый листок",
                "Сабақтан өт — ағашта жаңа жапырақ пайда болады",
                "Finish a lesson and a new leaf will grow on the tree"),
            ["tree_grow"] = ("Ещё {0} — и дерево подрастёт", "Тағы {0} — ағаш өседі", "{0} more and the tree will grow"),
            ["tree_big"] = ("Какое большое дерево!", "Қандай үлкен ағаш!", "What a big tree!"),
            ["leaf_new"] = ("новый!", "жаңа!", "new!"),
            ["leaf_mum"] = ("отметила мама", "анасы белгіледі", "ticked by mum"),
            ["leaf_today"] = ("сегодня", "бүгін", "today"),
            ["leaf_yesterday"] = ("вчера", "кеше", "yesterday"),
            ["offer_for_parents"] = ("ДЛЯ РОДИТЕЛЕЙ", "АТА-АНАЛАРҒА", "FOR PARENTS"),
            ["offer_title"] = ("Понравились уроки?", "Сабақтар ұнады ма?", "Enjoying the lessons?"),
            ["offer_text"] = ("Откройте все уроки и сказки, Дерево добрых дел и прогресс ребёнка",
                "Барлық сабақ пен ертегіні, игі істер ағашын және баланың жетістігін ашыңыз",
                "Unlock all lessons and stories, the Tree of good deeds and your child's progress"),
            ["offer_go"] = ("Открыть всё", "Бәрін ашу", "Unlock all"),
            ["offer_later"] = ("Позже", "Кейін", "Later"),
            ["avatar_no_camera"] = ("Нет доступа к камере. Разрешите его в настройках телефона",
                "Камераға рұқсат жоқ. Телефон баптауларынан рұқсат беріңіз",
                "No camera access. Allow it in the phone settings"),
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

        /// <summary>«Аватар для Айши» / «Айшаға арналған аватар» / «Aisha's avatar».</summary>
        public static string AvatarTitleChild()
        {
            var name = TaleText.ChildName;
            if (string.IsNullOrWhiteSpace(name))
                return Lang switch { "ru" => "Аватар ребёнка", "kz" => "Баланың аватары", _ => "Child's avatar" };
            var form = ChildNameForms.ForDedication(name, TaleText.ChildGender, Lang);
            return Lang switch
            {
                "ru" => "Аватар для " + form,
                "kz" => form + " арналған аватар",
                _ => name + "'s avatar",
            };
        }

        /// <summary>"+1 звезда", "+2 звезды", "+2 жұлдыз", "+2 stars".</summary>
        public static string Stars(int n) => "+" + n + " " + Lang switch
        {
            "ru" => RuPlural(n, "звезда", "звезды", "звёзд"),
            "kz" => "жұлдыз",
            _ => n == 1 ? "star" : "stars",
        };

        /// <summary>"+1 листок", "+2 листка", "+2 жапырақ", "+2 leaves".</summary>
        public static string Leaves(int n) => "+" + LeavesCount(n);

        /// <summary>"1 листок", "2 листка", "2 жапырақ", "2 leaves".</summary>
        public static string LeavesCount(int n) => n + " " + Lang switch
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
