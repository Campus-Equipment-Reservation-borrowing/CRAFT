using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ViewModelClasses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Services.Services
{
    /// <summary>
    /// Authentication + account self-service (screens 1.1 - 1.4).
    /// Password hashing lives in <see cref="PasswordManager"/>; failed attempts and the lockout
    /// window are persisted on <c>users</c> so they survive restarts.
    /// </summary>
    public class AuthService : ServiceBase, IAuthService
    {
        private const int MaxFailedAttempts = 5;
        private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromHours(24);
        private const string LockedOutMessage = "Too many failed attempts. Please try again in 15 minutes.";

        private readonly IUnitOfWork _unitOfWork;

        private AllianceDbContext DbContext => (AllianceDbContext)_unitOfWork.Database;

        public AuthService(ILoggerFactory loggerFactory, IUnitOfWork unitOfWork)
            : base(loggerFactory)
        {
            this._unitOfWork = unitOfWork;
        }

        public (SignInResult Result, AuthenticatedUserViewModel User, string Error) SignIn(string userId, string password)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrEmpty(password))
            {
                return (SignInResult.UserNotFound, null, "Please enter your username and password.");
            }

            var key = userId.Trim().ToLowerInvariant();
            var user = this.DbContext.Users
                .Include(u => u.Role)
                .Include(u => u.Department)
                .Include(u => u.AccountStatus)
                .FirstOrDefault(u =>
                    u.SchoolEmail.ToLower() == key ||
                    u.AccountIdentifier.ToLower() == key);

            if (user == null)
            {
                return (SignInResult.UserNotFound, null, "User Account does not exist.");
            }

            var now = DateTime.UtcNow;
            if (user.LockoutUntilUtc.HasValue && user.LockoutUntilUtc.Value > now)
            {
                return (SignInResult.LockedOut, null, LockedOutMessage);
            }

            if (!PasswordManager.Verify(password, user.PasswordHash))
            {
                user.FailedLoginCount++;
                var lockedNow = user.FailedLoginCount >= MaxFailedAttempts;
                if (lockedNow)
                {
                    user.LockoutUntilUtc = now.Add(LockoutWindow);
                    user.FailedLoginCount = 0;
                }

                this._unitOfWork.SaveChanges();
                this.WriteAudit(user.UserId, user.AccountIdentifier, "LOGIN_FAILED", "User",
                    lockedNow
                        ? $"Failed sign-in for '{user.AccountIdentifier}'; locked out for {LockoutWindow.TotalMinutes:0} minutes."
                        : $"Failed sign-in for '{user.AccountIdentifier}'.");

                return lockedNow
                    ? (SignInResult.LockedOut, null, LockedOutMessage)
                    : (SignInResult.InvalidPassword, null, "Invalid username or password.");
            }

            var statusCode = user.AccountStatus?.Code;
            if (statusCode == AccountStatusCodes.Suspended || statusCode == AccountStatusCodes.Inactive)
            {
                return (SignInResult.AccountDisabled, null, "This account has been suspended. Please contact the administrator.");
            }

            if (statusCode == AccountStatusCodes.Locked)
            {
                return (SignInResult.AccountDisabled, null, "This account is locked. Please contact the administrator.");
            }

            if (user.FailedLoginCount != 0 || user.LockoutUntilUtc.HasValue)
            {
                user.FailedLoginCount = 0;
                user.LockoutUntilUtc = null;
                this._unitOfWork.SaveChanges();
            }

            this.WriteAudit(user.UserId, user.AccountIdentifier, "LOGIN", "User",
                $"Signed in as '{user.AccountIdentifier}'.");

            return (SignInResult.Success, this.MapUser(user), null);
        }

        public void RecordSignOut(long userId)
        {
            try
            {
                var identifier = this.DbContext.Users
                    .AsNoTracking()
                    .Where(u => u.UserId == userId)
                    .Select(u => u.AccountIdentifier)
                    .FirstOrDefault();
                if (identifier != null)
                {
                    this.WriteAudit(userId, identifier, "LOGOUT", "User", $"Signed out '{identifier}'.");
                }
            }
            catch (Exception ex)
            {
                // Sign-out must never fail because of auditing.
                this._logger.LogWarning(ex, "Failed to audit sign-out for user {UserId}.", userId);
            }
        }

        public (bool Success, string Error, long UserId) Register(RegisterViewModel form)
        {
            try
            {
                if (form == null)
                {
                    return (false, "Please complete all required fields.", 0);
                }

                form.FirstName = form.FirstName?.Trim();
                form.LastName = form.LastName?.Trim();
                form.AccountIdentifier = form.AccountIdentifier?.Trim();
                form.SchoolEmail = form.SchoolEmail?.Trim();

                if (string.IsNullOrWhiteSpace(form.FirstName) ||
                    string.IsNullOrWhiteSpace(form.LastName) ||
                    string.IsNullOrWhiteSpace(form.AccountIdentifier) ||
                    string.IsNullOrWhiteSpace(form.SchoolEmail) ||
                    string.IsNullOrEmpty(form.Password) ||
                    form.RoleId == null ||
                    form.DepartmentId == null)
                {
                    return (false, "Please complete all required fields.", 0);
                }

                if (!form.PrivacyConsent)
                {
                    return (false, "You must accept the Data Privacy consent to continue.", 0);
                }

                if (!PasswordManager.MeetsPolicy(form.Password))
                {
                    return (false, "Password must be at least 8 characters with 1 uppercase letter and 1 number.", 0);
                }

                if (!string.Equals(form.Password, form.ConfirmPassword, StringComparison.Ordinal))
                {
                    return (false, "Passwords do not match.", 0);
                }

                var role = this.DbContext.Roles.FirstOrDefault(r => r.RoleId == form.RoleId.Value);
                if (role == null || !role.IsBorrowerRole)
                {
                    return (false, "Only Student or Faculty accounts can be registered here.", 0);
                }

                var identifierTaken = this.DbContext.Users
                    .Any(u => u.AccountIdentifier.ToLower() == form.AccountIdentifier.ToLower());
                var emailTaken = this.DbContext.Users
                    .Any(u => u.SchoolEmail.ToLower() == form.SchoolEmail.ToLower());

                if (identifierTaken || emailTaken)
                {
                    return (false, "An account with this email or ID already exists.", 0);
                }

                var unverified = this.DbContext.AccountStatuses
                    .FirstOrDefault(s => s.Code == AccountStatusCodes.Unverified)
                    ?? this.DbContext.AccountStatuses.FirstOrDefault(s => s.Code == AccountStatusCodes.Active);

                if (unverified == null)
                {
                    return (false, "Account statuses are not configured. Contact the administrator.", 0);
                }

                var entity = new User
                {
                    FirstName = form.FirstName,
                    LastName = form.LastName,
                    AccountIdentifier = form.AccountIdentifier,
                    SchoolEmail = form.SchoolEmail,
                    PasswordHash = PasswordManager.Hash(form.Password),
                    RoleId = role.RoleId,
                    DepartmentId = form.DepartmentId,
                    AccountStatusId = unverified.AccountStatusId,
                    CreatedAtUtc = default,
                };

                this.DbContext.Users.Add(entity);
                this._unitOfWork.SaveChanges();

                this.WriteAudit(entity.UserId, entity.AccountIdentifier, "REGISTER", "User",
                    $"Self-registered borrower account '{entity.FirstName} {entity.LastName}' ({entity.AccountIdentifier}).");

                return (true, null, entity.UserId);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Registration failed for {Identifier}.", form?.AccountIdentifier);
                return (false, "Unable to save changes. Please try again.", 0);
            }
        }

        public (bool Success, string Error, string Token) CreatePasswordResetToken(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return (false, "Please enter your registered email address.", null);
            }

            var key = email.Trim().ToLowerInvariant();
            var user = this.DbContext.Users.FirstOrDefault(u => u.SchoolEmail.ToLower() == key);
            if (user == null)
            {
                return (false, "User Account does not exist.", null);
            }

            var token = PasswordManager.NewToken();
            var now = DateTime.UtcNow;

            // Invalidate any earlier outstanding tokens for this user.
            var previous = this.DbContext.PasswordResetTokens
                .Where(t => t.UserId == user.UserId && t.UsedAtUtc == null && t.ExpiresAtUtc > now)
                .ToList();
            foreach (var old in previous)
            {
                old.UsedAtUtc = now;
            }

            this.DbContext.PasswordResetTokens.Add(new PasswordResetToken
            {
                UserId = user.UserId,
                Token = token,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.Add(ResetTokenLifetime),
            });

            this._unitOfWork.SaveChanges();

            return (true, null, token);
        }

        public bool IsResetTokenValid(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var now = DateTime.UtcNow;
            return this.DbContext.PasswordResetTokens
                .Any(t => t.Token == token && t.UsedAtUtc == null && t.ExpiresAtUtc > now);
        }

        public (bool Success, string Error) ResetPassword(ResetPasswordViewModel form)
        {
            try
            {
                if (form == null || string.IsNullOrWhiteSpace(form.Token))
                {
                    return (false, "This reset link is no longer valid. Please request a new one.");
                }

                if (string.IsNullOrEmpty(form.NewPassword) || string.IsNullOrEmpty(form.ConfirmPassword))
                {
                    return (false, "Please complete all required fields.");
                }

                if (!PasswordManager.MeetsPolicy(form.NewPassword))
                {
                    return (false, "Password must be at least 8 characters with 1 uppercase letter and 1 number.");
                }

                if (!string.Equals(form.NewPassword, form.ConfirmPassword, StringComparison.Ordinal))
                {
                    return (false, "Passwords do not match.");
                }

                var now = DateTime.UtcNow;
                var reset = this.DbContext.PasswordResetTokens
                    .FirstOrDefault(t => t.Token == form.Token && t.UsedAtUtc == null && t.ExpiresAtUtc > now);

                if (reset == null)
                {
                    return (false, "This reset link is no longer valid. Please request a new one.");
                }

                var user = this.DbContext.Users.FirstOrDefault(u => u.UserId == reset.UserId);
                if (user == null)
                {
                    return (false, "This reset link is no longer valid. Please request a new one.");
                }

                user.PasswordHash = PasswordManager.Hash(form.NewPassword);
                user.MustChangePassword = false;
                user.FailedLoginCount = 0;
                user.LockoutUntilUtc = null;
                reset.UsedAtUtc = now;

                this._unitOfWork.SaveChanges();

                this.WriteAudit(user.UserId, user.AccountIdentifier, "PASSWORD_RESET", "User",
                    $"Password reset completed for '{user.FirstName} {user.LastName}' ({user.AccountIdentifier}).");

                return (true, null);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Password reset failed.");
                return (false, "Unable to save changes. Please try again.");
            }
        }

        public (bool Success, string Error) ChangePassword(long userId, string currentPassword, string newPassword)
        {
            try
            {
                var user = this.DbContext.Users.FirstOrDefault(u => u.UserId == userId);
                if (user == null)
                {
                    return (false, "User Account does not exist.");
                }

                if (!PasswordManager.Verify(currentPassword, user.PasswordHash))
                {
                    return (false, "The current password you entered is incorrect.");
                }

                if (!PasswordManager.MeetsPolicy(newPassword))
                {
                    return (false, "Password must be at least 8 characters with 1 uppercase letter and 1 number.");
                }

                if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
                {
                    return (false, "Choose a new password that is different from the current one.");
                }

                user.PasswordHash = PasswordManager.Hash(newPassword);
                user.MustChangePassword = false;
                this._unitOfWork.SaveChanges();

                this.WriteAudit(user.UserId, user.AccountIdentifier, "PASSWORD_CHANGE", "User",
                    $"Password changed for '{user.FirstName} {user.LastName}'.");

                return (true, null);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Password change failed for user {UserId}.", userId);
                return (false, "Unable to save changes. Please try again.");
            }
        }

        public List<LookupViewModel> GetBorrowerRoleOptions()
        {
            return this.DbContext.Roles
                .AsNoTracking()
                .Where(r => r.IsBorrowerRole)
                .OrderBy(r => r.RoleName)
                .Select(r => new LookupViewModel { Id = r.RoleId, Text = r.RoleName })
                .ToList();
        }

        public List<LookupViewModel> GetDepartmentOptions()
        {
            return this.DbContext.Departments
                .AsNoTracking()
                .Where(d => d.IsActive)
                .OrderBy(d => d.DepartmentName)
                .Select(d => new LookupViewModel { Id = d.DepartmentId, Text = d.DepartmentName })
                .ToList();
        }

        public AuthenticatedUserViewModel GetAuthenticatedUser(long userId)
        {
            var user = this.DbContext.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .Include(u => u.Department)
                .Include(u => u.AccountStatus)
                .FirstOrDefault(u => u.UserId == userId);

            return user == null ? null : this.MapUser(user);
        }

        private AuthenticatedUserViewModel MapUser(User user)
        {
            return new AuthenticatedUserViewModel
            {
                UserId = user.UserId,
                FullName = $"{user.FirstName} {user.LastName}".Trim(),
                AccountIdentifier = user.AccountIdentifier,
                SchoolEmail = user.SchoolEmail,
                RoleCode = user.Role?.RoleCode,
                RoleName = user.Role?.RoleName,
                IsBorrowerRole = user.Role?.IsBorrowerRole ?? false,
                DepartmentName = user.Department?.DepartmentName,
                AccountStatusCode = user.AccountStatus?.Code,
                MustChangePassword = user.MustChangePassword,
            };
        }

        private void WriteAudit(long? userId, string referenceNumber, string actionType, string entityType, string details)
        {
            this.DbContext.AuditLogs.Add(new AuditLog
            {
                ActorUserId = userId,
                ActionType = actionType,
                EntityType = entityType,
                EntityId = userId,
                ReferenceNumber = referenceNumber,
                Details = details,
                OccurredAtUtc = DateTime.UtcNow,
            });
            this._unitOfWork.SaveChanges();
        }
    }
}
