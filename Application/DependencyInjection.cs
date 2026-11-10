
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Application.Abstractions;

namespace Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

            // Register command handlers

            // Register query handlers

            return services;
        }


        #pragma warning disable S1144
        private static void AddCommandHandler<TCommand, TResult, THandler>(this IServiceCollection services) where THandler : class, ICommandHandler<TCommand, TResult>
        {
            services.AddScoped<THandler>();
            services.AddScoped<ICommandHandler<TCommand, TResult>>(provider => new ValidatingCommandHandler<TCommand, TResult>(
                provider.GetRequiredService<THandler>(),
                provider.GetServices<IValidator<TCommand>>()));
        }
    }
}
