
using Microsoft.EntityFrameworkCore;
using MiniDrive.Common;
using MiniDrive.Common.Caching;
using MiniDrive.Common.Observability;
using MiniDrive.Sharing;
using MiniDrive.Sharing.Repositories;
using MiniDrive.Sharing.Services;
using MiniDrive.Clients.Identity;
using MiniDrive.Clients.Audit;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddControllers();

// Add OpenAPI
builder.Services.AddEndpointsApiExplorer();

// Common infrastructure
builder.Services.AddRedisCache(builder.Configuration);
builder.Services.AddOpenTelemetryTracing(builder.Configuration, "MiniDrive.Sharing.Api");

// Sharing DI
builder.Services.AddDbContext<SharingDbContext>(options =>
{
    if (builder.Environment.IsEnvironment("Testing"))
    {
        options.UseInMemoryDatabase("SharingDb");
    }
    else
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("SharingDb")
            ?? throw new InvalidOperationException("Connection string 'SharingDb' not found."));
    }
});

builder.Services.AddScoped<ShareRepository>();
builder.Services.AddScoped<IShareService, ShareService>();

// Microservice clients
builder.Services.AddCachedIdentityClient(builder.Configuration);

var auditServiceUrl = builder.Configuration["Services:Audit"] ?? "http://localhost:5005";
builder.Services.AddHttpClient<IAuditClient, AuditClient>(client =>
{
    client.BaseAddress = new Uri(auditServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddDefaultResilience();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "Sharing", timestamp = DateTime.UtcNow }));

// Database initialization
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SharingDbContext>();
    try
    {
        if (!app.Environment.IsEnvironment("Testing"))
        {
            dbContext.Database.Migrate();
        }
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}

app.Run();
