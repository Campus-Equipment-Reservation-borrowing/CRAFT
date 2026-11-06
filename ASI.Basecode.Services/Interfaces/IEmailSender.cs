namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// Outbound email seam. The registered implementation only logs; swap in an SMTP sender
    /// (configured from user-secrets / environment, never committed) to deliver real mail.
    /// </summary>
    public interface IEmailSender
    {
        void Send(string toAddress, string subject, string body);
    }
}
