using Domain.Entities;
using Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using WebApi.Extensions;

namespace WebApi.Tests
{
    // The real pipeline on a throwaway in-memory SQLite, never your real database, with a stand-in for DRMCAppManagement so no SSO server has to run.
    public sealed class SsoAuthTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
    {
        private const string Me = "/api/account/me";

        private readonly string _connectionString = $"Data Source={Guid.NewGuid()};Mode=Memory;Cache=Shared";
        private readonly SqliteConnection _keepAlive;
        private readonly FakeSso _sso = new();
        private readonly HttpClient _client;

        public SsoAuthTests(WebApplicationFactory<Program> factory)
        {
            _keepAlive = new SqliteConnection(_connectionString);
            _keepAlive.Open();
            using (var db = NewDb())
                db.Database.EnsureCreated();

            _client = factory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:PayrollDb", "Server=unused");
                builder.UseSetting("Sso:ClientSecret", "test-only-secret");
                builder.UseSetting("Sso:AppUrl", "http://localhost:5174");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IDbContextOptionsConfiguration<PayrollDbContext>>();
                    services.AddDbContext<PayrollDbContext>(options => options.UseSqlite(_connectionString));
                    services.AddControllers().AddApplicationPart(typeof(TestSessionController).Assembly);
                    services.AddHttpClient(AuthExtensions.SsoClient).ConfigurePrimaryHttpMessageHandler(() => _sso);
                    services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
                        options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(new OpenIdConnectConfiguration
                        {
                            AuthorizationEndpoint = "https://sso.test/connect/authorize",
                        }));
                });
            }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        }

        public void Dispose() => _keepAlive.Dispose();

        [Fact]
        public async Task SessionsAreSavedInTheDatabaseWithTheirTokensEncrypted()
        {
            var cookie = await SignInAsync();

            await using (var db = NewDb())
            {
                var session = await db.AuthSessions.SingleAsync();
                Assert.Equal(-1, session.Ticket.AsSpan().IndexOf("test-token"u8));
            }

            Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, Me, cookie)).StatusCode);
            Assert.Contains("token=test-token", _sso.LastRequest);
        }

        [Fact]
        public async Task ExpiredSessionsAreRemovedOnTheNextSignIn()
        {
            await using (var db = NewDb())
            {
                db.AuthSessions.Add(new AuthSession { Id = Guid.NewGuid(), Ticket = [1], ExpiresAt = DateTime.UtcNow.AddMinutes(-1) });
                await db.SaveChangesAsync();
            }

            await SignInAsync();

            await using (var db = NewDb())
                Assert.True((await db.AuthSessions.SingleAsync()).ExpiresAt > DateTime.UtcNow);
        }

        [Fact]
        public async Task MeWithoutASessionReturns401()
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(Me)).StatusCode);
        }

        [Fact]
        public async Task MeWithALiveSessionReturnsTheUser()
        {
            var response = await SendAsync(HttpMethod.Get, Me, await SignInAsync());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("TEST-1", body.GetProperty("data").GetProperty("fullName").GetString());
        }

        [Fact]
        public async Task SessionCookieIsHiddenFromScriptsAndOtherSites()
        {
            var response = await _client.GetAsync("/test-session");

            var cookie = response.Headers.GetValues("Set-Cookie").Single().ToLowerInvariant();
            Assert.Contains("httponly", cookie);
            Assert.Contains("secure", cookie);
            Assert.Contains("samesite=strict", cookie);
            Assert.DoesNotContain("test-token", cookie);
        }

        [Fact]
        public async Task SigningOutOnThePortalEndsTheSessionOnTheNextRequest()
        {
            var cookie = await SignInAsync();
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, Me, cookie)).StatusCode);

            _sso.Active = false;
            Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, Me, cookie)).StatusCode);

            // The session itself is gone, not just refused once.
            _sso.Active = true;
            Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, Me, cookie)).StatusCode);
        }

        [Fact]
        public async Task ARequestIsRefusedWhenThePortalCannotConfirmTheSession()
        {
            var cookie = await SignInAsync();

            _sso.Status = HttpStatusCode.BadRequest;

            Assert.Equal(HttpStatusCode.InternalServerError, (await SendAsync(HttpMethod.Get, Me, cookie)).StatusCode);
        }

        [Fact]
        public async Task LoginGoesToThePortalWithThisAppsCallback()
        {
            var response = await _client.GetAsync("/api/account/login?returnUrl=https://evil.test/");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location!.ToString();
            Assert.StartsWith("https://sso.test/connect/authorize", location);
            Assert.Contains(Uri.EscapeDataString("http://localhost:5174/callback"), location);
            Assert.Contains("code_challenge=", location);
        }

        [Theory]
        [InlineData("?error=access_denied")]
        [InlineData("?error=server_error")]
        public async Task AFailedSignInGoesBackToThePortal(string query)
        {
            var portal = (await _client.GetAsync("/api/account/portal")).Headers.Location;

            var response = await _client.GetAsync("/callback" + query);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(portal, response.Headers.Location);
        }

        [Theory]
        [InlineData("cross-site")]
        [InlineData("same-site")]
        public async Task ChangesStartedByAnotherSiteAreRefused(string site)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Me);
            request.Headers.Add("Sec-Fetch-Site", site);

            Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(request)).StatusCode);
        }

        [Fact]
        public async Task PagesCarryTheSecurityHeaders()
        {
            var headers = (await _client.GetAsync("/alive")).Headers;

            Assert.StartsWith("default-src 'self';", headers.GetValues("Content-Security-Policy").Single());
            Assert.Equal("DENY", headers.GetValues("X-Frame-Options").Single());
            Assert.Equal("nosniff", headers.GetValues("X-Content-Type-Options").Single());
        }

        private PayrollDbContext NewDb() => new(new DbContextOptionsBuilder<PayrollDbContext>().UseSqlite(_connectionString).Options);

        private async Task<string> SignInAsync()
        {
            var response = await _client.GetAsync("/test-session");
            return response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        }

        private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string cookie)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Add("Cookie", cookie);
            return await _client.SendAsync(request);
        }

        private sealed class FakeSso : HttpMessageHandler
        {
            public bool Active { get; set; } = true;

            public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

            public string LastRequest { get; private set; } = "";

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = await request.Content!.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(Status) { Content = JsonContent.Create(new { active = Active }) };
            }
        }
    }

    // Stands in for the real sign-in, which needs a live SSO server. Must stay an instance method: MVC ignores static ones.
    [AllowAnonymous]
    [ApiController]
    [Route("test-session")]
    public class TestSessionController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> SignIn()
        {
            var identity = new ClaimsIdentity([new Claim("name", "TEST-1")], CookieAuthenticationDefaults.AuthenticationScheme, "name", null);
            var properties = new AuthenticationProperties();
            properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "test-token" }]);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), properties);
            return NoContent();
        }
    }
}
