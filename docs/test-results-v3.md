# CRAFT test results v3

Plan: [test-plan-v3.md](test-plan-v3.md). Automated suite: [`testing/craft-suite.js`](testing/craft-suite.js), run against LocalDB `AllianceDB` (Development build).

| Suite | Checks | Baseline (before fixes) | Final (after access fixes + restructure) |
|---|---|---|---|
| AUTH | 27 | 26/27 (AUTH-19: test data) | 27/27 |
| RBAC | 43 | 46/46 | 43/43 (retired /Operator removed) |
| ACL | 245 | 239/245 | 245/245 |
| EQP | 10 | 10/10 | 10/10 |
| RES | 26 | 26/26 | 26/26 |
| CUS | 18 | 18/18 | 18/18 |
| RET | 11 | crashed (outdated return-time check) | 11/11 |
| ADM | 35 | 25/33 (outdated: verify needs custodian, operator retired) | 35/35 |
| COM | 16 | 16/16 | 16/16 |
| PRF | 10 | 10/10 | 10/10 |
| MON | 14 | 14/14 | 14/14 |
| SEC | 4 | 4/4 | 4/4 |
| **Total** | **459** | | **459/459** |

Stock invariant (both queries in the plan's section 0): 0 rows after the final run.

## Baseline defects (all fixed)
| Case | Defect | Fix |
|---|---|---|
| ACL-20 | Custodian could open details and pickup slip of reservations routed to another custodian | Same scope rule as Review |
| ACL-22, ACL-24 | Every custodian could read, answer and resolve every support ticket | Support handled by admins; custodians see their own tickets |
| ACL-23 | Any custodian could adjust stock | Admin-only; buttons hidden |
| ACL-40 | Inspection photos were public files under wwwroot | Private storage + staff-only, scope-checked, no-store route |

## Manual cases
Cases marked (M) and (B) in the plan were exercised during development walkthroughs (expiry, warnings, verification with custodian, stock adjustments, QR label, dropdown). Re-run them before a release.
