using DDDExample.Domain.Repositories;
using DDDExample.Infrastructure.Persistence.SqlServer;
using DDDExample.Infrastructure.Repositories.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DDDExample.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure SQL Server settings
        var sqlServerSettings = configuration.GetSection("SQLServerSettings").Get<SqlServerSettings>() 
            ?? throw new InvalidOperationException("SQLServerSettings configuration section is missing or invalid");
        
        // Configure SQL Server for Products
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                sqlServerSettings.ConnectionString,
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));
        
        // Register SQL Server Product Repository
        services.AddScoped<IRepository<Domain.Entities.Product, Guid>, SqlProductRepository>();

        return services;
    }
}
