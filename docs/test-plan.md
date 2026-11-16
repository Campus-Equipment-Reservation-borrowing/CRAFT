# CRAFT functional test plan

A reusable checklist for a full manual or scripted pass. Run it against a local build:

```
dotnet run --project ASI.Basecode.WebApp
```

Then open https://localhost:59360. Back up `AllianceDB` before you start, because the pass creates test data.

## Accounts
All demo accounts use the password `Campus123`.

| Role | ID |
|---|---|
| Admin | `ADMIN-0001` |
| Custodian | `EMP-1001` |
| Student | `2023-104857` |
| Faculty | `EMP-2001` |
| Unverified student | `2024-118002` |

## Checks applied to every form
- [ ] Required fields are marked, and the server enforces them when the browser check is bypassed.
- [ ] Text inputs carry a `maxlength` that matches the server limit.
- [ ] Number inputs have a `min`/`max`; values of 0, negatives, decimals and text are rejected.
- [ ] Date and time rules hold: no past dates, the return comes after the borrow, and the policy maximums are applied.
- [ ] An empty select option is rejected; a forged option value is rejected.
- [ ] An invalid post shows an inline message and keeps the entered values.
- [ ] A valid post shows a success toast or redirects.
- [ ] A post without the anti-forgery token returns 400.
- [ ] A double-click doesn't double-submit.

## Checks applied to every page
- [ ] Returns 200, with no broken links or images.
- [ ] A role that isn't allowed is sent to AccessDenied (403); an anonymous user is sent to Login with a ReturnUrl.
- [ ] No horizontal scrolling at 375 px wide.
- [ ] Search inputs survive SQL-like text, HTML and very long input (no errors, no injected script).

## Account
- [ ] Register:
  - each required field
  - School ID format (4–20 characters; letters, digits and dashes)
  - email format
  - password rules
  - confirm password mismatch
  - privacy consent
  - duplicate ID and duplicate email
- [ ] Login:
  - blank fields
  - wrong password
  - unknown user
  - sign in by email
  - ReturnUrl, and that an external ReturnUrl is ignored
  - lockout after 5 failed attempts
- [ ] Forgot and reset password:
  - blank, malformed and unknown email
  - known email produces a link
  - password mismatch
  - weak password
  - successful reset
  - a used or invalid token is refused
- [ ] Forced password change after an admin reset; Logout; AccessDenied.

## Borrower (student, faculty, unverified)
- [ ] The dashboard and its overdue banner.
- [ ] Available equipment: search, category filter, paging.
- [ ] EquipmentDetails.
- [ ] The New reservation form checks:
  - all fields
  - quantity limits (policy and availability)
  - acknowledgements
  - dates: past, return before borrow, longer than policy, more than 30 days ahead
  - purpose longer than 500 characters
  - an invalid pickup location
  - the open-request limit
  - the overdue block
  - an unverified account
- [ ] My Reservations, Details, Slip, Cancel (and cancelling twice).
- [ ] Borrowings: Mine, Details, History.
- [ ] Profile: contact validation and save; notification preferences; change password (wrong current, mismatch, weak, same as old).
- [ ] Support: Create (blank, invalid category, overlong, HTML in subject); Reply (blank); another user's ticket is refused.
- [ ] Notifications:
  - Open goes to the right page and marks the item read
  - another user's notification is refused
  - MarkRead and MarkAllRead
  - filters
  - the bell count

## Custodian
- [ ] Reservation queue, Review, Approve (with and without notes, including very long notes), Reject and Cancel (reason required).
- [ ] Release: quantity 0, over the approved amount, no condition, valid, and releasing twice.
- [ ] Return: quantity 0, over the released amount, a future return date, valid, and returning again.
- [ ] Inspect:
  - good, damaged and lost must add up to the inspected count
  - repair cost and penalty can't be negative
  - can't inspect more than was returned
  - photo type and size
- [ ] Scan; Borrowings list.

## Equipment (admin and custodian)
- [ ] Create, Edit, Details, Label, Delete (unused record, and a record in use).
- [ ] Required fields, quantity range, unique asset code.
- [ ] Image upload: wrong file type and file over 5 MB must show an error.

## Admin
- [ ] Users:
  - Create (required fields, ID format, duplicate ID or email, weak temporary password)
  - Edit (invalid email)
  - Details and History
  - ResetPassword and InviteLink
  - Delete (own account, and a user with history)
- [ ] Verifications: Verify (and verifying twice), Reject (reason required).
- [ ] Custodian assignments: Assign (blank, duplicate), Reassign, Unassign.
- [ ] Announcements: Publish (blank, no audience, expiry in the past), Delete.
- [ ] Settings:
  - publish a policy; no-op publish; out-of-range values
  - category and location add, duplicate, invalid code and delete
- [ ] Monitoring: Dashboard, Transactions, Reports (reversed date range), AuditLogs filter, all four CSV exports.
- [ ] Operator select.

## End to end
1. [ ] The student reserves.
2. [ ] The custodian approves; the student is notified and the notification opens the reservation.
3. [ ] The custodian releases; the student is notified.
4. [ ] The custodian records the return; the inspection completes; inventory balances return to their starting values.
5. [ ] Overdue: set `due_at_utc` in the past. The lifecycle job notifies the borrower and staff within 5 minutes, the borrower's next reservation is blocked, and the dashboard shows the item as overdue.
