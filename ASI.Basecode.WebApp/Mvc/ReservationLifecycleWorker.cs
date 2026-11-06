using ASI.Basecode.Services;
using ASI.Basecode.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Mvc
{
    /// <summary>
    /// Runs <see cref="IReservationLifecycleService.RunOnce"/> on an interval, each pass in its own
    /// DI scope (the DbContext and unit of work are scoped).
    /// </summary>
    public class ReservationLifecycleWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReservationLifecycleWorker> _logger;
        private readonly ReservationLifecycleOptions _options;

        public ReservationLifecycleWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<ReservationLifecycleWorker> logger,
            IOptions<ReservationLifecycleOptions> options)
        {
            this._scopeFactory = scopeFactory;
            this._logger = logger;
            this._options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!this._options.Enabled)
            {
                this._logger.LogInformation("Reservation lifecycle job is disabled.");
                return;
            }

            // Let startup seeding (schema + codebooks) finish first.
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            var interval = TimeSpan.FromMinutes(Math.Max(1, this._options.IntervalMinutes));
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = this._scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IReservationLifecycleService>();
                    var result = service.RunOnce();
                    if (result.Expired + result.MarkedOverdue + result.DueReminders > 0)
                    {
                        this._logger.LogInformation(
                            "Lifecycle pass: {Expired} expired, {Overdue} marked overdue, {Reminders} due reminders.",
                            result.Expired, result.MarkedOverdue, result.DueReminders);
                    }
                }
                catch (Exception ex)
                {
                    // One bad pass must not stop the job.
                    this._logger.LogError(ex, "Reservation lifecycle pass failed.");
                }

                await Task.Delay(interval, stoppingToken);
            }
        }
    }
}
