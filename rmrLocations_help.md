# 📖 rmrLocationsApi — Documentación y Referencia

## Información General

| Campo | Valor |
|-------|-------|
| **Proyecto** | rmrLocationsApi |
| **Framework** | .NET 8.0 Web API |
| **Base de datos** | PostgreSQL 16 + PostGIS |
| **Autenticación** | JWT (HMAC-SHA512) + Refresh Tokens |
| **ORM** | Entity Framework Core 8.0 |
| **Serialización** | Newtonsoft.Json |
| **Geoespacial** | NetTopologySuite + PostGIS |

---

## Acceso a la API

### URL Base

```
https://92.118.59.247/rmrlocations/api
```

> Cuando se configure DNS: `https://api.rmrlocations.com/api`

### Postman Setup

1. URL base: `https://92.118.59.247/rmrlocations`
2. **SSL:** Settings → General → SSL certificate verification: OFF (solo para IP)
3. **Auth:** Obtener token en `/api/auth/login` y usar `Bearer {token}` en Authorization header

---

## 🔐 Autenticación

Todos los endpoints (excepto login y register) requieren header:
```
Authorization: Bearer {accessToken}
```

### Endpoints de Auth

#### POST `/api/auth/register`
Registrar nuevo usuario.

```json
{
    "userName": "nuevo_usuario",
    "email": "usuario@email.com",
    "password": "MiPassword123!"
}
```

#### POST `/api/auth/login`
Iniciar sesión. Retorna `accessToken` y `refreshToken`.

```json
{
    "userName": "admin",
    "email": "admin@rmrlocations.com",
    "password": "Admin123!"
}
```

**Respuesta:**
```json
{
    "accessToken": "eyJhbGciOiJIUzUxMiIs...",
    "refreshToken": "abc123..."
}
```

#### POST `/api/auth/logout`
Cerrar sesión (invalida el refresh token).  
🔒 Requiere autenticación.

#### POST `/api/auth/change-password`
Cambiar contraseña.  
🔒 Requiere autenticación.

```json
{
    "currentPassword": "Admin123!",
    "newPassword": "NuevaPassword456!"
}
```

#### POST `/api/auth/refresh-token`
Renovar `accessToken` usando el `refreshToken`.

```json
{
    "refreshToken": "abc123..."
}
```

#### GET `/api/auth/authenticated-only`
Endpoint de prueba — solo usuarios autenticados.  
🔒 Requiere autenticación.

#### GET `/api/auth/admin-only`
Endpoint de prueba — solo rol `Admin`.  
🔒 Requiere rol Admin.

---

## 📍 Locations

Base: `/api/locations`

Todos los endpoints requieren autenticación.  
Operaciones de escritura (POST/PUT/DELETE) requieren rol `Admin`.

### GET `/api/locations`
Obtener todas las ubicaciones.

### GET `/api/locations/{id}`
Obtener ubicación por ID (GUID).

### POST `/api/locations`
Crear nueva ubicación.

```json
{
    "name": "Mi Restaurante",
    "description": "Comida mexicana auténtica",
    "slug": "mi-restaurante",
    "address": {
        "street": "Av. Reforma 123",
        "city": "CDMX",
        "state": "CDMX",
        "postalCode": "06600",
        "country": "MX"
    },
    "coordinates": {
        "type": "Point",
        "coordinates": [-99.1332, 19.4326]
    },
    "status": "Published"
}
```

### PUT `/api/locations/{id}`
Actualizar ubicación existente.

### DELETE `/api/locations/{id}`
Eliminar ubicación (hard delete).

### GET `/api/locations/nearby`
Búsqueda geoespacial por proximidad.

| Parámetro | Tipo | Descripción |
|-----------|------|-------------|
| `lat` | double | Latitud del punto central |
| `lng` | double | Longitud del punto central |
| `radiusKm` | double | Radio de búsqueda en kilómetros |
| `limit` | int? | Máximo de resultados (default: 20) |

Ejemplo: `GET /api/locations/nearby?lat=19.4326&lng=-99.1332&radiusKm=5`

### GET `/api/locations/search`
Búsqueda combinada por nombre, categoría y tags.

| Parámetro | Tipo | Descripción |
|-----------|------|-------------|
| `name` | string? | Búsqueda por nombre (ILIKE) |
| `categoryId` | Guid? | Filtrar por categoría |
| `tagIds` | Guid[]? | Filtrar por tags (AND lógico) |

Ejemplo: `GET /api/locations/search?name=restaurante&categoryId=abc-123`

### POST `/api/locations/{id}/soft-delete`
Soft delete de ubicación (marca `IsDeleted`). Elimina en cascada lógica: media, reviews, opening hours, join tables.  
🔒 Requiere rol Admin.

### POST `/api/locations/{id}/restore`
Restaurar ubicación eliminada (quita `IsDeleted`).  
🔒 Requiere rol Admin.

---

## 📂 Categories

Base: `/api/categories`

🔒 Requiere autenticación.  
Escritura requiere rol Admin.

### GET `/api/categories`
Listar todas las categorías.

### GET `/api/categories/{id}`
Obtener categoría por ID (GUID).

### POST `/api/categories`
Crear categoría.

```json
{
    "name": "Restaurantes",
    "slug": "restaurantes",
    "description": "Lugares para comer",
    "icon": "restaurant"
}
```

### PUT `/api/categories/{id}`
Actualizar categoría.

### DELETE `/api/categories/{id}`
Eliminar categoría.

---

## 🏷️ Tags

Base: `/api/tags`

🔒 Requiere autenticación.  
Escritura requiere rol Admin.

### GET `/api/tags`
Listar todos los tags.

### GET `/api/tags/{id}`
Obtener tag por ID (GUID).

### POST `/api/tags`
Crear tag.

```json
{
    "name": "wifi-gratis",
    "description": "Tiene WiFi gratis"
}
```

### PUT `/api/tags/{id}`
Actualizar tag.

### DELETE `/api/tags/{id}`
Eliminar tag.

---

## ⭐ Reviews

Base: `/api/reviews`

🔒 Requiere autenticación.

### GET `/api/reviews/location/{locationId}`
Obtener reseñas de una ubicación.

### GET `/api/reviews/{id}`
Obtener reseña por ID.

### POST `/api/reviews`
Crear reseña.

```json
{
    "locationId": "guid-de-la-ubicacion",
    "userId": "guid-del-usuario",
    "rating": 4,
    "comment": "¡Excelente lugar!",
    "title": "Muy recomendado"
}
```

### PUT `/api/reviews/{id}`
Actualizar reseña.

### DELETE `/api/reviews/{id}`
Eliminar reseña.

---

## 🖼️ Media

Base: `/api/media`

🔒 Requiere autenticación.  
Escritura requiere rol Admin.

### GET `/api/media/location/{locationId}`
Obtener media de una ubicación (fotos, videos).

### GET `/api/media/{id}`
Obtener media por ID.

### POST `/api/media`
Agregar media a una ubicación.

```json
{
    "locationId": "guid-de-la-ubicacion",
    "url": "https://ejemplo.com/foto.jpg",
    "type": "Image",
    "caption": "Fachada del local",
    "sortOrder": 1
}
```

### PUT `/api/media/{id}`
Actualizar metadata de media.

### DELETE `/api/media/{id}`
Eliminar media.

---

## 📋 Collections

Base: `/api/collections`

🔒 Requiere autenticación.

### GET `/api/collections/user/{userId}`
Obtener colecciones de un usuario.

### GET `/api/collections/{id}`
Obtener colección por ID.

### POST `/api/collections`
Crear colección.

```json
{
    "name": "Mis Favoritos",
    "description": "Lugares que más me gustan",
    "userId": "guid-del-usuario",
    "visibility": "Public"
}
```

### PUT `/api/collections/{id}`
Actualizar colección.

### DELETE `/api/collections/{id}`
Eliminar colección.

### POST `/api/collections/{collectionId}/items`
Agregar ubicación a colección.

```json
{
    "locationId": "guid-de-la-ubicacion",
    "notes": "Visitar en fin de semana"
}
```

### DELETE `/api/collections/{collectionId}/items/{locationId}`
Quitar ubicación de colección.

---

## 👥 Roles de Usuario

| Rol | Permisos |
|-----|----------|
| `Admin` | Acceso total: CRUD en todas las entidades |
| `User` | Lectura, crear reseñas y colecciones |
| `UserL1` | Nivel 1 |
| `UserL2` | Nivel 2 |
| `UserL3` | Nivel 3 |

---

## 🗄️ Estructura de la Base de Datos

### Entidades Principales (Aggregate Roots)

| Entidad | Tabla | Soft Delete |
|---------|-------|-------------|
| `User` | `users` | No |
| `Location` | `locations` | ✅ Sí |
| `Category` | `categories` | No |
| `Tag` | `tags` | No |
| `Review` | `reviews` | ✅ Sí (cascada con Location) |
| `LocationMedia` | `location_media` | ✅ Sí (cascada con Location) |
| `LocationCollection` | `location_collections` | No |

### Entidades de Join

| Entidad | Tabla | Descripción |
|---------|-------|-------------|
| `LocationCategory` | `location_categories` | Ubicación ↔ Categoría (+ `IsPrimary`) |
| `LocationTag` | `location_tags` | Ubicación ↔ Tag (+ `AddedByUserId`) |
| `UserFavoriteLocation` | `user_favorite_locations` | Favoritos de usuario |
| `CollectionItem` | `collection_items` | Items en colección (+ `Notes`) |
| `UserCategoryInterest` | `user_category_interests` | Intereses del usuario |
| `UserTagFollow` | `user_tag_follows` | Tags seguidos por usuario |

### Tipos de Valor / Owned Types

| Tipo | Pertenece a | Mapeo |
|------|-------------|-------|
| `Address` | `Location` | Columnas en tabla `locations` (OwnsOne) |
| `OpeningHour` | `Location` | Tabla separada `opening_hours` |

### Campos Geoespaciales

- `Location.Coordinates` → `geography(Point, 4326)` — Índice GiST
- `Location.Name` → Índice GIN trigram (`pg_trgm`) para búsqueda ILIKE

### Campos Denormalizados

- `Location.AverageRating` / `Location.ReviewCount` → Actualizar via `RefreshRatingAsync()`
- `Tag.UsageCount` → Incrementar/decrementar al asociar tags

---

## 🛠️ Guía para Modificaciones

### Agregar un nuevo endpoint

1. **DTO** (si necesita) → `DTOs/`
2. **Interfaz del servicio** → `Services/INuevoService.cs`
3. **Implementación** → `Services/NuevoService.cs`
4. **Controlador** → `Controllers/NuevoController.cs`
5. **Registrar en DI** → `Program.cs` → `builder.Services.AddScoped<INuevoService, NuevoService>()`

### Agregar un campo a una entidad

1. Editar el modelo en `Models/`
2. Si es campo calculado/denormalizado, actualizar el servicio correspondiente
3. Crear migración:
   ```bash
   dotnet ef migrations add AddNuevoCampo
   ```
4. El pipeline despliega automáticamente — la migración se aplica en startup (`Database.MigrateAsync()`)

### Modificar lógica de negocio

1. Ubicar el servicio en `Services/`
2. Hacer cambios respetando los patrones existentes:
   - `AsNoTracking()` en queries de solo lectura
   - Manejo de concurrencia con `xmin` (campo `Version`)
   - Soft delete via `ExecuteUpdate`/`ExecuteDelete` para cascada
3. Probar localmente:
   ```bash
   dotnet build
   dotnet run
   ```
4. Commit + push → CI/CD despliega automáticamente

### Reglas importantes

1. **Coordenadas:** NTS usa `(X=longitud, Y=latitud)`. Siempre usar el helper `Geo.Point(lat, lng)` — invierte los parámetros.
2. **Distancias:** `geography(Point,4326)` retorna metros. No usar `geometry` (retorna grados).
3. **Soft Delete:** No usar `Remove()` directo en entidades `AuditableEntity`. Usar los métodos del servicio.
4. **DateTimeOffset:** Solo `DateTimeOffset.UtcNow`, nunca `.Now`. Npgsql requiere offset cero.
5. **Índices únicos con soft delete:** Algunos índices usan filtro `NOT "IsDeleted"` para permitir duplicados de eliminados.
6. **`xmin`:** Es read-only (columna de sistema PostgreSQL). No incluir en DTOs de escritura.

---

## 🚀 CI/CD

Cada `git push` a `master` dispara:

1. **GitHub Actions** construye la imagen Docker (.NET 8 SDK → ASP.NET runtime)
2. **Push** a `ghcr.io/raymondmr/rmrlocationsapi:latest`
3. **SSH** al VPS → `docker compose pull api && docker compose up -d`
4. La API se reinicia con cero downtime (~5 segundos)

### Comandos útiles para desarrollo

```bash
# Local
dotnet run
dotnet ef migrations add <Nombre>
dotnet ef database update

# Ver logs en VPS
ssh deploy@92.118.59.247
cd /home/apps/rmrLocationsApi
sudo docker compose logs -f api

# Verificar estado del workflow
gh run list --repo RaymondMR/rmrLocationsApi
gh run watch
```

### Estado del último deploy

```bash
ssh deploy@92.118.59.247
cd /home/apps/rmrLocationsApi
sudo docker compose ps
```

---

## 🔑 Credenciales por Defecto (Desarrollo)

| Campo | Valor |
|-------|-------|
| **Admin user** | `admin` |
| **Admin email** | `admin@rmrlocations.com` |
| **Admin password** | `Admin123!` |

> ⚠️ **Cambiar en producción.** El admin se crea automáticamente en el seed inicial solo si no existe.

---

## 📦 Estructura del Proyecto

```
rmrLocationsApi/
├── Controllers/          # 7 controladores (Auth, Location, Category,
│                         #   Tag, Review, Media, Collection)
├── Converters/           # PointJsonConverter (NTS ↔ JSON)
├── Data/                 # AppDbContext (EF Core + PostGIS)
├── DTOs/                 # Records: UserDto, TokenResponseDto, etc.
├── Handlers/             # GlobalExceptionHandler
├── Migrations/           # Migraciones EF Core
├── Models/               # Entidades de dominio + enums
├── Services/             # 7 servicios con sus interfaces
├── Settings/             # JwtSettings (IOptions<T>)
├── Dockerfile            # Multi-stage build
├── docker-compose.yml    # Orquestación
├── .github/workflows/    # CI/CD
└── Program.cs            # Entry point + DI + middleware
```
