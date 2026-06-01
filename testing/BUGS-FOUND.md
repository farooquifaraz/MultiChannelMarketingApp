# Bugs Found During QA Testing (2026-05-31)

## 🐛 BUG-001: First-registered user does NOT become admin

**Severity**: 🔴 High — onboarding blocker
**Discovered in**: Section 1 (AUTH-2), Section 4 (SS-2), Section 5 (AUD-1)
**Location**: `src/MarketingApp.Application/Services/AuthService.cs` line 44

### Current behavior
```csharp
var user = new User {
    FullName = dto.FullName,
    Email = dto.Email.ToLowerInvariant(),
    PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 12),
    Role = "user"   // ← ALWAYS "user", regardless of first/Nth signup
};
```

### Expected behavior (industry standard for fresh SaaS install)
Either:
- **Option A**: First user on a completely empty `users` table → role = `admin`
- **Option B**: Provide a one-time bootstrap CLI / env var to designate the first admin
- **Option C**: Use registration token gated by env-var `INITIAL_ADMIN_EMAIL=...`

Without one of these, a fresh deployment has **zero admins**, making it impossible to reach Settings, Audit, SMTP Groups, or Admin User Management without direct DB intervention.

### Impact on QA
Following endpoints **return 403** for the test user even after successful login:
- `PUT /api/v1/admin/system-settings` — "Only admins can change platform settings"
- `GET /api/v1/admin/audit-logs` — "Admin role required"
- `GET /api/v1/admin/smtp-groups`
- `GET /api/v1/admin/users`
- All admin-only endpoints (~25% of the API surface)

### Recommended fix
In `RegisterAsync`, before creating the user:
```csharp
var anyUserExists = await _userRepo.AnyAsync(u => true, ct);
var role = anyUserExists ? "user" : "admin";
```

Tradeoff: this is convenient but a race condition exists if two users register simultaneously. A migration-time admin seed via env var is more robust for production, but the first-user-admin pattern is industry-standard for self-hosted SaaS.

### Manual workaround (for QA continuation)
Promote a user via direct local Postgres:
```sql
UPDATE users SET role = 'admin' WHERE email = 'qatest.admin@dev.test';
```

---

## ✅ BUG-002: RETRACTED — `GET /api/v1/me` 404 is by design

**Status**: False alarm, test had wrong URL.
**Actual route**: `GET /api/v1/me/profile` (exists, works, frontend uses it via `meApi.ts`).
**Action**: no code change needed. The 404 on `/api/v1/me` (root) is correct — there's simply no endpoint there.

### ❌ Original report (kept for traceability)



**Severity**: 🟡 Medium — affects profile UI flow
**Discovered in**: Section 2 (PROF-1)
**Location**: `MeController.cs` (route registration)

### Current behavior
- `GET /api/v1/me/signature` → ✅ 200
- `PUT /api/v1/me/signature` → ✅ 200
- `GET /api/v1/me` → ❌ 404 (no route)

### Expected behavior
A `GET /api/v1/me` should return the current user's profile (email, name, role, IsActive, etc.). This is what the React frontend would call to display the user header.

### Impact on QA
Frontend `useAuthStore` may already cache user data from login response, so this might be cosmetic. But REST convention expects this endpoint.

### Recommended fix
Add a `[HttpGet("")]` action in `MeController` returning current user.

### Notes
Confirm that the frontend doesn't already work around this — check `frontend/src/api/meApi.ts`.

---

---

## 🐛 BUG-003: Negative `pageNumber` causes HTTP 500

**Severity**: 🟡 Medium — defense-in-depth issue, not data-corrupting
**Discovered in**: Section 13 (VAL-6)
**Endpoint**: `GET /api/v1/contacts?pageNumber=-1&pageSize=5`

### Current behavior
Server returns 500 "An unexpected error occurred." Likely a `Skip(-N)` exception inside the repository.

### Expected behavior
Either:
- Return 400 Bad Request with a friendly message ("pageNumber must be >= 1")
- Or silently coerce to `Math.Max(1, pageNumber)` and return page 1

### Impact on QA
Frontend doesn't generate negative page numbers, but malformed clients or attackers probing endpoints will hit this. Reveals an unhandled `ArgumentOutOfRangeException` from underlying Skip/Take.

### Recommended fix
In `ContactsController.GetAllAsync` (and any other paged endpoint), clamp inputs at the top:
```csharp
pageNumber = Math.Max(1, pageNumber);
pageSize   = Math.Clamp(pageSize, 1, 200);
```

Or use a FluentValidation rule on a `PagedQueryParams` DTO.

---

## ⚠️ Observation (not a bug): XSS payload stored raw in `fullName`

**Discovered in**: Section 13 (VAL-3)

Storing user input as-is is fine **only** if the frontend escapes on render. The codebase uses **DOMPurify** in `frontend/src/components/SafeHtml.tsx`, and rendered names go through React JSX (which auto-escapes text content). So this is safe — but worth a code review note.

**Recommended**: keep DOMPurify but also add a length validation (e.g. `fullName` ≤ 150 chars) so malicious users can't fill the DB with multi-MB strings.

---

## Summary

| Bug | Severity | Status |
|-----|----------|--------|
| BUG-001 First-user-not-admin | 🔴 High | Documented, manual workaround applied for QA |
| BUG-002 GET /api/v1/me 404 | 🟡 Medium | Documented |
| BUG-003 Negative pageNumber → 500 | 🟡 Medium | Documented |

**Recommendation**: fix all three before Phase 1 (WhatsApp) work begins. Each is small (<30 lines of code) and they all benefit any feature that lands later.

(Full per-test results live in the `results-section-*.json` files; the narrative report is in `TEST-REPORT-2026-05-31.md`.)
