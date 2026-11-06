namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// Stand-in identity provider until web login/auth is reintroduced.
    /// Returns the current operator (admin or custodian) acting on the data.
    /// </summary>
    public interface ICurrentUserService
    {
        /// <summary>
        /// User id of the operator, or null when nobody is selected yet.
        /// </summary>
        long? GetActorUserId();

        /// <summary>
        /// Human readable name of the operator for audit details.
        /// </summary>
        string GetActorName();

        /// <summary>
        /// Role code of the signed-in user (STUDENT / FACULTY / TECHNICIAN / ADMIN), or null.
        /// </summary>
        string GetActorRoleCode();

        /// <summary>
        /// True for administrators, who see every custodian's work unscoped.
        /// </summary>
        bool IsActorAdmin();
    }
}
