using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Infrastructure.Persistence;
using DraftView.Infrastructure.Persistence.Repositories;
using DraftView.Web.Data;
using DraftView.Web;
using DraftView.Infrastructure.Parsing;
using DraftView.Infrastructure.Sync;
using DraftView.Web.Services;
using DraftView.Infrastructure.Dropbox;
using DraftView.Application.Services;
using DraftView.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Identity;
using System;

namespace DraftView.Web.Extensions
{
    internal static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddPersistenceServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<DraftViewDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IUnitOfWork>(sp =>
                sp.GetRequiredService<DraftViewDbContext>());
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IInvitationRepository, InvitationRepository>();
            services.AddScoped<IScrivenerProjectRepository, ScrivenerProjectRepository>();
            services.AddScoped<ISectionRepository, SectionRepository>();
            services.AddScoped<ICommentRepository, CommentRepository>();
            services.AddScoped<IReadEventRepository, ReadEventRepository>();
            services.AddScoped<IUserNotificationPreferencesRepository, UserNotificationPreferencesRepository>();
            services.AddScoped<IEmailDeliveryLogRepository, EmailDeliveryLogRepository>();
            services.AddScoped<IDropboxConnectionRepository, DropboxConnectionRepository>();
            services.AddScoped<IReaderAccessRepository, ReaderAccessRepository>();

            return services;
        }

        public static IServiceCollection AddConfiguredSettings(this IServiceCollection services, IConfiguration configuration)
        {
            // Bind configuration sections to POCOs using the Options pattern
            services.Configure<DraftViewSettings>(configuration.GetSection("DraftView"));
            services.Configure<EmailSettings>(configuration.GetSection("Email"));
            services.Configure<DropboxClientSettings>(configuration.GetSection("Dropbox"));

            // DiscoveryServiceOptions only needs the LocalCachePath value from configuration.
            // LocalCachePath is an init-only property, so create and register the instance directly.
            services.AddSingleton(new DiscoveryServiceOptions
            {
                LocalCachePath = configuration["Dropbox:LocalCachePath"] ?? string.Empty
            });

            // For backward compatibility, register the concrete types resolved from IOptions<T>.Value
            services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DraftViewSettings>>().Value);
            services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailSettings>>().Value);
            services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DropboxClientSettings>>().Value);
            // DiscoveryServiceOptions is registered as a concrete singleton above.

            return services;
        }

        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<ISyncService, SyncService>();
            services.AddScoped<IPublicationService, PublicationService>();
            services.AddSingleton<ISyncProgressTracker, SyncProgressTracker>();
            services.AddScoped<IScrivenerProjectDiscoveryService, ScrivenerProjectDiscoveryService>();
            services.AddScoped<ICommentService, CommentService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IReadingProgressService, ReadingProgressService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IDashboardService, DashboardService>();

            return services;
        }

        public static IServiceCollection AddWebServices(this IServiceCollection services, IConfiguration configuration)
        {
            // MVC / Razor Pages
            services.AddControllersWithViews();
            services.AddRazorPages();

            // Session (required for OAuth state)
            services.AddDistributedMemoryCache();
            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(10);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            // Parsing and Dropbox
            services.AddSingleton<IScrivenerProjectParser, ScrivenerProjectParser>();
            services.AddSingleton<IRtfConverter, RtfConverter>();
            services.AddScoped<IDropboxConnectionChecker, DropboxConnectionChecker>();
            services.AddScoped<IDropboxClientFactory, DropboxClientFactory>();
            services.AddScoped<IDropboxFileDownloader, DropboxFileDownloader>();

            // Background sync service
            services.AddHostedService<SyncBackgroundService>();

            // Email sender selection (from configuration)
            var emailProvider = configuration["Email:Provider"] ?? string.Empty;
            if (emailProvider == "Console")
                services.AddScoped<IEmailSender, ConsoleEmailSender>();
            else
                services.AddScoped<IEmailSender, SmtpEmailSender>();

            // Path resolver
            services.AddScoped<ILocalPathResolver>(sp =>
            {
                var settings = sp.GetRequiredService<DraftViewSettings>();
                return new LocalPathResolver(settings.ResolvedLocalCachePath);
            });

            return services;
        }

        public static IServiceCollection AddIdentityServices(this IServiceCollection services)
        {
            // Configure Identity options similar to the original Program.cs
            services.AddIdentity<IdentityUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddEntityFrameworkStores<DraftViewDbContext>()
            .AddDefaultTokenProviders();

            services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
            });

            return services;
        }
    }
}
