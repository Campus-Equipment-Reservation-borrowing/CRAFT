# CRAFT test plan v2: functional and business-rule coverage

Expected results below come from the rules in the code: the services, the `Errors.resx` messages and the controllers.

- **Automated cases** are in [`testing/craft-suite.js`](testing/craft-suite.js).
- **Manual or SQL-assisted cases** are marked **(M)**.
- **Priority:** P1 protects data or inventory; P2 covers validation and messages; P3 is cosmetic.

## Environment
1. **Restore a known baseline,** with the app stopped:
   ```
   RESTORE DATABASE AllianceDB FROM DISK='C:\tmp\craftbak\AllianceDB_v2_pretest.bak' WITH REPLACE
   ```
2. **Start the app:** `dotnet run --project ASI.Basecode.WebApp`, then open https://localhost:59360.
3. **Run the suite:** paste `docs/testing/craft-suite.js` into the browser console on any app page, then run `await CRAFT.runAll()`. You can also run one suite at a time, for example `await CRAFT.suites.RES()`.

**Accounts** (password `Campus123`):

| Account | Role |
|---|---|
| `ADMIN-0001` | Admin |
| `EMP-1001`, `EMP-1002` | Custodian |
| `EMP-2001` | Faculty |
| `2023-104857` | Student |
| `2024-118002` | Unverified student |

**Inventory invariant**, checked after every stock-moving step (P1):
```sql
SELECT * FROM equipment_inventory_balances
WHERE reserved_quantity < 0 OR released_quantity < 0 OR awaiting_inspection_quantity < 0
   OR maintenance_quantity < 0 OR unreconciled_quantity < 0
   OR reserved_quantity + released_quantity + awaiting_inspection_quantity + maintenance_quantity + unreconciled_quantity > total_quantity
```
The query must return no rows.

## AUTH: authentication
| ID | Case | Expected | P |
|---|---|---|---|
| AUTH-01 | Register with all fields blank | Required-field messages, no account created | P2 |
| AUTH-02 | School ID of 3 characters / 21 characters / containing `$` | "4 to 20 characters" / "letters, digits and dashes only" | P2 |
| AUTH-03 | Invalid email | Email message | P2 |
| AUTH-04 | Password without an uppercase letter or number, or shorter than 8 characters | Password rule message | P2 |
| AUTH-05 | Confirm password mismatch | "Passwords do not match." | P2 |
| AUTH-06 | Privacy consent unchecked | Consent message; the privacy notice is present on the page | P2 |
| AUTH-07 | Duplicate ID / duplicate email | "already exists" | P1 |
| AUTH-08 | Valid registration | Redirect to Login; the account is UNVERIFIED | P1 |
| AUTH-09 | Log in by ID and by email | Lands on the role's dashboard | P1 |
| AUTH-10 | Wrong password / unknown user | Generic message (the unknown-user wording is known defect D7) | P2 |
| AUTH-11 | 5 wrong passwords | Locked for 15 minutes; the correct password is also refused | P1 |
| AUTH-12 | (M) Clear `lockout_until_utc` | Login works again | P2 |
| AUTH-13 | Local ReturnUrl / external ReturnUrl | Goes to the local URL / the external one is ignored | P1 |
| AUTH-14 | Logout, then open a protected page | Login page with a ReturnUrl | P1 |
| AUTH-15 | Forgot password: known / unknown / blank email | Link issued / message / required | P2 |
| AUTH-16 | Reset: mismatch, weak password, valid, reuse the token | Error / error / success / refused | P1 |
| AUTH-17 | (M) Reset token with `expires_at_utc` in the past | Refused | P1 |
| AUTH-18 | Admin issues a temporary password, user logs in with it | Forced to the Change Password page before any other page | P1 |
| AUTH-19 | Unverified student logs in and tries to reserve | Login works; reserving is refused with "pending verification" | P1 |
| AUTH-20 | (M) Inactive or suspended account logs in | Refused | P1 |
| AUTH-21 | Remember me | The auth cookie is persistent; without Remember me it lasts the session | P3 |

## RBAC: role access and object ownership
| ID | Case | Expected | P |
|---|---|---|---|
| RBAC-01 | Anonymous user opens every protected route | Login redirect | P1 |
| RBAC-02 | Student opens staff/admin routes (Custodian, Returns, Equipment, Users, Settings, Monitoring, Announcements, CustodianAssignments, Operator) | 403 AccessDenied | P1 |
| RBAC-03 | Custodian opens admin routes (Users, Settings, Monitoring, Announcements, CustodianAssignments, Operator) | 403 | P1 |
| RBAC-04 | Custodian or admin opens borrower-only routes (Reservations/New, Mine) | 403 | P2 |
| RBAC-05 | A mutating POST without the anti-forgery token | 400 | P1 |
| RBAC-06 | Student opens another user's reservation, borrowing, ticket or notification by id | Redirect away; no data shown | P1 |
| RBAC-07 | Student posts Cancel for another user's reservation | Refused; status unchanged | P1 |
| RBAC-08 | Custodian `EMP-1001` approves a reservation routed to `EMP-1002` | "assigned to another custodian" | P1 |

## RES: reservations
| ID | Case | Expected | P |
|---|---|---|---|
| RES-01 | Available: search, category filter, paging | Results narrow correctly | P2 |
| RES-02 | An item with 0 available, or under maintenance | Reserve is unavailable, with a reason shown | P1 |
| RES-03 | Reservations/New for a missing or inactive item | Redirect to Available | P2 |
| RES-04 | Every required field blank | One message per field | P2 |
| RES-05 | Quantity 0 / negative / decimal / text | Rejected | P2 |
| RES-06 | Quantity above the per-reservation maximum / above availability | Rejected with the limit shown | P1 |
| RES-07 | Borrow time in the past / less than 1 day of lead time / more than 30 days ahead | Rejected | P1 |
| RES-08 | Return before the borrow time | Rejected | P2 |
| RES-09 | Student duration over 7 days; faculty over 21 days (faculty 15 days is allowed) | Rejected / accepted | P1 |
| RES-10 | Fourth open request (limit 3) | Rejected | P1 |
| RES-11 | Borrower has an overdue item | Rejected | P1 |
| RES-12 | Acknowledgement unchecked | Rejected; both checked, the request is accepted | P1 |
| RES-13 | Invalid or inactive pickup location | Rejected | P2 |
| RES-14 | Valid request | PENDING; the units are held (reserved +qty); custodians are notified | P1 |
| RES-15 | Times are displayed exactly as entered | 09:00 shows as 09:00 AM | P1 |
| RES-16 | Details and Slip | Pages render | P3 |
| RES-17 | Cancel while pending | CANCELLED; held units released | P1 |
| RES-18 | Cancel after release / cancel twice | Refused | P1 |

## CUS: custodian workflow
| ID | Case | Expected | P |
|---|---|---|---|
| CUS-01 | Queue search and status filter | Narrows correctly | P2 |
| CUS-02 | Claim a pooled (unassigned) reservation | Assigned to the claimer; another custodian is then refused | P1 |
| CUS-03 | Approve, with notes up to 500 characters / over 500 | Approved, borrower notified / refused | P1 |
| CUS-04 | Reject without a reason / with a reason | Required / REJECTED, units released, borrower notified | P1 |
| CUS-05 | Approve or reject a reservation that isn't pending | "not pending" | P1 |
| CUS-06 | Cancel an approved reservation with a reason | CANCELLED; units released | P1 |
| CUS-07 | Expire an approved no-show / expire after release | EXPIRED / refused | P1 |
| CUS-08 | Release: quantity 0, more than approved, no condition | Rejected | P1 |
| CUS-09 | Release valid | Borrowing ACTIVE; reserved goes to released; borrower notified | P1 |
| CUS-10 | Release twice | Refused | P1 |
| CUS-11 | Scan an asset code / a transaction number / an unknown code / an empty code | Finds the item / finds the borrowing / not found / no error | P2 |

## RET: returns and inspection
| ID | Case | Expected | P |
|---|---|---|---|
| RET-01 | Return quantity 0 / more than outstanding | Rejected with a message (quantity 0 was "Server error" in v1, D8) | P1 |
| RET-02 | Return dated in the future | Rejected | P1 |
| RET-03 | Return the full quantity | Released goes to awaiting inspection; borrowing returned | P1 |
| RET-04 | Partial return of 1 of 2 units | Borrowing stays open for the remaining unit | P1 |
| RET-05 | Late return | The return is flagged late, with the days overdue | P2 |
| RET-06 | Inspect: good + damaged + lost ≠ inspected | Rejected | P1 |
| RET-07 | Inspect: negative cost or penalty / more units than returned | Rejected | P1 |
| RET-08 | Inspect all good | Awaiting inspection goes back to available; borrower notified | P1 |
| RET-09 | Inspect 1 damaged + 1 lost | Maintenance +1, unreconciled +1; staff alert raised | P1 |
| RET-10 | Photo `.exe` / over 5 MB / valid `.png` | Skipped or rejected / rejected / stored | P2 |

## EQP: equipment
| ID | Case | Expected | P |
|---|---|---|---|
| EQP-01 | Create with blank fields / quantity 0 or over 10,000 / a duplicate asset code | Rejected | P2 |
| EQP-02 | Create valid, with a PNG image | Created; the image is served | P2 |
| EQP-03 | Image of the wrong type / over 5 MB | A visible error (missing in v1, D6) | P2 |
| EQP-04 | Edit; remove the image | Saved | P2 |
| EQP-05 | Lower the total quantity below the units in use | "below committed" | P1 |
| EQP-06 | Delete an unused item / an item that has been used | Deleted / blocked with a message | P1 |
| EQP-07 | Label, Details, filters | Pages render; filters narrow | P3 |

## ADM: administration
| ID | Case | Expected | P |
|---|---|---|---|
| ADM-01 | Create a user: blank fields / duplicate / weak temporary password / bad ID format | Rejected (the ID format check is missing, D10) | P2 |
| ADM-02 | Create a valid user | Created, with the temporary password flag set | P1 |
| ADM-03 | Edit a user: invalid email; change the role | Rejected / the user's access changes to match the new role | P1 |
| ADM-04 | Delete yourself / the last admin / a user with transactions / a fresh user | Blocked / blocked / blocked with a message / deleted | P1 |
| ADM-05 | Reset password, then the user logs in | Forced password change | P1 |
| ADM-06 | Invite link | A link is generated and can be used to set a password | P2 |
| ADM-07 | Verify / verify twice / reject without a reason / reject with a reason | Verified / "not awaiting verification" / required / REJECTED, user notified | P1 |
| ADM-08 | Rejected student opens an appeal ticket | The ticket shows a verification shortcut for staff | P2 |
| ADM-09 | Assign a custodian to a borrower / location / category; duplicate; unassign; reassign a reservation | Routing follows the assignment; the duplicate is refused | P1 |
| ADM-10 | Announcement: blank / no audience / valid for students | Rejected / rejected / only students see the banner and get a notification | P2 |
| ADM-11 | Announcement with an expiry date in the past | It shouldn't be published (D16) | P3 |
| ADM-12 | Policy: out-of-range value / no change / valid change | Rejected / "No changes" / new version, and the reservation form uses it | P1 |
| ADM-13 | Category and location add, duplicate, delete, delete in use | Per the messages | P2 |
| ADM-14 | Operator: act as a custodian | Banner shows the acting user; actions are attributed to them in the audit log | P2 |

## MON: monitoring
| ID | Case | Expected | P |
|---|---|---|---|
| MON-01 | Dashboard KPIs | Match SQL counts | P2 |
| MON-02 | Transactions tabs, Reports (normal and reversed date range), Audit log filters | Render without errors | P2 |
| MON-03 | Every mutating action in this plan | An `audit_logs` row exists for it | P1 |
| MON-04 | The four CSV exports | 200, `text/csv`, a header row, commas and quotes escaped, a value starting with `=` is neutralised | P2 |

## COM: support and notifications
| ID | Case | Expected | P |
|---|---|---|---|
| COM-01 | Ticket: blank / invalid category / subject over 200 characters | Rejected (an invalid category shows no message, D13) | P2 |
| COM-02 | Create a ticket in each category | Created; admins notified | P2 |
| COM-03 | Staff reply, then borrower reply | The other side is notified, with a link to the ticket | P1 |
| COM-04 | Resolve, then reply | "closed" | P2 |
| COM-05 | Each notification type opens the right page | Reservation, Return, Overdue, DueReminder, Support, Account and Announcement links all work | P1 |
| COM-06 | MarkRead, MarkAllRead, filters, bell count | Counts update | P2 |
| COM-07 | Turn a notification type off in preferences, then trigger it | No notification created | P1 |

## PRF: profile
| ID | Case | Expected | P |
|---|---|---|---|
| PRF-01 | Contact number blank / letters / 200 characters / valid | Rejected / rejected / rejected / saved and persisted | P2 |
| PRF-02 | Change password: wrong current / mismatch / weak / same as old / valid | Messages; afterwards the new password logs in | P1 |

## LIFE: lifecycle jobs (M, needs the worker interval)
| ID | Case | Expected | P |
|---|---|---|---|
| LIFE-01 | A pending reservation submitted more than 24 hours ago | Auto-cancelled to EXPIRED, held units released, borrower notified once | P1 |
| LIFE-02 | A borrowing due within 24 hours | Reminder sent once | P2 |
| LIFE-03 | A borrowing past due plus the grace period | Overdue notice sent once to the borrower and staff | P1 |
| LIFE-04 | Available stock at or below the low-stock threshold | Low-stock alert sent to staff | P2 |

## E2E: end-to-end journeys (balances checked after each step)
- **E2E-1, student (clean path):** reserve → approve → release → return → inspect all good. Balances end where they started.
- **E2E-2, faculty (long loan):** reserve 2 units for 15 days → approve → release → return 1 → return 1 → inspect.
- **E2E-3, damage and loss:** reserve 2 → release → return 2 → inspect with 1 damaged and 1 lost. Maintenance +1, unreconciled +1; the staff alert links back.

## UI and SEC
| ID | Case | Expected | P |
|---|---|---|---|
| UI-01 | Every page at 375, 768 and 1440 px | No horizontal scroll | P2 |
| UI-02 | Every text input | Has a `maxlength` | P3 |
| UI-03 | Every input | Has an associated label | P3 |
| SEC-01 | HTML/script entered in names, tickets, announcements, notes and purpose | Rendered escaped everywhere | P1 |
| SEC-02 | SQL-like text in search boxes | No error | P1 |
| SEC-03 | Auth cookie flags | HttpOnly, Secure, SameSite set | P1 |
