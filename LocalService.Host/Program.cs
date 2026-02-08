using LocalService.Host.Core;
using LocalService.Host.Infra;
using LocalService.Host.Models;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// ===== Settings =====
var port = GetIntArg(args, "--port", 5017);
var configPath = GetStringArg(args, "--config", @"N:\data\printers.config.json");
var logPath = GetStringArg(args, "--log", @"N:\data\LocalServicelog");
var curDate =DateTime.Now.ToString("ddMMyyyy");
logPath = Path.Combine(logPath,string.Concat( Environment.MachineName , $"_{curDate}.log"));

#region temp for simulate using pdf - not reuired
var environment = GetStringArg(args, "--env", "D002");


Environment.SetEnvironmentVariable(
    "AVIV_ENV",
    environment,
    EnvironmentVariableTarget.Process);

//if (EnvironmentService.IsDevelopment)
//{
//    builder.Services.AddSingleton<IPrinterService, PrinterService>();
//}
//else
//{
//   // builder.Services.AddSingleton<IPrinterService, DevModePdfPrinter>();
//}
#endregion
// ===== Logging to file (stable, no dependencies) =====
builder.Logging.ClearProviders();
builder.Logging.AddProvider(new SimpleFileLoggerProvider(logPath));

// ===== Services (DI) =====
builder.Services.AddSingleton(new PrinterConfigStore(configPath));
builder.Services.AddSingleton<PrintWorker>();
builder.Services.AddSingleton<IPrinterService, PrinterService>();
builder.Services.AddSingleton<ActionDispatcher>();

// ===== Kestrel: localhost only =====
builder.WebHost.ConfigureKestrel(opt =>
{
    opt.ListenLocalhost(port); // binds to 127.0.0.1 only
});

var app = builder.Build();

var logger = app.Logger;
logger.LogInformation("LocalService starting. Port={Port}, Config={ConfigPath}, Log={LogPath}", port, configPath, logPath);

// Global exception safety net
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unhandled error");
        ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        await ctx.Response.WriteAsJsonAsync(new { error = "internal_error" });
    }
});

// ===== Auth + localhost guard middleware =====
app.Use(async (ctx, next) =>
{
    // ensure request is from localhost
    var remoteIp = ctx.Connection.RemoteIpAddress;
    if (remoteIp is null || !(IPAddress.IsLoopback(remoteIp)))
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        await ctx.Response.WriteAsJsonAsync(new { error = "forbidden_non_localhost" });
        return;
    }
    // Require JWT-like token for /api/*
    if (ctx.Request.Path.StartsWithSegments("/api"))
    {
        if (!JwtHeaderValidator.TryValidateAuthorizationHeader(ctx.Request, out var error))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new { error });
            return;
        }
    }

    await next();
});

var lifetime = app.Lifetime;

// ===== API =====
app.MapPost("/api/execute", async (ExecuteRequest req, ActionDispatcher dispatcher) =>
{
    var result = await dispatcher.ExecuteAsync(req);
    return Results.Json(result, statusCode: result.HttpStatus);
});

app.MapPost("/api/shutdown", (HttpContext ctx) =>
{
    // We return "accepted" immediately, then stop gracefully
    _ = Task.Run(async () =>
    {
        try
        {
            await Task.Delay(50);
            lifetime.StopApplication();
        }
        catch { /* best effort */ }
    });

    return Results.Json(new { ok = true, message = "received_shutdown" }, statusCode: StatusCodes.Status202Accepted);
});

app.MapGet("/health", () => Results.Json(new { ok = true }));

app.Run();

static int GetIntArg(string[] args, string name, int defaultValue)
{
    var idx = Array.IndexOf(args, name);
    if (idx >= 0 && idx + 1 < args.Length && int.TryParse(args[idx + 1], out var val))
        return val;
    return defaultValue;
}

static string GetStringArg(string[] args, string name, string defaultValue)
{
    var idx = Array.IndexOf(args, name);
    if (idx >= 0 && idx + 1 < args.Length && !string.IsNullOrWhiteSpace(args[idx + 1]))
        return args[idx + 1];
    return defaultValue;
}
