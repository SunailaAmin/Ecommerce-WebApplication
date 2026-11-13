using ECommerce.Products.Application;
using ECommerce.Products.Infrastructure;
using ECommerce.Products.Infrastructure.Data;
using ECommerce.Products.Infrastructure.Middleware;
using ECommerce.Shared.Middleware;
using ECommerce.Shared.Swagger;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddServiceSwagger("ECommerce Product Service", "ECommerce.Product");
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Phase D stopgap: the monolith ran Migrate + Seed here, and neither exists for the new
    // services yet. EnsureCreated gives this service its own ECommerce_Products database so
    // it can be run on its own without touching ECommerceDb. Phase I replaces this with
    // migrations, dropping this development database first so the schemas do not collide.
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Before authentication on purpose: /internal carries a service key instead of a user
// token, so it must be rejected before any user identity is consulted.
app.UseMiddleware<InternalServiceKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
