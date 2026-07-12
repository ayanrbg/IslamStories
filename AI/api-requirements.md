# API — Status

## All endpoints implemented ✅

Full documentation: `AI/API.md`

### Endpoints Summary (18 total)

| # | Endpoint | Method | Status |
|---|----------|--------|--------|
| 1 | `/health` | GET | ✅ |
| 2 | `/api/auth/login` | POST | ✅ |
| 3 | `/api/auth/register` | POST | ✅ |
| 4 | `/api/user/profile` | GET | ✅ |
| 5 | `/api/user/profile` | PUT | ✅ |
| 6 | `/api/voice/clone` | POST | ✅ |
| 7 | `/api/voice` | DELETE | ✅ |
| 8 | `/api/tales` | GET | ✅ |
| 9 | `/api/tales/:id` | GET | ✅ |
| 10 | `/api/tales/:id/personalize` | POST | ✅ |
| 11 | `/api/tales/:id/narrate?page=N` | POST | ✅ |
| 12 | `/api/tales/:id/narrate-all` | POST | ✅ |
| 13 | `/api/tales/:id/narration-status` | GET | ✅ |
| 14 | `/api/tales/:id/narration/:page` | GET | ✅ |
| 15 | `/api/voice/drafts` | GET | ✅ |
| 16 | `/api/voice/drafts` | POST | ✅ |
| 17 | `/api/voice/drafts/:id` | GET | ✅ |
| 18 | `/api/voice/drafts/:id` | DELETE | ✅ |

### Personalization Templates
- `{childName}` → child's name
- `{m:text|f:text}` → gender-based text selection
