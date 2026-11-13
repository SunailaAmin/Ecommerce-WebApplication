using ECommerce.API.Hubs;
using ECommerce.API.Middleware;
using ECommerce.API.Notifications;
using ECommerce.API.Swagger;
using ECommerce.Application;
using ECommerce.Application.Interfaces;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

const string CorsPolicyName = "frontend";

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var documentationFiles = new[] { "ECommerce.API", "ECommerce.Application" }
        .Select(name => Path.Combine(AppContext.BaseDirectory, $"{name}.xml"))
        .Where(File.Exists);

    foreach (var file in documentationFiles)
    {
        options.IncludeXmlComments(file);
    }

    options.OperationFilter<AuthorizationOperationFilter>();

    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ECommerce API",
        Version = "v1"
    });

    // Declaring the scheme is what gives Swagger UI its Authorize button.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSignalR();

builder.Services.AddSingleton<IOrderNotificationSender, SignalROrderNotificationSender>();

// The Vite dev server proxies /api and /hubs, so a normal dev session is same origin and
// needs nothing here. This policy is for when the client is pointed straight at the API
// (VITE_API_BASE_URL), and it is registered in Development only so a production host is
// never opened up by an environment file.
if (builder.Environment.IsDevelopment())
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? ["http://localhost:5173"];

    builder.Services.AddCors(options =>
    {
        options.AddPolicy(CorsPolicyName, policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10)));
    });
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

        await context.Database.MigrateAsync();
        await DbContextSeeder.SeedAsync(context);
    }

    app.UseCors(CorsPolicyName);
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<OrderNotificationHub>(OrderNotificationHub.Route);

app.Run();

public partial class Program;
