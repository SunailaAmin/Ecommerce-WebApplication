using ECommerce.Payments.Application;
using ECommerce.Payments.Infrastructure;
using ECommerce.Payments.Infrastructure.Data;
using ECommerce.Shared.Middleware;
using ECommerce.Shared.Swagger;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddServiceSwagger("ECommerce Payment Service", "ECommerce.Payment");
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Phase D stopgap: the monolith ran Migrate + Seed here, and neither exists for the new
    // services yet. EnsureCreated gives this service its own ECommerce_Payments database so
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
