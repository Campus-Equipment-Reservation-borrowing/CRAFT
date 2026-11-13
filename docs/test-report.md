# CRAFT functional test report: 2026-11-14

- **Build:** `demo/feat-ux-cleanup` @ `a460ca1`.
- **Environment:** local, LocalDB `AllianceDB`, https://localhost:59360.
- **Method:** page crawls per role, plus scripted form posts with the anti-forgery token, in the built-in browser.
- **Backup:** the DB was backed up before the pass to `C:\tmp\craftbak\AllianceDB_pretest.bak`.

## Summary
- **Pages:** 157 pages crawled across the student, custodian and admin roles. All returned 200, with no broken images.
- **Access control:** roles that aren't allowed get 403 (AccessDenied); anonymous users go to Login. Posts without the anti-forgery token return 400.
- **No horizontal scrolling at 375 px wide** on these borrower pages: the dashboard, Available, the New reservation form, Mine, Borrowings, Notifications, Profile and Support.
- **Results:** about 150 cases run. Most pass. **19 defects** were found (listed below).

## Defects

### High
- **D1: Reservation times shift by +8 hours.**
  - **Steps:** reserve with borrowing time 09:00 on 20 Nov.
  - **Result:** My Reservations and the custodian queue show "20 Nov 2026, 05:00 PM".
  - **Cause:** the form's local time is stored as if it were UTC, then shown with `ToLocalTime()`.
  - **Knock-on effect:** the 1-day lead-time check is also evaluated against the wrong clock. A request for "today 23:00" was accepted, and is shown as 15 Nov 07:00 AM.
  - **Same problem in Returns:** the "Returned at" default shows the UTC value in a local datetime field.
- **D2: Announcement notifications send borrowers to an admin-only page (403).**
  - This is a regression from `a460ca1`: the fallback link for the ANNOUNCEMENT type is `/Announcements`, which only admins can open.
- **D3: Approve with long notes saves the approval, then fails with "Server error occured!".**
  - **Steps:** approve with 3,000 characters of notes.
  - **Result:** the reservation becomes APPROVED, but the audit log entry and the borrower notification are lost, because the notes overflow the 1,000-character audit/notification text. The notes box has no `maxlength`.
- **D4: Deleting a user who has history shows "Server error occured!".**
  - **Steps:** create a user, issue a temporary password, then delete.
  - **Result:** the delete fails because other records reference the user, but the admin only sees a generic error.

### Medium
- **D5: Reset and forgot-password errors are invisible.**
  - The views only render `ModelOnly` errors.
  - A "Passwords do not match" error on reset shows nothing.
  - A malformed email on Forgot Password shows nothing.
  - An invalid or used reset token silently redirects to Forgot Password, with no explanation.
- **D6: Equipment image errors are invisible.**
  - The error for a wrong file type (`.exe`) or a file over 5 MB is added under the key `ImagePath`, but the form has no message element for `ImagePath`.
  - The form just reloads with no message.
- **D7: The app reveals whether an account exists.**
  - Login says "User Account does not exist." for an unknown ID, versus "Invalid username or password." for a wrong password.
  - Forgot Password says "User Account does not exist." for an unknown email, while its own text for a known email says "If that email is registered…".
- **D8: Returning 0 units shows "Server error occured!"** instead of a validation message.
- **D9: A return date in the future (2027-01-01) is accepted.**
- **D10: The admin user form doesn't apply the School ID rules.**
  - Users/Create accepts the identifier `bad id !!` (`maxlength` 100, no format check). Registration enforces 4–20 characters with letters, digits and dashes only.

### Low
- **D11:** Change password with mismatched new passwords says "Please complete all required fields." instead of "Passwords do not match."
- **D12:** Replying to a ticket with an empty body says "Choose a category and enter a subject and message."
- **D13:** Support Create with an invalid category value reloads the form with no message.
- **D14:** Register with every field blank shows "Please complete all required fields." six times.
- **D15:** These text fields have no `maxlength`:
  - the custodian Approve notes and the Reject/Cancel reason boxes
  - Scan code
  - the Profile contact numbers (the server rejects bad values, but typing isn't limited)
  - every search box
- **D16:** An announcement can be published with an expiry date in the past.
- **D17:** Staff overdue notifications have no link (only the borrower's does).
- **D18:** The Release condition list offers "Lost", "Damaged" and "Under Maintenance" as the condition at release.
- **D19:** Inspection photos with a disallowed extension are skipped silently instead of reported. Allowed photos are checked by file extension only, not by content.

## Passed areas (highlights)
- **Register:** required fields, ID length and format, email format, password rules, mismatch, privacy consent, duplicate ID or email, valid registration.
- **Login:** blank, wrong password, sign in by email, ReturnUrl, an external ReturnUrl is ignored, lockout after 4 failed attempts (15 minutes), and the lockout holds even with the correct password.
- **Reset password:** a weak password is rejected, a valid reset works, and a used token is rejected.
- **Reservation form:** every required field, quantity 0, negative, decimal and text values, quantity over the policy limit, past date, return before borrow, duration over the policy, more than 30 days ahead, purpose over 500 characters, invalid pickup location, open-request limit (3), overdue block, unverified account block. A valid request with both acknowledgements now succeeds.
- **Reservation pages:** Cancel (and cancelling twice), Details, Slip; another user's or a missing reservation redirects back.
- **Search:** SQL-like text, HTML and 2,000-character input are handled safely; ticket subjects with HTML are escaped.
- **Profile:** contact validation and save, all change-password rules.
- **Notifications:** Open (links work and the item is marked read), another user's id refused, MarkAllRead, filters.
- **Custodian:** a reject reason is required; release quantity, condition and double-release rules; inspection sum, negative costs and over-inspect rules.
- **Equipment:** required fields, quantity range, duplicate asset code, Create, Edit, Label, Delete, and delete of an in-use record is blocked with a clear message.
- **Admin users:** required fields, duplicate ID, weak temporary password, invalid email on edit, ResetPassword, InviteLink, and an admin can't delete their own account.
- **Verifications:** Verify, verify twice, a reject reason is required.
- **Custodian assignments:** Assign, duplicate assign, Unassign.
- **Announcements:** validation, publish, delete.
- **Settings:** policy publish, no-op publish, range errors; category and location add, duplicate and delete.
- **Monitoring:** all four CSV exports download with headers; the Reports and AuditLogs filters work.
- **Operator:** select works.
- **End to end:**
  1. Reserve.
  2. Approve; the borrower is notified.
  3. Release; the borrower is notified.
  4. Return and inspection; the inventory balances return to their starting values.
  5. Overdue: after the due date was moved into the past in SQL, the lifecycle job notified the borrower (with a link) and the staff within 5 minutes. The borrower's next reservation was blocked, and the dashboard shows the item as overdue.

## Test data left in the DB
- **Restore option:** restore the pre-test backup to clear all of it: `RESTORE DATABASE AllianceDB FROM DISK='C:\tmp\craftbak\AllianceDB_pretest.bak' WITH REPLACE`, with the app stopped.
- **Accounts created:** `2026-900001` (registered and verified), and `2026-900100`/`QA User` (admin-created, delete failed per D4).
- **Accounts changed:**
  - `ADMIN-0001` now acts as operator Rina Bautista (EMP-1002); switch this back on `/Operator`.
  - One existing custodian assignment was unassigned during the test, and a CATEGORY assignment was added.
- **Policy:** versions v1.2 and up (values now 7 days, 3 units, 3 open requests).
- **Reservations and borrowings:** RSV-2026-0001 through 0003, TXN-0001 (returned) and TXN-0002 (released, made overdue).
- **Other records:** QA support tickets, an announcement (deleted), and audit and notification rows.
