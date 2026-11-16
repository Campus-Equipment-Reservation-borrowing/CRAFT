# CRAFT test plan v3: every flow, every role

This plan replaces [test-plan-v2.md](test-plan-v2.md). It keeps the v2 case IDs and adds:
- custodian assignment on verification
- the 72-hour expiry and expiry warnings
- stock adjustments
- an access-control section (ACL) that checks every route against every role and checks that no user can see or change another user's records

**How each case is run:**
- **Automated** cases run in [`testing/craft-suite.js`](testing/craft-suite.js); the suite name is shown in brackets, e.g. `[ACL]`.
- **(M)** marks a manual case, or one that needs SQL.
- **(B)** marks a case that needs the background job: backdate a timestamp in SQL, then restart the app; the first pass runs 30 seconds after startup.

**Priority:**
- **P1** protects data, money, stock or privacy.
- **P2** covers rules and messages.
- **P3** is cosmetic.

Results go in [test-results-v3.md](test-results-v3.md).

## 0. Environment

1. **Database.** Use LocalDB `AllianceDB`.
   - Start from a clean copy if you can.
   - Otherwise restart the app. Startup reconciliation rebuilds reserved stock from the open reservations and logs every correction.
2. **Start the app** in Development, so the reset link is shown on screen: `dotnet run --project ASI.Basecode.WebApp`, then open https://localhost:52988.
3. **Run the suite.**
   - Open any page of the app.
   - Paste `docs/testing/craft-suite.js` into the browser console.
   - Run `await CRAFT.runAll()`, or a single suite with `await CRAFT.suites.ACL()`.
4. **Guided tour.** It starts once per account. Skip it, or mark it done in SQL:
   ```sql
   UPDATE user_preferences SET tour_completed_at_utc = SYSUTCDATETIME()
   ```
5. **Accounts.** All use the password `Campus123`.

| Account | Role | Notes |
|---|---|---|
| `ADMIN-0001` | Admin | The only admin |
| `EMP-1001` | Custodian Carlos | Covers location PO-MAIN and borrowers Maria and Liza |
| `EMP-1002` | Custodian Rina | Covers PO-ANNEX, the CAMERA category and borrower Jose |
| `2023-104857` | Student Maria | Active |
| `EMP-2001` | Faculty Jose | Active; 21-day loan limit |
| `2024-118002` | Student Liza | Unverified on a fresh database |
| *(new)* | Self-registered user | Created in AUTH-08 |

6. **Stock invariant (P1).** Run this after every case that moves stock. Both queries must return no rows.
   ```sql
   -- every bucket is non-negative and fits in the total
   SELECT * FROM equipment_inventory_balances
   WHERE reserved_quantity < 0 OR released_quantity < 0 OR awaiting_inspection_quantity < 0
      OR maintenance_quantity < 0 OR unreconciled_quantity < 0
      OR reserved_quantity + released_quantity + awaiting_inspection_quantity
         + maintenance_quantity + unreconciled_quantity > total_quantity;
   -- reserved stock equals what open reservations still hold
   SELECT b.equipment_id, b.reserved_quantity, ISNULL(SUM(r.held_quantity),0) AS held
   FROM equipment_inventory_balances b
   LEFT JOIN reservations r ON r.equipment_id = b.equipment_id
     AND r.status_id IN (SELECT status_id FROM reservation_statuses WHERE code IN ('PENDING','APPROVED','PARTIALLY_RELEASED'))
   GROUP BY b.equipment_id, b.reserved_quantity HAVING b.reserved_quantity <> ISNULL(SUM(r.held_quantity),0);
   ```

---

## ACL: access control

These are the cases that prove a signed-in user can't do or see anything outside their role.

**Route groups used by the matrix:**

| Group | Routes | Who may open them |
|---|---|---|
| borrower | Borrower dashboard; Reservations Available, EquipmentDetails, New, Mine; Borrowings Mine, History | Student, faculty |
| staff | Custodian Dashboard, Reservations, Scan, Borrowings; Returns; Equipment list, Details, Label | Custodian, admin |
| admin | Equipment Create and Edit; Users (all pages); CustodianAssignments; Monitoring (all pages and CSV exports); Announcements; Settings | Admin |
| anyUser | Profile, Notifications, Support list, Support/Create | Every signed-in user |

| ID | Case | Expected | P |
|---|---|---|---|
| ACL-01 | Each of the 5 actors (anonymous, student, faculty, custodian, admin) opens every route in every group `[ACL]` | Allowed: 200. Not allowed: 403. Anonymous: redirect to login | P1 |
| ACL-06 | Every sidebar link shown to each role `[ACL]` | Opens with 200 (no link to a page the role can't use) | P2 |
| ACL-10 | A student opens faculty Jose's reservation details, pickup slip and support ticket by id `[ACL]` | Not shown; redirected away | P1 |
| ACL-11 | A student posts a reply to Jose's ticket `[ACL]` | No reply is added | P1 |
| ACL-12 | A student posts Cancel on Jose's reservation `[ACL]` | Jose's reservation stays open | P1 |
| ACL-20 | Custodian Carlos opens details, slip, review and release for a reservation routed to Rina `[ACL]` | Not shown ("not found" or redirect) | P1 |
| ACL-21 | Carlos posts Approve on Rina's reservation `[ACL]` | Refused; status unchanged | P1 |
| ACL-22 | A custodian opens another user's support ticket `[ACL]` | Not shown. Support is admin-only for staff; custodians see only their own tickets | P1 |
| ACL-23 | A custodian posts a stock adjustment `[ACL]` | 403; Mark repaired / Write off buttons are not shown | P1 |
| ACL-24 | A custodian posts Resolve on a ticket `[ACL]` | 403 | P1 |
| ACL-25 | A custodian posts admin actions: save policy, assign custodian, reset password, publish announcement `[ACL]` | 403 each | P1 |
| ACL-30 | A student posts custodian actions: approve, reject, record return, adjust stock `[ACL]` | 403 each | P1 |
| ACL-31 | An admin posts Claim (custodian-only) `[ACL]` | 403 | P3 |
| ACL-32 | An admin opens any ticket `[ACL]` | 200 | P2 |
| ACL-40 | An anonymous user requests an inspection photo `[ACL]` | Redirect to login. Photos are not public files | P1 |
| ACL-41 | A custodian opens a photo of an inspection outside their scope (M) | Not found | P1 |
| ACL-42 | Any page shows only data in the role's scope: each dashboard count, list and notification belongs to the user or their custodian scope (M) | Yes | P1 |
| ACL-43 | Suspend a signed-in borrower from another browser (M) | Within about 1 minute their next request goes to Login | P1 |
| ACL-44 | Change a signed-in user's role (M) | Their next check signs them out | P1 |
| ACL-45 | Reset a signed-in user's password elsewhere (M) | Their other sessions end; the session that changed it stays signed in | P1 |
| RBAC-05 | Any POST without the anti-forgery token `[RBAC]` | 400 | P1 |
| SEC-01 | HTML or script typed into names, tickets, announcements, notes and purpose `[SEC]` | Shown escaped everywhere | P1 |
| SEC-03 | Auth and session cookies (M) | HttpOnly, Secure, SameSite=Lax | P1 |

## AUTH: registration, sign-in, passwords

| ID | Case | Expected | P |
|---|---|---|---|
| AUTH-01…07 | Registration field rules: blank fields, ID 4-20 letters/digits/dashes, email format, password policy (8+ characters, 1 uppercase, 1 digit), confirmation matches, privacy consent, duplicate ID or email `[AUTH]` | The message for each rule; no account created | P2 |
| AUTH-08 | Valid registration `[AUTH]` | Redirect to Login with "pending verification"; account is UNVERIFIED; audit REGISTER | P1 |
| AUTH-08b | The Register role list (M) | Only Student and Faculty can be chosen | P1 |
| AUTH-09 | Sign in by email and by ID, any letter case `[AUTH]` | Lands on the role's dashboard | P2 |
| AUTH-10 | Wrong password, and unknown user `[AUTH]` | Both show the same "Invalid username or password." | P1 |
| AUTH-11 | 5 wrong passwords in a row `[AUTH]` | Locked for 15 minutes; the correct password is refused until then | P1 |
| AUTH-12 | Remember me (M) | Persistent cookie for 14 days; without it, a session cookie lasting 30 minutes (sliding) | P3 |
| AUTH-13 | ReturnUrl is local / external `[AUTH]` | Local URL is followed; external URL is ignored | P1 |
| AUTH-14 | Logout `[AUTH]` | Protected pages redirect to Login | P1 |
| AUTH-15 | Forgot password with a blank, unknown or known email `[AUTH]` | Same "if registered…" message every time; a link appears only for a known email and only in Development | P1 |
| AUTH-16 | Reset: mismatch, weak password, valid, link used twice `[AUTH]` | Messages for the errors; the second use is refused | P1 |
| AUTH-17 | Reset link older than 24 hours (M, SQL backdate) | Refused | P2 |
| AUTH-18 | Requesting a new link (M) | Earlier links stop working | P2 |
| AUTH-19 | An unverified user reserves `[AUTH]` | "Pending verification"; no stock is held | P1 |
| AUTH-20 | Suspended, inactive or locked account signs in with the correct password (M) | Refused with the status message; forgot password issues no link | P1 |
| AUTH-21 | Admin creates a user with a temporary password; that user signs in (M) | Every page redirects to Change Password until the password is changed | P1 |
| AUTH-22 | Forced change: wrong current password, weak new password, new password same as the old one (M) | Refused | P2 |

## PRF: profile and preferences

| ID | Case | Expected | P |
|---|---|---|---|
| PRF-01 | Contact number: blank, invalid, valid `[PRF]` | Rules from `^[0-9+\-\s()]{7,50}$` | P2 |
| PRF-02 | Switch each of the 5 notification types off, then trigger that event (M) | That type is not delivered; ACCOUNT, ALERT and SUPPORT are always delivered | P2 |
| PRF-03 | Change password from Profile (M) | This session stays signed in; a second browser is signed out | P1 |
| PRF-04 | Photo upload: an image over 2 MB, a renamed non-image, a valid JPG; then Remove (M) | The first two are refused (checked by file content); the valid one shows in the header; Remove falls back to initials | P2 |
| PRF-05 | Guided tour: first sign-in, Skip, replay from Guide (M) | Auto-starts once; Skip records it; replay works; the forced-change page doesn't break it | P3 |

## ADM: administration

| ID | Case | Expected | P |
|---|---|---|---|
| ADM-01 | Users list: search, filters, paging `[ADM]` | Narrows correctly | P3 |
| ADM-02 | Create a user; create a borrower as ACTIVE (M) | The user is created and the temporary password is shown once. The ACTIVE borrower is refused ("verify through the queue") | P1 |
| ADM-03 | Edit your own role or status; demote the last admin (M) | Both refused | P1 |
| ADM-04 | Make a borrower with no custodian ACTIVE through Edit (M) | Refused | P1 |
| ADM-05 | Deactivate a custodian who has borrowers or open work (M) | Refused ("reassign first") | P1 |
| ADM-06 | Delete yourself, the last admin, or a user with history (M) | Refused. A user without history is deleted; their assignments, notifications and audit actor are cleaned up | P1 |
| ADM-07 | Reset another user's password (M) | Temporary password shown; the user must change it; they get an ACCOUNT notification | P1 |
| ADM-08 | Invite link (M) | Shows a working reset link. For a suspended user it shows no token | P2 |
| ADM-10 | Verification queue: Approve without choosing a custodian, with the browser check bypassed (M) | Server refuses | P1 |
| ADM-11 | Approve with a custodian (M) | ACTIVE; borrower assignment created; open reservations rerouted; audit VERIFY and ASSIGN; borrower and custodian notified | P1 |
| ADM-12 | Reject with no reason, then with a reason (M) | No reason: refused. With a reason: REJECTED; borrower sees the reason and an Appeal link | P1 |
| ADM-13 | Approve from the Rejected tab (M) | Allowed | P2 |
| ADM-14 | No active custodian exists (M) | Approve is disabled; a banner links to create one | P2 |
| ADM-20 | Assign by borrower, location and category; duplicate location or category (M) | Assigned; the duplicate is refused | P2 |
| ADM-21 | Change an active borrower's custodian (M) | Confirm dialog; their open reservations move | P1 |
| ADM-22 | Unassign an active borrower (forged POST) (M) | Refused | P1 |
| ADM-23 | Routing order (M) | Borrower assignment beats location, which beats category. A borrower with no rule is refused at reservation time ("no custodian") | P1 |
| ADM-24 | Reassign a reservation to a custodian or to the pool, from the Monitoring workload panel (M) | Moves; the new custodian is notified | P2 |
| ADM-30 | Policy: every field at, below and above its range; faculty days below student days; advance days ≤ lead time; saving with no changes `[ADM]` | Each refused with its message; a valid save creates a new version | P2 |
| ADM-31 | Categories and locations: bad code, duplicate, delete while in use (M) | Refused with an accurate message | P2 |
| ADM-40 | Announcement: missing title, body or role; expiry in the past; valid; delete (M) | Shown on the target roles' dashboards until expiry; ANNOUNCEMENT notifications sent | P2 |

## EQP: equipment

| ID | Case | Expected | P |
|---|---|---|---|
| EQP-01 | Create (admin): duplicate asset code, status/condition mismatch, oversized image `[EQP]` | Refused | P2 |
| EQP-02 | Edit total below the units already committed `[EQP]` | Refused | P1 |
| EQP-03 | Deactivate, or set UNDER_MAINTENANCE, while open reservations exist (M) | Refused with the count | P1 |
| EQP-04 | Delete an item with history (M) | Refused | P1 |
| EQP-05 | Admin: Mark repaired, Mark found, Write off (damaged / lost) (M) | Buckets move; written-off units lower the total; audit STOCK_ADJUST; invariant holds | P1 |
| EQP-06 | QR label and pickup slip (M) | The whole code is visible and scales; scanning opens Custodian/Scan | P2 |

## RES: borrower reservations

| ID | Case | Expected | P |
|---|---|---|---|
| RES-01 | Browse, search, category filter `[RES]` | Works; unavailable items are greyed | P3 |
| RES-02 | Item details for: under maintenance, broken or retired, damaged condition, no stock, account not active, no custodian (M) | Reserve is disabled with the specific reason; opening `/New` directly redirects back | P1 |
| RES-03…13 | The form's server-side rules: blank fields; quantity 0, negative, decimal or text; quantity above the per-reservation maximum; quantity above stock; past date; lead time; too far ahead; return before pickup; over the student or faculty day limit; open-request limit; overdue block; acknowledgements; invalid pickup point `[RES]` | The rule's message; no stock held | P1 |
| RES-14 | Valid reservation `[RES]` | PENDING; exactly the requested units held; routed to the borrower's custodian; custodian and admins notified | P1 |
| RES-15 | Quantity control (M) | Fixed "1 unit" when only 1 can be requested; otherwise capped; whole form disabled with the reason when nothing can be reserved | P2 |
| RES-16 | Two borrowers reserve the last unit at the same moment (M, two browsers) | Exactly one succeeds; the other sees "just reserved by someone else" | P1 |
| RES-17 | Cancel while PENDING or APPROVED; cancel after release (forged) `[RES]` | The hold is returned exactly; after release the cancel is refused | P1 |
| RES-18 | Details and pickup slip for your own reservation (M) | Shown | P2 |

## CUS: custodian workflow

| ID | Case | Expected | P |
|---|---|---|---|
| CUS-01 | Queue scope `[CUS]` | Carlos sees only his reservations plus the pool; Rina's are hidden | P1 |
| CUS-02 | Claim a pooled reservation; admin opens Claim (M) | It becomes his; admin gets 403 | P2 |
| CUS-03 | Approve: borrower inactive, pickup passed, item out of service (M) | Button disabled with the reason; a forged POST is refused | P1 |
| CUS-04 | Approve `[CUS]` | APPROVED; stock unchanged; borrower notified | P1 |
| CUS-05 | Reject with no reason, then with a reason `[CUS]` | Refused, then REJECTED; hold returned; borrower gets the reason | P1 |
| CUS-06 | Custodian Cancel or Expire with a reason (M) | Hold returned; borrower notified; Expire not allowed after a partial release | P1 |
| CUS-07 | Release a partial quantity, then the rest `[CUS]` | PARTIALLY_RELEASED, then COMPLETED; each release gets its own TXN number; stock moves from reserved to released | P1 |
| CUS-08 | Over-release; release twice; return date already past; release condition Damaged (M) | Each refused; invariant holds | P1 |
| CUS-09 | Scan a reservation number and an asset code; scan an unknown code `[CUS]` | Opens the match or a list; unknown says not found; respects scope | P2 |
| CUS-10 | Low-stock alert after a release (M) | ALERT to custodians and admins when available ≤ threshold | P3 |

## RET: returns and inspections

| ID | Case | Expected | P |
|---|---|---|---|
| RET-01 | Partial then full return `[RET]` | PARTIALLY_RETURNED, then RETURNED; released → awaiting inspection; the return time and receiving custodian come from the server | P1 |
| RET-02 | Over-return; return again after a full return (forged) `[RET]` | Refused; invariant holds | P1 |
| RET-03 | Inspection PENDING, then COMPLETED (M) | PENDING moves no stock; COMPLETED applies the outcome | P1 |
| RET-04 | Outcome split doesn't add up; negative amounts; quantity over what is waiting `[RET]` | Refused | P1 |
| RET-05 | 1 good, 1 damaged, 1 lost (M) | Available +1, maintenance +1, unreconciled +1; borrowing ends LOST; staff ALERT; the item's condition changes only when good units come back | P1 |
| RET-06 | Photos: 7 files, an oversized file, a renamed non-image, a valid photo (M) | Skipped files are reported; valid photos are stored privately and visible to staff in scope only | P2 |
| RET-07 | Penalty recorded (M) | Shown on the borrower's history and in the notification | P3 |

## LIFE: background job (B)

| ID | Case | Expected | P |
|---|---|---|---|
| LIFE-01 | PENDING reservation submitted more than 48h ago (inside the 24h warning window) | One "Expiring soon" notification to its custodian; the next pass sends no duplicate; the "Review within…" badge turns amber | P2 |
| LIFE-02 | PENDING older than 72h, or its pickup time has passed | EXPIRED; hold returned; borrower and custodian notified | P1 |
| LIFE-03 | APPROVED, pickup more than 72h ago, never released | EXPIRED; hold returned. PARTIALLY_RELEASED is never expired | P1 |
| LIFE-04 | Borrowing past due + grace period | OVERDUE once; borrower, custodian and admins notified; that borrower's new reservations are blocked | P1 |
| LIFE-05 | Borrowing due within 24h | One due reminder to the borrower | P3 |

## COM: notifications, announcements, support

| ID | Case | Expected | P |
|---|---|---|---|
| COM-01 | Notification bell and list: unread count; status and type filters; Open marks read and follows the link; Mark all read `[COM]` | Correct; Open only works for your own notifications | P2 |
| COM-02 | Who is notified for each event (M). Check the [notification matrix](#notification-matrix) | Exactly those recipients and nobody else | P1 |
| COM-10 | Create a ticket in each category; VERIFICATION and APPEAL links from the dashboard `[COM]` | OPEN; admins notified; the category is preselected | P2 |
| COM-11 | Admin replies, then the requester replies `[COM]` | First admin reply auto-assigns the ticket; each side is notified | P2 |
| COM-12 | Resolve, then try to reply (M) | RESOLVED; replying is refused; the requester is told to open a new ticket | P2 |
| COM-13 | A custodian uses Support (M) | Sees and replies only to their own tickets | P1 |

### Notification matrix

| Event | Recipients |
|---|---|
| Reservation submitted | Its custodian, or every custodian if it is pooled, plus admins |
| Approved / rejected / released / cancelled or expired by a custodian | Borrower |
| Expired by the job | Borrower and its custodian |
| Expiring soon | Its custodian |
| Overdue | Borrower, its custodian and admins |
| Due soon | Borrower |
| Return recorded / inspection completed | Borrower |
| Damage or loss recorded | Admins and its custodian |
| Low stock | Custodians and admins |
| Verified | Borrower and the newly assigned custodian |
| Rejected | Borrower |
| Admin password reset | That user |
| New assignment / reservation reassigned | That custodian |
| Announcement | Active users in the target roles |
| Ticket created / requester replies | Admins (or the assignee) |
| Admin replies / resolves | Requester |

## MON: monitoring

| ID | Case | Expected | P |
|---|---|---|---|
| MON-01 | Dashboard totals, custodian workload, verification stats `[MON]` | Match SQL counts | P2 |
| MON-02 | Transactions search and tabs; audit log filter and paging `[MON]` | Work | P3 |
| MON-03 | The 4 CSV exports `[MON]` | Download for admin only; values with commas or quotes are escaped | P2 |

## E2E: end-to-end journeys (run the invariant after each step)

- **E2E-1, new student:**
  1. Register.
  2. Admin approves and assigns a custodian.
  3. The student reserves.
  4. The custodian approves, releases, records the return and inspects as good.
  5. Stock ends where it started.
- **E2E-2, faculty:**
  1. Reserve 2 units for 15 days.
  2. Partial release, then the rest.
  3. Two partial returns.
  4. Inspection.
- **E2E-3, damage and loss:**
  1. Inspect with damaged and lost units.
  2. Admin marks the damaged units repaired and writes off the lost ones.
  3. The total drops by the lost count.
- **E2E-4, appeal:**
  1. Register, then the admin rejects.
  2. The user opens an APPEAL ticket.
  3. The admin approves from the Rejected tab.
  4. The user reserves.

## UI

| ID | Case | Expected | P |
|---|---|---|---|
| UI-01 | Every page at 375, 768 and 1440 px `[UI]` | No horizontal scroll | P2 |
| UI-03 | Every input `[UI]` | Has a label | P3 |
| UI-04 | Controls that don't apply (M) | Disabled, with the reason on screen | P2 |
| UI-05 | Dropdowns in Chrome and Edge (M) | The open list has rounded corners to match the closed box; other browsers show the native list | P3 |
