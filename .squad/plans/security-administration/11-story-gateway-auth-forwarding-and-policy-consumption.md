# Story 11 - Gateway Auth Forwarding And Policy Consumption

## Prerequisites

- Story 06 completed: `06-story-identity-auth-foundation.md`.
- Story 08 completed: `08-story-permission-model-and-policy-administration.md`.
- Story 10 completed: `10-story-system-settings-api-and-policy-enforcement.md`.

---

## Story Goal

Integrate gateway authentication with JWT bearer tokens and ensure downstream services receive identity/claims context without placeholder header-based trust.

---

## Context - Read These Files First

1. `src/CustomerManagement.Gateway/Program.cs` - read ~lines 1-37 for middleware ordering, Ocelot setup, and CORS pipeline.
2. `src/CustomerManagement.Gateway/ocelot.json` - read ~lines 1-137 for existing customer route mapping and forwarded header usage.
3. `src/CustomerManagement.Api/Program.cs` - read ~lines 19-29 for current authorization policies.
4. `src/CustomerManagement.Api/Infrastructure/Auth/HeaderAuthenticationHandler.cs` - read ~lines 14-37. This is the placeholder mechanism to retire from gateway-facing requests.
5. `tests/CustomerManagement.Gateway.Tests/CustomerManagementGatewayRoutingTests.cs` - read ~lines 25-123 for routing and error propagation regression coverage.
6. `src/CustomerManagement.Gateway/ROUTES.md` - read route contract to extend with auth requirements.

---

## Backend Tasks

### 1. Add gateway JWT authentication configuration

File: `src/CustomerManagement.Gateway/Program.cs`

- Register JWT bearer authentication for gateway endpoints.
- Validate issuer, audience, signature against shared auth settings.
- Keep CORS before Ocelot in middleware order.

File: `src/CustomerManagement.Gateway/ocelot.json`

- For protected routes, add Ocelot `AuthenticationOptions` with JWT scheme.
- Remove trust in arbitrary `X-User-Role` from public clients.

### 2. Define claim forwarding contract

File: `src/CustomerManagement.Gateway/ocelot.json`

- Forward identity metadata headers for downstream observability only:
  - `X-User-Id`
  - `X-User-Email`
- Do not forward or accept role from client headers.

File: `src/CustomerManagement.Gateway/ROUTES.md`

- Document required `Authorization: Bearer <token>` for protected routes.
- Document auth-free routes (`/api/auth/register`, `/api/auth/login`, `/api/auth/refresh`).

### 3. Align downstream API policy consumption

File: `src/CustomerManagement.Api/Program.cs`

- Ensure permission and role policies consume claims from JWT, not header auth.
- Keep fallback compatibility disabled for production routes.

### 4. Add gateway auth tests

File: `tests/CustomerManagement.Gateway.Tests/CustomerManagementGatewayRoutingTests.cs`

- Add tests for:
  - missing bearer token -> 401
  - invalid token -> 401
  - valid token with insufficient permission -> 403
  - valid token with permission -> downstream success

Create file: `tests/CustomerManagement.Gateway.Tests/Infrastructure/TestJwtFactory.cs`

- Create signed test JWTs for role/permission combinations.

---

## Edge Cases & Failure Modes

- Gateway accepts malformed bearer token: must return 401 without forwarding downstream.
- Expired token used through gateway: 401 with WWW-Authenticate challenge.
- JWT valid but missing permission claim: downstream returns 403; gateway preserves response.
- CORS preflight for protected route: returns 204 with CORS headers and no auth challenge.

---

## Test Plan

1. Extend gateway integration tests with token/no-token variants.
2. Add API integration test asserting header auth is no longer accepted on protected routes.
3. Re-run existing customer-management gateway routing tests to ensure no route regressions.

---

## Verification Steps

1. **Backend builds:** `dotnet build src/CustomerManagement.Gateway/CustomerManagement.Gateway.csproj /p:UseAppHost=false` and `dotnet build src/CustomerManagement.Api/CustomerManagement.Api.csproj /p:UseAppHost=false`.
2. **Backend tests:** `dotnet test tests/CustomerManagement.Gateway.Tests/CustomerManagement.Gateway.Tests.csproj /p:UseAppHost=false`.
3. **Regression:** `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj /p:UseAppHost=false`.

---

## Done Criteria

- [x] Gateway enforces JWT authentication on protected routes.
- [x] Client-supplied role headers are no longer trusted as auth source.
- [x] Downstream services authorize using JWT role/permission claims.
- [x] Gateway tests cover unauthorized/forbidden/success token paths.
- [x] Route contract docs include authentication expectations.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 12.**
