using BlazorChatApp.Components;
using BlazorChatApp.Hubs;
using BlazorChatApp.Models;
using BlazorChatApp.Models.Identity;
using BlazorChatApp.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Radzen;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.addLogging();
builder.ConfigureSettings();
builder.Services.Configure<JanusSettings>(builder.Configuration.GetSection("Janus"));
builder.configureKestrel(); 
builder.Services
    .AddDb()
    .AddIdentityServices()
    .AddRadzenComponents()
    .AddHttpClient()
    .AddChatServices()
    ;

builder.Services.AddControllers(); 
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();  
app.UseAuthorization();   
//yukarıdakiler antiforgeryden önce olmalı
app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.EnsureDatabaseAsync();
await app.CreateDefaultUserAsync();
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<ChatDataSeeder>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var settings = scope.ServiceProvider.GetRequiredService<IOptions<SettingsModel>>().Value;
    
    var defaultUser = await userManager.FindByEmailAsync(settings.Email);
    if (defaultUser != null)
    {
        await seeder.SeedChatDataAsync(defaultUser.Id);
    }
}
app.MapPost("/logout", async (SignInManager<AppUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/login");
}).DisableAntiforgery();
app.MapHub<ChatHub>("/chathub");
app.UseSerilogRequestLogging(opts =>
{
    // 200-499 arası istekleri INF yerine sadece Warning/ üstü yapma örneği:
    opts.GetLevel = (http, elapsed, ex) =>
        ex != null || http.Response.StatusCode >= 500
            ? LogEventLevel.Error
            : LogEventLevel.Warning;

    // Path’i log context’e koyuyoruz ki yukarıdaki Serilog Filter expression’ları çalışsın
    opts.EnrichDiagnosticContext = (diag, http) =>
    {
        diag.Set("RequestPath", http.Request.Path.Value);
    };
});
await app.RunAsync();
