using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Context;
using Infrastructure.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using WebApi.Filters;
using WebApi.Middlewares;

namespace WebApi.Tests
{
    // Small test host with only MVC and the error middleware. The database is a throwaway in-memory SQLite, never your real one.
    public class IdempotentAttributeTests
    {
        [Fact]
        public async Task SameKeyRunsActionOnceAndReplaysResponse()
        {
            var probe = new Probe();
            await using var app = await StartAppAsync(probe);
            var client = app.GetTestClient();
            var key = Guid.NewGuid().ToString();

            var first = await PostAsync(client, key, "Juan");
            var second = await PostAsync(client, key, "Juan");

            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            Assert.Equal(HttpStatusCode.Created, second.StatusCode);
            Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
            Assert.Equal(first.Headers.Location, second.Headers.Location);
            Assert.Equal("true", second.Headers.GetValues("Idempotent-Replayed").Single());
            Assert.Equal(1, probe.Runs);
        }

        [Fact]
        public async Task MissingKeyOrChangedBodyIsRefused()
        {
            var probe = new Probe();
            await using var app = await StartAppAsync(probe);
            var client = app.GetTestClient();
            var key = Guid.NewGuid().ToString();

            Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(client, null, "Juan")).StatusCode);
            await PostAsync(client, key, "Juan");
            Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(client, key, "Pedro")).StatusCode);
            Assert.Equal(1, probe.Runs);
        }

        [Fact]
        public async Task SecondRequestWhileFirstIsRunningGets409()
        {
            var probe = new Probe();
            var release = new TaskCompletionSource();
            probe.Release = release.Task;
            await using var app = await StartAppAsync(probe);
            var client = app.GetTestClient();
            var key = Guid.NewGuid().ToString();

            var first = PostAsync(client, key, "Juan");
            await probe.Entered.Task;
            var second = PostAsync(client, key, "Juan");
            try
            {
                // If the filter breaks, the 2nd request also waits on Release: fail instead of hanging.
                Assert.Same(second, await Task.WhenAny(second, Task.Delay(TimeSpan.FromSeconds(10))));
            }
            finally
            {
                release.SetResult();
            }

            Assert.Equal(HttpStatusCode.Conflict, (await second).StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await first).StatusCode);
            Assert.Equal(1, probe.Runs);
        }

        [Fact]
        public async Task FormRetryWithNewBoundaryReplaysAndChangedFileIsRefused()
        {
            var probe = new Probe();
            await using var app = await StartAppAsync(probe);
            var client = app.GetTestClient();
            var key = Guid.NewGuid().ToString();

            var first = await PostFormAsync(client, key, "logo-1");
            var retry = await PostFormAsync(client, key, "logo-1");
            var changed = await PostFormAsync(client, key, "logo-2");

            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            Assert.Contains("logo-1", await first.Content.ReadAsStringAsync()); // the action can still read the file after it was hashed
            Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
            Assert.Equal("true", retry.Headers.GetValues("Idempotent-Replayed").Single());
            Assert.Equal(HttpStatusCode.BadRequest, changed.StatusCode);
            Assert.Equal(1, probe.Runs);
        }

        [Fact]
        public async Task MalformedFormGets400()
        {
            await using var app = await StartAppAsync(new Probe());
            using var request = new HttpRequestMessage(HttpMethod.Post, "/idempotent-test/form") { Content = new StringContent("not a form") };
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data"); // no boundary
            request.Headers.Add(IdempotentAttribute.HeaderName, Guid.NewGuid().ToString());

            var response = await app.GetTestClient().SendAsync(request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task ResponseIsSavedEvenIfTheClientHasGone()
        {
            var probe = new Probe();
            await using var app = await StartAppAsync(probe);
            var client = app.GetTestClient();
            var key = Guid.NewGuid().ToString();

            await Assert.ThrowsAnyAsync<Exception>(() => PostAsync(client, key, "Juan", "/idempotent-test/client-gone"));
            var retry = await PostAsync(client, key, "Juan", "/idempotent-test/client-gone");

            Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
            Assert.Equal("true", retry.Headers.GetValues("Idempotent-Replayed").Single());
            Assert.Equal(1, probe.Runs);
        }

        [Fact]
        public async Task FailedRequestCanRetryWithTheSameKey()
        {
            var probe = new Probe { Fail = true };
            await using var app = await StartAppAsync(probe);
            var client = app.GetTestClient();
            var key = Guid.NewGuid().ToString();

            Assert.Equal(HttpStatusCode.InternalServerError, (await PostAsync(client, key, "Juan")).StatusCode);
            probe.Fail = false;
            var retry = await PostAsync(client, key, "Juan");

            Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
            Assert.False(retry.Headers.Contains("Idempotent-Replayed"));
            Assert.Equal(2, probe.Runs);
        }

        [Fact]
        public async Task ClaimThatLosesTheRaceGetsTheWinnersRecord()
        {
            var connectionString = NewDatabase();
            await using var keepAlive = await OpenAndCreateAsync(connectionString);
            var key = RandomNumberGenerator.GetBytes(32);
            var options = new DbContextOptionsBuilder<PayrollDbContext>().UseSqlite(connectionString).AddInterceptors(new RivalClaimsFirst(connectionString, key)).Options;
            await using var db = new PayrollDbContext(options);

            var existing = await new IdempotencyRepository(db).TryClaimAsync(NewClaim(key));

            Assert.NotNull(existing);
            Assert.Empty(db.ChangeTracker.Entries()); // or the action's own SaveChanges would retry the failed insert
        }

        [Fact]
        public async Task AbandonedClaimCanBeTakenOverAndItsOwnerCannotOverwriteIt()
        {
            var connectionString = NewDatabase();
            await using var keepAlive = await OpenAndCreateAsync(connectionString);
            await using var db = NewContext(connectionString);
            var repository = new IdempotencyRepository(db);
            var key = RandomNumberGenerator.GetBytes(32);
            var crashed = NewClaim(key);

            Assert.Null(await repository.TryClaimAsync(crashed));
            Assert.NotNull(await repository.TryClaimAsync(NewClaim(key)));

            await db.IdempotencyRecords.ExecuteUpdateAsync(s => s.SetProperty(r => r.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
            crashed.ExpiresAt = await db.IdempotencyRecords.Select(r => r.ExpiresAt).SingleAsync();
            Assert.Null(await repository.TryClaimAsync(NewClaim(key)));

            await repository.CompleteAsync(crashed, 201, null, null, []);
            Assert.Null((await repository.TryClaimAsync(NewClaim(key)))!.StatusCode);
        }

        // Inserts a rival claim right before this context saves, so the repository's insert loses the race every time.
        private sealed class RivalClaimsFirst(string connectionString, byte[] key) : SaveChangesInterceptor
        {
            public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                await using var rival = NewContext(connectionString);
                var claim = NewClaim(key);
                claim.ExpiresAt = DateTime.UtcNow.AddMinutes(5);
                rival.IdempotencyRecords.Add(claim);
                await rival.SaveChangesAsync(cancellationToken);
                return result;
            }
        }

        private static IdempotencyRecord NewClaim(byte[] key) => new() { Key = key, RequestHash = [1] };

        private static string NewDatabase() => $"Data Source={Guid.NewGuid()};Mode=Memory;Cache=Shared";

        private static PayrollDbContext NewContext(string connectionString) =>
            new(new DbContextOptionsBuilder<PayrollDbContext>().UseSqlite(connectionString).Options);

        // In-memory SQLite lives only while one connection stays open.
        private static async Task<SqliteConnection> OpenAndCreateAsync(string connectionString)
        {
            var keepAlive = new SqliteConnection(connectionString);
            await keepAlive.OpenAsync();
            await using var db = NewContext(connectionString);
            await db.Database.EnsureCreatedAsync();
            return keepAlive;
        }

        private static async Task<WebApplication> StartAppAsync(Probe probe)
        {
            var connectionString = NewDatabase();
            var keepAlive = await OpenAndCreateAsync(connectionString);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddDbContext<PayrollDbContext>(options => options.UseSqlite(connectionString));
            builder.Services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
            builder.Services.AddSingleton(probe);
            builder.Services.AddControllers().AddApplicationPart(typeof(IdempotentTestController).Assembly);

            var app = builder.Build();
            app.Lifetime.ApplicationStopped.Register(keepAlive.Dispose);
            app.UseMiddleware<ErrorHandlingMiddleware>();
            app.MapControllers();
            await app.StartAsync();
            return app;
        }

        private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string? key, string name, string path = "/idempotent-test")
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new TestItem(name)) };
            if (key is not null)
                request.Headers.Add(IdempotentAttribute.HeaderName, key);
            return await client.SendAsync(request);
        }

        private static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string key, string fileContent)
        {
            // MultipartFormDataContent picks a new random boundary each time, like a browser does.
            using var form = new MultipartFormDataContent
            {
                { new StringContent("Juan"), "name" },
                { new ByteArrayContent(Encoding.UTF8.GetBytes(fileContent)), "logo", "logo.png" },
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, "/idempotent-test/form") { Content = form };
            request.Headers.Add(IdempotentAttribute.HeaderName, key);
            return await client.SendAsync(request);
        }
    }

    public sealed record TestItem(string Name);

    // Counts how often the action really ran; Release lets a test hold the action open.
    public sealed class Probe
    {
        public int Runs { get; set; }
        public bool Fail { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Release { get; set; } = Task.CompletedTask;
    }

    [ApiController]
    [Route("idempotent-test")]
    public class IdempotentTestController(Probe probe) : ControllerBase
    {
        [HttpPost]
        [Idempotent]
        public async Task<IActionResult> Create(TestItem item)
        {
            probe.Runs++;
            if (probe.Fail)
                throw new InvalidOperationException("boom");
            probe.Entered.TrySetResult();
            await probe.Release;
            return Created($"/items/{probe.Runs}", new { item.Name, run = probe.Runs });
        }

        [HttpPost("client-gone")]
        [Idempotent]
        public IActionResult ClientGone(TestItem item)
        {
            probe.Runs++;
            HttpContext.Abort();
            return Created($"/items/{probe.Runs}", new { item.Name });
        }

        [HttpPost("form")]
        [Idempotent]
        public async Task<IActionResult> CreateFromForm([FromForm] string name, IFormFile logo)
        {
            probe.Runs++;
            using var reader = new StreamReader(logo.OpenReadStream());
            return Created($"/items/{probe.Runs}", new { name, logo = await reader.ReadToEndAsync() });
        }
    }
}
