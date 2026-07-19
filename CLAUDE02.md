# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

A .NET Web API for a location-based service (similar to Google Places / Yelp) — locations with geospatial search, categories, tags, reviews, favorites, and curated collections. Backed by **PostgreSQL + PostGIS** via Npgsql and NetTopologySuite.

**Early stage** — domain entities and data layer exist; controllers, Program.cs, and migrations are not yet scaffolded.

## Build / Run / Tooling

```bash
# Restore and build
dotnet restore
dotnet build

# Add EF migrations (run from solution root)
dotnet ef migrations add <Name> --project Locations.Data --startup-project Locations.WebApi

# Apply migrations
dotnet ef database update --project Locations.Data --startup-project Locations.WebApi

# Generate a SQL script from migrations (for review / DBA approval)
dotnet ef migrations script --project Locations.Data --startup-project Locations.WebApi
```

Required NuGet packages:
- `Npgsql.EntityFrameworkCore.PostgreSQL`
- `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite`
- `NetTopologySuite`

DbContext registration (`Program.cs` pattern — not yet created):
```csharp
builder.Services.AddDbContext<LocationsDbContext>(opt =>
    opt.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        npgsql =>
        {
            npgsql.UseNetTopologySuite();
            npgsql.MigrationsAssembly("Locations.Data");
        }));
builder.Services.AddScoped<LocationService>();
```

## Architecture

### Domain layer (`Entities.cs` — namespace `Locations.Domain.Entities`)

All entities live in a single file for now. The aggregate roots are **User**, **Location**, **Category**, **Tag**, **Review**, **LocationMedia**, and **LocationCollection**.

**Base types:**
- `ISoftDeletable` — contract for `IsDeleted` + `DeletedAtUtc`
- `AuditableEntity` — abstract base implementing `ISoftDeletable`, adds `CreatedAtUtc` / `UpdatedAtUtc` / `Version` (maps to PostgreSQL `xmin` for optimistic concurrency). Every aggregate root inherits from this.
- `Address` — owned type (value object), mapped as columns on the `locations` table via `OwnsOne()`. **Not** a separate table.

**Join entities** (`LocationCategory`, `LocationTag`, `UserFavoriteLocation`, etc.) are **explicit** (not `HasMany().WithMany()`) because each carries its own payload (`IsPrimary`, `Weight`, `Notes`, `CreatedAtUtc`). They do **not** inherit `AuditableEntity` — when a location is deleted, these are physically removed via `ExecuteDeleteAsync`, and PostgreSQL cascade works on them.

**Geo helper** — `Geo.Point(latitude, longitude)` creates an NTS `Point` with SRID 4326. **Critical:** NTS uses `X=longitude, Y=latitude` — always use the helper, never construct `Point` manually.

**Enums:** `LocationStatus` (Draft/Published/Archived), `MediaType` (Image/Video), `CollectionVisibility` (Private/Unlisted/Public). Stored as strings in the DB.

### Data layer (`LocationsDbContext.cs` — namespace `Locations.Data`)

- **PostgreSQL extensions** enabled via `HasPostgresExtension`: `postgis`, `citext`, `pg_trgm`
- **Table names** use `snake_case` (e.g., `"locations"`, `"user_favorite_locations"`)
- **Column names** remain PascalCase by default — quoted in raw SQL and index filters (`"IsDeleted"`, `"AverageRating"`)
- **`citext`** used on `Slug`, `UserName`, `Email`, `Tag.Name` for case-insensitive uniqueness
- **`geography(Point,4326)`** type on `Location.Coordinates` — distances in **meters** (not degrees). Backed by a **GiST index** for `ST_DWithin` and KNN ordering.
- **GIN trigram index** on `Location.Name` for `ILIKE '%search%'` queries using `pg_trgm`
- **Global query filters** for soft delete on all aggregate roots — use `.IgnoreQueryFilters()` to include deleted rows
- **`xmin`** mapped as a concurrency token via `IsRowVersion()` convention loop — read-only, never assign in application code
- **`SaveChangesAsync`** override: converts `EntityState.Deleted` → `EntityState.Modified` for `AuditableEntity` types (soft delete), and auto-sets `CreatedAtUtc`/`UpdatedAtUtc`

### Delete cascade matrix

| FK | Behavior | Reason |
|---|---|---|
| Location → User (Owner) | **Restrict** | Don't orphan content; reassign first |
| Category → Category (parent) | **Restrict** | Avoid cascade cycles |
| Join tables → Location | **Cascade** | Links don't survive without the location |
| LocationCategory → Category | **Restrict** | Protect taxonomy in use |
| LocationTag → User (AddedBy) | **SetNull** | Tag survives user deletion |
| Review → User | **Cascade** | PostgreSQL allows multiple cascade paths |
| Media/Review/OpeningHour → Location | **Cascade** | Composition |

### `LocationService.cs` — service layer

Handles operations that need transactional consistency across multiple tables:

- **`SoftDeleteLocationAsync`** — logical cascade inside a transaction: soft-deletes `LocationMedia` and `Review` (marks `IsDeleted`), physically deletes join/child rows (`OpeningHours`, `UserFavoriteLocations`, `CollectionItems`, `LocationCategories`, `LocationTags`), and decrements `Tag.UsageCount` for removed tags. Uses `ExecuteUpdate`/`ExecuteDelete` — zero materialization, single round-trip per table.
- **`RestoreLocationAsync`** — unmarks `IsDeleted` via `ExecuteUpdate` with `IgnoreQueryFilters()`
- **`NearbyAsync`** — `ST_DWithin` + KNN distance ordering via `IsWithinDistance()` / `Distance()` — returns `List<(Location, double Meters)>`
- **`SearchAsync`** — combined geo + category + tag filter. Tags are AND logic (location must have all specified tags). Uses `AsSplitQuery()` to avoid cartesian explosion from multiple includes.
- **`RefreshRatingAsync`** — recalculates denormalized `AverageRating` / `ReviewCount` via raw SQL `ExecuteSqlAsync`

### Denormalized fields to maintain

- `Location.AverageRating` / `Location.ReviewCount` → call `RefreshRatingAsync` after every review create/edit/delete
- `Tag.UsageCount` → increment/decrement when associating or removing tags from locations

These are candidates for PostgreSQL triggers if preferred over application-level maintenance.

## Key pitfalls

1. **Coordinate order:** NTS uses `(X=longitude, Y=latitude)`. Always use `Geo.Point(lat, lng)` — it swaps them correctly.
2. **Distance units:** `geography(Point,4326)` gives meters. `geometry` would give degrees — don't switch types.
3. **Soft delete disables DB cascade:** `SaveChangesAsync` converts deletes to UPDATEs, so `ON DELETE CASCADE` never fires for `AuditableEntity` types. Always use `LocationService.SoftDeleteLocationAsync` (or equivalent) for aggregate deletion, not raw `Remove()`.
4. **`DateTimeOffset` requires UTC:** Npgsql maps it to `timestamptz` and requires offset zero. Always use `DateTimeOffset.UtcNow`, never `.Now`.
5. **Unique indexes with soft delete:** Some unique indexes use `HasFilter("NOT \"IsDeleted\"")` to allow soft-deleted duplicates (reviews, collection names). `Location.Slug` intentionally omits this filter to prevent slug recycling and protect indexed URLs.
6. **`xmin` is read-only:** It's a PostgreSQL system column. Don't include it in write DTOs.
