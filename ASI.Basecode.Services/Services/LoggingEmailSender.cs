using ASI.Basecode.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace ASI.Basecode.Services.Services
{
    /// <summary>
    /// Placeholder transport: records that an email would have been sent. Message bodies are
    /// not logged because they can contain account details.
    /// </summary>
    public class LoggingEmailSender : ServiceBase, IEmailSender
    {
        public LoggingEmailSender(ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
        }

        public void Send(string toAddress, string subject, string body)
        {
            this._logger.LogInformation("Email transport not configured; skipped email to {To} with subject {Subject}.",
                toAddress, subject);
        }
    }
}
