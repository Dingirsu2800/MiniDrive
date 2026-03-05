var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddControllers();

// Add HttpClient for health checks
builder.Services.AddHttpClient();

// Add CORS with restricted origins
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? (builder.Environment.IsDevelopment() 
        ? new[] { "http://localhost:3000", "http://localhost:3001" }
        : Array.Empty<string>());

if (corsOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("RestrictedCors", policy =>
        {
            policy
                .WithOrigins(corsOrigins)
                .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH")
                .WithHeaders("Content-Type", "Authorization")
                .AllowCredentials()
                .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
        });
    });
}

// Add YARP reverse proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    
    // Add Swagger UI endpoint
    app.MapGet("/swagger", () => Results.Content("""
    <!DOCTYPE html>
    <html>
    <head>
        <title>Gateway API - Swagger UI</title>
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

// Apply CORS if configured
if (corsOrigins.Length > 0)
{
    app.UseCors("RestrictedCors");
}

app.UseAuthorization();

// Health check endpoint for gateway
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "Gateway", timestamp = DateTime.UtcNow }));

// Aggregate health check (checks all downstream services)
app.MapGet("/health/aggregate", async (HttpClient httpClient) =>
{
    var services = new[]
    {
        new { Name = "Identity", Url = "http://localhost:5001/health" },
        new { Name = "Files", Url = "http://localhost:5002/health" },
        new { Name = "Folders", Url = "http://localhost:5003/health" },
        new { Name = "Quota", Url = "http://localhost:5004/health" },
        new { Name = "Audit", Url = "http://localhost:5005/health" },
        new { Name = "Sharing", Url = "http://localhost:5006/health" }
    };

    var healthChecks = new Dictionary<string, object>();

    foreach (var service in services)
    {
        try
        {
            var response = await httpClient.GetAsync(service.Url);
            healthChecks[service.Name] = new
            {
                status = response.IsSuccessStatusCode ? "healthy" : "unhealthy",
                statusCode = (int)response.StatusCode
            };
        }
        catch (Exception ex)
        {
            healthChecks[service.Name] = new
            {
                status = "unhealthy",
                error = ex.Message
            };
        }
    }

    var allHealthy = healthChecks.Values.All(h => ((dynamic)h).status == "healthy");
    return Results.Json(new
    {
        status = allHealthy ? "healthy" : "degraded",
        services = healthChecks,
        timestamp = DateTime.UtcNow
    }, statusCode: allHealthy ? 200 : 503);
});

app.MapReverseProxy();
app.MapControllers();

app.Run();
