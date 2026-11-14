using System;
using System.Collections.Generic;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data;

public partial class AllianceDbContext : DbContext
{
    public AllianceDbContext()
    {
    }

    public AllianceDbContext(DbContextOptions<AllianceDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AccountStatus> AccountStatuses { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Borrowing> Borrowings { get; set; }

    public virtual DbSet<BorrowingPolicyVersion> BorrowingPolicyVersions { get; set; }

    public virtual DbSet<BorrowingReturn> BorrowingReturns { get; set; }

    public virtual DbSet<BorrowingStatus> BorrowingStatuses { get; set; }

    public virtual DbSet<Condition> Conditions { get; set; }

    public virtual DbSet<ConditionInspection> ConditionInspections { get; set; }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<Equipment> Equipment { get; set; }

    public virtual DbSet<EquipmentCategory> EquipmentCategories { get; set; }

    public virtual DbSet<EquipmentInventoryBalance> EquipmentInventoryBalances { get; set; }

    public virtual DbSet<InspectionStatus> InspectionStatuses { get; set; }

    public virtual DbSet<InventoryMovement> InventoryMovements { get; set; }

    public virtual DbSet<Location> Locations { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Reservation> Reservations { get; set; }

    public virtual DbSet<ReservationStatus> ReservationStatuses { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<User> Users { get; set; }

    // NOTE: connection string comes from appsettings.json ("AllianceConnection") via DI (see WebApp/Startup.cs).
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountStatus>(entity =>
        {
            entity.ToTable("account_statuses");

            entity.HasIndex(e => e.Code, "UQ_account_statuses_code").IsUnique();

            entity.Property(e => e.AccountStatusId).HasColumnName("account_status_id");
            entity.Property(e => e.Code)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("description");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");

            entity.HasIndex(e => e.ActorUserId, "IX_audit_logs_actor");

            entity.HasIndex(e => new { e.EntityType, e.EntityId }, "IX_audit_logs_entity");

            entity.HasIndex(e => e.OccurredAtUtc, "IX_audit_logs_occurred_at").IsDescending();

            entity.Property(e => e.AuditLogId).HasColumnName("audit_log_id");
            entity.Property(e => e.ActionType)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("action_type");
            entity.Property(e => e.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(e => e.Details)
                .HasMaxLength(1000)
                .HasColumnName("details");
            entity.Property(e => e.EntityId).HasColumnName("entity_id");
            entity.Property(e => e.EntityType)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("entity_type");
            entity.Property(e => e.OccurredAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("occurred_at_utc");
            entity.Property(e => e.ReferenceNumber)
                .HasMaxLength(100)
                .HasColumnName("reference_number");

            entity.HasOne(d => d.ActorUser).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.ActorUserId)
                .HasConstraintName("FK_audit_logs_actor");
        });

        modelBuilder.Entity<Borrowing>(entity =>
        {
            entity.ToTable("borrowings");

            entity.HasIndex(e => new { e.BorrowerUserId, e.StatusId }, "IX_borrowings_borrower");

            entity.HasIndex(e => e.EquipmentId, "IX_borrowings_equipment");

            entity.HasIndex(e => e.InspectionStatusId, "IX_borrowings_inspection_status");

            entity.HasIndex(e => e.BorrowingPolicyVersionId, "IX_borrowings_policy_version");

            entity.HasIndex(e => e.ReleaseConditionId, "IX_borrowings_release_condition");

            entity.HasIndex(e => e.ReleasedByUserId, "IX_borrowings_released_by");

            entity.HasIndex(e => e.ReservationId, "IX_borrowings_reservation");

            entity.HasIndex(e => new { e.StatusId, e.DueAtUtc }, "IX_borrowings_status");

            entity.HasIndex(e => e.TransactionNumber, "UQ_borrowings_transaction_number").IsUnique();

            entity.Property(e => e.BorrowingId).HasColumnName("borrowing_id");
            entity.Property(e => e.BorrowerUserId).HasColumnName("borrower_user_id");
            entity.Property(e => e.BorrowingPolicyVersionId).HasColumnName("borrowing_policy_version_id");
            entity.Property(e => e.DueAtUtc)
                .HasPrecision(3)
                .HasColumnName("due_at_utc");
            entity.Property(e => e.EquipmentId).HasColumnName("equipment_id");
            entity.Property(e => e.InspectionStatusId).HasColumnName("inspection_status_id");
            entity.Property(e => e.ReleaseConditionId).HasColumnName("release_condition_id");
            entity.Property(e => e.ReleasedAtUtc)
                .HasPrecision(3)
                .HasColumnName("released_at_utc");
            entity.Property(e => e.ReleasedByUserId).HasColumnName("released_by_user_id");
            entity.Property(e => e.ReleasedQuantity).HasColumnName("released_quantity");
            entity.Property(e => e.ReservationId).HasColumnName("reservation_id");
            entity.Property(e => e.RowVersion)
                .IsRequired()
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.StatusId).HasColumnName("status_id");
            entity.Property(e => e.TransactionNumber)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("transaction_number");

            entity.HasOne(d => d.BorrowerUser).WithMany(p => p.BorrowingBorrowerUsers)
                .HasForeignKey(d => d.BorrowerUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_borrowings_borrower");

            entity.HasOne(d => d.BorrowingPolicyVersion).WithMany(p => p.Borrowings)
                .HasForeignKey(d => d.BorrowingPolicyVersionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_borrowings_policy_version");

            entity.HasOne(d => d.Equipment).WithMany(p => p.Borrowings)
                .HasForeignKey(d => d.EquipmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_borrowings_equipment");

            entity.HasOne(d => d.InspectionStatus).WithMany(p => p.Borrowings)
                .HasForeignKey(d => d.InspectionStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_borrowings_inspection_status");

            entity.HasOne(d => d.ReleaseCondition).WithMany(p => p.Borrowings)
                .HasForeignKey(d => d.ReleaseConditionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_borrowings_release_condition");

            entity.HasOne(d => d.ReleasedByUser).WithMany(p => p.BorrowingReleasedByUsers)
                .HasForeignKey(d => d.ReleasedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_borrowings_released_by");

            entity.HasOne(d => d.Reservation).WithMany(p => p.Borrowings)
                .HasForeignKey(d => d.ReservationId)
                .HasConstraintName("FK_borrowings_reservation");

            entity.HasOne(d => d.Status).WithMany(p => p.Borrowings)
                .HasForeignKey(d => d.StatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_borrowings_status");
        });

        modelBuilder.Entity<BorrowingPolicyVersion>(entity =>
        {
            entity.HasKey(e => e.PolicyVersionId);

            entity.ToTable("borrowing_policy_versions");

            entity.HasIndex(e => e.VersionNumber, "UQ_borrowing_policy_versions_version_number").IsUnique();

            entity.HasIndex(e => e.IsCurrent, "UX_bp_versions_single_current")
                .IsUnique()
                .HasFilter("([is_current]=(1))");

            entity.Property(e => e.PolicyVersionId).HasColumnName("policy_version_id");
            entity.Property(e => e.BlockReservationWhenOverdue)
                .HasDefaultValue(true)
                .HasColumnName("block_reservation_when_overdue");
            entity.Property(e => e.MaxUnitsPerBorrower)
                .HasDefaultValue(3)
                .HasColumnName("max_units_per_borrower");
            entity.Property(e => e.ReservationLeadTimeDays)
                .HasDefaultValue(1)
                .HasColumnName("reservation_lead_time_days");
            entity.Property(e => e.MaxAdvanceBookingDays)
                .HasDefaultValue(30)
                .HasColumnName("max_advance_booking_days");
            entity.Property(e => e.FacultyMaxBorrowDays)
                .HasDefaultValue(21)
                .HasColumnName("faculty_max_borrow_days");
            entity.Property(e => e.EffectiveFromUtc)
                .HasPrecision(3)
                .HasColumnName("effective_from_utc");
            entity.Property(e => e.IsCurrent).HasColumnName("is_current");
            entity.Property(e => e.LowStockThreshold).HasColumnName("low_stock_threshold");
            entity.Property(e => e.MaxActiveBorrowingUnits).HasColumnName("max_active_borrowing_units");
            entity.Property(e => e.MaxBorrowDurationDays).HasColumnName("max_borrow_duration_days");
            entity.Property(e => e.OverdueGracePeriodMinutes).HasColumnName("overdue_grace_period_minutes");
            entity.Property(e => e.VersionNumber)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("version_number");
        });

        modelBuilder.Entity<BorrowingReturn>(entity =>
        {
            entity.HasKey(e => e.ReturnId);

            entity.ToTable("borrowing_returns");

            entity.HasIndex(e => e.BorrowingId, "IX_borrowing_returns_borrowing");

            entity.HasIndex(e => e.ReceivedByUserId, "IX_borrowing_returns_received_by");

            entity.Property(e => e.ReturnId).HasColumnName("return_id");
            entity.Property(e => e.BorrowingId).HasColumnName("borrowing_id");
            entity.Property(e => e.DaysOverdue).HasColumnName("days_overdue");
            entity.Property(e => e.ReceivedByUserId).HasColumnName("received_by_user_id");
            entity.Property(e => e.ReturnedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("returned_at_utc");
            entity.Property(e => e.ReturnedQuantity).HasColumnName("returned_quantity");
            entity.Property(e => e.WasReturnedLate).HasColumnName("was_returned_late");

            entity.HasOne(d => d.Borrowing).WithMany(p => p.BorrowingReturns)
                .HasForeignKey(d => d.BorrowingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_borrowing_returns_borrowing");

            entity.HasOne(d => d.ReceivedByUser).WithMany(p => p.BorrowingReturns)
                .HasForeignKey(d => d.ReceivedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_borrowing_returns_received_by");
        });

        modelBuilder.Entity<BorrowingStatus>(entity =>
        {
            entity.HasKey(e => e.StatusId);

            entity.ToTable("borrowing_statuses");

            entity.HasIndex(e => e.Code, "UQ_borrowing_statuses_code").IsUnique();

            entity.Property(e => e.StatusId).HasColumnName("status_id");
            entity.Property(e => e.Code)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("description");
        });

        modelBuilder.Entity<Condition>(entity =>
        {
            entity.ToTable("conditions");

            entity.HasIndex(e => e.Code, "UQ_conditions_code").IsUnique();

            entity.Property(e => e.ConditionId).HasColumnName("condition_id");
            entity.Property(e => e.AffectsAvailability).HasColumnName("affects_availability");
            entity.Property(e => e.Code)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("description");
        });

        modelBuilder.Entity<ConditionInspection>(entity =>
        {
            entity.HasKey(e => e.InspectionId);

            entity.ToTable("condition_inspections");

            entity.HasIndex(e => e.BorrowingId, "IX_condition_inspections_borrowing");

            entity.HasIndex(e => e.CurrentConditionId, "IX_condition_inspections_current_condition");

            entity.HasIndex(e => e.EquipmentId, "IX_condition_inspections_equipment");

            entity.HasIndex(e => e.InspectedByUserId, "IX_condition_inspections_inspected_by");

            entity.HasIndex(e => e.ReleaseConditionId, "IX_condition_inspections_release_condition");

            entity.HasIndex(e => e.BorrowingReturnId, "IX_condition_inspections_return");

            entity.HasIndex(e => e.InspectionStatusId, "IX_condition_inspections_status");

            entity.Property(e => e.InspectionId).HasColumnName("inspection_id");
            entity.Property(e => e.BorrowingId).HasColumnName("borrowing_id");
            entity.Property(e => e.BorrowingReturnId).HasColumnName("borrowing_return_id");
            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at_utc");
            entity.Property(e => e.CurrentConditionId).HasColumnName("current_condition_id");
            entity.Property(e => e.EquipmentId).HasColumnName("equipment_id");
            entity.Property(e => e.InspectedByUserId).HasColumnName("inspected_by_user_id");
            entity.Property(e => e.InspectedQuantity).HasColumnName("inspected_quantity");
            entity.Property(e => e.InspectionNotes)
                .HasMaxLength(1000)
                .HasColumnName("inspection_notes");
            entity.Property(e => e.InspectionStatusId).HasColumnName("inspection_status_id");
            entity.Property(e => e.ReleaseConditionId).HasColumnName("release_condition_id");

            entity.HasOne(d => d.Borrowing).WithMany(p => p.ConditionInspections)
                .HasForeignKey(d => d.BorrowingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_condition_inspections_borrowing");

            entity.HasOne(d => d.BorrowingReturn).WithMany(p => p.ConditionInspections)
                .HasForeignKey(d => d.BorrowingReturnId)
                .HasConstraintName("FK_condition_inspections_return");

            entity.HasOne(d => d.CurrentCondition).WithMany(p => p.ConditionInspectionCurrentConditions)
                .HasForeignKey(d => d.CurrentConditionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_condition_inspections_current_condition");

            entity.HasOne(d => d.Equipment).WithMany(p => p.ConditionInspections)
                .HasForeignKey(d => d.EquipmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_condition_inspections_equipment");

            entity.HasOne(d => d.InspectedByUser).WithMany(p => p.ConditionInspections)
                .HasForeignKey(d => d.InspectedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_condition_inspections_inspected_by");

            entity.HasOne(d => d.InspectionStatus).WithMany(p => p.ConditionInspections)
                .HasForeignKey(d => d.InspectionStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_condition_inspections_status");

            entity.HasOne(d => d.ReleaseCondition).WithMany(p => p.ConditionInspectionReleaseConditions)
                .HasForeignKey(d => d.ReleaseConditionId)
                .HasConstraintName("FK_condition_inspections_release_condition");
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("departments");

            entity.HasIndex(e => e.DepartmentCode, "UQ_departments_department_code").IsUnique();

            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.DepartmentCode)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("department_code");
            entity.Property(e => e.DepartmentName)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("department_name");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
        });

        modelBuilder.Entity<Equipment>(entity =>
        {
            entity.ToTable("equipment");

            entity.HasIndex(e => e.EquipmentCategoryId, "IX_equipment_category_id");

            entity.HasIndex(e => e.CurrentConditionId, "IX_equipment_current_condition_id");

            entity.HasIndex(e => e.LocationId, "IX_equipment_location_id");

            entity.HasIndex(e => e.OperationalStatusId, "IX_equipment_operational_status_id");

            entity.HasIndex(e => e.AssetCode, "UQ_equipment_asset_code").IsUnique();

            entity.Property(e => e.EquipmentId).HasColumnName("equipment_id");
            entity.Property(e => e.AssetCode)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("asset_code");
            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at_utc");
            entity.Property(e => e.CurrentConditionId).HasColumnName("current_condition_id");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .HasColumnName("description");
            entity.Property(e => e.EquipmentCategoryId).HasColumnName("equipment_category_id");
            entity.Property(e => e.EquipmentName)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("equipment_name");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.LocationId).HasColumnName("location_id");
            entity.Property(e => e.OperationalStatusId).HasColumnName("operational_status_id");
            entity.Property(e => e.RowVersion)
                .IsRequired()
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");

            entity.HasOne(d => d.CurrentCondition).WithMany(p => p.EquipmentCurrentConditions)
                .HasForeignKey(d => d.CurrentConditionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_equipment_current_condition");

            entity.HasOne(d => d.EquipmentCategory).WithMany(p => p.Equipment)
                .HasForeignKey(d => d.EquipmentCategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_equipment_categories");

            entity.HasOne(d => d.Location).WithMany(p => p.Equipment)
                .HasForeignKey(d => d.LocationId)
                .HasConstraintName("FK_equipment_locations");

            entity.HasOne(d => d.OperationalStatus).WithMany(p => p.EquipmentOperationalStatuses)
                .HasForeignKey(d => d.OperationalStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_equipment_operational_status");
        });

        modelBuilder.Entity<EquipmentCategory>(entity =>
        {
            entity.ToTable("equipment_categories");

            entity.HasIndex(e => e.CategoryCode, "UQ_equipment_categories_category_code").IsUnique();

            entity.Property(e => e.EquipmentCategoryId).HasColumnName("equipment_category_id");
            entity.Property(e => e.CategoryCode)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("category_code");
            entity.Property(e => e.CategoryName)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("category_name");
        });

        modelBuilder.Entity<EquipmentInventoryBalance>(entity =>
        {
            entity.HasKey(e => e.EquipmentId);

            entity.ToTable("equipment_inventory_balances");

            entity.Property(e => e.EquipmentId)
                .ValueGeneratedNever()
                .HasColumnName("equipment_id");
            entity.Property(e => e.AwaitingInspectionQuantity).HasColumnName("awaiting_inspection_quantity");
            entity.Property(e => e.MaintenanceQuantity).HasColumnName("maintenance_quantity");
            entity.Property(e => e.ReleasedQuantity).HasColumnName("released_quantity");
            entity.Property(e => e.ReservedQuantity).HasColumnName("reserved_quantity");
            entity.Property(e => e.RowVersion)
                .IsRequired()
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.TotalQuantity).HasColumnName("total_quantity");
            entity.Property(e => e.UnreconciledQuantity).HasColumnName("unreconciled_quantity");

            entity.HasOne(d => d.Equipment).WithOne(p => p.EquipmentInventoryBalance)
                .HasForeignKey<EquipmentInventoryBalance>(d => d.EquipmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_balances_equipment");
        });

        modelBuilder.Entity<InspectionStatus>(entity =>
        {
            entity.HasKey(e => e.StatusId);

            entity.ToTable("inspection_statuses");

            entity.HasIndex(e => e.Code, "UQ_inspection_statuses_code").IsUnique();

            entity.Property(e => e.StatusId).HasColumnName("status_id");
            entity.Property(e => e.Code)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("description");
        });

        modelBuilder.Entity<InventoryMovement>(entity =>
        {
            entity.HasKey(e => e.MovementId);

            entity.ToTable("inventory_movements");

            entity.HasIndex(e => new { e.EquipmentId, e.OccurredAtUtc }, "IX_inventory_movements_equipment").IsDescending(false, true);

            entity.HasIndex(e => e.PerformedByUserId, "IX_inventory_movements_performed_by");

            entity.HasIndex(e => e.ReferenceNumber, "IX_inventory_movements_reference_number");

            entity.Property(e => e.MovementId).HasColumnName("movement_id");
            entity.Property(e => e.EquipmentId).HasColumnName("equipment_id");
            entity.Property(e => e.FromState)
                .HasMaxLength(50)
                .HasColumnName("from_state");
            entity.Property(e => e.MovementType)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("movement_type");
            entity.Property(e => e.OccurredAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("occurred_at_utc");
            entity.Property(e => e.PerformedByUserId).HasColumnName("performed_by_user_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.ReferenceNumber)
                .HasMaxLength(100)
                .HasColumnName("reference_number");
            entity.Property(e => e.ToState)
                .HasMaxLength(50)
                .HasColumnName("to_state");

            entity.HasOne(d => d.Equipment).WithMany(p => p.InventoryMovements)
                .HasForeignKey(d => d.EquipmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_inventory_movements_equipment");

            entity.HasOne(d => d.PerformedByUser).WithMany(p => p.InventoryMovements)
                .HasForeignKey(d => d.PerformedByUserId)
                .HasConstraintName("FK_inventory_movements_users");
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.ToTable("locations");

            entity.HasIndex(e => e.LocationCode, "UQ_locations_location_code").IsUnique();

            entity.Property(e => e.LocationId).HasColumnName("location_id");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.IsPickupLocation).HasColumnName("is_pickup_location");
            entity.Property(e => e.LocationCode)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("location_code");
            entity.Property(e => e.LocationName)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("location_name");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");

            entity.HasIndex(e => new { e.RecipientUserId, e.IsRead }, "IX_notifications_recipient");

            entity.Property(e => e.NotificationId).HasColumnName("notification_id");
            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at_utc");
            entity.Property(e => e.IsRead).HasColumnName("is_read");
            entity.Property(e => e.LinkUrl)
                .HasMaxLength(300)
                .HasColumnName("link_url");
            entity.Property(e => e.Message)
                .IsRequired()
                .HasMaxLength(1000)
                .HasColumnName("message");
            entity.Property(e => e.NotificationType)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("notification_type");
            entity.Property(e => e.RecipientUserId).HasColumnName("recipient_user_id");
            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("title");

            entity.HasOne(d => d.RecipientUser).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.RecipientUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_notifications_recipient");
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.ToTable("reservations");

            entity.HasIndex(e => new { e.BorrowerUserId, e.StatusId }, "IX_reservations_borrower");

            entity.HasIndex(e => new { e.EquipmentId, e.StatusId }, "IX_reservations_equipment");

            entity.HasIndex(e => e.PickupLocationId, "IX_reservations_pickup_location");

            entity.HasIndex(e => e.BorrowingPolicyVersionId, "IX_reservations_policy_version");

            entity.HasIndex(e => new { e.StatusId, e.ExpectedReturnAtUtc }, "IX_reservations_status");

            entity.HasIndex(e => e.ReservationNumber, "UQ_reservations_reservation_number").IsUnique();

            entity.Property(e => e.ReservationId).HasColumnName("reservation_id");
            entity.Property(e => e.ApprovedQuantity).HasColumnName("approved_quantity");
            entity.Property(e => e.BorrowerUserId).HasColumnName("borrower_user_id");
            entity.Property(e => e.BorrowingPolicyVersionId).HasColumnName("borrowing_policy_version_id");
            entity.Property(e => e.CompletedAtUtc)
                .HasPrecision(3)
                .HasColumnName("completed_at_utc");
            entity.Property(e => e.EquipmentId).HasColumnName("equipment_id");
            entity.Property(e => e.ExpectedReturnAtUtc)
                .HasPrecision(3)
                .HasColumnName("expected_return_at_utc");
            entity.Property(e => e.HeldQuantity).HasColumnName("held_quantity");
            entity.Property(e => e.PickupLocationId).HasColumnName("pickup_location_id");
            entity.Property(e => e.Purpose)
                .HasMaxLength(500)
                .HasColumnName("purpose");
            entity.Property(e => e.RequestedQuantity).HasColumnName("requested_quantity");
            entity.Property(e => e.RequestedStartAtUtc)
                .HasPrecision(3)
                .HasColumnName("requested_start_at_utc");
            entity.Property(e => e.ReservationNumber)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("reservation_number");
            entity.Property(e => e.RowVersion)
                .IsRequired()
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.StatusId).HasColumnName("status_id");
            entity.Property(e => e.SubmittedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("submitted_at_utc");

            entity.HasOne(d => d.BorrowerUser).WithMany(p => p.Reservations)
                .HasForeignKey(d => d.BorrowerUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_reservations_borrower");

            entity.HasOne(d => d.BorrowingPolicyVersion).WithMany(p => p.Reservations)
                .HasForeignKey(d => d.BorrowingPolicyVersionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_reservations_policy_version");

            entity.HasOne(d => d.Equipment).WithMany(p => p.Reservations)
                .HasForeignKey(d => d.EquipmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_reservations_equipment");

            entity.HasOne(d => d.PickupLocation).WithMany(p => p.Reservations)
                .HasForeignKey(d => d.PickupLocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_reservations_pickup_location");

            entity.HasOne(d => d.Status).WithMany(p => p.Reservations)
                .HasForeignKey(d => d.StatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_reservations_status");
        });

        modelBuilder.Entity<ReservationStatus>(entity =>
        {
            entity.HasKey(e => e.StatusId);

            entity.ToTable("reservation_statuses");

            entity.HasIndex(e => e.Code, "UQ_reservation_statuses_code").IsUnique();

            entity.Property(e => e.StatusId).HasColumnName("status_id");
            entity.Property(e => e.Code)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("description");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");

            entity.HasIndex(e => e.RoleCode, "UQ_roles_role_code").IsUnique();

            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.IsBorrowerRole).HasColumnName("is_borrower_role");
            entity.Property(e => e.RoleCode)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("role_code");
            entity.Property(e => e.RoleName)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("role_name");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");

            entity.HasIndex(e => e.AccountStatusId, "IX_users_account_status_id");

            entity.HasIndex(e => e.DepartmentId, "IX_users_department_id");

            entity.HasIndex(e => e.RoleId, "IX_users_role_id");

            entity.HasIndex(e => e.AccountIdentifier, "UQ_users_account_identifier").IsUnique();

            entity.HasIndex(e => e.SchoolEmail, "UQ_users_school_email").IsUnique();

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.AccountIdentifier)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("account_identifier");
            entity.Property(e => e.AccountStatusId).HasColumnName("account_status_id");
            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at_utc");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.FirstName)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("first_name");
            entity.Property(e => e.LastName)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("last_name");
            entity.Property(e => e.PasswordHash)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.RowVersion)
                .IsRequired()
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.SchoolEmail)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("school_email");

            entity.HasOne(d => d.AccountStatus).WithMany(p => p.Users)
                .HasForeignKey(d => d.AccountStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_users_account_statuses");

            entity.HasOne(d => d.Department).WithMany(p => p.Users)
                .HasForeignKey(d => d.DepartmentId)
                .HasConstraintName("FK_users_departments");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_users_roles");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
