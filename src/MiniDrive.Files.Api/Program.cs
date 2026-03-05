using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiniDrive.Common;
using MiniDrive.Common.Caching;
using MiniDrive.Common.Observability;
using MiniDrive.Files;
using MiniDrive.Files.Repositories;
using MiniDrive.Files.Services;
using MiniDrive.Storage;
using MiniDrive.Clients.Quota;
using MiniDrive.Clients.Audit;
using MiniDrive.Clients.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddControllers();

// Add OpenAPI
builder.Services.AddEndpointsApiExplorer();

// Common infrastructure
builder.Services.AddRedisCache(builder.Configuration);
builder.Services.AddOpenTelemetryTracing(builder.Configuration, "MiniDrive.Files.Api");

// Storage configuration
builder.Services.AddFileStorage(options =>
{
    options.BasePath = builder.Configuration["Storage:BasePath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "storage");
    options.MaxFileSizeBytes = builder.Configuration.GetValue<long>("Storage:MaxFileSizeBytes", 100 * 1024 * 1024);

    var allowedExtensions = builder.Configuration.GetSection("Storage:AllowedExtensions").Get<string[]>();
    if (allowedExtensions != null && allowedExtensions.Length > 0)
    {
        options.AllowedExtensions = new HashSet<string>(allowedExtensions, StringComparer.OrdinalIgnoreCase);
    }
});

// Files DI
builder.Services.AddDbContext<FileDbContext>(options =>
{
    if (builder.Environment.IsEnvironment("Testing"))
    {
        options.UseInMemoryDatabase("FilesDb");
    }
    else
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("FilesDb")
            ?? throw new InvalidOperationException("Connection string 'FilesDb' not found."));
    }
});
builder.Services.AddScoped<FileRepository>();

// Register adapters for microservice communication
builder.Services.AddScoped<MiniDrive.Quota.Services.IQuotaService, MiniDrive.Files.Api.Adapters.QuotaServiceAdapter>();
builder.Services.AddScoped<MiniDrive.Audit.Services.IAuditService, MiniDrive.Files.Api.Adapters.AuditServiceAdapter>();

builder.Services.AddScoped<IFileService, FileService>();

// Microservice clients
builder.Services.AddCachedIdentityClient(builder.Configuration);

var quotaServiceUrl = builder.Configuration["Services:Quota"] ?? "http://localhost:5004";
builder.Services.AddHttpClient<IQuotaClient, QuotaClient>(client =>
{
    client.BaseAddress = new Uri(quotaServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddDefaultResilience();

var auditServiceUrl = builder.Configuration["Services:Audit"] ?? "http://localhost:5005";
builder.Services.AddHttpClient<IAuditClient, AuditClient>(client =>
{
    client.BaseAddress = new Uri(auditServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddDefaultResilience();

var app = builder.Build();

// Apply database migrations automatically (skip for Testing environment)
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<FileDbContext>();
        try
        {
            dbContext.Database.Migrate();
        }
        catch (Exception ex)
        {
            var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("Files.Api");
            logger.LogError(ex, "An error occurred while migrating the Files database.");
            throw;
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    
    // Add Swagger UI endpoint
    app.MapGet("/swagger", () => Results.Content("""
    <!DOCTYPE html>
    <html>
    <head>
        <title>Files API - Swagger UI</title>
        <link rel="stylesheet" type="text/css" href="https://unpkg.com/swagger-ui-dist@5.10.3/swagger-ui.css" />
        <style>
            html { box-sizing: border-box; overflow: -moz-scrollbars-vertical; overflow-y: scroll; }
            *, *:before, *:after { box-sizing: inherit; }
            body { margin:0; background: #fafafa; }
        </style>
    </head>
    <body>
        <div id="swagger-ui"></div>
        <script src="https://unpkg.com/swagger-ui-dist@5.10.3/swagger-ui-bundle.js"></script>
        <script src="https://unpkg.com/swagger-ui-dist@5.10.3/swagger-ui-standalone-preset.js"></script>
        <script>
            window.onload = function() {
                const ui = SwaggerUIBundle({
                    url: '/openapi/v1.json',
                    dom_id: '#swagger-ui',
                    deepLinking: true,
                    presets: [
                        SwaggerUIBundle.presets.apis,
                        SwaggerUIStandalonePreset
                    ],
                    plugins: [
                        SwaggerUIBundle.plugins.DownloadUrl
                    ],
                    layout: "StandaloneLayout"
                });
            };
        </script>
    </body>
    </html>
    """, "text/html"));
}

// Only use HTTPS redirection if HTTPS is configured
if (!string.IsNullOrEmpty(app.Configuration["ASPNETCORE_HTTPS_PORT"]) || 
    app.Configuration["ASPNETCORE_URLS"]?.Contains("https://") == true)
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "Files", timestamp = DateTime.UtcNow }));

app.MapControllers();
app.Run();
