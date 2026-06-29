using ChatSystem.Server.Data;
using ChatSystem.Server.Hubs;
using ChatSystem.Server.Authentication;
using ChatSystem.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

if (string.IsNullOrWhiteSpace(builder.Configuration["urls"]))
{
    builder.WebHost.UseUrls("http://127.0.0.1:5098");
}

builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddSingleton<DesktopTokenService>();
builder.Services.AddSingleton<FileStorageService>();
builder.Services.AddScoped<GroupService>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ChatSystem.Auth";
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    })
    .AddScheme<AuthenticationSchemeOptions, DesktopTokenAuthenticationHandler>(
        DesktopTokenAuthenticationDefaults.AuthenticationScheme,
        options => { });

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlite(DatabasePath.GetConnectionString(builder.Environment.ContentRootPath));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

await DatabaseInitializer.InitializeAsync(app.Services);

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();
app.MapHub<ChatHub>("/chatHub");

await app.RunAsync();

