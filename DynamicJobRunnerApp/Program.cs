using DynamicJobRunnerApp.Data;
using DynamicJobRunnerApp.Services;
using DynamicJobRunnerApp.Quartz;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.SetBasePath(builder.Environment.ContentRootPath);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddJsonFile("appsettings.private.json", optional: true)
    .AddEnvironmentVariables();

// Service configuration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration["DefaultConnection"]));

// Quartz configuration
builder.Services.AddQuartz(q =>
{
    q.UseMicrosoftDependencyInjectionJobFactory();
    q.InterruptJobsOnShutdown = true;
    q.InterruptJobsOnShutdownWithWait = true;
});

builder.Services.AddQuartzHostedService(options =>
{
    options.WaitForJobsToComplete = true;
});

builder.Services.AddHostedService<JobInitializer>();
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IJobSchedulerService, JobSchedulerService>();

// Basic Authentication configuration
builder.Services.AddAuthentication("BasicAuthentication")
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);

var app = builder.Build();

// HTTP request pipeline configuration
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Database initialization
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Error during database migration");
}

app.UseStaticFiles();
app.UseRouting();

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Map controller routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Jobs}/{action=Home}/{id?}");

await app.RunAsync();

/// <summary>
/// Basic Authentication handler implementation.
/// </summary>
public class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly string _authUsername;
    private readonly string _authPassword;

    public BasicAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IConfiguration configuration)
        : base(options, logger, encoder, clock)
    {
        _authUsername = configuration["AUTH_USERNAME"] ?? "defaultuser";
        _authPassword = configuration["AUTH_PASSWORD"] ?? "defaultpassword";
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check if the Authorization header is present
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            Response.Headers["WWW-Authenticate"] = "Basic realm=\"Access to the site\"";
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization Header"));
        }

        try
        {
            var authHeader = AuthenticationHeaderValue.Parse(Request.Headers["Authorization"]);
            var credentialBytes = Convert.FromBase64String(authHeader.Parameter);
            var credentials = System.Text.Encoding.UTF8.GetString(credentialBytes).Split(':');
            var username = credentials[0];
            var password = credentials[1];

            // Validate the username and password
            if (username == _authUsername && password == _authPassword)
            {
                var claims = new[] { new Claim(ClaimTypes.Name, username) };
                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);

                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
            else
            {
                Response.Headers["WWW-Authenticate"] = "Basic realm=\"Access to the site\"";
                return Task.FromResult(AuthenticateResult.Fail("Invalid Username or Password"));
            }
        }
        catch
        {
            Response.Headers["WWW-Authenticate"] = "Basic realm=\"Access to the site\"";
            return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization Header"));
        }
    }
}