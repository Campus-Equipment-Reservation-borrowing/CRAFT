# AGENTS.md

ASP.NET Core MVC educational scaffold (ASI Bridge/JumpStart). Targets `net9.0`, no tests, no CI, no linter — `dotnet build ASI.Basecode.sln` is the only verification gate.

## Build

- Targets `net9.0`; a newer SDK (verified with .NET 10) builds it. `dotnet build ASI.Basecode.sln` succeeds on this machine. Visual Studio 2022 works too.
- Set `ASI.Basecode.WebApp` as the startup project. It uses `AllianceConnection` in `ASI.Basecode.WebApp/appsettings.json`, currently `(localdb)\MSSQLLocalDB` / `AllianceDB` (the only SQL Server instance installed here). Change it to your SSMS server/database if different.
- The scaffolded schema arrives **empty**. `DatabaseSeeder` (registered in `Startup.DI.cs`, invoked from `Program.cs`) seeds codebooks + demo data, and creates `password_reset_tokens`, the profile contact columns and `user_preferences`, on every startup, idempotently.

## Architecture

Four projects, dependency flow: `WebApp → Services → Data → Resources` (Services and Data also reference Resources directly). `Resources` is the leaf — never reference other projects from it.

- `ASI.Basecode.WebApp` — controllers + Razor views. Controllers must stay thin; push logic down to Services.
- `ASI.Basecode.Services` — business logic, view models, managers. Services extend `ServiceBase`.
- `ASI.Basecode.Data` — EF Core repositories and `AsiBasecodeDBContext` only; no business logic. Repositories extend generic `BaseRepository`.
- `ASI.Basecode.Resources` — `Const`/`Enums`, resx message/label resources for localization.

## Wiring conventions (easy to miss)

- `Program.cs` does not configure the app directly: `Startup.cs` is a `partial StartupConfigurer` split across `Startup.DI.cs`, `Startup.AutoMapper.cs`, and `Startup.Logger.cs`. Register new services/repositories in `Startup.DI.cs` (`ConfigureOtherServices`); add AutoMapper mappings in the profile inside `Startup.AutoMapper.cs`.
- New controllers derive from `ASI.Basecode.WebApp.Mvc.ControllerBase<T>` (constructor needs `IHttpContextAccessor`, `ILoggerFactory`, `IConfiguration`, `IMapper`) — see `HomeController` for the pattern.
- `AsiBasecodeDBContext` is `partial`; keep entity config in `OnModelCreating` and don't fight the `OnModelCreatingPartial` hook.
- Cookie authentication is wired in `Startup.cs` (scheme `CampusPulse.Auth`), with role authorization via `[Authorize(Roles = RoleCodes.*)]` from `ASI.Basecode.Resources/Constants/RoleCodes.cs`. Role codes are `STUDENT`, `FACULTY`, `TECHNICIAN` (custodian) and `ADMIN`.
- `AccountController` implements 1.1-1.4 (register/login/forgot/reset). Passwords are PBKDF2 via `Services/Manager/PasswordManager.cs`; reset tokens live in `password_reset_tokens` (mapped in `AllianceDbContext.Auth.cs`). Reset links are emailed via `IEmailSender`; because the default transport only logs, the link is surfaced on the forgot-password screen **in Development only**. Login accepts school email only (no account IDs).
- `CurrentUserService` reads the cookie principal (falls back to legacy session keys). Dashboard routing lives in `HomeController.Index`; nav is role-aware in `Views/Shared/_Layout.cshtml`.
- Role dashboards: `BorrowerController.Dashboard` (`/Borrower/Dashboard`, screen 2.0), `CustodianController.Dashboard` (`/Custodian/Dashboard`, screen 3.0), `MonitoringController.Dashboard` (`/Monitoring/Dashboard`, screen 4.0). Data comes from `IDashboardService` / `IMonitoringService`.
- `ProfileController` (`/Profile`) serves screens 2.10 / 3.10 / 4.8 through one shared partial with role-specific views. Contact details and notification preferences persist in `users.contact_number` / `users.alternate_contact` and `user_preferences` (created by the seeder); password changes go through `IAuthService.ChangePassword`.
- Sign-in is **school email only** (account IDs are rejected). Demo accounts (password `Campus123`): `ana.reyes@university.edu.ph` (admin), `carlos.diaz@university.edu.ph` (custodian), `maria.santos@university.edu.ph` (student), `jose.cruz@university.edu.ph` (faculty), `liza.mendoza@university.edu.ph` (unverified student).

## Notes

- Empty `Models/`/`Repositories/`/`Services/` subfolders are kept via `.gitkeep` files — don't delete them.
- `wwwroot/lib` contains committed vendored libraries (bootstrap, jquery); don't replace with CDN links.
- README.md documents the same layering rules; trust both, but executable code and csproj files win on any conflict.
