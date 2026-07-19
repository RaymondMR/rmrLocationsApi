# Modelo de dominio — PostgreSQL + PostGIS + soft delete

## Paquetes

```bash
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite
dotnet add package NetTopologySuite
```

## Registro en `Program.cs`

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

La extensión PostGIS debe existir en la BD. `HasPostgresExtension("postgis")`
hace que la migración emita `CREATE EXTENSION IF NOT EXISTS postgis`, pero el
rol necesita permisos de superusuario la primera vez.

## Mapa de relaciones

| Relación | Tipo | Implementación |
|---|---|---|
| User → Location | 1 : N | `Location.OwnerId` + `User.OwnedLocations` |
| Location ↔ Category | N : M | `LocationCategory` (`IsPrimary`) |
| Location ↔ Tag | N : M | `LocationTag` (`AddedByUserId`) |
| User ↔ Location | N : M | `UserFavoriteLocation` (favoritos) |
| User ↔ Category | N : M | `UserCategoryInterest` (peso para recomendaciones) |
| User ↔ Tag | N : M | `UserTagFollow` |
| Category → Category | 1 : N | auto-referencia `ParentCategoryId` |
| Location → Media / OpeningHour | 1 : N | composición |
| Location ↔ User | N : M | `Review` (única viva por par) |
| User → Collection ↔ Location | 1 : N + N : M | listas curadas |

Todas las N:M usan **entidad de unión explícita**, no `HasMany().WithMany()`,
porque cada una lleva payload propio (`IsPrimary`, `Weight`, `Notes`,
`CreatedAtUtc`) y así puedes consultarlas e indexarlas directamente.

## Matriz de borrado (FK físicas)

| FK | Comportamiento | Razón |
|---|---|---|
| Location → User (Owner) | **Restrict** | No dejar contenido huérfano; reasigna primero |
| Category → Category (padre) | **Restrict** | Evita ciclo de cascada |
| LocationCategory → Location | **Cascade** | El vínculo no existe sin la ubicación |
| LocationCategory → Category | **Restrict** | Protege la taxonomía en uso |
| LocationTag → Location / Tag | **Cascade** | Los tags se despegan solos |
| LocationTag → User (AddedBy) | **SetNull** | El tag sobrevive al usuario |
| UserFavoriteLocation, UserCategoryInterest, UserTagFollow | **Cascade** | Vínculo puro |
| LocationMedia / Review / OpeningHour → Location | **Cascade** | Composición |
| LocationMedia → User (Uploader) | **SetNull** | Metadato opcional |
| Review → User | **Cascade** | PostgreSQL admite rutas múltiples de cascada |
| Collection → User, CollectionItem → Collection/Location | **Cascade** | Composición / vínculo |

En SQL Server, `Review → User` y `CollectionItem → Location` tendrían que ser
`Restrict` (error 1785 por rutas múltiples). PostgreSQL no tiene esa
restricción, por eso quedaron en `Cascade`.

## El punto delicado: soft delete anula la cascada de la BD

`SaveChangesAsync` convierte cualquier `DELETE` de una `AuditableEntity` en un
`UPDATE`. Consecuencia directa: **las FK con `ON DELETE CASCADE` nunca se
disparan** para esas entidades, porque nunca se ejecuta un `DELETE` real.

Por eso existe `LocationService.SoftDeleteLocationAsync`, que hace la cascada
lógica explícita dentro de una transacción:

- **Se marcan** (soft): `LocationMedia`, `Review`.
- **Se borran** (físico): `OpeningHours`, `UserFavoriteLocations`,
  `CollectionItems`, `LocationCategories`, `LocationTags`.

Las tablas de unión **no** heredan de `AuditableEntity` a propósito: ahí sí
quieres borrado real, y así la cascada de PostgreSQL sí funciona sobre ellas.

Si necesitas los borrados en una consulta: `.IgnoreQueryFilters()`.

## Índices únicos y soft delete

Un índice único normal seguiría bloqueando el slug de un registro borrado.
Donde importa, los índices llevan filtro `NOT "IsDeleted"` (reseña única por
par, nombre de colección, portada). En `Location.Slug` lo dejé **sin** filtro a
propósito: así no puedes reciclar el slug de una ubicación borrada y romper
URLs indexadas. Si prefieres reciclarlos, agrega el mismo filtro.

## Notas específicas de Npgsql

- **`geography(Point,4326)`**: `Distance` e `IsWithinDistance` trabajan en
  **metros**. Si usaras `geometry`, serían grados y tendrías que envolver todo
  en transformaciones. El índice **GiST** es obligatorio para que `ST_DWithin`
  y el ordenamiento KNN (`<->`) no hagan seq scan.
- **Orden de coordenadas**: en NTS, `X = longitud`, `Y = latitud`. Es el error
  más común; por eso está el helper `Geo.Point(lat, lng)`.
- **`xmin`** como token de concurrencia: es de solo lectura y lo genera
  PostgreSQL. No lo incluyas en DTOs de escritura.
- **`DateTimeOffset`** se mapea a `timestamptz` y Npgsql exige offset cero —
  `DateTimeOffset.UtcNow` cumple. Nunca uses `DateTimeOffset.Now`.
- **`citext`** en slugs, email y nombre de tag: unicidad case-insensitive sin
  columnas normalizadas extra.
- **`pg_trgm` + índice GIN** sobre `Location.Name` para búsqueda por
  coincidencia parcial (`ILIKE '%texto%'`) con índice.
- Los nombres de tabla van en `snake_case` pero las columnas quedan en PascalCase
  (por eso los filtros de índice usan comillas: `"IsDeleted"`). Si prefieres
  todo uniforme, agrega el paquete `EFCore.NamingConventions` y
  `.UseSnakeCaseNamingConvention()` — pero entonces hay que reescribir los
  filtros de índice y el SQL crudo de `RefreshRatingAsync`.

## Denormalizados que hay que mantener

- `Location.AverageRating` / `ReviewCount` → `RefreshRatingAsync` tras cada alta,
  edición o baja de reseña.
- `Tag.UsageCount` → al asociar/desasociar tags.

Si prefieres no acarrear esto en el código de aplicación, son buenos candidatos
para triggers en PostgreSQL.

## Entidades futuras

`Country`/`City` normalizadas, `CheckIn` (historial de visitas), `AuditLog`,
`LocationEditSuggestion` (moderación colaborativa).
