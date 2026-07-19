# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
dotnet restore
dotnet build
dotnet run
```

The API starts at `http://localhost:5113` (HTTPS) by default. Swagger UI is available at `/swagger` in Development mode. The project is `rmrLocationsApi.csproj`, an ASP.NET Core 8.0 Web API targeting `net8.0`.

## Database / Migrations

Uses EF Core 8.0 with PostgreSQL (via `Npgsql.EntityFrameworkCore.PostgreSQL`). The connection string in `appsettings.json` points to `localhost:5432` with database `rmrLocationsApi`. Local PostgreSQL uses `trust` auth for localhost connections.

```bash
# Add a new migration
dotnet ef migrations add <MigrationName>

# Update database
dotnet ef database update
```

On startup, `DbSeeder.SeedAdminUser` runs to ensure an admin user exists (username: `admin`, password: `Admin123!`).

## Architecture

**Layered approach:** Controllers → Services → EF Core DbContext → PostgreSQL.

### Controllers
- `AuthController` (`/api/auth`) — register, login, logout, change-password, refresh-token. Some endpoints require `[Authorize]`.
- `ProductsController` (`/api/products`) — full CRUD + JSON Patch. All actions require `[Authorize]`; write operations (POST/PUT/DELETE/PATCH) require `Admin` role.

### Services (registered as scoped in DI)
- `IAuthService` / `AuthService` — JWT access token (1-hour expiry, HMAC-SHA512) + refresh token (7-day expiry, stored in DB on the User record). Uses `Microsoft.AspNetCore.Identity.PasswordHasher<User>` for password hashing. On password change, all refresh tokens are invalidated.
- `IProductService` / `ProductService` — standard CRUD with concurrency exception handling on update.

### Models / Data
- `Producto` — `Id` (int PK), `Nombre`, `Precio` (decimal with precision 18,2), `Stock`.
- `User` — `Id` (Guid PK), `Username`, `PasswordHash`, `Roles` (List<string>), `RefreshToken`, `RefreshTokenExpiryTime`.

### Roles (defined in `UserRoles` static class)
- `Admin`, `User`, `UserL1`, `UserL2`, `UserL3` — roles stored as `List<string>` directly on the User entity (no separate roles table).

### Auth Flow
JWT settings are in `appsettings.json` → `AppSettings` section, bound to `JwtSettings` via `IOptions<T>`. Tokens carry the user's `sub` (id), `name`, `jti`, and role claims. Refresh tokens are opaque random bytes (Base64) stored in the DB and validated by userId + token value + expiry.

### Key Infrastructure
- **GlobalExceptionHandler** — implements `IExceptionHandler`, returns a generic 500 ProblemDetails response for unhandled exceptions.
- **CORS** — `MyCorsPolicy` allows origins `localhost:3000`, `localhost:5113`, `localhost:5173` (HTTP and HTTPS for Vite).
- **Newtonsoft.Json** — controllers use `AddNewtonsoftJson()` for JSON serialization (not System.Text.Json).
- **Swagger** — configured with JWT Bearer security definition for testing authenticated endpoints.

## Project Conventions

- **Primary constructors** are used consistently in services and controllers.
- **Records** for DTOs where appropriate (e.g., `UserDto` is a `record`).
- Services return `null` or `false` for failure cases; controllers translate those to appropriate HTTP status codes (401, 400, 404).
- `AsNoTracking()` used on all read-only EF queries.
- There is an empty `Tests\` folder — no test project or tests exist yet.

## dotnet new Template

This project doubles as a `dotnet new` template via `.template.config/template.json`.

```bash
# Install the template
dotnet new install .

# Create a new project
dotnet new rmrlocapi -n MyProject -o ./MyProject

# Available options
dotnet new rmrlocapi -n MyProject --Framework net9.0   # Target framework
dotnet new rmrlocapi -n MyProject --EnableAuth false    # Exclude JWT auth
dotnet new rmrlocapi -n MyProject --EnableSwagger false # Flag for Swagger exclusion (future)
```

**Template parameters:** `Framework` (choice: net8.0/net9.0/net10.0, replaces in .csproj), `EnableAuth` (bool, excludes 12 auth-related files when false), `EnableSwagger` (bool, reserved for future `#if` conditional compilation).
