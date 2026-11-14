# CRAFT test report v2: 2026-11-14

> **Update, after the fixes: 248 of 248 automated checks pass.** All the defects listed below (D5–D8, D10–D12, D14–D21) are fixed. One more was found and fixed along the way: the Notifications "Borrowings" filter matched nothing, and the Support, Announcement and Alert types couldn't be filtered.
>
> The suite was re-run from the restored baseline, and the inventory invariant held. The results below are from the first run, before the fixes.

## Run details
- **Build:** branch `demo-v2` @ `6ad15aa`, running at https://localhost:59360 on LocalDB.
- **Baseline:** the run started from a restored baseline, `AllianceDB_v2_pretest.bak`. The database was restored to it again afterwards, so the run left no test data behind.
- **Plan:** [`test-plan-v2.md`](test-plan-v2.md).
- **Harness:** [`testing/craft-suite.js`](testing/craft-suite.js). Paste it into the browser console on any app page, then run `await CRAFT.runAll()`.

## Results
**243 automated checks: 229 pass, 14 fail.** Every failure is either a known v1 defect that's still open, or one of two new minor findings (D20, D21). No P1 data or inventory defect was found.

| Suite | Pass | Fail | Notes |
|---|---|---|---|
| AUTH | 25 | 3 | D5, D7 (×2) |
| RBAC | 46 | 0 | Anonymous/student/custodian route matrix, token check, foreign and missing ids |
| RES | 26 | 0 | Every reservation rule, including faculty 21 days, the open-request limit and local times |
| CUS | 17 | 0 | Claim, approve, reject, expire, release limits, release twice, scan |
| RET | 8 | 2 | D20 (new), D8 |
| EQP | 9 | 1 | D6 |
| ADM | 30 | 2 | D10, D16 |
| COM | 14 | 0 | Tickets in all 4 categories, replies both ways, resolve, every notification link opens (no 403) |
| PRF | 9 | 1 | D11 |
| MON | 14 | 0 | Pages, reversed date range, audit rows, 4 CSV exports, no formula injection |
| SEC | 4 | 0 | Stored HTML/script escaped, SQL-like search, auth cookie not readable by script |
| UI | 27 | 5 | No horizontal scroll at 375/768/1440 px on all 8 borrower pages; D21 (new) |

### Manual and SQL-assisted cases
| ID | Result | Evidence |
|---|---|---|
| **E2E-1:** student clean path | Pass | Reserve → approve → release → return → inspect good. Units returned to available. |
| **E2E-2:** faculty, 2 units over 15 days, partial release | Pass | Released 1 + 1 (two transactions); the reservation completed with 0 held; returned and inspected separately; 1 damaged unit went to maintenance. |
| **E2E-3:** damage and loss | Pass | 2 units: 1 damaged (maintenance +1), 1 lost (unreconciled +1). The staff "Damage/loss recorded" alert and the borrower's inspection notice with the penalty both arrived. |
| **Inventory invariant** after every step | Pass | The invariant query returned no rows; no negative counts. |
| **LIFE-01:** auto-cancel a pending request older than 24 hours | Pass | RSV-2026-0018 became EXPIRED by the worker, its held unit was released, and the borrower was notified with a link. |
| **LIFE-02:** due reminder within 24 hours | Pass | One "Return due soon" notification, linking to `/Borrowings/Details/2`. |
| **LIFE-03:** overdue notice | Pass (v2 run of 2026-11-14) | Borrower and staff were notified; the borrower was blocked from reserving. |
| **LIFE-04:** low-stock alert | Not run | Needs stock driven below the threshold; left for the next run. |
| **AUTH-12/17/20/21:** lockout clear, expired token, inactive login, Remember me | Not run | Need SQL setup; listed in the plan as (M). |

## Defects
### Still open from v1
| ID | Severity | Summary |
|---|---|---|
| D5 | Medium | The reset-password form doesn't show "Passwords do not match" (only summary errors are rendered). |
| D6 | Medium | Equipment image errors (wrong type, too large) are never shown on the form. |
| D7 | Medium | Login and Forgot Password say "User Account does not exist.", which reveals that the account doesn't exist. |
| D8 | Medium | Returning 0 units gives "Server error occured!" instead of a validation message. |
| D10 | Medium | The admin's Create User form accepts the ID `bad id!!`; registration's ID rules aren't applied there. |
| D11 | Low | Change password with a mismatch says "Please complete all required fields." |
| D16 | Low | An announcement can be published with an expiry date in the past. |
| D12, D14, D15, D17, D18, D19 | Low | Unchanged from v1 (wrong wording, missing `maxlength`, no link on staff overdue notices, the release condition list, silent photo skips). |

### New in v2
- **D20 (Medium): no Record return action on the custodian Borrowings list.**
  - **Steps:** sign in as `EMP-1001` and open `/Custodian/Borrowings`.
  - **Result:** the active rows have no link or button. A return can only be started by scanning the asset code, which opens the form when it finds exactly one match, or by typing `/Returns/Create/{id}`.
- **D21 (Low): some inputs have no label for screen readers.**
  - The search and filter inputs on Available, My Reservations, My Borrowings and Notifications have no `<label>` or `aria-label`.
  - The Pickup Point select on the reservation form has a visible label that isn't linked to it (it lacks `for`/`asp-for`).

### Observations (not defects)
- **Scan doesn't look up transaction numbers.** It matches reservation numbers (on the pickup slip) and asset codes (on the label) only, so typing `TXN-…` returns "no match". This matches the page's description, but custodians may expect it to work.

## v1 defects confirmed fixed
- **D1:** times are no longer shifted, and the lead time is enforced (RES-15, RES-07b).
- **D2:** every notification link opens without a 403 (COM-05b).
- **D3:** notes or reasons over 500 characters are refused before saving (CUS-03a).
- **D4:** a user with a password reset can be deleted, and a user with transactions is blocked with a clear message (ADM-04b/c).
- **D9:** a future return date is refused (RET-02).
- **D13:** an invalid ticket category now shows a message (COM-01b).
