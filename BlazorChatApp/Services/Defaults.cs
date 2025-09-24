using BlazorChatApp.Models;
using BlazorChatApp.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Serilog.Filters;

namespace BlazorChatApp.Services;

public static class Defaults
{
    
   public static WebApplicationBuilder addLogging(this WebApplicationBuilder builder)
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
        .MinimumLevel.Override("System", LogEventLevel.Error)

        // SignalR açık kalsın
        .MinimumLevel.Override("Microsoft.AspNetCore.SignalR", LogEventLevel.Information)
        .MinimumLevel.Override("Microsoft.AspNetCore.Http.Connections", LogEventLevel.Information)
        .MinimumLevel.Override("Microsoft.Azure.SignalR", LogEventLevel.Information)

        // Gürültüler
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Error)
        .MinimumLevel.Override("Microsoft.AspNetCore.Hosting", LogEventLevel.Fatal)
        .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", LogEventLevel.Fatal)
        .MinimumLevel.Override("Microsoft.AspNetCore.Routing", LogEventLevel.Fatal)
        .MinimumLevel.Override("Microsoft.AspNetCore.Server.Kestrel", LogEventLevel.Fatal)
        .MinimumLevel.Override("Microsoft.AspNetCore.Components.Server.Circuits", LogEventLevel.Fatal)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Fatal)
        .MinimumLevel.Override("Microsoft.Extensions.Http", LogEventLevel.Fatal)

        // *** RequestLoggingMiddleware'ı tamamen sustur ***
        .MinimumLevel.Override("Serilog.AspNetCore", LogEventLevel.Fatal)
        .MinimumLevel.Override("Serilog.AspNetCore.RequestLoggingMiddleware", LogEventLevel.Fatal)

        // Çift emniyet: kaynak bazlı exclude
        .Filter.ByExcluding(Matching.FromSource("Serilog.AspNetCore.RequestLoggingMiddleware"))
        .Filter.ByExcluding(Matching.FromSource("Microsoft.AspNetCore.Hosting.Diagnostics"))
        .Filter.ByExcluding(Matching.FromSource("Microsoft.AspNetCore.Routing.EndpointMiddleware"))
        .Filter.ByExcluding(Matching.FromSource("Microsoft.AspNetCore.Components.Server.Circuits"))
        .Filter.ByExcluding(Matching.FromSource("Microsoft.EntityFrameworkCore"))

        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithProperty("Application", "ShelfPlayer")

        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            "logs/ShelfPlayer-.txt",
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
            rollingInterval: RollingInterval.Day)
        .CreateLogger();

    builder.Host.UseSerilog();
    builder.Services.AddLogging(lb =>
    {
        lb.ClearProviders();
        lb.AddSerilog(dispose: true);
    });
    return builder;
}

    public static IServiceCollection AddDb(this IServiceCollection services)
    {
        services.AddDbContext<ApplicationDbContext>(options => { options.UseSqlite("Data Source=app.db"); });
        return services;
    }
    
    public static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        services.AddIdentityCore<AppUser>(options =>
            {
                // Email ile login yapmaya izin ver
                options.SignIn.RequireConfirmedEmail = false;
                options.User.RequireUniqueEmail = true;
            
                // Password gereksinimleri
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 8;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // Cookie ayarları
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(60); // 1 saat
            options.LoginPath = "/login";
            options.LogoutPath = "/logout";
            options.AccessDeniedPath = "/access-denied";
            options.SlidingExpiration = true;
            options.Cookie.Name = "BlazorChatApp.Auth";
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

        services.AddCascadingAuthenticationState();

        return services;
    }
  
    public static async Task EnsureDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }
    
    public static async Task CreateDefaultUserAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<SettingsModel>>().Value;
    
        var existingUser = await userManager.FindByEmailAsync(settings.Email);
        if (existingUser == null)
        {
            var defaultUser = new AppUser
            {
                Name = "Serkan",
                Surname = "Polat",
                UserName = settings.Email,
                Email = settings.Email,
                EmailConfirmed = true,
                AvatarUrl = "pics/man.png",
                IsOnline = true,
                LastSeen = DateTime.UtcNow.AddMinutes(-5)
            };
        
            await userManager.CreateAsync(defaultUser, settings.Password);
        }
    }
    
    public static WebApplicationBuilder configureKestrel(this WebApplicationBuilder? builder)
    {
        if (!builder.Environment.IsDevelopment())
        {
            builder.WebHost.ConfigureKestrel((context, serverOptions) =>
            {
                var settings = context.Configuration.GetSection("Settings").Get<SettingsModel>();

                serverOptions.ListenAnyIP(settings.DefaultHttpListenPort);

                // Optional: Configure other Kestrel options
                serverOptions.Limits.MaxConcurrentConnections = settings.MaxConcurrentConnections;
                serverOptions.Limits.MaxRequestBodySize = settings.MaxRequestBodySize;
            });
        }

        return builder;
    }
    
    public static WebApplicationBuilder ConfigureSettings(this WebApplicationBuilder builder)
    {
        builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
        builder.Configuration.AddEnvironmentVariables();
        builder.Services.AddHttpClient();
    
        // Settings'i DI container'a ekle
        builder.Services.Configure<SettingsModel>(builder.Configuration.GetSection("Settings"));
    
        return builder;
    }
    
    public static IServiceCollection AddChatServices(this IServiceCollection services)
    {
        services.AddScoped<EventBus>();
        services.AddScoped<ChatState>();
        services.AddScoped<ChatService>();
        services.AddScoped<ChatDataSeeder>();
        //bugün Signalr yarın mqtt :))
        services.AddScoped<IMessageService, SignalRService>();
        //bugün mediasoup yarın başka bir şey :))
        //services.AddScoped<IVideoConferenceService, MediaSoupVideoService>();
        //mesela janus kullanmak istersen (henüz implement etmedim)
        //services.AddScoped<IVideoConferenceService, JanusVideoConferenceService>();
        services.AddScoped<JanusVideoService>();
        services.AddScoped<VideoCallSignalHandler>();
        
        services.AddScoped<GroupService>();
        services.AddTransient<FriendService>();
        services.AddSignalR();
        return services;
    }
}