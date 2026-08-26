using Microsoft.AspNetCore.Authorization;

namespace CustomerManagement.Api.Infrastructure.Auth;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
