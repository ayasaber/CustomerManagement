using System.Text;
using CustomerManagement.Api.Domain.Security;
using CustomerManagement.Api.Endpoints.Admin;
using CustomerManagement.Api.Endpoints.Auth;
using CustomerManagement.Api.Endpoints.Customers;
using CustomerManagement.Api.Endpoints.Dashboard;
using CustomerManagement.Api.Endpoints.Tickets;
using CustomerManagement.Api.Infrastructure.Attachments;
using CustomerManagement.Api.Infrastructure.Auditing;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<CustomerManagementDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CustomerManagement")));
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<CustomerManagementDbContext>()
    .AddSignInManager();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey must be configured and at least 32 characters long.");
}

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IAuditLogWriter, AuditLogWriter>();
builder.Services.Configure<AttachmentStorageOptions>(
    builder.Configuration.GetSection(AttachmentStorageOptions.SectionName));
builder.Services.AddSingleton<IAttachmentStorage, LocalFileSystemAttachmentStorage>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AgentOnly", policy =>
        policy.RequireAuthenticatedUser()
            .RequireRole(AuthRoles.Agent));

    options.AddPolicy("AdminOnly", policy =>
        policy.RequireAuthenticatedUser()
            .RequireAssertion(context =>
                context.User.IsInRole(AuthRoles.Admin) ||
                context.User.HasClaim("permission", Permissions.UsersManage)));
});
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

var app = builder.Build();

await IdentitySeedData.EnsureSeededAsync(app.Services);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/db", async (CustomerManagementDbContext dbContext) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync();
    return Results.Ok(new { database = canConnect ? "reachable" : "unreachable" });
});

app.MapCustomerProfileEndpoints();
app.MapCustomerNoteEndpoints();
app.MapCustomerAttachmentEndpoints();
app.MapCustomerInteractionHistoryEndpoints();
app.MapTicketCategoryEndpoints();
app.MapTicketPriorityEndpoints();
app.MapTicketEndpoints();
app.MapTicketMessagesEndpoints();
app.MapAgentDashboardEndpoints();
app.MapTicketTasksEndpoints();
app.MapTicketNotesEndpoints();
app.MapQuickRepliesEndpoints();
app.MapAuthEndpoints();
app.MapUsersEndpoints();
app.MapPermissionsEndpoints();
app.MapAuditLogEndpoints();
app.MapSystemSettingsEndpoints();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public partial class Program;
