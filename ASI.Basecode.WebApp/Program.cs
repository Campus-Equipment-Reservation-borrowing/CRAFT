using System.IO;
using ASI.Basecode.Data;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.WebApp;
using ASI.Basecode.WebApp.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var appBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ContentRootPath = Directory.GetCurrentDirectory(),
});

appBuilder.Configuration.AddJsonFile("appsettings.json",
    optional: true,
    reloadOnChange: true);

// Re-apply env vars so they still override appsettings.json (e.g. ConnectionStrings__AllianceConnection
// for running against a non-LocalDB SQL Server on macOS/Linux).
appBuilder.Configuration.AddEnvironmentVariables();

appBuilder.WebHost.UseIISIntegration();

appBuilder.Logging
    .AddConfiguration(appBuilder.Configuration.GetLoggingSection())
    .AddConsole()
    .AddDebug();

var configurer = new StartupConfigurer(appBuilder.Configuration);
configurer.ConfigureServices(appBuilder.Services);

var app = appBuilder.Build();

configurer.ConfigureApp(app, app.Environment);

// Seed the empty scaffolded database (codebooks + demo accounts) on startup.
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetService<IDatabaseSeeder>();
    seeder?.Seed(includeDemoData: app.Environment.IsDevelopment());

    // Inspection photos used to be public static files; move any left in wwwroot to private storage.
    var movedPhotos = ASI.Basecode.Services.InspectionPhotoStore.MigrateFromPublicFolder();
    if (movedPhotos > 0)
    {
        app.Logger.LogInformation("Moved {Count} inspection photo(s) out of wwwroot into private storage.", movedPhotos);
    }
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();
app.MapRazorPages();

// Run application
app.Run();
