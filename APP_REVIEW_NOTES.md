# App Review Notes — Islam Stories

Paste the plain-text block below into **App Store Connect → App Review Information → Notes**.
It is written in English for the reviewer and explains how to navigate the non-English UI.

> Replace the CONTACT and SUBSCRIPTION lines with your real values before submitting.

---

## Plain-text notes for the reviewer (copy from here)

Hello, and thank you for reviewing Islam Stories.

ABOUT THE APP
Islam Stories is a children's app with a library of illustrated Islamic tales. Stories can be narrated by a built-in professional narrator or in the parent's own recorded voice, and are personalized with the child's name.

IMPORTANT — INTERFACE LANGUAGE
The app interface is in Russian and Kazakh only. There is no English localization. Please select "Русский" (Russian) on the first screen and test in Russian — do not choose "English" (that option is not supported and its content is not translated). To help you test, here are translations of the on-screen buttons you will need.

NO LOGIN REQUIRED
No account, email, or password is needed. A user profile is created automatically on the device, so you can go straight into the app. There is no sign-in screen and no demo credentials are required.

HOW TO START (onboarding)
1. Language screen — title "Выберите язык" ("Choose language"). Options:
   - "Русский" = Russian (please select this one)
   - "Қазақ" = Kazakh
   - "English" = not supported, please do not select
   Tap "Русский", then tap "Продолжить" ("Continue").
2. Personalization screen — title "Как зовут ребёнка?" ("What is the child's name?").
   - Tap the input field ("Введите имя" = "Enter name") and type any name.
   - Choose gender: "Мальчик" = Boy, "Девочка" = Girl.
   - Tap "Продолжить" ("Continue").
   - The globe button (🌐) at the top left returns to the language screen.
3. A loading screen appears ("Подготавливаем библиотеку..." = "Preparing the library...").
4. The main library of stories opens. Tap any cover to open a story and read/listen.

PARENTAL GATE (important for reaching some sections)
Certain sections (such as voice recording and subscription/purchase) are protected by a parental gate to keep children from accessing them. A popup appears with the title "Решите пример" ("Solve the problem") and a simple math expression, for example "24+32=?".
- Read the expression, calculate the answer, and enter it using the on-screen number pad on the right.
- For "24+32=?" the answer is 56, so tap 5 then 6.
- The "✕" button in the top-right corner closes the popup.
Once the correct answer is entered, the protected section opens.

VOICE NARRATION (optional to test)
Inside a story you can play narration. To record a parent's voice, open the voice section (behind the parental gate), record a short sample when prompted, and the story can then be narrated in that voice. Microphone permission is requested for this feature only.

PERMISSIONS
The app requests only the Microphone permission, and only when the user chooses to record a parent's voice for narration (behind the parental gate). The permission is optional — you can review and use the entire app, including reading and built-in narration, without granting it. No other sensitive permissions are requested.

SUBSCRIPTION / IN-APP PURCHASE
Full access to all stories and features is offered via an auto-renewable subscription. The paywall is reached from the library or when opening a locked (paid) story, and is behind the parental gate described above.
- Product IDs: com.mozz.islamStories (monthly), com.mozz.islamStoriesYearly (yearly)
- Please test purchases with a Sandbox account.

CONTACT
If anything is unclear or you need additional access, please contact us at ayanrbgcreator@gmail.com and we will respond quickly.

Thank you very much for your time.

---

## Russian reference (for us, not for the reviewer field)

- Логин не требуется — профиль создаётся автоматически по устройству.
- Онбординг: «Выберите язык» → имя ребёнка («Как зовут ребёнка?») → пол
  (Мальчик/Девочка) → «Продолжить» → загрузка → библиотека.
- Родительский барьер («Решите пример», напр. 24+32=?): решить пример и ввести
  ответ на цифровой клавиатуре справа; закрыть — «✕».
- Барьер закрывает: запись голоса и экран подписки/покупки.
- Подписка: Apple SKU `com.mozz.islamStories` / `com.mozz.islamStoriesYearly`
  (в коде unified id `fairytales_monthly`/`fairytales_yearly`, маппинг в IAPManager),
  тестировать через Sandbox.
- Промокоды на iOS скрыты (Guideline 3.1.1) — кнопка и панель отключаются в
  PaymentScreen.Awake под `#if UNITY_IOS`.
