# UI/UX Flow

## Screens

### 1. Onboarding (first launch)

**Screen 1 — Language Select**
- 3 buttons: Русский / Қазақ / English with flags
- "Продолжить" button
- Top-right: music toggle (present on ALL screens)

**Screen 2 — Personalization**
- Input: child's name
- Gender select: Мальчик / Девочка
- Left: cat illustration
- Top-left: globe (change language)
- "Продолжить" button

**Screen 3 — Loading** *(removed from main flow)*
- Was: cat animation + progress bar
- Register + Personalize moved to LibraryScreen (lazy)

### 2. Library (main screen)
- Top bar: settings (→ Personalization), notifications, music toggle
- Settings button opens PersonalizationScreen (change name/gender)
- Banner: "Разблокировать все книги" (placeholder)
- Grid of tale cards (cover + title)
- Some books free ("Книга в подарок!"), others locked
- Scrollable grid

### 3. Tale Detail (2 states)

**Not narrated:**
- Cover, title
- Buttons: "Читать", "Озвучить"
- Nav: home (top-left), music (top-right)

**Narrated:**
- Same but buttons: "Читать", "Слушать", "Озвучить заново"

### 4. Reading
- Fullscreen illustration background
- Top: Home, Table of Contents, Music
- Bottom: page text in panel, nav arrows (left/right)
- Page transitions: DOTween fade
- Default narration plays automatically

### 5. Table of Contents
- Popup overlay over reading screen
- Book title, close button (X)
- Horizontal scroll of page thumbnails (1, 2, 3, 4...)
- Current page highlighted (cyan border)
- Tap thumbnail → jump to page

### 6. Narration Setup
- Book cover (left)
- Two tabs: "Новая запись" / "Черновики"
- New recording: narrator name field, "Начать" button
- Drafts: list of saved recordings — tap → opens saved narration

### 7. Voice Recording
- Same as reading screen but top panel has:
  - Record button (red circle) / Stop (square)
  - Timer (00.0 → 03.5...)
  - Play button (listen to recording)
- Page text at bottom
- Records 4 sample sentences → sends to server for cloning
- After clone: server narrates entire book automatically

## Global Elements
- Background music: one track for menus, different per tale
- Music toggle button on every screen (top-right)
- All transitions: DOTween fade
- Purple/dark theme throughout
