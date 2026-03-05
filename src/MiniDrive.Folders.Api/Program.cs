using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiniDrive.Common;
using MiniDrive.Common.Caching;
using MiniDrive.Common.Observability;
using MiniDrive.Folders;
using MiniDrive.Folders.Repositories;
using MiniDrive.Folders.Services;
using MiniDrive.Clients.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddControllers();

// Add OpenAPI
builder.Services.AddEndpointsApiExplorer();

// Common infrastructure
builder.Services.AddRedisCache(builder.Configuration);
builder.Services.AddOpenTelemetryTracing(builder.Configuration, "MiniDrive.Folders.Api");

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

// Microservice clients
builder.Services.AddCachedIdentityClient(builder.Configuration);

var app = builder.Build();

// Apply database migrations automatically (skip for Testing environment)
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<FolderDbContext>();
        try
        {
            dbContext.Database.Migrate();
        }
        catch (Exception ex)
        {
            var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("Folders.Api");
            logger.LogError(ex, "An error occurred while migrating the Folders database.");
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
        <title>Folders API - Swagger UI</title>
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
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "Folders", timestamp = DateTime.UtcNow }));

app.MapControllers();
app.Run();
