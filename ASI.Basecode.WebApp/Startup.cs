using ASI.Basecode.Data;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.WebApp.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Text;

namespace ASI.Basecode.WebApp
{
    /// <summary>
    /// For configuring services on application startup.
    /// </summary>
    /// <remarks>
    /// <para>Method call sequence for instances of this class:</para>
    /// <para>1. constructor</para>
    /// <para>2. <see cref="ConfigureServices(IServiceCollection)"/></para>
    /// <para>3. (create <see cref="IApplicationBuilder"/> instance)</para>
    /// <para>4. <see cref="ConfigureApp(IApplicationBuilder, IWebHostEnvironment)"/></para>
    /// </remarks>
    internal partial class StartupConfigurer
    {
        /// <summary>
        /// Gets the configuration.
        /// </summary>
        private IConfiguration Configuration { get; }

        private IApplicationBuilder _app;

        private IWebHostEnvironment _environment;

        private IServiceCollection _services;

        /// <summary>
        /// Initialize new <see cref="StartupConfigurer"/> instance using <paramref name="configuration"/>
        /// </summary>
        /// <param name="configuration"></param>
        public StartupConfigurer(IConfiguration configuration)
        {
            this.Configuration = configuration;

            PathManager.Setup(this.Configuration.GetSetupRootDirectoryPath());

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// Use this method to add services to the container.
        /// </summary>
        /// <param name="services">Services</param>
        public void ConfigureServices(IServiceCollection services)
        {
            this._services = services;

            services.AddMemoryCache();

            // Register SQL database configuration context as services.
            // AllianceDbContext targets the AllianceDB schema (all CampusPulse entities).
            services.AddDbContext<AllianceDbContext>(options =>
            {
                options.UseSqlServer(
                    Configuration.GetConnectionString("AllianceConnection"),
                    sqlServerOptions => sqlServerOptions.CommandTimeout(120));
            });

            services.AddControllersWithViews(options =>
            {
                options.Filters.Add<ASI.Basecode.WebApp.Mvc.MustChangePasswordFilter>();
            });
            services.AddRazorPages().AddRazorRuntimeCompilation();

            // Session (shared by the current-operator/audit helpers)
            services.AddSession(options =>
            {
                options.Cookie.Name = Const.Issuer;
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            // Cookie authentication — screens 1.1-1.4 plus role based access control.
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.Cookie.Name = "CampusPulse.Auth";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SameSite = SameSiteMode.Lax;
                    options.LoginPath = "/Account/Login";
                    options.LogoutPath = "/Account/Logout";
                    options.AccessDeniedPath = "/Account/AccessDenied";
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                    options.SlidingExpiration = true;
                });

            services.AddAuthorization();

            // DI Services AutoMapper(Add Profile)
            this.ConfigureAutoMapper();

            // DI Services
            this.ConfigureOtherServices();

            services.Configure<FormOptions>(options =>
            {
                options.ValueLengthLimit = 1024 * 1024 * 100;
            });

            services.AddSingleton<IFileProvider>(
                new PhysicalFileProvider(
                    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")));
        }

        /// <summary>
        /// Configure application
        /// </summary>
        /// <param name="app"></param>
        /// <param name="env"></param>
        public void ConfigureApp(IApplicationBuilder app, IWebHostEnvironment env)
        {
            this._app = app;
            this._environment = env;

            if (!this._environment.IsDevelopment())
            {
                this._app.UseHsts();
            }

            this.ConfigureLogger();

            this._app.UseHttpsRedirection();
            this._app.UseStaticFiles();

            // Localization
            var options = this._app.ApplicationServices.GetService<IOptions<RequestLocalizationOptions>>();
            this._app.UseRequestLocalization(options.Value);

            this._app.UseSession();
            this._app.UseRouting();

            this._app.UseAuthentication();
            this._app.UseAuthorization();
        }
    }
}
