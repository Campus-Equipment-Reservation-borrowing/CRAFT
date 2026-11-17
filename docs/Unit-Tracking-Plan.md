# Plan: give every physical unit its own identifier

## The problem

Today an `equipment` row is a *model*, not a thing you can hold. `LP-001` "Dell Latitude 5440" has
`total_quantity = 10`, and every downstream record only stores a count:

| Record | What it stores today | What's missing |
|---|---|---|
| `equipment_inventory_balances` | how many are available, released, awaiting inspection, in maintenance, unreconciled | *which* 10 laptops exist |
| `borrowings.released_quantity` | "2 laptops went to Maria" | which 2 |
| `borrowing_returns.returned_quantity` | "1 came back" | which one, and whether it's the one she took |
| `condition_inspections` good / damaged / lost counts | "1 damaged" | which unit is damaged, so maintenance can find it |
| `inventory_movements.quantity` | "2 moved to maintenance" | an audit trail per unit |
| QR label (`Equipment/Label`) | one code for the whole model | a label you can stick on each unit |

So a custodian can't tell which laptop was damaged, the borrower could hand back a different unit
than they took, and a lost unit can't be named.

## The goal

Every physical unit gets a unique, printable code, such as `LP-001-03`, and an optional
manufacturer serial number. Release, return, inspection, maintenance and lost/found then act on
named units. The counts on the dashboards stay, but they are derived from the units.

Reservations stay quantity-based. A borrower asks for "2 laptops", not for specific ones. The
custodian picks the actual units at release.

## Data model

### New table `equipment_units`

| Column | Type | Notes |
|---|---|---|
| `unit_id` | bigint identity PK | |
| `equipment_id` | bigint FK → `equipment` | the model this unit belongs to |
| `unit_code` | nvarchar(60), **unique** | `{asset_code}-{seq:00}`, e.g. `LP-001-03`; printed on the QR label |
| `serial_number` | nvarchar(100), null, unique when not null | manufacturer serial, optional |
| `state` | nvarchar(30) | `AVAILABLE`, `RELEASED`, `AWAITING_INSPECTION`, `MAINTENANCE`, `LOST`, `RETIRED` |
| `condition_id` | bigint FK → `conditions` | physical condition of *this* unit |
| `location_id` | bigint FK, null | defaults to the model's location |
| `notes` | nvarchar(500), null | |
| `created_at_utc`, `retired_at_utc` | datetime2 | |
| `row_version` | rowversion | concurrency guard, as on `equipment` |

The state names match the existing `inventory_movements.from_state` / `to_state` strings, so nothing
new has to be learned.

### New table `borrowing_units` (which units went out on which borrowing)

| Column | Type | Notes |
|---|---|---|
| `borrowing_id` + `unit_id` | composite PK | |
| `release_condition_id` | FK → `conditions` | condition when handed over |
| `return_id` | FK → `borrowing_returns`, null | set when this unit comes back |
| `inspection_id` | FK → `condition_inspections`, null | set when it is inspected |
| `outcome` | nvarchar(20), null | `GOOD`, `DAMAGED` or `LOST`, from the inspection |
| `outcome_condition_id` | FK → `conditions`, null | damage level for this unit |

There is also a filtered unique index: one open row per unit (`return_id IS NULL`). The database then
refuses to release the same unit twice. This is a DB constraint rather than app code.

### Existing tables

- **`inventory_movements`**: add a nullable `unit_id`. A movement then names the unit; quantity stays 1 per row.
- **`condition_inspections`**: keep the good / damaged / lost counts as a summary. The per-unit truth lives in `borrowing_units`.
- **`equipment_inventory_balances`**: keep it as a cache, so the dashboards and queries don't change, but update it from unit state changes in the same transaction. A nightly check, or one on the Monitoring page, compares it with `COUNT(*) GROUP BY state` and reports drift.
- **`equipment.total_quantity`** (set on the equipment form): after this work it can't be typed in. It is the number of non-retired units. "Add units" and "Retire unit" replace editing the number.

## How each screen changes

| Screen | Today | After |
|---|---|---|
| Equipment details (`/Equipment/Details`) | one quantity | a **Units** table: code, serial, state, condition, last borrower; **Add units** and **Retire** actions |
| Labels (`/Equipment/Label`) | one QR per model | one QR per unit (`LP-001-03`); a **Print all labels** sheet per model |
| Release (`/Custodian/Release`) | type a quantity | pick units: scan each unit's QR, or tick from the list of available units (pre-ticked with the ones in best condition). The count must match the approved quantity. |
| Scan (`/Custodian/Scan`) | resolves asset codes and slips | also resolves unit codes: a released unit opens its return, an available unit opens its details |
| Return (`/Returns/Create`) | type how many came back | tick which units came back (all ticked by default). Scanning a unit that wasn't on this borrowing gives a clear error: "LP-001-07 wasn't released on TXN-2026-0009." |
| Inspection (`/Returns/Inspect`) | one outcome for N units (the new option A) | the same outcome cards **per unit** (the option B mockup): one row per returned unit, each with Good / Damaged / Lost and a damage level |
| Maintenance / lost and found | counts | lists of named units, with **Back in service** and **Found** actions per unit |
| Borrower views (`/Borrowings/Mine`, details) | "2 × Dell Latitude" | also list the unit codes they're holding, which is useful at return time |

The new one-tap Inspect form already uses the same Good / Damaged / Lost vocabulary, so the
per-unit version just repeats those cards for each unit.

## Where the code goes (follows AGENTS.md layering)

- **Data:** `Models/EquipmentUnit.cs` and `Models/BorrowingUnit.cs`, config in `AllianceDbContext.OnModelCreating`, and an `EquipmentUnitRepository` (extends `BaseRepository`) with `IEquipmentUnitRepository`.
- **Resources:** `Constants/UnitStateCodes.cs` (`AVAILABLE`, `RELEASED`, ...), and new error messages in `Messages/Errors.resx` (unit not on this borrowing, unit not available, count mismatch).
- **Services:** `UnitRules` in `Manager/` for the allowed state transitions. Changes to `CustodianService.Release`, `ReturnProcessingService.CreateReturn` and `CreateInspection`, and to the equipment service (add and retire units). New view models in `ViewModelClasses/`.
- **Schema:** like `user_preferences` and `custodian_assignments`, `DatabaseSeeder` creates both tables with idempotent `IF OBJECT_ID(...) IS NULL CREATE TABLE`. It runs on every startup.
- **WebApp:** controllers stay thin. Per-unit QR labels reuse `Helpers/QrCode`.

## Backfill of existing data (in the seeder, idempotent)

1. For each equipment row with no units yet, create `total_quantity` units, coded `{asset_code}-01`
   to `-NN`. Set their states from the balance buckets: the first `available` units are AVAILABLE,
   the next `released` are RELEASED, then AWAITING_INSPECTION, MAINTENANCE, and LOST for unreconciled.
2. For each open borrowing, attach that many RELEASED units to it in `borrowing_units`, and write
   an audit note that they were assigned by the backfill, since the real units were never recorded.
3. Run the balance check. It must report zero drift before the old quantity paths are removed.

For the demo data this means LP-001 gets 10 units, AUD-301 gets 8, and so on: 38 in total today.

## Phases (each one ships and works on its own)

| # | Deliverable | Done when |
|---|---|---|
| 1 | Tables, models, repository, backfill; **Units** list on equipment details; per-unit QR labels | every equipment shows its units, unit counts match `total_quantity`, labels print |
| 2 | Release picks units (scan or tick); Scan resolves unit codes | releasing 2 laptops records 2 named units; the same unit can't be released twice (DB rejects it) |
| 3 | Return ticks units; Inspect gets per-unit outcome rows | you can see which unit was damaged; it shows as MAINTENANCE on its own row |
| 4 | Maintenance and lost/found act on units; balances derived from units; quantity field made read-only | the balance check reports zero drift; the dashboards are unchanged |
| 5 | Borrower views show unit codes; docs, demo script and test plan updated | demo script runs end to end with unit codes |

Each phase is verified by `dotnet build ASI.Basecode.sln` and a click-through in the running app with
the demo accounts, since the repo has no test project. Phase 2 also gets a scripted double-release
attempt to prove the unique index holds.

## Decisions to confirm before starting

1. **Code format:** `LP-001-03` (asset code plus sequence) is readable and sorts well. The other option is a random code such as `U-7F3K9`, which doesn't change when a unit is re-categorised. My recommendation is asset code plus sequence.
2. **Serial numbers:** optional, as above. Should any categories, such as laptops and cameras, require them?
3. **Bulk items:** cables and adapters may not be worth tracking one by one. One option is an `is_serialized` flag on the category, where non-serialized models keep today's quantity flow. My recommendation is to add the flag in phase 1 and default it to on.
4. **Release by scan only, or scan and tick?** My recommendation is both. Scanning is accurate, and ticking keeps working when the QR label is damaged.

## Risks

- **Backfill guesses which units are out.** That's unavoidable, because the data was never captured. It's flagged in the audit log, and it settles once those borrowings are returned.
- **Balances drifting from units during phases 1 to 3.** Both are written in the same transaction, and the drift check catches any bug before phase 4 removes the quantity paths.
- **Release on large orders gets slower** (ticking 10 units). The list is pre-ticked with the best units, and the QR scanner adds a unit per beep.
