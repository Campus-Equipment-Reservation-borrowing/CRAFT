using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Services.Services
{
    /// <summary>
    /// Idempotent seeding for the empty scaffolded database.
    /// Demo password for every seeded account is <c>Campus123</c>.
    /// </summary>
    public class DatabaseSeeder : ServiceBase, IDatabaseSeeder
    {
        /// <summary>Shared password for all demo accounts (meets the HLD policy).</summary>
        public const string DemoPassword = "Campus123";

        private readonly IUnitOfWork _unitOfWork;

        private AllianceDbContext DbContext => (AllianceDbContext)_unitOfWork.Database;

        public DatabaseSeeder(ILoggerFactory loggerFactory, IUnitOfWork unitOfWork)
            : base(loggerFactory)
        {
            this._unitOfWork = unitOfWork;
        }

        public void Seed()
        {
            try
            {
                this.EnsureAuthSchema();
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Database seeding failed while ensuring the auth schema.");
            }

            // Each feature schema step is isolated so one failure doesn't block the others.
            this.RunSchemaStep("account lifecycle", this.EnsureAccountLifecycleSchema);
            this.RunSchemaStep("custodian assignments", this.EnsureCustodianAssignmentSchema);
            this.RunSchemaStep("reservation lifecycle", this.EnsureLifecycleSchema);
            this.RunSchemaStep("announcements and support", this.EnsureCommunicationSchema);

            try
            {
                this.SeedCodebooks();
                this.EnsureCodebookEntries();
                this.SeedDemoData();
            }
            catch (Exception ex)
            {
                // Never take the app down on seeding failure — surface it in the log instead.
                this._logger.LogError(ex, "Database seeding failed.");
            }
        }

        private void RunSchemaStep(string name, Action step)
        {
            try
            {
                step();
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Database schema step '{Step}' failed.", name);
            }
        }

        /// <summary>Verification + provisioning + persisted lockout columns on <c>users</c>.</summary>
        private void EnsureAccountLifecycleSchema()
        {
            this.DbContext.Database.ExecuteSqlRaw(@"
IF COL_LENGTH('dbo.users', 'verification_note') IS NULL
    ALTER TABLE dbo.users ADD verification_note nvarchar(500) NULL;

IF COL_LENGTH('dbo.users', 'verified_at_utc') IS NULL
    ALTER TABLE dbo.users ADD verified_at_utc datetime2 NULL;

IF COL_LENGTH('dbo.users', 'verified_by_user_id') IS NULL
    ALTER TABLE dbo.users ADD verified_by_user_id bigint NULL;

IF COL_LENGTH('dbo.users', 'must_change_password') IS NULL
    ALTER TABLE dbo.users ADD must_change_password bit NOT NULL CONSTRAINT DF_users_must_change_password DEFAULT (0);

IF COL_LENGTH('dbo.users', 'failed_login_count') IS NULL
    ALTER TABLE dbo.users ADD failed_login_count int NOT NULL CONSTRAINT DF_users_failed_login_count DEFAULT (0);

IF COL_LENGTH('dbo.users', 'lockout_until_utc') IS NULL
    ALTER TABLE dbo.users ADD lockout_until_utc datetime2 NULL;");
        }

        /// <summary>Announcements, support tickets and ticket replies.</summary>
        private void EnsureCommunicationSchema()
        {
            this.DbContext.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'announcements')
BEGIN
    CREATE TABLE dbo.announcements (
        announcement_id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_announcements PRIMARY KEY,
        title nvarchar(200) NOT NULL,
        body nvarchar(2000) NOT NULL,
        target_roles nvarchar(100) NOT NULL,
        created_by_user_id bigint NULL,
        created_at_utc datetime2 NOT NULL CONSTRAINT DF_announcements_created DEFAULT (sysutcdatetime()),
        expires_at_utc datetime2 NULL,
        CONSTRAINT FK_announcements_created_by FOREIGN KEY (created_by_user_id) REFERENCES dbo.users(user_id)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'support_tickets')
BEGIN
    CREATE TABLE dbo.support_tickets (
        ticket_id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_support_tickets PRIMARY KEY,
        requester_user_id bigint NOT NULL,
        category nvarchar(20) NOT NULL,
        subject nvarchar(200) NOT NULL,
        body nvarchar(4000) NOT NULL,
        status nvarchar(20) NOT NULL CONSTRAINT DF_support_tickets_status DEFAULT ('OPEN'),
        assigned_to_user_id bigint NULL,
        created_at_utc datetime2 NOT NULL CONSTRAINT DF_support_tickets_created DEFAULT (sysutcdatetime()),
        resolved_at_utc datetime2 NULL,
        CONSTRAINT FK_support_tickets_requester FOREIGN KEY (requester_user_id) REFERENCES dbo.users(user_id),
        CONSTRAINT FK_support_tickets_assignee FOREIGN KEY (assigned_to_user_id) REFERENCES dbo.users(user_id)
    );
    CREATE INDEX IX_support_tickets_requester ON dbo.support_tickets(requester_user_id);
    CREATE INDEX IX_support_tickets_status ON dbo.support_tickets(status);
END;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'support_ticket_replies')
BEGIN
    CREATE TABLE dbo.support_ticket_replies (
        reply_id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_support_ticket_replies PRIMARY KEY,
        ticket_id bigint NOT NULL,
        author_user_id bigint NOT NULL,
        body nvarchar(4000) NOT NULL,
        created_at_utc datetime2 NOT NULL CONSTRAINT DF_support_ticket_replies_created DEFAULT (sysutcdatetime()),
        CONSTRAINT FK_support_ticket_replies_ticket FOREIGN KEY (ticket_id) REFERENCES dbo.support_tickets(ticket_id) ON DELETE CASCADE,
        CONSTRAINT FK_support_ticket_replies_author FOREIGN KEY (author_user_id) REFERENCES dbo.users(user_id)
    );
    CREATE INDEX IX_support_ticket_replies_ticket ON dbo.support_ticket_replies(ticket_id);
END;");
        }

        /// <summary>Once-only reminder markers on <c>borrowings</c> for the lifecycle job.</summary>
        private void EnsureLifecycleSchema()
        {
            this.DbContext.Database.ExecuteSqlRaw(@"
IF COL_LENGTH('dbo.borrowings', 'due_reminder_sent_at_utc') IS NULL
    ALTER TABLE dbo.borrowings ADD due_reminder_sent_at_utc datetime2 NULL;

IF COL_LENGTH('dbo.borrowings', 'overdue_notified_at_utc') IS NULL
    ALTER TABLE dbo.borrowings ADD overdue_notified_at_utc datetime2 NULL;

IF COL_LENGTH('dbo.condition_inspections', 'good_quantity') IS NULL
    ALTER TABLE dbo.condition_inspections ADD good_quantity int NULL;

IF COL_LENGTH('dbo.condition_inspections', 'damaged_quantity') IS NULL
    ALTER TABLE dbo.condition_inspections ADD damaged_quantity int NULL;

IF COL_LENGTH('dbo.condition_inspections', 'lost_quantity') IS NULL
    ALTER TABLE dbo.condition_inspections ADD lost_quantity int NULL;

IF COL_LENGTH('dbo.condition_inspections', 'repair_cost') IS NULL
    ALTER TABLE dbo.condition_inspections ADD repair_cost decimal(12, 2) NULL;

IF COL_LENGTH('dbo.condition_inspections', 'penalty_amount') IS NULL
    ALTER TABLE dbo.condition_inspections ADD penalty_amount decimal(12, 2) NULL;

IF COL_LENGTH('dbo.condition_inspections', 'penalty_note') IS NULL
    ALTER TABLE dbo.condition_inspections ADD penalty_note nvarchar(500) NULL;");
        }

        /// <summary>Custodian ownership (borrower / location / category) and reservation routing.</summary>
        private void EnsureCustodianAssignmentSchema()
        {
            this.DbContext.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'custodian_assignments')
BEGIN
    CREATE TABLE dbo.custodian_assignments (
        assignment_id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_custodian_assignments PRIMARY KEY,
        custodian_user_id bigint NOT NULL,
        scope_type nvarchar(20) NOT NULL,
        borrower_user_id bigint NULL,
        location_id bigint NULL,
        equipment_category_id bigint NULL,
        assigned_by_user_id bigint NULL,
        assigned_at_utc datetime2 NOT NULL CONSTRAINT DF_custodian_assignments_assigned DEFAULT (sysutcdatetime()),
        CONSTRAINT FK_custodian_assignments_custodian FOREIGN KEY (custodian_user_id) REFERENCES dbo.users(user_id),
        CONSTRAINT FK_custodian_assignments_borrower FOREIGN KEY (borrower_user_id) REFERENCES dbo.users(user_id),
        CONSTRAINT FK_custodian_assignments_location FOREIGN KEY (location_id) REFERENCES dbo.locations(location_id),
        CONSTRAINT FK_custodian_assignments_category FOREIGN KEY (equipment_category_id) REFERENCES dbo.equipment_categories(equipment_category_id),
        CONSTRAINT CK_custodian_assignments_scope CHECK (
            (scope_type = 'BORROWER' AND borrower_user_id IS NOT NULL AND location_id IS NULL AND equipment_category_id IS NULL) OR
            (scope_type = 'LOCATION' AND location_id IS NOT NULL AND borrower_user_id IS NULL AND equipment_category_id IS NULL) OR
            (scope_type = 'CATEGORY' AND equipment_category_id IS NOT NULL AND borrower_user_id IS NULL AND location_id IS NULL))
    );
    CREATE UNIQUE INDEX UQ_custodian_assignments_borrower ON dbo.custodian_assignments(borrower_user_id) WHERE borrower_user_id IS NOT NULL;
    CREATE UNIQUE INDEX UQ_custodian_assignments_location ON dbo.custodian_assignments(location_id) WHERE location_id IS NOT NULL;
    CREATE UNIQUE INDEX UQ_custodian_assignments_category ON dbo.custodian_assignments(equipment_category_id) WHERE equipment_category_id IS NOT NULL;
    CREATE INDEX IX_custodian_assignments_custodian ON dbo.custodian_assignments(custodian_user_id);
END;

IF COL_LENGTH('dbo.reservations', 'assigned_custodian_user_id') IS NULL
BEGIN
    ALTER TABLE dbo.reservations ADD assigned_custodian_user_id bigint NULL;
    ALTER TABLE dbo.reservations ADD CONSTRAINT FK_reservations_assigned_custodian
        FOREIGN KEY (assigned_custodian_user_id) REFERENCES dbo.users(user_id);
    CREATE INDEX IX_reservations_assigned_custodian ON dbo.reservations(assigned_custodian_user_id);
END;");
        }

        /// <summary>
        /// A second demo custodian plus example assignments, so custodian scoping is visible:
        /// Carlos (EMP-1001) covers Property Office - Main; Rina (EMP-1002) covers the Annex,
        /// cameras, and faculty borrower Jose Cruz. Runs only while no assignments exist.
        /// </summary>
        private void SeedDemoCustodianAssignments()
        {
            var db = this.DbContext;

            if (!db.Users.Any(u => u.AccountIdentifier == "EMP-1002"))
            {
                var technicianRoleId = db.Roles.Where(r => r.RoleCode == RoleCodes.Technician).Select(r => (long?)r.RoleId).FirstOrDefault();
                var activeStatusId = db.AccountStatuses.Where(s => s.Code == AccountStatusCodes.Active).Select(s => (long?)s.AccountStatusId).FirstOrDefault();
                var departmentId = db.Departments.Where(d => d.DepartmentCode == "CENG").Select(d => (long?)d.DepartmentId).FirstOrDefault();
                if (technicianRoleId == null || activeStatusId == null)
                {
                    return;
                }

                db.Users.Add(new User
                {
                    AccountIdentifier = "EMP-1002",
                    FirstName = "Rina",
                    LastName = "Bautista",
                    SchoolEmail = "rina.bautista@university.edu.ph",
                    PasswordHash = PasswordManager.Hash(DemoPassword),
                    RoleId = technicianRoleId.Value,
                    DepartmentId = departmentId,
                    AccountStatusId = activeStatusId.Value,
                    CreatedAtUtc = default,
                });
                this._unitOfWork.SaveChanges();
            }

            if (db.CustodianAssignments.Any())
            {
                return;
            }

            var carlos = db.Users.Where(u => u.AccountIdentifier == "EMP-1001").Select(u => (long?)u.UserId).FirstOrDefault();
            var rina = db.Users.Where(u => u.AccountIdentifier == "EMP-1002").Select(u => (long?)u.UserId).FirstOrDefault();
            var jose = db.Users.Where(u => u.AccountIdentifier == "EMP-2001").Select(u => (long?)u.UserId).FirstOrDefault();
            var main = db.Locations.Where(l => l.LocationCode == "PO-MAIN").Select(l => (long?)l.LocationId).FirstOrDefault();
            var annex = db.Locations.Where(l => l.LocationCode == "PO-ANNEX").Select(l => (long?)l.LocationId).FirstOrDefault();
            var cameras = db.EquipmentCategories.Where(c => c.CategoryCode == "CAMERA").Select(c => (long?)c.EquipmentCategoryId).FirstOrDefault();

            void Assign(long? custodian, string scope, long? borrower = null, long? location = null, long? category = null)
            {
                if (custodian == null || (borrower ?? location ?? category) == null)
                {
                    return;
                }

                db.CustodianAssignments.Add(new CustodianAssignment
                {
                    CustodianUserId = custodian.Value,
                    ScopeType = scope,
                    BorrowerUserId = borrower,
                    LocationId = location,
                    EquipmentCategoryId = category,
                    AssignedAtUtc = DateTime.UtcNow,
                });
            }

            Assign(carlos, CustodianScopeTypes.Location, location: main);
            Assign(rina, CustodianScopeTypes.Location, location: annex);
            Assign(rina, CustodianScopeTypes.Category, category: cameras);
            Assign(rina, CustodianScopeTypes.Borrower, borrower: jose);
            this._unitOfWork.SaveChanges();
        }

        /// <summary>
        /// <see cref="SeedCodebooks"/> only fills empty tables; codes introduced later are added
        /// individually so existing databases pick them up too.
        /// </summary>
        private void EnsureCodebookEntries()
        {
            var db = this.DbContext;
            var changed = false;

            if (!db.AccountStatuses.Any(s => s.Code == AccountStatusCodes.Rejected))
            {
                db.AccountStatuses.Add(new AccountStatus { Code = AccountStatusCodes.Rejected, Description = "Rejected" });
                changed = true;
            }

            foreach (var (code, description) in new[] { ("EXPIRED", "Expired"), ("PARTIALLY_RELEASED", "Partially Released") })
            {
                if (!db.ReservationStatuses.Any(s => s.Code == code))
                {
                    db.ReservationStatuses.Add(new ReservationStatus { Code = code, Description = description });
                    changed = true;
                }
            }

            if (changed)
            {
                this._unitOfWork.SaveChanges();
            }
        }

        private void EnsureAuthSchema()
        {
            // The scaffolded schema predates password reset support. Create the table
            // in place so no EF migration is required for the database-first setup.
            this.DbContext.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'password_reset_tokens')
BEGIN
    CREATE TABLE dbo.password_reset_tokens (
        password_reset_token_id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_password_reset_tokens PRIMARY KEY,
        user_id bigint NOT NULL,
        token nvarchar(200) NOT NULL,
        created_at_utc datetime2 NOT NULL CONSTRAINT DF_password_reset_tokens_created DEFAULT (sysutcdatetime()),
        expires_at_utc datetime2 NOT NULL,
        used_at_utc datetime2 NULL,
        CONSTRAINT FK_password_reset_tokens_users FOREIGN KEY (user_id) REFERENCES dbo.users(user_id) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX UQ_password_reset_tokens_token ON dbo.password_reset_tokens(token);
END;

IF COL_LENGTH('dbo.users', 'contact_number') IS NULL
    ALTER TABLE dbo.users ADD contact_number nvarchar(50) NULL;

IF COL_LENGTH('dbo.users', 'alternate_contact') IS NULL
    ALTER TABLE dbo.users ADD alternate_contact nvarchar(100) NULL;

IF COL_LENGTH('dbo.equipment', 'image_path') IS NULL
    ALTER TABLE dbo.equipment ADD image_path nvarchar(500) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'user_preferences')
BEGIN
    CREATE TABLE dbo.user_preferences (
        user_id bigint NOT NULL CONSTRAINT PK_user_preferences PRIMARY KEY,
        notify_reservation_updates bit NOT NULL CONSTRAINT DF_user_preferences_res DEFAULT (1),
        notify_due_reminders bit NOT NULL CONSTRAINT DF_user_preferences_due DEFAULT (1),
        notify_overdue_alerts bit NOT NULL CONSTRAINT DF_user_preferences_overdue DEFAULT (1),
        notify_return_confirmations bit NOT NULL CONSTRAINT DF_user_preferences_return DEFAULT (1),
        notify_announcements bit NOT NULL CONSTRAINT DF_user_preferences_ann DEFAULT (1),
        updated_at_utc datetime2 NOT NULL CONSTRAINT DF_user_preferences_updated DEFAULT (sysutcdatetime()),
        CONSTRAINT FK_user_preferences_users FOREIGN KEY (user_id) REFERENCES dbo.users(user_id) ON DELETE CASCADE
    );
END;");
        }

        private void SeedCodebooks()
        {
            var db = this.DbContext;
            var changed = false;

            if (!db.Roles.Any())
            {
                db.Roles.AddRange(
                    new Role { RoleCode = RoleCodes.Student, RoleName = "Student", IsBorrowerRole = true },
                    new Role { RoleCode = RoleCodes.Faculty, RoleName = "Faculty", IsBorrowerRole = true },
                    new Role { RoleCode = RoleCodes.Technician, RoleName = "Custodian", IsBorrowerRole = false },
                    new Role { RoleCode = RoleCodes.Admin, RoleName = "Administrator", IsBorrowerRole = false });
                changed = true;
            }

            if (!db.AccountStatuses.Any())
            {
                db.AccountStatuses.AddRange(
                    new AccountStatus { Code = AccountStatusCodes.Active, Description = "Active" },
                    new AccountStatus { Code = AccountStatusCodes.Unverified, Description = "Unverified" },
                    new AccountStatus { Code = AccountStatusCodes.Inactive, Description = "Inactive" },
                    new AccountStatus { Code = AccountStatusCodes.Suspended, Description = "Suspended" },
                    new AccountStatus { Code = AccountStatusCodes.Locked, Description = "Locked" });
                changed = true;
            }

            if (!db.ReservationStatuses.Any())
            {
                db.ReservationStatuses.AddRange(
                    new ReservationStatus { Code = "PENDING", Description = "Pending" },
                    new ReservationStatus { Code = "APPROVED", Description = "Approved" },
                    new ReservationStatus { Code = "REJECTED", Description = "Rejected" },
                    new ReservationStatus { Code = "CANCELLED", Description = "Cancelled" },
                    new ReservationStatus { Code = "READY_FOR_RELEASE", Description = "Ready for Release" },
                    new ReservationStatus { Code = "COMPLETED", Description = "Completed" });
                changed = true;
            }

            if (!db.BorrowingStatuses.Any())
            {
                db.BorrowingStatuses.AddRange(
                    new BorrowingStatus { Code = "ACTIVE", Description = "Active" },
                    new BorrowingStatus { Code = "OVERDUE", Description = "Overdue" },
                    new BorrowingStatus { Code = "PARTIALLY_RETURNED", Description = "Partially Returned" },
                    new BorrowingStatus { Code = "RETURNED", Description = "Returned" },
                    new BorrowingStatus { Code = "LOST", Description = "Lost" });
                changed = true;
            }

            if (!db.InspectionStatuses.Any())
            {
                db.InspectionStatuses.AddRange(
                    new InspectionStatus { Code = "PENDING", Description = "Awaiting Inspection" },
                    new InspectionStatus { Code = "COMPLETED", Description = "Inspection Completed" });
                changed = true;
            }

            if (!db.Conditions.Any())
            {
                db.Conditions.AddRange(
                    new Condition { Code = "GOOD", Description = "Good", AffectsAvailability = false },
                    new Condition { Code = "MINOR_DAMAGE", Description = "Minor Damage", AffectsAvailability = false },
                    new Condition { Code = "DAMAGED", Description = "Damaged", AffectsAvailability = true },
                    new Condition { Code = "MISSING_PARTS", Description = "Missing Parts", AffectsAvailability = true },
                    new Condition { Code = "LOST", Description = "Lost", AffectsAvailability = true },
                    new Condition { Code = "AVAILABLE", Description = "Available", AffectsAvailability = false },
                    new Condition { Code = "UNDER_MAINTENANCE", Description = "Under Maintenance", AffectsAvailability = true });
                changed = true;
            }

            if (!db.Departments.Any())
            {
                db.Departments.AddRange(
                    new Department { DepartmentCode = "CCS", DepartmentName = "College of Computer Studies", IsActive = true },
                    new Department { DepartmentCode = "CENG", DepartmentName = "College of Engineering", IsActive = true },
                    new Department { DepartmentCode = "CBA", DepartmentName = "College of Business Administration", IsActive = true },
                    new Department { DepartmentCode = "COED", DepartmentName = "College of Education", IsActive = true },
                    new Department { DepartmentCode = "CAS", DepartmentName = "College of Arts and Sciences", IsActive = true });
                changed = true;
            }

            if (!db.Locations.Any())
            {
                db.Locations.AddRange(
                    new Location { LocationCode = "PO-MAIN", LocationName = "Property Office - Main", IsPickupLocation = true, IsActive = true },
                    new Location { LocationCode = "PO-ANNEX", LocationName = "Property Office - Annex", IsPickupLocation = true, IsActive = true },
                    new Location { LocationCode = "LIB", LocationName = "Library Media Room", IsPickupLocation = false, IsActive = true },
                    new Location { LocationCode = "GYM", LocationName = "Gymnasium Storage", IsPickupLocation = false, IsActive = true });
                changed = true;
            }

            if (!db.EquipmentCategories.Any())
            {
                db.EquipmentCategories.AddRange(
                    new EquipmentCategory { CategoryCode = "LAPTOP", CategoryName = "Laptops" },
                    new EquipmentCategory { CategoryCode = "PROJECTOR", CategoryName = "Projectors" },
                    new EquipmentCategory { CategoryCode = "CAMERA", CategoryName = "Cameras" },
                    new EquipmentCategory { CategoryCode = "AUDIO", CategoryName = "Audio Equipment" },
                    new EquipmentCategory { CategoryCode = "NETWORK", CategoryName = "Networking Equipment" },
                    new EquipmentCategory { CategoryCode = "LAB", CategoryName = "Laboratory Tools" });
                changed = true;
            }

            if (!db.BorrowingPolicyVersions.Any())
            {
                db.BorrowingPolicyVersions.Add(new BorrowingPolicyVersion
                {
                    VersionNumber = "v1.0",
                    EffectiveFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsCurrent = true,
                    MaxBorrowDurationDays = 7,
                    MaxActiveBorrowingUnits = 3,
                    OverdueGracePeriodMinutes = 60,
                    LowStockThreshold = 2,
                    BlockReservationWhenOverdue = true,
                });
                changed = true;
            }

            if (changed)
            {
                this._unitOfWork.SaveChanges();
            }
        }

        private void SeedDemoData()
        {
            var db = this.DbContext;

            if (!db.Users.Any())
            {
                var roles = db.Roles.ToDictionary(r => r.RoleCode, r => r.RoleId);
                var statuses = db.AccountStatuses.ToDictionary(s => s.Code, s => s.AccountStatusId);
                var departments = db.Departments.ToDictionary(d => d.DepartmentCode, d => d.DepartmentId);

                string Hash() => PasswordManager.Hash(DemoPassword);

                db.Users.AddRange(
                    new User
                    {
                        AccountIdentifier = "ADMIN-0001",
                        FirstName = "Ana",
                        LastName = "Reyes",
                        SchoolEmail = "ana.reyes@university.edu.ph",
                        PasswordHash = Hash(),
                        RoleId = roles[RoleCodes.Admin],
                        DepartmentId = departments["CCS"],
                        AccountStatusId = statuses[AccountStatusCodes.Active],
                        CreatedAtUtc = default,
                    },
                    new User
                    {
                        AccountIdentifier = "EMP-1001",
                        FirstName = "Carlos",
                        LastName = "Diaz",
                        SchoolEmail = "carlos.diaz@university.edu.ph",
                        PasswordHash = Hash(),
                        RoleId = roles[RoleCodes.Technician],
                        DepartmentId = departments["CCS"],
                        AccountStatusId = statuses[AccountStatusCodes.Active],
                        CreatedAtUtc = default,
                    },
                    new User
                    {
                        AccountIdentifier = "2023-104857",
                        FirstName = "Maria",
                        LastName = "Santos",
                        SchoolEmail = "maria.santos@university.edu.ph",
                        PasswordHash = Hash(),
                        RoleId = roles[RoleCodes.Student],
                        DepartmentId = departments["CCS"],
                        AccountStatusId = statuses[AccountStatusCodes.Active],
                        CreatedAtUtc = default,
                    },
                    new User
                    {
                        AccountIdentifier = "EMP-2001",
                        FirstName = "Jose",
                        LastName = "Cruz",
                        SchoolEmail = "jose.cruz@university.edu.ph",
                        PasswordHash = Hash(),
                        RoleId = roles[RoleCodes.Faculty],
                        DepartmentId = departments["CENG"],
                        AccountStatusId = statuses[AccountStatusCodes.Active],
                        CreatedAtUtc = default,
                    },
                    new User
                    {
                        AccountIdentifier = "2024-118002",
                        FirstName = "Liza",
                        LastName = "Mendoza",
                        SchoolEmail = "liza.mendoza@university.edu.ph",
                        PasswordHash = Hash(),
                        RoleId = roles[RoleCodes.Student],
                        DepartmentId = departments["CBA"],
                        AccountStatusId = statuses[AccountStatusCodes.Unverified],
                        CreatedAtUtc = default,
                    });

                this._unitOfWork.SaveChanges();
            }

            if (!db.Equipment.Any())
            {
                var categories = db.EquipmentCategories.ToDictionary(c => c.CategoryCode, c => c.EquipmentCategoryId);
                var locations = db.Locations.ToDictionary(l => l.LocationCode, l => l.LocationId);
                var conditions = db.Conditions.ToDictionary(c => c.Code, c => c.ConditionId);

                void AddEquipment(string asset, string name, string description, string category,
                    string location, int total, int reserved = 0, int released = 0,
                    string condition = "GOOD", string operational = "AVAILABLE")
                {
                    var equipment = new Equipment
                    {
                        AssetCode = asset,
                        EquipmentName = name,
                        Description = description,
                        EquipmentCategoryId = categories[category],
                        LocationId = locations[location],
                        CurrentConditionId = conditions[condition],
                        OperationalStatusId = conditions[operational],
                        IsActive = true,
                        CreatedAtUtc = default,
                    };

                    equipment.EquipmentInventoryBalance = new EquipmentInventoryBalance
                    {
                        TotalQuantity = total,
                        ReservedQuantity = reserved,
                        ReleasedQuantity = released,
                    };

                    db.Equipment.Add(equipment);
                }

                AddEquipment("LP-001", "Dell Latitude 5440", "14-inch business laptop, 16GB RAM, 512GB SSD.", "LAPTOP", "PO-MAIN", 10);
                AddEquipment("LP-002", "MacBook Air M2", "13-inch laptop for creative work and presentations.", "LAPTOP", "PO-MAIN", 5, reserved: 1);
                AddEquipment("PRJ-101", "Epson EB-X06 Projector", "3600 lumens XGA projector with HDMI input.", "PROJECTOR", "PO-ANNEX", 6);
                AddEquipment("CAM-201", "Canon EOS 250D", "Entry-level DSLR with 18-55mm kit lens.", "CAMERA", "LIB", 3, condition: "MINOR_DAMAGE");
                AddEquipment("AUD-301", "Shure SM58 Microphone Set", "Dynamic vocal microphone with XLR cable and stand.", "AUDIO", "GYM", 8);
                AddEquipment("NET-401", "Cisco Catalyst 2960 Switch", "24-port managed network switch for lab use.", "NETWORK", "PO-MAIN", 2, operational: "UNDER_MAINTENANCE");
                AddEquipment("LAB-501", "Arduino Uno Starter Kit", "Microcontroller kit with sensors and breadboard.", "LAB", "PO-MAIN", 0);
                AddEquipment("LP-003", "Lenovo ThinkPad E14", "14-inch laptop, 8GB RAM, 256GB SSD.", "LAPTOP", "PO-ANNEX", 4);

                this._unitOfWork.SaveChanges();
            }

            this.SeedDemoCustodianAssignments();

            // Every account gets a default (all-on) notification preference row.
            this.DbContext.Database.ExecuteSqlRaw(@"
INSERT INTO dbo.user_preferences (user_id)
SELECT u.user_id FROM dbo.users u
WHERE NOT EXISTS (SELECT 1 FROM dbo.user_preferences p WHERE p.user_id = u.user_id);");

            if (!db.Notifications.Any())
            {
                var student = db.Users.FirstOrDefault(u => u.AccountIdentifier == "2023-104857");
                if (student != null)
                {
                    db.Notifications.AddRange(
                        new Notification
                        {
                            RecipientUserId = student.UserId,
                            NotificationType = "ACCOUNT",
                            Title = "Welcome to CampusPulse",
                            Message = "Your account is active. Browse equipment availability to start a reservation.",
                            IsRead = false,
                            CreatedAtUtc = DateTime.UtcNow,
                        });
                    this._unitOfWork.SaveChanges();
                }
            }
        }
    }
}
