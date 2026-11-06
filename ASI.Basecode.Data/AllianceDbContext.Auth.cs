using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data;

/// <summary>
/// Auth-related additions to the scaffolded <see cref="AllianceDbContext"/>.
/// Kept in its own partial file so re-scaffolding the database-first model
/// never clobbers it; configuration hangs off the OnModelCreatingPartial hook.
/// </summary>
public partial class AllianceDbContext
{
    public virtual DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

    public virtual DbSet<UserPreference> UserPreferences { get; set; }

    public virtual DbSet<CustodianAssignment> CustodianAssignments { get; set; }

    public virtual DbSet<Announcement> Announcements { get; set; }

    public virtual DbSet<SupportTicket> SupportTickets { get; set; }

    public virtual DbSet<SupportTicketReply> SupportTicketReplies { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        ConfigureCustodianAssignments(modelBuilder);
        ConfigureCommunication(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.ContactNumber)
                .HasMaxLength(50)
                .HasColumnName("contact_number");
            entity.Property(e => e.AlternateContact)
                .HasMaxLength(100)
                .HasColumnName("alternate_contact");
            entity.Property(e => e.VerificationNote)
                .HasMaxLength(500)
                .HasColumnName("verification_note");
            entity.Property(e => e.VerifiedAtUtc).HasColumnName("verified_at_utc");
            entity.Property(e => e.VerifiedByUserId).HasColumnName("verified_by_user_id");
            entity.Property(e => e.MustChangePassword).HasColumnName("must_change_password");
            entity.Property(e => e.FailedLoginCount).HasColumnName("failed_login_count");
            entity.Property(e => e.LockoutUntilUtc).HasColumnName("lockout_until_utc");
        });

        modelBuilder.Entity<Equipment>(entity =>
        {
            entity.Property(e => e.ImagePath)
                .HasMaxLength(500)
                .HasColumnName("image_path");
        });

        modelBuilder.Entity<Borrowing>(entity =>
        {
            entity.Property(e => e.DueReminderSentAtUtc).HasColumnName("due_reminder_sent_at_utc");
            entity.Property(e => e.OverdueNotifiedAtUtc).HasColumnName("overdue_notified_at_utc");
        });

        modelBuilder.Entity<ConditionInspection>(entity =>
        {
            entity.Property(e => e.GoodQuantity).HasColumnName("good_quantity");
            entity.Property(e => e.DamagedQuantity).HasColumnName("damaged_quantity");
            entity.Property(e => e.LostQuantity).HasColumnName("lost_quantity");
            entity.Property(e => e.RepairCost).HasColumnType("decimal(12, 2)").HasColumnName("repair_cost");
            entity.Property(e => e.PenaltyAmount).HasColumnType("decimal(12, 2)").HasColumnName("penalty_amount");
            entity.Property(e => e.PenaltyNote).HasMaxLength(500).HasColumnName("penalty_note");
        });

        modelBuilder.Entity<UserPreference>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.ToTable("user_preferences");

            entity.Property(e => e.UserId).ValueGeneratedNever().HasColumnName("user_id");
            entity.Property(e => e.NotifyReservationUpdates).HasColumnName("notify_reservation_updates");
            entity.Property(e => e.NotifyDueReminders).HasColumnName("notify_due_reminders");
            entity.Property(e => e.NotifyOverdueAlerts).HasColumnName("notify_overdue_alerts");
            entity.Property(e => e.NotifyReturnConfirmations).HasColumnName("notify_return_confirmations");
            entity.Property(e => e.NotifyAnnouncements).HasColumnName("notify_announcements");
            entity.Property(e => e.UpdatedAtUtc)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at_utc");

            entity.HasOne(d => d.User).WithOne(p => p.Preference)
                .HasForeignKey<UserPreference>(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_user_preferences_users");
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(e => e.PasswordResetTokenId);

            entity.ToTable("password_reset_tokens");

            entity.HasIndex(e => e.Token, "UQ_password_reset_tokens_token").IsUnique();

            entity.Property(e => e.PasswordResetTokenId).HasColumnName("password_reset_token_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Token)
                .HasMaxLength(200)
                .HasColumnName("token");
            entity.Property(e => e.CreatedAtUtc)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at_utc");
            entity.Property(e => e.ExpiresAtUtc).HasColumnName("expires_at_utc");
            entity.Property(e => e.UsedAtUtc).HasColumnName("used_at_utc");

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_password_reset_tokens_users");
        });
    }

    /// <summary>Announcements and support tickets (+ replies).</summary>
    private static void ConfigureCommunication(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Announcement>(entity =>
        {
            entity.HasKey(e => e.AnnouncementId);
            entity.ToTable("announcements");
            entity.Property(e => e.AnnouncementId).HasColumnName("announcement_id");
            entity.Property(e => e.Title).HasMaxLength(200).HasColumnName("title");
            entity.Property(e => e.Body).HasMaxLength(2000).HasColumnName("body");
            entity.Property(e => e.TargetRoles).HasMaxLength(100).HasColumnName("target_roles");
            entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at_utc");
            entity.Property(e => e.ExpiresAtUtc).HasColumnName("expires_at_utc");
            entity.HasOne(d => d.CreatedByUser).WithMany()
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_announcements_created_by");
        });

        modelBuilder.Entity<SupportTicket>(entity =>
        {
            entity.HasKey(e => e.TicketId);
            entity.ToTable("support_tickets");
            entity.Property(e => e.TicketId).HasColumnName("ticket_id");
            entity.Property(e => e.RequesterUserId).HasColumnName("requester_user_id");
            entity.Property(e => e.Category).HasMaxLength(20).HasColumnName("category");
            entity.Property(e => e.Subject).HasMaxLength(200).HasColumnName("subject");
            entity.Property(e => e.Body).HasMaxLength(4000).HasColumnName("body");
            entity.Property(e => e.Status).HasMaxLength(20).HasColumnName("status");
            entity.Property(e => e.AssignedToUserId).HasColumnName("assigned_to_user_id");
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at_utc");
            entity.Property(e => e.ResolvedAtUtc).HasColumnName("resolved_at_utc");
            entity.HasOne(d => d.RequesterUser).WithMany()
                .HasForeignKey(d => d.RequesterUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_support_tickets_requester");
            entity.HasOne(d => d.AssignedToUser).WithMany()
                .HasForeignKey(d => d.AssignedToUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_support_tickets_assignee");
        });

        modelBuilder.Entity<SupportTicketReply>(entity =>
        {
            entity.HasKey(e => e.ReplyId);
            entity.ToTable("support_ticket_replies");
            entity.Property(e => e.ReplyId).HasColumnName("reply_id");
            entity.Property(e => e.TicketId).HasColumnName("ticket_id");
            entity.Property(e => e.AuthorUserId).HasColumnName("author_user_id");
            entity.Property(e => e.Body).HasMaxLength(4000).HasColumnName("body");
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at_utc");
            entity.HasOne(d => d.Ticket).WithMany(p => p.Replies)
                .HasForeignKey(d => d.TicketId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_support_ticket_replies_ticket");
            entity.HasOne(d => d.AuthorUser).WithMany()
                .HasForeignKey(d => d.AuthorUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_support_ticket_replies_author");
        });
    }

    /// <summary>Custodian ownership table + the routed-custodian column on reservations.</summary>
    private static void ConfigureCustodianAssignments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustodianAssignment>(entity =>
        {
            entity.HasKey(e => e.AssignmentId);
            entity.ToTable("custodian_assignments");

            entity.Property(e => e.AssignmentId).HasColumnName("assignment_id");
            entity.Property(e => e.CustodianUserId).HasColumnName("custodian_user_id");
            entity.Property(e => e.ScopeType).HasMaxLength(20).HasColumnName("scope_type");
            entity.Property(e => e.BorrowerUserId).HasColumnName("borrower_user_id");
            entity.Property(e => e.LocationId).HasColumnName("location_id");
            entity.Property(e => e.EquipmentCategoryId).HasColumnName("equipment_category_id");
            entity.Property(e => e.AssignedByUserId).HasColumnName("assigned_by_user_id");
            entity.Property(e => e.AssignedAtUtc)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("assigned_at_utc");

            // NO ACTION everywhere: SQL Server rejects multiple cascade paths to users.
            entity.HasOne(d => d.CustodianUser).WithMany()
                .HasForeignKey(d => d.CustodianUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_custodian_assignments_custodian");
            entity.HasOne(d => d.BorrowerUser).WithMany()
                .HasForeignKey(d => d.BorrowerUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_custodian_assignments_borrower");
            entity.HasOne(d => d.Location).WithMany()
                .HasForeignKey(d => d.LocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_custodian_assignments_location");
            entity.HasOne(d => d.EquipmentCategory).WithMany()
                .HasForeignKey(d => d.EquipmentCategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_custodian_assignments_category");
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.Property(e => e.AssignedCustodianUserId).HasColumnName("assigned_custodian_user_id");
            entity.HasOne(d => d.AssignedCustodianUser).WithMany()
                .HasForeignKey(d => d.AssignedCustodianUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_reservations_assigned_custodian");
        });
    }
}
