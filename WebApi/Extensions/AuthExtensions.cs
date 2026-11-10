using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Text.Json;

namespace WebApi.Extensions
{
    public static class AuthExtensions
    {
        public const string SsoClient = "sso";

        // Users log in on DRMCAppManagement. This server is the client: the tokens stay here, and the browser only gets a session cookie that scripts can't read.
        public static IServiceCollection AddSsoAuth(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
        {
            string Setting(string name) => configuration[$"Sso:{name}"]
                ?? throw new InvalidOperationException($"Missing setting: set Sso__{name} in .env (see .env.example).");

            var authority = Setting("Authority");
            var clientId = Setting("ClientId");
            var clientSecret = Setting("ClientSecret");
            var appUrl = Setting("AppUrl"); // from settings, because the request's Host header is whatever the sender says

            services.AddHttpClient(SsoClient);
            services.AddSingleton<ITicketStore, SessionTicketStore>();
            services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
                .Configure<ITicketStore>((options, store) => options.SessionStore = store);

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    // The __Host- prefix stops a sibling subdomain planting this cookie, but http://localhost (Vite) can't set it.
                    options.Cookie.Name = environment.IsDevelopment() ? "payroll.session" : "__Host-payroll.session";
                    options.Cookie.SameSite = SameSiteMode.Strict;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    // The access token's lifetime. After that the portal signs the user back in without a password.
                    options.ExpireTimeSpan = TimeSpan.FromHours(1);
                    options.SlidingExpiration = false;
                    options.Events.OnRedirectToLogin = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized; // the Vue app starts the sign-in
                        return Task.CompletedTask;
                    };
                    options.Events.OnValidatePrincipal = context => EndSessionIfRevokedAsync(context, authority, clientId, clientSecret);
                })
                .AddOpenIdConnect(options =>
                {
                    options.Authority = authority;
                    options.ClientId = clientId;
                    options.ClientSecret = clientSecret;
                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.ResponseMode = OpenIdConnectResponseMode.Query;
                    options.CallbackPath = "/callback";
                    options.SaveTokens = true; // kept in the server-side session, for the token check
                    options.TokenValidationParameters.NameClaimType = "name";

                    options.Events.OnRedirectToIdentityProvider = context =>
                    {
                        context.ProtocolMessage.RedirectUri = $"{appUrl}/callback";
                        return Task.CompletedTask;
                    };
                    options.Events.OnRemoteFailure = context =>
                    {
                        context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(SsoClient).LogWarning(context.Failure, "SSO sign-in failed.");
                        context.Response.Redirect(authority);
                        context.HandleResponse();
                        return Task.CompletedTask;
                    };
                });

            return services;
        }

        // Asks DRMCAppManagement on every API call, so signing out on the portal ends this session at once.
        private static async Task EndSessionIfRevokedAsync(CookieValidatePrincipalContext context, string authority, string clientId, string clientSecret)
        {
            // Pages hold no data.
            if (!context.Request.Path.StartsWithSegments("/api"))
                return;

            var http = context.HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient(SsoClient);
            using var response = await http.PostAsync($"{authority}/connect/introspect", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["token"] = context.Properties.GetTokenValue("access_token") ?? "",
            }), context.HttpContext.RequestAborted);

            // No answer means the session can't be trusted: this fails the request instead of letting it through.
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(context.HttpContext.RequestAborted);
            if (!result.GetProperty("active").GetBoolean())
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        }
    }
}
