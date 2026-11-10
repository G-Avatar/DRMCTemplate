using Application.Interfaces;
using Infrastructure.Persistence.Context;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("PayrollDb");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Connection string 'PayrollDb' is missing. Set ConnectionStrings__PayrollDb in .env (see .env.example).");
            }

            services.AddDbContext<PayrollDbContext>(options => options.UseSqlServer(connectionString));

            // Scoped
            services.AddScoped<IAuthSessionRepository, AuthSessionRepository>();

            // Singleton


            return services;
        }
    }
}
