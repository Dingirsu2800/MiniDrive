using Microsoft.EntityFrameworkCore;
using MiniDrive.Audit;
using MiniDrive.Common.Caching;
using MiniDrive.Common.Jwt;
using MiniDrive.Files;
using MiniDrive.Files.Repositories;
using MiniDrive.Files.Services;
using MiniDrive.Folders;
using MiniDrive.Folders.Repositories;
using MiniDrive.Folders.Services;
using MiniDrive.Identity;
using MiniDrive.Identity.Repositories;
using MiniDrive.Identity.Services;
using MiniDrive.Quota;
using MiniDrive.Storage;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddControllers();

// Add OpenAPI
builder.Services.AddEndpointsApiExplorer();

// Common infrastructure
builder.Services.AddRedisCache(builder.Configuration);
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.ConfigurationSectionName));

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

// Identity DI
builder.Services.AddDbContext<IdentityDbContext>(options =>
{
    if (builder.Environment.IsEnvironment("Testing"))
    {
        options.UseInMemoryDatabase("IdentityDb");
    }
    else
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("IdentityDb")
            ?? throw new InvalidOperationException("Connection string 'IdentityDb' not found."));
    }
});
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();

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
builder.Services.AddScoped<IFileService, FileService>();

// Folders DI
builder.Services.AddDbContext<FolderDbContext>(options =>
{
    if (builder.Environment.IsEnvironment("Testing"))
    {
        options.UseInMemoryDatabase("FoldersDb");
    }
    else
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("FoldersDb")
            ?? throw new InvalidOperationException("Connection string 'FoldersDb' not found."));
    }
});
builder.Services.AddScoped<FolderRepository>();
builder.Services.AddScoped<IFolderService, FolderService>();

// Audit DI
builder.Services.AddAuditServices(builder.Configuration, builder.Environment.EnvironmentName);

// Quota DI
builder.Services.AddQuotaServices(builder.Configuration, builder.Environment.EnvironmentName);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Add Swagger UI endpoint for API discovery
    app.MapGet("/swagger", () => Results.Content("""
    <!DOCTYPE html>
    <html>
    <head>
        <title>MiniDrive API - Swagger UI</title>
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

// Health check endpoint to test Redis connectivity
app.MapGet("/health/redis", async (IConnectionMultiplexer redis, ICacheService cache) =>
{
    try
    {
        // Test connection
        var server = redis.GetServer(redis.GetEndPoints().First());
        var pingResult = await server.PingAsync();
        
        // Test cache operations
        var testKey = "health:test";
        var testValue = DateTime.UtcNow.ToString("O");
        await cache.SetAsync(testKey, testValue, TimeSpan.FromSeconds(10));
        var retrieved = await cache.GetAsync<string>(testKey);
        await cache.RemoveAsync(testKey);
        
        var isConnected = redis.IsConnected;
        var database = redis.GetDatabase();
        var dbPing = await database.PingAsync();
        
        return Results.Ok(new
        {
            status = "healthy",
            redis = new
            {
                connected = isConnected,
                serverPing = pingResult.TotalMilliseconds,
                databasePing = dbPing.TotalMilliseconds,
                cacheTest = retrieved == testValue ? "passed" : "failed",
                endpoints = redis.GetEndPoints().Select(e => e.ToString()).ToArray()
            }
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: ex.Message,
            statusCode: 503,
            title: "Redis Health Check Failed");
    }
});

app.MapControllers();
app.Run();