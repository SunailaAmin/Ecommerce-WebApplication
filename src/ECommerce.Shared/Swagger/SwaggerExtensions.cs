using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ECommerce.Shared.Swagger;

/// <summary>
/// The Swagger setup is the same in every service apart from the document title and which
/// assemblies carry XML comments, so it lives here rather than being repeated four times.
/// </summary>
public static class SwaggerExtensions
{
    /// <param name="services">Service collection being configured.</param>
    /// <param name="title">Document title shown in this service's Swagger UI.</param>
    /// <param name="xmlDocumentNames">Assembly names whose generated XML comments to include.</param>
    public static IServiceCollection AddServiceSwagger(
        this IServiceCollection services,
        string title,
        params string[] xmlDocumentNames)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            var documentationFiles = xmlDocumentNames
                .Select(name => Path.Combine(AppContext.BaseDirectory, $"{name}.xml"))
                .Where(File.Exists);

            foreach (var file in documentationFiles)
            {
                options.IncludeXmlComments(file);
            }

            options.OperationFilter<AuthorizationOperationFilter>();

            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = title,
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

        return services;
    }
}
