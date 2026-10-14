using System.Text.Json.Serialization;

namespace NextProject.Configuration;

/// <summary>
/// The configuration (JSON, <c>{data}/config/app.json</c>): how this installation is deployed and what the administrator set up.
/// The sections <see cref="Database"/> and <see cref="Server"/> can be overridden by environment variables
/// (<c>NEXTPROJECT__Database__Host</c>); the rest is edited on the administration pages.
/// </summary>
public sealed class AppConfig
{
    public DatabaseConfig Database { get; set; } = new();

    public ServerConfig Server { get; set; } = new();

    public OrganizationConfig Organization { get; set; } = new();

    public StorageConfig Storage { get; set; } = new();

    public SecurityConfig Security { get; set; } = new();

    public BackupConfig Backup { get; set; } = new();
}

/// <summary>The company that uses the installation: its name stands on the pages that are shared by a link.</summary>
public sealed class OrganizationConfig
{
    public string Name { get; set; } = "";

    /// <summary>Todos that are due within this many days count as "due soon" on the dashboard.</summary>
    public int DueSoonDays { get; set; } = 7;

    /// <summary>A project is flagged as endangered when it has this many overdue todos (or a blocked one).</summary>
    public int OverdueLimit { get; set; } = 3;
}

public sealed class DatabaseConfig
{
    public string Host { get; set; } = "db";

    public int Port { get; set; } = 5432;

    public string Database { get; set; } = "nextproject";

    public string Username { get; set; } = "postgres";

    public string Password { get; set; } = "nextproject";
}

public sealed class ServerConfig
{
    /// <summary>The address people use, for the links that are handed on ("https://projekte.example.de"). Empty: taken from the request.</summary>
    public string PublicUrl { get; set; } = "";

    /// <summary>"Europe/Berlin": the time zone of the installation, for dates and schedules.</summary>
    public string TimeZone { get; set; } = "Europe/Berlin";

    /// <summary>The cookie is marked secure when the site is reached over HTTPS (also behind a reverse proxy that says so).</summary>
    public bool ForceSecureCookie { get; set; }
}

public enum StorageMode
{
    /// <summary>The files are kept in the data volume.</summary>
    Local,
    /// <summary>The files are kept on a share of a NAS (SMB).</summary>
    Smb,
}

public sealed class StorageConfig
{
    public StorageMode Mode { get; set; } = StorageMode.Local;

    public string Host { get; set; } = "";

    public int Port { get; set; } = 445;

    public string Share { get; set; } = "";

    /// <summary>A folder inside the share.</summary>
    public string Path { get; set; } = "NextProject";

    public string Domain { get; set; } = "";

    public string UserName { get; set; } = "";

    public string PasswordProtected { get; set; } = "";
}

public sealed class SecurityConfig
{
    public int SessionDays { get; set; } = 14;

    /// <summary>Administrators have to set up an authenticator app.</summary>
    public bool RequireTotpForAdmins { get; set; }

    /// <summary>Default life of a share link, in days.</summary>
    public int ShareLinkDays { get; set; } = 14;

    [JsonIgnore]
    public int MaxFailedLogins => 5;
}

public sealed class BackupConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>The scratch folder of a backup; empty: {data}/tmp.</summary>
    public string TempDirectory { get; set; } = "";
}
