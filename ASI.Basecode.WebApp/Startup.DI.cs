using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Services;
using ASI.Basecode.Services.ViewModelClasses;
using ASI.Basecode.WebApp.Models;
using ASI.Basecode.WebApp.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ASI.Basecode.WebApp
{
    // Other services configuration
    internal partial class StartupConfigurer
    {
        /// <summary>
        /// Configures the other services.
        /// </summary>
        private void ConfigureOtherServices()
        {
            // Framework
            this._services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            this._services.TryAddSingleton<IActionContextAccessor, ActionContextAccessor>();

            // Common
            this._services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Cross-role notifications (in-app + pluggable email transport)
            this._services.AddScoped<IEmailSender, LoggingEmailSender>();
            this._services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

            // Current operator identity (cookie claims, with session fallback)
            this._services.AddScoped<ICurrentUserService, CurrentUserService>();

            // Authentication (1.1-1.4) and startup seeding
            this._services.AddScoped<IAuthService, AuthService>();
            this._services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();

            // Role dashboards (2.0 / 3.0) and profile & settings (2.10 / 3.10 / 4.8)
            this._services.AddScoped<IDashboardService, DashboardService>();
            this._services.AddScoped<IProfileService, ProfileService>();

            // Borrower borrowings (2.6 / 2.7 / 2.8)
            this._services.AddScoped<IBorrowingService, BorrowingService>();

            // Equipment administration
            this._services.AddScoped<IEquipmentRepository, EquipmentRepository>();
            this._services.AddScoped<IEquipmentService, EquipmentService>();

            // User management
            this._services.AddScoped<IUserRepository, UserRepository>();
            this._services.AddScoped<IUserService, UserService>();

            // Borrower reservations
            this._services.AddScoped<IReservationRepository, ReservationRepository>();
            this._services.AddScoped<IReservationService, ReservationService>();

            // Reservation lifecycle automation (expiry, overdue, due-soon reminders)
            this._services.Configure<ASI.Basecode.Services.ReservationLifecycleOptions>(
                this.Configuration.GetSection(ASI.Basecode.Services.ReservationLifecycleOptions.SectionName));
            this._services.AddScoped<IReservationLifecycleService, ReservationLifecycleService>();
            this._services.AddHostedService<ASI.Basecode.WebApp.Mvc.ReservationLifecycleWorker>();

            // Announcements, support tickets, verification stats and borrower history
            this._services.AddScoped<ICommunicationService, CommunicationService>();

            // Custodian assignment, routing and scoping
            this._services.AddScoped<ICustodianAssignmentService, CustodianAssignmentService>();

            // Custodian console
            this._services.AddScoped<ICustodianRepository, CustodianRepository>();
            this._services.AddScoped<ICustodianService, CustodianService>();

            // Return processing + condition inspection
            this._services.AddScoped<IReturnProcessingRepository, ReturnProcessingRepository>();
            this._services.AddScoped<IReturnProcessingService, ReturnProcessingService>();

            // Monitoring & audit
            this._services.AddScoped<IMonitoringRepository, MonitoringRepository>();
            this._services.AddScoped<IMonitoringService, MonitoringService>();

            // System settings + notifications
            this._services.AddScoped<ISettingsRepository, SettingsRepository>();
            this._services.AddScoped<ISettingsService, SettingsService>();

            this._services.AddHttpClient();
        }
    }
}
