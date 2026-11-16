using System.Collections.Generic;

namespace ASI.Basecode.Data.Models;

/// <summary>
/// Profile fields added by CampusPulse on top of the scaffolded users table.
/// Kept in a separate partial so re-scaffolding the database-first model never clobbers it.
/// </summary>
public partial class User
{
    /// <summary>Editable contact number used for reservation and due-date notices.</summary>
    public string ContactNumber { get; set; }

    /// <summary>Optional alternate contact (guardian, landline).</summary>
    public string AlternateContact { get; set; }

    /// <summary>Web-relative path to the uploaded profile photo (e.g. /uploads/profiles/x.png); null shows initials.</summary>
    public string ProfileImagePath { get; set; }

    public virtual UserPreference Preference { get; set; }
}
