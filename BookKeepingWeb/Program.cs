using BookKeepingWeb;
using BookKeepingWeb.Data;
using BookKeepingWeb.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// 🔧 Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Error()
    .WriteTo.Console() // optional but good for dev
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day) // creates daily log files
    .Enrich.FromLogContext()
    .CreateLogger();

// 📌 Replace the default logging
builder.Host.UseSerilog();

// Configure maximum file upload size (e.g., 200 MB)
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 209715200; // 200MB
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 209715200; // 200 MB
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 209_715_200; // 200 MB
});

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(
    builder.Configuration.GetConnectionString("Default Connection")
    ));

builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

builder.Services.Configure<IdentityOptions>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
});

builder.Services.AddScoped<ImageService>();
builder.Services.AddScoped<AccountValidationService>();
builder.Services.AddSignalR();


// ? Enable Session Support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Keep session for 30 minutes
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        // Check if it's an AJAX request
        if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsync("{\"success\":false,\"message\":\"🚫 You must be logged in.\"}");
        }

        // Redirect non-AJAX requests to banned page (or login page if you prefer)
        context.Response.Redirect("/Home/Banned"); // ✅ YOUR CUSTOM PAGE
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.Redirect("/Home/Banned"); // Or a separate access denied page
        return Task.CompletedTask;
    };
});

builder.Services.AddRateLimiter(options =>
{
    // Apply to everyone globally by IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });

    // 👇 Still allows additional named policies:
    options.AddTokenBucketLimiter("CommentLimiter", opt =>
    {
        opt.TokenLimit = 5;
        opt.TokensPerPeriod = 5;
        opt.ReplenishmentPeriod = TimeSpan.FromMinutes(1);
        opt.AutoReplenishment = true;
        opt.QueueLimit = 0;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    options.AddPolicy("UploadLimiter", context =>
    {
        var username = context.User.Identity?.Name ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(username, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });

    options.AddTokenBucketLimiter("ReportLimiter", opt =>
    {
        opt.TokenLimit = 5; // Max 5 reports
        opt.TokensPerPeriod = 5;
        opt.ReplenishmentPeriod = TimeSpan.FromMinutes(2);
        opt.AutoReplenishment = true;
        opt.QueueLimit = 0;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });


    options.OnRejected = async (context, token) =>
    {
        var http = context.HttpContext;
        var isAjax = http.Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (isAjax)
        {
            http.Response.ContentType = "application/json";
            await http.Response.WriteAsync("{\"success\":false,\"message\":\"⛔ You're doing that too fast.\",\"redirectUrl\":\"/Home/RateLimit\"}");
        }
        else if (http.Request.Method == "POST")
        {
            http.Response.ContentType = "text/html";
            await http.Response.WriteAsync(@"
            <html>
                <head><title>Rate Limited</title></head>
                <body>
                    <h2>⛔ You're doing that too fast.</h2>
                    <script>window.location = '/Home/RateLimit';</script>
                </body>
            </html>");
        }
        else
        {
            http.Response.ContentType = "text/html";
            await http.Response.WriteAsync("<script>window.location='/Home/RateLimit';</script>");
        }
    };




});
 

var app = builder.Build();
 
app.MapRazorPages();


app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ? Enable Session Middleware
app.UseSession();

app.UseRateLimiter();

app.Use(async (context, next) =>
{
    if (context.User.Identity.IsAuthenticated)
    {
        var userManager = context.RequestServices.GetRequiredService<UserManager<IdentityUser>>();
        var signInManager = context.RequestServices.GetRequiredService<SignInManager<IdentityUser>>();
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                // Invalidate session and cookie
                await signInManager.SignOutAsync();
                context.Session.Clear();

                if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    // If it's an AJAX call, return a 401 so JS can handle it
                    context.Response.StatusCode = 401;
                    return;
                }

                context.Response.Redirect("/Home/Banned");

                return;
            }
        }
    }

    await next();
});



app.UseAuthentication(); // Enable Authentication
app.UseAuthorization();

app.MapHub<ChatHub>("/chatHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Homepage}/{id?}");

try
{
    Log.Information("Starting web host");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}