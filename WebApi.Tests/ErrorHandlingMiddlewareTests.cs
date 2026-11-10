using Application.Common.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace WebApi.Tests
{
    public class ErrorHandlingMiddlewareTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client = factory.WithWebHostBuilder(builder =>
        {
            // CI has no .env; the DbContext is registered but never opened in these tests
            builder.UseSetting("ConnectionStrings:PayrollDb", "Server=unused");
            builder.ConfigureServices(services =>
                services.AddControllers().AddApplicationPart(typeof(ThrowController).Assembly));
        }).CreateClient();

        [Fact]
        public async Task AliveReturnsOk()
        {
            var response = await _client.GetAsync("/alive");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task NotFoundExceptionReturns404WithMessage()
        {
            var response = await _client.GetAsync("/test/not-found");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Employee 7 not found.", body.GetProperty("message").GetString());
        }

        [Fact]
        public async Task ValidationExceptionReturns400WithErrors()
        {
            var response = await _client.GetAsync("/test/validation");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Name is required.", body.GetProperty("errors").GetProperty("Name")[0].GetString());
        }

        [Fact]
        public async Task UnknownExceptionReturns500()
        {
            var response = await _client.GetAsync("/test/crash");

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Something went wrong.", body.GetProperty("message").GetString());
        }
    }

    // Actions must stay instance methods: MVC ignores static methods, so "Make static" breaks these routes
    [ApiController]
    [Route("test")]
    public class ThrowController : ControllerBase
    {
        [HttpGet("not-found")]
        public IActionResult NotFoundError() => throw new NotFoundException("Employee 7 not found.");

        [HttpGet("validation")]
        public IActionResult ValidationError() =>
            throw new ValidationException(new() { ["Name"] = ["Name is required."] }, "Validation failed.");

        [HttpGet("crash")]
        public IActionResult Crash() => throw new InvalidOperationException("boom");
    }
}
