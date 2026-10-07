using System.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace CustomerManagement.Gateway.Tests.Infrastructure;

public sealed class DownstreamStubServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    public string BaseAddress { get; }

    public int Port { get; }

    private DownstreamStubServer(WebApplication app, string baseAddress, int port)
    {
        _app = app;
        BaseAddress = baseAddress;
        Port = port;
    }

    public static async Task<DownstreamStubServer> StartAsync(CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var app = builder.Build();

        app.MapMethods(
            "/api/customers/{**catchAll}",
            ["GET", "POST", "PUT"],
            (Delegate)HandleRequestAsync);

        app.MapMethods(
            "/api/tickets/{**catchAll}",
            ["GET", "POST", "PUT"],
            (Delegate)HandleRequestAsync);

        app.MapMethods(
            "/api/dashboard/{**catchAll}",
            ["GET"],
            (Delegate)HandleRequestAsync);

        app.MapMethods(
            "/api/ticket-tasks/{**catchAll}",
            ["GET", "POST", "PUT"],
            (Delegate)HandleRequestAsync);

        app.MapMethods(
            "/api/quick-replies/{**catchAll}",
            ["GET", "POST", "PUT"],
            (Delegate)HandleRequestAsync);

        app.MapMethods(
            "/api/ticket-notes/{**catchAll}",
            ["GET", "POST"],
            (Delegate)HandleRequestAsync);

        app.MapMethods(
            "/api/ticket-messages/{**catchAll}",
            ["GET", "POST"],
            (Delegate)HandleRequestAsync);

        app.MapMethods(
            "/api/faq/{**catchAll}",
            ["GET", "POST", "PUT"],
            (Delegate)HandleRequestAsync);

        app.MapMethods(
            "/api/feedback/{**catchAll}",
            ["GET", "POST"],
            (Delegate)HandleRequestAsync);

        app.MapMethods(
            "/api/help-articles/{**catchAll}",
            ["GET", "POST", "PUT"],
            (Delegate)HandleRequestAsync);

        app.MapMethods(
            "/api/guides/{**catchAll}",
            ["GET", "POST", "PUT"],
            (Delegate)HandleRequestAsync);

        app.MapMethods(
            "/api/knowledge-base/{**catchAll}",
            ["GET"],
            (Delegate)HandleRequestAsync);

        await app.StartAsync(cancellationToken);

        var address = app.Urls.Single();
        var uri = new Uri(address);
        return new DownstreamStubServer(app, address, uri.Port);
    }

    private static async Task<IResult> HandleRequestAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var query = context.Request.QueryString.Value ?? string.Empty;
        var method = context.Request.Method;

        if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader) ||
            string.IsNullOrWhiteSpace(authHeader))
        {
            return Results.Unauthorized();
        }

        var token = authHeader.ToString();
        if (!token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Unauthorized();
        }

        var principal = ValidateToken(token[7..].Trim());
        if (principal is null)
        {
            return Results.Unauthorized();
        }

        var permissions = principal.FindAll("permission").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requiredReadPermission = ResolveRequiredPermission(path);

        if (!permissions.Contains(requiredReadPermission))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var userId = principal.FindFirst("sub")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? string.Empty;
        var email = principal.FindFirst("email")?.Value
            ?? principal.FindFirst(ClaimTypes.Email)?.Value
            ?? string.Empty;

        if (context.Request.Query.TryGetValue("simulateError", out var simulateErrorValue) &&
            string.Equals(simulateErrorValue.ToString(), "true", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(new { error = "downstream_failure" }, statusCode: StatusCodes.Status500InternalServerError);
        }

        if (string.Equals(path, "/api/customers", StringComparison.OrdinalIgnoreCase) &&
            HttpMethods.IsPost(method))
        {
            var body = await TryReadJsonAsync(context.Request);
            var name = body.TryGetValue("name", out var rawName) && rawName.ValueKind == JsonValueKind.String
                ? rawName.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(name))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["name"] = ["The name field is required."]
                });
            }

            return Results.Created($"/api/customers/{Guid.NewGuid()}", new { created = true, path, method });
        }

        if (path.Contains(Guid.Empty.ToString(), StringComparison.OrdinalIgnoreCase) &&
            path.EndsWith("/interaction-history", StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound();
        }

        if (path.EndsWith("/interaction-history", StringComparison.OrdinalIgnoreCase) &&
            context.Request.Query.TryGetValue("channel", out var channel) &&
            string.Equals(channel.ToString(), "fax", StringComparison.OrdinalIgnoreCase))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["channel"] = ["Channel filter is invalid."]
            });
        }

        return Results.Json(new
        {
            method,
            path,
            query,
            userId,
            email,
            forwardedUserId = context.Request.Headers["X-User-Id"].ToString(),
            forwardedEmail = context.Request.Headers["X-User-Email"].ToString()
        }, statusCode: (int)HttpStatusCode.OK);
    }

    private static string ResolveRequiredPermission(string path)
    {
        if (path.StartsWith("/api/tickets", StringComparison.OrdinalIgnoreCase))
        {
            return "tickets.read";
        }

        if (path.StartsWith("/api/dashboard", StringComparison.OrdinalIgnoreCase))
        {
            return "dashboard.read";
        }

        if (path.StartsWith("/api/ticket-tasks", StringComparison.OrdinalIgnoreCase))
        {
            return "ticket-tasks.read";
        }

        if (path.StartsWith("/api/quick-replies", StringComparison.OrdinalIgnoreCase))
        {
            return "quick-replies.read";
        }

        if (path.StartsWith("/api/ticket-notes", StringComparison.OrdinalIgnoreCase))
        {
            return "ticket-notes.read";
        }

        if (path.StartsWith("/api/ticket-messages", StringComparison.OrdinalIgnoreCase))
        {
            return "ticket-messages.read";
        }

        if (path.StartsWith("/api/faq", StringComparison.OrdinalIgnoreCase))
        {
            return "faq.read";
        }

        if (path.StartsWith("/api/feedback", StringComparison.OrdinalIgnoreCase))
        {
            return "feedback.read";
        }

        if (path.StartsWith("/api/help-articles", StringComparison.OrdinalIgnoreCase))
        {
            return "help-articles.read";
        }

        if (path.StartsWith("/api/guides", StringComparison.OrdinalIgnoreCase))
        {
            return "guides.read";
        }

        if (path.StartsWith("/api/knowledge-base", StringComparison.OrdinalIgnoreCase))
        {
            return "knowledge-base.search";
        }

        return "customers.read";
    }

    private static ClaimsPrincipal? ValidateToken(string jwt)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = TestJwtFactory.Issuer,
            ValidateAudience = true,
            ValidAudience = TestJwtFactory.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtFactory.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        try
        {
            var handler = new JwtSecurityTokenHandler();
            return handler.ValidateToken(jwt, parameters, out _);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<Dictionary<string, JsonElement>> TryReadJsonAsync(HttpRequest request)
    {
        if (request.ContentLength is null or 0)
        {
            return [];
        }

        try
        {
            var payload = await JsonSerializer.DeserializeAsync<Dictionary<string, JsonElement>>(request.Body);
            return payload ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
