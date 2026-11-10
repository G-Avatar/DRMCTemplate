using Application;
using Application.Common.Exceptions;
using DotNetEnv;
using Infrastructure;
using Serilog;
using System.Text.Json.Serialization;
using WebApi.Extensions;
using WebApi.Middlewares;

// .env lives in the repo root, but VS and Aspire start the API from WebApi/
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddSsoAuth(builder.Configuration, builder.Environment);

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = true;
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            ReferenceHandler.IgnoreCycles;

        options.JsonSerializerOptions.Converters
            .Add(new JsonStringEnumConverter());
    });
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// writeToProviders keeps the OpenTelemetry logger from ServiceDefaults, so logs reach the Aspire dashboard
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            "logs/app-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30
        );
}, writeToProviders: true);

var app = builder.Build();

app.UseSerilogRequestLogging();

app.UseMiddleware<ErrorHandlingMiddleware>();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
}

// The policy is what stops injected scripts running or sending data elsewhere.
// shortcut: form-action still allows the portal though nothing posts there now; removing it makes Sonar S7039 flag style-src 'unsafe-inline', so settle that first.
var contentSecurityPolicy = $"default-src 'self'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; img-src 'self' data:; object-src 'none'; base-uri 'self'; frame-ancestors 'none'; form-action 'self' {app.Configuration["Sso:Authority"]}";
app.Use((context, next) =>
{
    var headers = context.Response.Headers;
    headers.ContentSecurityPolicy = contentSecurityPolicy;
    headers.XFrameOptions = "DENY";
    headers.XContentTypeOptions = "nosniff";
    headers["Referrer-Policy"] = "same-origin";
    return next(context);
});

app.UseHttpsRedirection();

// The session cookie goes with every request, so refuse changes another site started. Browsers set this header and scripts can't forge it.
app.Use((context, next) =>
{
    var isReadOnly = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method) || HttpMethods.IsOptions(context.Request.Method);
    if (!isReadOnly && context.Request.Headers["Sec-Fetch-Site"].ToString() is not ("" or "same-origin"))
        throw new ForbiddenException("Cross-site requests are not allowed.");

    return next(context);
});

app.UseDefaultFiles(); // serves index.html for "/", which the fallback below can't match

app.UseStaticFiles();

app.UseAuthentication();

app.UseAuthorization();

// Every controller needs a login unless it is marked [AllowAnonymous].
app.MapControllers().RequireAuthorization();

// Vue handles every page that is not the API, including the SSO /callback page.
app.MapFallbackToFile("{*path:nonfile:regex(^(?!api/).*$)}", "index.html");

await app.RunAsync();
