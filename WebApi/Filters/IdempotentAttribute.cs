using Application.Common.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WebApi.Filters
{
    // Put [Idempotent] on a POST, PUT or PATCH action. The client sends an Idempotency-Key header: a new GUID per submit,
    // and the same GUID when it retries. A retry gets the first response back, and the action does not run twice.
    // shortcut: a crash after the action saved but before its response is saved lets a retry run it again once the claim expires; to close that, save the key and the data in one transaction
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class IdempotentAttribute : Attribute, IAsyncResourceFilter
    {
        public const string HeaderName = "Idempotency-Key";

        public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
        {
            var http = context.HttpContext;
            string? key = http.Request.Headers[HeaderName];
            if (string.IsNullOrWhiteSpace(key) || key.Length > 100)
                throw new ValidationException(new() { [HeaderName] = ["Send an Idempotency-Key header of at most 100 characters."] }, "Validation failed.");

            // Per user and endpoint, so nobody gets another user's saved response by sending the same key.
            var user = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
            var recordKey = SHA256.HashData(Encoding.UTF8.GetBytes($"{user}:{http.Request.Method}:{http.Request.Path}:{key}"));

            // Hash the body, so the same key with different data is refused instead of getting the old answer.
            http.Request.EnableBuffering();
            var requestHash = http.Request.HasFormContentType
                ? await HashFormAsync(http.Request, http.RequestAborted)
                : await SHA256.HashDataAsync(http.Request.Body, http.RequestAborted);
            http.Request.Body.Position = 0;

            // Atomic claim: a double-click sends 2 requests at once, and only one may run the action.
            var repository = http.RequestServices.GetRequiredService<IIdempotencyRepository>();
            var claim = new IdempotencyRecord { Key = recordKey, RequestHash = requestHash };
            var existing = await repository.TryClaimAsync(claim);
            if (existing is not null)
            {
                if (existing.StatusCode is null)
                    throw new ConflictException("A request with this Idempotency-Key is still running. Try again shortly.");

                if (!existing.RequestHash.AsSpan().SequenceEqual(requestHash))
                    throw new ValidationException(new() { [HeaderName] = ["This key was already used with a different request."] }, "Validation failed.");

                await ReplayAsync(context, existing);
                return;
            }

            // Write the response to memory first, so it can be saved and sent again later.
            var original = http.Response.Body;
            using var buffer = new MemoryStream();
            http.Response.Body = buffer;
            ResourceExecutedContext executed;
            try
            {
                executed = await next();
            }
            finally
            {
                http.Response.Body = original;
            }

            // Save before sending: if the client has already gone, this is the answer its retry needs.
            // Only successes are saved, so a failed request can be retried with the same key.
            if (executed.Exception is null && http.Response.StatusCode is >= 200 and < 300)
                await repository.CompleteAsync(claim, http.Response.StatusCode, http.Response.ContentType, http.Response.Headers.Location, buffer.ToArray());
            else
                await repository.ReleaseAsync(claim);

            // The error goes on to ErrorHandlingMiddleware, which writes its own response.
            if (executed.Exception is not null && !executed.ExceptionHandled)
                return;

            buffer.Position = 0;
            await buffer.CopyToAsync(original);
        }

        // A browser picks a new random boundary per send, so a retry's raw bytes differ; compare the parsed fields and files instead.
        private static async Task<byte[]> HashFormAsync(HttpRequest request, CancellationToken token)
        {
            IFormCollection form;
            try
            {
                form = await request.ReadFormAsync(token);
            }
            catch (InvalidDataException)
            {
                throw new ValidationException(new() { ["Form"] = ["The form data is malformed or too large."] }, "Validation failed.");
            }

            var files = new List<object>();
            foreach (var file in form.Files)
            {
                await using var content = file.OpenReadStream();
                files.Add(new { file.Name, file.FileName, Content = await SHA256.HashDataAsync(content, token) });
            }

            var fields = form.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => new { f.Key, Values = f.Value.ToArray() });
            return SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { fields, files }));
        }

        private static async Task ReplayAsync(ResourceExecutingContext context, IdempotencyRecord saved)
        {
            var response = context.HttpContext.Response;
            response.StatusCode = saved.StatusCode!.Value;
            response.ContentType = saved.ContentType;
            if (saved.Location is not null)
                response.Headers.Location = saved.Location;
            response.Headers["Idempotent-Replayed"] = "true";

            await response.Body.WriteAsync(saved.Body, context.HttpContext.RequestAborted);
            context.Result = new EmptyResult(); // already written above; stops the action from running
        }
    }
}
