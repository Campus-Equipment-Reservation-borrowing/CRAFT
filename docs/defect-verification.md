# Defect verification — 22 reported issues

Run 2026-11-14 against the local build (`demo-v2` @ `bba6e43`) on macOS, SQL Server 2022 in Docker, app at `http://localhost:5080`.
Each claim was checked in code and, where safe, reproduced live with the demo accounts (curl + antiforgery tokens, results confirmed in the DB).

**Result: 16 confirmed · 3 partial · 3 not a bug**

| # | Claim | Verdict | Live? |
|---|---|---|---|
| 1 | Reservation form POST can't bind | **Not a bug** | Yes |
| 2 | Under-maintenance equipment reservable | **Confirmed** | Yes |
| 3 | Quantity can drop below damaged/lost | **Confirmed** | Yes |
| 4 | LOST status dead; lost shows "Returned" | **Confirmed** | Yes |
| 5 | Transaction numbers collide | **Confirmed** (TXN) / partial (RSV) | Code + SQL |
| 6 | Inspection overwrites global condition | **Confirmed** | Yes |
| 7 | Partial inspection fails with defaults | **Confirmed** | Yes |
| 8 | Pending inspection > returned units | **Confirmed** | Yes |
| 9 | Returns search ignores TXN/RSV number | **Confirmed** | Yes |
| 10 | Cancel releases wrong quantity | **Not a bug** | Yes |
| 11 | "Today" mixes UTC and local | **Confirmed** | Yes |
| 12 | Create crashes with no policy row | **Partial** | Code only |
| 13 | Custodian Release POST can 500 | **Confirmed** | Yes |
| 14 | Status dropdown is the Condition codebook | **Confirmed** | Yes |
| 15 | Operator "act as" switcher inert | **Confirmed** | Yes |
| 16 | Overdue counts inconsistent | **Confirmed** | Yes |
| 17 | Details shows only last return | **Confirmed** | Yes |
| 18 | Inspection photos outside wwwroot | **Not a bug** | Yes |
| 19 | Inspection status hardcoded to 1 | **Partial** | Yes |
| 20 | Transactions omit return/inspection events | **Confirmed** | Yes |
| 21 | Lead time / advance booking hardcoded | **Confirmed** | Code only |
| 22 | CreateReturn skips status check | **Partial** | Yes |

## Confirmed

**2 — Under-maintenance equipment can be reserved.** `ReservationService.Create` never checks `OperationalStatus`; the only check is in `GetEquipmentDetailsViewModel` (ReservationService.cs:119-142), which just hides the Reserve button. `GetReservableEquipment` filters on `IsActive` only (ReservationRepository.cs:72-82).
Live: PRJ-102 set to Under Maintenance → `GET /Reservations/New/9` 200, POST 302, reservation created PENDING. Seeded NET-401 is already in this state (status Under Maintenance, 2 units shown available).

**3 — Quantity can go below damaged/lost units.** `EquipmentService.Update` guards against `Reserved + Released + AwaitingInspection` only (EquipmentService.cs:254-258), ignoring `MaintenanceQuantity`/`UnreconciledQuantity`.
Live: LAB-502 with 4 maintenance + 3 unreconciled, Edit to total 5 → saved; list shows **"-2 / 5"**, Details clamps to "Available 0".

**4 — LOST borrowing status is never written.** `CreateReturn` sets only RETURNED/PARTIALLY_RETURNED (ReturnProcessingService.cs:201-218); `ApplyInspectionOutcome` never changes borrowing status.
Live: 2 units inspected as Lost → borrowing RETURNED, student sees a green "Returned" badge, item is in History.

**5 — Transaction numbers.** `CustodianRepository.GetNextTransactionNumber` (CustodianRepository.cs:153-172) takes the string-max and formats `D4`; `TXN-2026-9999` sorts above `TXN-2026-10000`, so after 10000 it regenerates `-10000` forever and every release fails on the unique index (generic ServerError, no retry). Concurrent releases collide the same way. Reservation numbers (ReservationService.cs:556-571) survive >9999 but are check-then-insert with no retry, so concurrent submits can collide.

**6 — Inspection overwrites equipment condition regardless of finalize.** ReturnProcessingService.cs:356-361 sets `equipment.CurrentConditionId` unconditionally; condition is per item, not per unit.
Live: a PENDING inspection marked CAM-202 Damaged (non-releasable); inspecting 2 of 3 AUD-302 units as Lost marked the whole item Lost.

**7 — Pre-filled defaults break follow-up inspections.** ReturnsController.cs:98-99 defaults to total ever returned, but the service allows only returned − already inspected (ReturnProcessingService.cs:283).
Live: return 1, inspect 1 OK; return 1 more → form defaults to 2/2 → "Inspected quantity can't exceed the units returned…".

**8 — Pending inspections unbounded.** Quantity checks run only inside `if (finalize)` (ReturnProcessingService.cs:276-296); the view's `max` is client-side.
Live: PENDING inspection with quantity 50 on a 2-unit borrowing with 0 returns → saved.

**9 — Returns search.** ReturnProcessingRepository.cs:57-65 matches equipment name, asset code and borrower name only, though the placeholder promises transaction search.
Live: `?search=TXN-2026-0001` → "No returns recorded yet"; `?search=CAM-202` → both returns.

**11 — UTC vs local "today".** DashboardService (lines 34-35, 106, 129-131, 169, 210-219) and BorrowingService (30-31, 75) use `DateTime.UtcNow.Date`; forms, announcements and views use local time. In UTC+8 the day rolls over at 08:00.
Live: a release due tomorrow 07:00 local appeared under "Releases Today".

**13 — Release POST 500s.** `GetReleaseContext` returns null for unknown/foreign reservations and both error branches dereference it (CustodianController.cs:205-206, 215-216); the GET does null-check.
Live: unknown id, missing/non-numeric id, empty body, or another custodian's reservation → **500** (`NullReferenceException` in log). Bad quantity/condition on a valid reservation → proper 200 with validation message.

**14 — Status dropdown uses the conditions codebook.** Both selects come from `GetConditionOptions()` (EquipmentService.cs:190-198; EquipmentController.cs:70, 87, 114, 125, 145, 178, 189); both columns FK to `conditions`. Status can be saved as "Good"/"Lost", condition as "Available". Only `UNDER_MAINTENANCE` blocks anything, and only in the UI (see #2).

**15 — Operator switcher inert.** `OperatorController.Select` writes session keys (OperatorController.cs:83-84); `CurrentUserService` reads cookie claims first and only falls back to session when unauthenticated (CurrentUserService.cs:29-60), which never happens on an `[Authorize(Roles=Admin)]` page.
Live: "Now acting as Maria Santos" shown, but Profile, layout, and access stay the admin's.

**16 — Overdue counts disagree.** Five predicates: lifecycle sweep (ACTIVE/PARTIALLY_RETURNED, 60-min grace), admin tile (OVERDUE or ACTIVE past due, no grace), borrower/custodian dashboards (not RETURNED/LOST, no grace), custodian list badge (whole days > 0), reservation block (includes LOST).
Live, two LP-001 borrowings 25 min past due: lifecycle 0, admin dashboard 1, custodian dashboard 2, borrower dashboard 2, custodian borrowings list 0.

**17 — Details shows last return only.** BorrowingService.cs:104, 125-127 uses `lastReturn`; block only shows when fully RETURNED (Details.cshtml:64); pluralises on total quantity ("1 units").
Live: returns of 1 + 1 → "Returned Quantity 1 units".

**20 — Transactions screen lacks returns/inspections.** `MonitoringService.GetTransactions` (MonitoringService.cs:91-130) unions only reservations and borrowings; tabs are All/Reservations/Borrowings vs. the planned Requests/Approvals/Releases/Returns/Overdue/Damaged (docs/CRUD-Implementation-Plan.md:110, 189). Returns/inspections appear only in Audit Logs.

**21 — Lead time / advance booking hardcoded.** ReservationService.cs:495-498 sets `ReservationLeadTimeDays = 1`, `MaxAdvanceBookingDays = 30`, `FacultyMaxBorrowDays = 21` even when a policy row exists; the table has no such columns (comment: "constants until Phase 7"). If students' max exceeds 21 days, faculty get less than students.

## Partial

**12 — No policy row.** `Create` reads `policy.PolicyVersionId.Value` (ReservationService.cs:294) on a fallback policy whose id is null → `InvalidOperationException`, caught and shown as "Server error occured!" (not a 500). Unreachable in practice: the seeder inserts a policy row on every startup and nothing deletes them.

**19 — Inspection status id 1.** The posted status is honoured; `?? 1` (ReturnProcessingService.cs:308) is only a fallback, and the GET preselects `InspectionStatusId = 1` (ReturnsController.cs:100). Magic number, correct today because PENDING = 1.

**22 — CreateReturn has no status check.** No explicit check (ReturnProcessingService.cs:150-168), but the remaining-quantity guard blocks returns on fully returned borrowings, and LOST is never set (#4). Only the message is poor: "Returned quantity must be between 1 and 0."

## Not a bug

**1 — Reservation form binds.** Fields are `Form.*` and the action parameter is `form`; ASP.NET Core prefix matching is case-insensitive. Live POST with the rendered field names → 302 and reservation created, times stored correctly as UTC.

**10 — Cancel quantity.** Borrowers can cancel only PENDING/APPROVED, where held = requested; reserved balance returns correctly. Minor: borrower cancel leaves `held_quantity` at its old value on the cancelled row (custodian close-out zeroes it).

**18 — Photo paths.** Files are renamed `photo_{n}{ext}` with an extension whitelist under a numeric inspection folder (ReturnProcessingService.cs:324-342). Live `../../probe.png` uploads landed inside `wwwroot/uploads/inspections/{id}/`. Hardening: use `IWebHostEnvironment.WebRootPath` instead of `Directory.GetCurrentDirectory()`; an empty folder is created per inspection.
