using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ScrivenerSync.Application.Services;
using ScrivenerSync.Domain.Interfaces.Repositories;
using ScrivenerSync.Domain.Interfaces.Services;
using ScrivenerSync.Infrastructure.Dropbox;
using ScrivenerSync.Infrastructure.Parsing;
using ScrivenerSync.Infrastructure.Sync;
using ScrivenerSync.Infrastructure.Persistence;
using ScrivenerSync.Infrastructure.Persistence.Repositories;
using ScrivenerSync.Web;
using ScrivenerSync.Web.Data;
using ScrivenerSync.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------
var scrivenerSettings = new ScrivenerSyncSettings();
builder.Configuration.GetSection("ScrivenerSync").Bind(scrivenerSettings);
builder.Services.AddSingleton(scrivenerSettings);

var emailSettings = new EmailSettings();
builder.Configuration.GetSection("Email").Bind(emailSettings);
builder.Services.AddSingleton(emailSettings);

var dropboxSettings = new DropboxClientSettings();
builder.Configuration.GetSection("Dropbox").Bind(dropboxSettings);
builder.Services.AddSingleton(dropboxSettings);

// ---------------------------------------------------------------------------
// Database
// ---------------------------------------------------------------------------
var dbPath = scrivenerSettings.DatabasePath;
if (string.IsNullOrWhiteSpace(dbPath))
    dbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScrivenerSync", "scrivener-sync.db");

Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

builder.Services.AddDbContext<ScrivenerSyncDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// ---------------------------------------------------------------------------
// ASP.NET Core Identity
// ---------------------------------------------------------------------------
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireDigit           = true;
    options.Password.RequiredLength         = 8;
    options.Password.RequireUppercase       = false;
    options.Password.RequireNonAlphanumeric = false;
    options.SignIn.RequireConfirmedEmail     = false;
})
.AddEntityFrameworkStores<ScrivenerSyncDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath         = "/Account/Login";
    options.LogoutPath        = "/Account/Logout";
    options.AccessDeniedPath  = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan    = TimeSpan.FromDays(14);
});

// ---------------------------------------------------------------------------
// MVC
// ---------------------------------------------------------------------------
builder.Services.AddControllersWithViews();

// ---------------------------------------------------------------------------
// Repositories
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IUnitOfWork>(sp =>
    sp.GetRequiredService<ScrivenerSyncDbContext>());
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IInvitationRepository, InvitationRepository>();
builder.Services.AddScoped<IScrivenerProjectRepository, ScrivenerProjectRepository>();
builder.Services.AddScoped<ISectionRepository, SectionRepository>();
builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddScoped<IReadEventRepository, ReadEventRepository>();
builder.Services.AddScoped<IUserNotificationPreferencesRepository, UserNotificationPreferencesRepository>();
builder.Services.AddScoped<IEmailDeliveryLogRepository, EmailDeliveryLogRepository>();

// ---------------------------------------------------------------------------
// Application services
// ---------------------------------------------------------------------------
builder.Services.AddScoped<ISyncService, SyncService>();
builder.Services.AddScoped<IPublicationService, PublicationService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IReadingProgressService, ReadingProgressService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// ---------------------------------------------------------------------------
// Parsing and Dropbox
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<IScrivenerProjectParser, ScrivenerProjectParser>();
builder.Services.AddSingleton<IRtfConverter, RtfConverter>();

if (!string.IsNullOrWhiteSpace(dropboxSettings.AccessToken))
{
    builder.Services.AddSingleton<IDropboxClient>(
        new ScrivenerSync.Infrastructure.Dropbox.DropboxClient(dropboxSettings));
}

// ---------------------------------------------------------------------------
// Email sender
// ---------------------------------------------------------------------------
if (emailSettings.Provider == "Console")
    builder.Services.AddScoped<IEmailSender, ConsoleEmailSender>();
else
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

// ---------------------------------------------------------------------------
// Path resolver
// ---------------------------------------------------------------------------
builder.Services.AddSingleton(new DropboxClientSettingsAccessor
{
    LocalCachePath = dropboxSettings.LocalCachePath
});
builder.Services.AddScoped<ILocalPathResolver, LocalPathResolver>();

// ---------------------------------------------------------------------------
// Background sync service
// ---------------------------------------------------------------------------
builder.Services.AddHostedService<SyncBackgroundService>();

// ---------------------------------------------------------------------------
// Build and configure pipeline
// ---------------------------------------------------------------------------
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ScrivenerSyncDbContext>();
    db.Database.Migrate();
}

// Seed initial data
var seedEmail    = builder.Configuration["Seed:AuthorEmail"]    ?? "author@scrivener-sync.local";
var seedPassword = builder.Configuration["Seed:AuthorPassword"] ?? "Password1!";
var seedName     = builder.Configuration["Seed:AuthorName"]     ?? "Author";
var seedPath     = builder.Configuration["Seed:TestProjectPath"] ?? "/Apps/Scrivener/Test.scriv";

await DatabaseSeeder.SeedAsync(app.Services, seedEmail, seedPassword, seedName, seedPath);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();


