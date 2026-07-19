# Frontend.md — Prompt Completo para Claude Design

## 🎯 Objetivo

Crear un frontend completo con **ReactJS + TailwindCSS** que consuma la API de **rmrLocationsApi**, permitiendo gestionar todas las operaciones CRUD de cada entidad, búsquedas geoespaciales, autenticación JWT, y una homepage atractiva. **Todas las páginas deben ser 100% responsivas** (mobile-first, tablet, desktop).

---

## 🏗️ Stack Tecnológico

| Capa | Tecnología |
|---|---|
| Framework | React 19 + Vite |
| Lenguaje | TypeScript (estricto) |
| Estilos | TailwindCSS 4 |
| Ruteo | React Router v7 |
| Estado global | Zustand (auth store + data stores) |
| Formularios | React Hook Form + Zod (validación) |
| HTTP Client | Axios (con interceptors para JWT) |
| Mapas | Leaflet / MapLibre GL JS (para búsqueda geoespacial) |
| Íconos | Lucide React |
| Notificaciones | Sonner (toast) |
| UI Components | shadcn/ui (basado en Radix + Tailwind) |

---

## 🔐 Autenticación y Manejo de Tokens

### Flujo de Auth

1. El usuario se **registra** (`POST /api/auth/register`) o inicia sesión (`POST /api/auth/login`).
2. La API responde con `accessToken` (JWT, expira 1 hora) y `refreshToken` (opaco, 7 días).
3. El `accessToken` se guarda en memoria (Zustand store) y se envía como header `Authorization: Bearer {token}`.
4. El `refreshToken` se guarda en `localStorage`.
5. Un **Axios interceptor** detecta errores 401 y automáticamente intenta renovar el token con `POST /api/auth/refresh-token`. Si falla, redirige al login.
6. Al hacer logout, se llama `POST /api/auth/logout` y se limpia el estado.

### Endpoints de Auth

```
POST /api/auth/register
  Body: { userName: string, email: string, password: string }
  Response 200: { id: Guid, userName: string, email: string, roles: string[] }
  Error 400: "Username already exists."

POST /api/auth/login
  Body: { userName: string, email: string, password: string }
  Response 200: { accessToken: string, refreshToken: string }
  Error 401: "Invalid credentials."

POST /api/auth/logout
  Header: Authorization: Bearer {accessToken}
  Response 200: "Logged out successfully."

POST /api/auth/change-password
  Header: Authorization: Bearer {accessToken}
  Body: { currentPassword: string, newPassword: string }
  Response 200: "Password updated successfully."
  Error 400: "Invalid current password or user not found."

POST /api/auth/refresh-token
  Body: { userId: Guid, refreshToken: string }
  Response 200: { accessToken: string, refreshToken: string }
  Error 401: "Invalid refresh token."

GET /api/auth
  Header: Authorization: Bearer {accessToken}
  Response 200: "You are authenticated!"
  Error 401: Unauthorized

GET /api/auth/admin-only
  Header: Authorization: Bearer {accessToken}  (requiere rol "Admin")
  Response 200: "You are an admin!"
  Error 401/403
```

### Páginas de Auth
- **Login**: formulario con userName + password, link a registro
- **Register**: formulario con userName + email + password + confirmar password
- **ChangePassword**: Modal/página con currentPassword + newPassword + confirmar

---

## 🏠 Homepage (Ruta: `/`)

Landing page pública con:

1. **Hero section**: Título "Descubre los Mejores Lugares", subtítulo, barra de búsqueda (texto + filtro de ubicación), botón CTA "Explorar"
2. **Categorías destacadas** — grid de tarjetas con iconos (usando `GET /api/category`)
3. **Lugares mejor valorados** — grid de cards (usando `GET /api/location` con `averageRating` descendente)
4. **Sección "Cómo funciona"** — 3 pasos (Buscar → Descubrir → Visitar/Reseñar)
5. **Footer** con links, about, contacto

---

## 📍 Locations (Rutas: `/locations`, `/locations/:id`, `/locations/new`, `/locations/:id/edit`)

Es la entidad central. Requiere el converter `PointJsonConverter` del lado del servidor. Las coordenadas se envían/reciben en formato **GeoJSON**:
```json
{ "type": "Point", "coordinates": [longitude, latitude] }
```

### Endpoints

```
GET /api/location
  Response 200: Location[]
  Nota: Incluye LocationCategories[].Category, LocationTags[].Tag, Media[], OpeningHours[], Reviews[]

GET /api/location/{id:guid}
  Response 200: Location (con todos los includes: categorías, tags, media, reviews, horarios)
  Error 404

POST /api/location
  Body: Location (ver modelo abajo)
  Response 201: Location creado (con ID generado)
  Error 400: Validación fallida

PUT /api/location/{id:guid}
  Body: Location (debe incluir id, ownerId, averageRating, reviewCount)
  Response 204: No Content
  Error 404/400

DELETE /api/location/{id:guid}
  Response 204: No Content (soft delete vía SaveChanges)
  Error 404

POST /api/location/{id:guid}/soft-delete
  Response 204: Soft delete con cascada lógica (marca Location, Media y Reviews como IsDeleted; elimina físicamente OpeningHours, UserFavoriteLocations, CollectionItems, LocationCategories, LocationTags; decrementa Tag.UsageCount)
  Error 404

POST /api/location/{id:guid}/restore
  Response 204: Restaura IsDeleted = false
  Error 404

GET /api/location/nearby?latitude={lat}&longitude={lng}&radiusMeters={meters}&take={n}
  Response 200: [ { location: Location, distanceMeters: number } ]
  Nota: Solo retorna locations con Status=Published e IsPublic=true

GET /api/location/search?latitude={lat}&longitude={lng}&radiusMeters={meters}&categoryIds={guid,guid}&tagIds={guid,guid}
  Response 200: Location[]
  Nota: categoryIds y tagIds son strings separados por coma. Tags usan lógica AND.
```

### Modelo Location

```typescript
interface Location {
  id: string;                        // Guid
  name: string;                      // max 200, required
  slug: string;                      // max 220, citext, unique, required
  description?: string;              // max 4000
  coordinates: GeoJSONPoint;         // geography(Point,4326)
  address: Address;                  // owned type
  status: LocationStatus;            // "Draft" | "Published" | "Archived"
  isPublic: boolean;
  websiteUrl?: string;               // max 2048
  phoneNumber?: string;              // max 32
  averageRating: number;             // 0..5, denormalized
  reviewCount: number;               // denormalized
  ownerId: string;                   // FK → User
  owner?: User;                       // navigation
  locationCategories?: LocationCategory[];
  locationTags?: LocationTag[];
  favoritedBy?: UserFavoriteLocation[];
  collectionItems?: CollectionItem[];
  media?: LocationMedia[];
  reviews?: Review[];
  openingHours?: OpeningHour[];
  // Read-only (computed, not persisted):
  latitude: number;                  // coordinates.Y
  longitude: number;                 // coordinates.X
  // Metadata:
  createdAtUtc: string;              // DateTimeOffset
  updatedAtUtc?: string;
  isDeleted: boolean;
  deletedAtUtc?: string;
  version: number;                   // xmin concurrency token (read-only, no incluir en writes)
}

interface GeoJSONPoint {
  type: "Point";
  coordinates: [number, number];     // [longitude, latitude]
}

interface Address {
  name?: string;                     // nombre descriptivo
  street?: string;
  exteriorNumber?: string;
  interiorNumber?: string;
  neighborhood?: string;
  city?: string;
  state?: string;
  postalCode?: string;
  countryCode?: string;              // ISO 3166-1 alpha-2 (2 chars)
}

enum LocationStatus {
  Draft = "Draft",
  Published = "Published",
  Archived = "Archived"
}
```

### Páginas de Location

- **Lista** (`/locations`): Grid responsivo de cards con imagen de portada, nombre, rating, categorías, distancia. Filtros: búsqueda por texto, categoría, tags, ordenar por rating/distancia/nombre. Paginación.
- **Detalle** (`/locations/:id`): Imagen principal + carrusel de media, nombre, rating, reviews, dirección en mapa (Leaflet), horarios, tags, categorías, reviews con paginación.
- **Crear/Editar** (`/locations/new`, `/locations/:id/edit`): Formulario con pestañas:
  - Info básica (nombre, slug autogenerado, descripción, teléfono, website)
  - Dirección (componente AddressFields)
  - Ubicación en mapa (pick coordinates con Leaflet)
  - Categorías (multi-select)
  - Tags (multi-select con autocompletar)
  - Horarios (lista dinámica de OpeningHour)
  - Estado (Draft/Published/Archived)
  - Visibilidad (isPublic toggle)

---

## 📂 Categories (Rutas: `/categories`, `/categories/:id`)

Jerarquía padre-hijo (self-referencing). Usar `ParentCategoryId` para árbol.

### Endpoints

```
GET /api/category
  Response 200: Category[] (incluye Children[], ParentCategory, LocationCategories[])

GET /api/category/{id:guid}
  Response 200: Category (con includes)
  Error 404

POST /api/category
  Body: { name, slug, description?, iconName?, colorHex?, sortOrder, parentCategoryId? }
  Response 201: Category

PUT /api/category/{id:guid}
  Body: Category (todas las props)
  Response 204

DELETE /api/category/{id:guid}
  Response 204
  Error 404 (o 500 si tiene Locations asociadas — Restrict)
```

### Modelo Category

```typescript
interface Category {
  id: string;
  name: string;                      // max 120, required
  slug: string;                      // max 140, citext, unique
  description?: string;              // max 1000
  iconName?: string;                 // max 64 (nombre de ícono Lucide)
  colorHex?: string;                 // "#RRGGBB"
  sortOrder: number;
  parentCategoryId?: string;         // FK auto-referencia
  parentCategory?: Category;
  children?: Category[];
  locationCategories?: LocationCategory[];
  interestedUsers?: UserCategoryInterest[];
  createdAtUtc: string;
  updatedAtUtc?: string;
  isDeleted: boolean;
}
```

### Páginas de Category

- **Lista** (`/categories`): Vista en árbol o grid con iconos y colores. Botón para crear categoría.
- **Detalle** (`/categories/:id`): Info + subcategorías + locations asociadas.
- **Crear/Editar** (modal o página): Formulario con nombre, slug, descripción, selector de ícono, color picker, parent category dropdown.

---

## 🏷️ Tags (Rutas: `/tags`, `/tags/:id`)

Sistema de etiquetas con `UsageCount` denormalizado.

### Endpoints

```
GET /api/tag
  Response 200: Tag[] (incluye LocationTags[].Location)

GET /api/tag/{id:guid}
  Response 200: Tag (con LocationTags)
  Error 404

POST /api/tag
  Body: { name, slug, description? }
  Response 201: Tag

PUT /api/tag/{id:guid}
  Body: { id, name, slug, description? }
  Response 204

DELETE /api/tag/{id:guid}
  Response 204 / 404
```

### Modelo Tag

```typescript
interface Tag {
  id: string;
  name: string;                      // max 60, citext, unique
  slug: string;                      // max 80, citext, unique
  description?: string;              // max 500
  usageCount: number;                // denormalized counter
  locationTags?: LocationTag[];
  followers?: UserTagFollow[];
  createdAtUtc: string;
  updatedAtUtc?: string;
  isDeleted: boolean;
}
```

### Páginas de Tag

- **Lista** (`/tags`): Nube de tags ordenados por usageCount. Buscador.
- **Detalle** (`/tags/:id`): Tag info + locations que usan este tag.
- **Crear/Editar**: Formulario simple (nombre, slug, descripción).

---

## ⭐ Reviews (Rutas: anidadas en `/locations/:id`)

Las reviews pertenecen a una Location y un User. Al crear/editar/eliminar una review, el backend recalcula automáticamente `Location.AverageRating` y `Location.ReviewCount`.

### Endpoints

```
GET /api/review/location/{locationId:guid}
  Response 200: Review[] (incluye User, ordenado por CreatedAtUtc desc)

GET /api/review/{id:guid}
  Response 200: Review (con User y Location)

POST /api/review
  Body: { locationId: Guid, userId: Guid, rating: 1..5, title?: string, body?: string, visitedOn?: DateOnly }
  Response 201: Review
  Error 400: Rating fuera de rango, o duplicado (unique index Location+User viva)

PUT /api/review/{id:guid}
  Body: Review completo
  Response 204

DELETE /api/review/{id:guid}
  Response 204 / 404
```

### Modelo Review

```typescript
interface Review {
  id: string;
  locationId: string;
  location?: Location;
  userId: string;
  user?: User;
  rating: number;                    // 1..5
  title?: string;                    // max 200
  body?: string;                     // max 4000
  visitedOn?: string;                // DateOnly (YYYY-MM-DD)
  createdAtUtc: string;
  updatedAtUtc?: string;
  isDeleted: boolean;
}
```

### Componentes de Review

- **ReviewList**: Lista de reviews en página de detalle de Location, con rating promedio, distribución de estrellas.
- **ReviewForm**: Formulario inline/modal con estrellas clickeables, título, cuerpo, fecha de visita.
- **ReviewCard**: Avatar + nombre de usuario + estrellas + fecha + título + cuerpo. Solo el autor puede editar/eliminar.

---

## 🖼️ Media (Rutas: anidadas en `/locations/:id`)

Fotos y videos de una Location. Soporta imagen de portada (`isCover`).

### Endpoints

```
GET /api/media/location/{locationId:guid}
  Response 200: LocationMedia[] (ordenado por SortOrder)

GET /api/media/{id:guid}
  Response 200: LocationMedia (con Location)

POST /api/media
  Body: { locationId, url, thumbnailUrl?, caption?, type: "Image"|"Video", isCover, sortOrder, uploadedByUserId? }
  Response 201: LocationMedia

PUT /api/media/{id:guid}
  Body: LocationMedia completo
  Response 204

DELETE /api/media/{id:guid}
  Response 204 / 404
```

### Modelo LocationMedia

```typescript
interface LocationMedia {
  id: string;
  locationId: string;
  location?: Location;
  url: string;                       // max 2048, required
  thumbnailUrl?: string;             // max 2048
  caption?: string;                  // max 300
  type: MediaType;                   // "Image" | "Video"
  isCover: boolean;
  sortOrder: number;
  uploadedByUserId?: string;
  uploadedByUser?: User;
  createdAtUtc: string;
  updatedAtUtc?: string;
  isDeleted: boolean;
}

enum MediaType {
  Image = "Image",
  Video = "Video"
}
```

### Componentes de Media

- **MediaGallery**: Grid/carrusel de imágenes y videos en página de detalle. Soporta lightbox (fancybox/lightgallery).
- **MediaUploader**: Componente de subida (drag & drop), requiere servicio de storage externo (S3, Cloudinary, etc.). Los URLs se almacenan en la API.
- **MediaCard**: Thumbnail con overlay de tipo (ícono de imagen/video), botón de eliminar, toggle de portada.

---

## 📋 Collections (Rutas: `/collections`, `/collections/:id`)

Listas curadas de locations creadas por usuarios.

### Endpoints

```
GET /api/collection/user/{userId:guid}
  Response 200: LocationCollection[] (incluye Items[].Location)

GET /api/collection/{id:guid}
  Response 200: LocationCollection (con Items[].Location, Owner)

POST /api/collection
  Body: { name, description?, visibility: "Private"|"Unlisted"|"Public", ownerId }
  Response 201: LocationCollection

PUT /api/collection/{id:guid}
  Body: LocationCollection
  Response 204

DELETE /api/collection/{id:guid}
  Response 204

POST /api/collection/{collectionId:guid}/items
  Body: { locationId: Guid, notes?: string }
  Response 204
  Error 400: Location no existe o item ya en colección

DELETE /api/collection/{collectionId:guid}/items/{locationId:guid}
  Response 204 / 404
```

### Modelo LocationCollection

```typescript
interface LocationCollection {
  id: string;
  name: string;                      // max 150, required
  description?: string;              // max 1000
  visibility: CollectionVisibility;   // "Private" | "Unlisted" | "Public"
  ownerId: string;
  owner?: User;
  items?: CollectionItem[];
  createdAtUtc: string;
  updatedAtUtc?: string;
  isDeleted: boolean;
}

enum CollectionVisibility {
  Private = "Private",
  Unlisted = "Unlisted",
  Public = "Public"
}

interface CollectionItem {
  collectionId: string;
  collection?: LocationCollection;
  locationId: string;
  location?: Location;
  sortOrder: number;
  notes?: string;                    // max 500
  createdAtUtc: string;
}
```

### Páginas de Collection

- **Mis Colecciones** (`/collections`): Grid de colecciones del usuario autenticado. Botón crear.
- **Detalle** (`/collections/:id`): Nombre, descripción, visibility badge, grid de locations con drag & drop para reordenar (actualizar SortOrder).
- **Colección pública** — visible para otros usuarios si visibility=Public.
- **Añadir a colección**: Botón "Guardar" en cada Location card que abre un dropdown/modal con las colecciones del usuario.

---

## 👤 Perfil de Usuario (Ruta: `/profile`)

### Modelo User (lectura)

```typescript
interface User {
  id: string;
  userName: string;                  // max 64, citext, unique
  email: string;                     // max 256, citext, unique
  displayName?: string;              // max 128
  avatarUrl?: string;                // max 2048
  roles: string[];                   // ["Admin", "User", "UserL1", "UserL2", "UserL3"]
  createdAtUtc: string;
  isDeleted: boolean;
  // No expuestos en responses públicas: passwordHash, refreshToken, refreshTokenExpiryTime
}
```

### Página de Perfil
- Avatar + nombre + email
- Cambiar contraseña (modal)
- Lista de colecciones del usuario
- Lista de reviews del usuario
- Si es Admin: panel de administración

---

## 🗺️ Componentes Geoespaciales

### Mapa Interactivo (Leaflet/MapLibre)

Componentes requeridos:

1. **LocationMap** — Mapa en página de detalle mostrando un pin en las coordenadas de la location.
2. **LocationPicker** — Mapa en formulario de crear/editar location donde el usuario puede hacer clic para seleccionar coordenadas. Muestra pin arrastrable.
3. **NearbyMap** — Mapa con radio de búsqueda ajustable. Muestra múltiples locations como pins. Al hacer clic en un pin, muestra card con info resumida.
4. **SearchRadiusSlider** — Control de radio en metros (100m, 500m, 1km, 5km, 10km, 50km).

### Formato de Coordenadas

⚠️ **IMPORTANTE**: El backend usa NetTopologySuite con SRID 4326. Las coordenadas se envían en formato GeoJSON:
```json
{
  "type": "Point",
  "coordinates": [-99.1332, 19.4326]  // [longitude, latitude] ⚠️ Orden NTS: X=lng, Y=lat
}
```

Leaflet usa `[latitude, longitude]` internamente. Hay que convertir al llamar a la API:
```typescript
// Leaflet → API
const apiCoordinates = {
  type: "Point" as const,
  coordinates: [leafletLng, leafletLat]
};

// API → Leaflet
const leafletPosition: [number, number] = [point.coordinates[1], point.coordinates[0]];
```

---

## 🧭 Navegación y Layout

### App Shell (Layout Principal)

```
┌────────────────────────────────────────────┐
│  Navbar (sticky top)                       │
│  Logo | Búsqueda | Categorías | Colecciones│
│  [Login/Register] o [Avatar▼| Logout]     │
├────────────────────────────────────────────┤
│                                            │
│  <Outlet /> (React Router)                 │
│                                            │
├────────────────────────────────────────────┤
│  Footer                                    │
│  Links | About | Contacto | Redes Sociales │
└────────────────────────────────────────────┘
```

### Navbar

- **Mobile**: Hamburguesa → drawer con navegación completa
- **Desktop**: Links horizontales + botón de crear ubicación + avatar dropdown
- **Admin**: Link extra a panel de admin
- **Barra de búsqueda**: Input de texto que navega a `/locations?search=...`

### Sidebar (Admin)

Panel de administración con accesos a:
- Gestionar Categorías
- Gestionar Tags
- Gestionar Ubicaciones (incluye soft-delete/restore)
- Dashboard con estadísticas

### Breadcrumbs

En páginas de detalle: `Home > Locations > {Location Name}`

---

## 📱 Responsive Design — Breakpoints

| Breakpoint | Ancho | Layout |
|---|---|---|
| Mobile | < 640px | 1 columna, cards apiladas, nav hamburguesa |
| Tablet | 640-1024px | 2 columnas, nav horizontal compacta |
| Desktop | > 1024px | 3-4 columnas, nav completa, sidebar opcional |

### Reglas responsive específicas:

1. **Homepage hero**: Texto centrado en mobile, alineado a izquierda en desktop
2. **Location cards**: 1 col (mobile), 2 cols (tablet), 3 cols (desktop)
3. **Formularios**: 1 columna en mobile, 2 columnas en desktop (campos agrupados lógicamente)
4. **Mapa**: Altura 300px mobile, 400px tablet, 500px desktop
5. **Tablas de admin**: Scroll horizontal en mobile, tabla completa en desktop
6. **Modales**: Full-screen en mobile, centrados con overlay en desktop

---

## 🎨 Diseño Visual (Design System)

### Paleta de Colores Sugerida

| Token | Color | Uso |
|---|---|---|
| Primary | `#6366F1` (Indigo-500) | Botones, links, acentos |
| Primary Dark | `#4F46E5` (Indigo-700) | Hover states |
| Secondary | `#10B981` (Emerald-500) | Éxito, ratings altos |
| Danger | `#EF4444` (Red-500) | Eliminar, errores |
| Warning | `#F59E0B` (Amber-500) | Ratings medios |
| Background | `#F8FAFC` (Slate-50) | Fondo general |
| Surface | `#FFFFFF` | Cards, modales |
| Text Primary | `#0F172A` (Slate-900) | Títulos, cuerpo |
| Text Secondary | `#64748B` (Slate-500) | Subtítulos, metadata |

### Tipografía
- Headings: `font-bold tracking-tight`
- Body: `text-base leading-relaxed`
- Small/Captions: `text-sm text-slate-500`

### Componentes shadcn/ui a usar:
- `Button` (variants: default, outline, ghost, destructive)
- `Input`, `Textarea`, `Select`, `Checkbox`, `Switch`
- `Card`, `CardHeader`, `CardContent`, `CardFooter`
- `Dialog` (modal), `Sheet` (drawer mobile)
- `DropdownMenu` (avatar menu, acciones)
- `Tabs` (formularios multi-pestaña)
- `Badge` (estado, categorías, tags)
- `Avatar` (usuario)
- `Skeleton` (loading states)
- `Command` (búsqueda con autocompletar tipo Spotlight)
- `Popover` (color picker, date picker)
- `Slider` (radius de búsqueda)
- `Toast` (notificaciones vía Sonner)

---

## 🚦 Estados de UI para Cada Componente

### Todo componente asíncrono debe manejar:

1. **Loading**: Skeleton/Spinner mientras se carga (axios pending)
2. **Empty**: Ilustración + mensaje cuando no hay datos
3. **Error**: Mensaje de error con botón de reintentar (retry)
4. **Success**: Los datos renderizados normalmente

### Feedback al Usuario

- **Toasts**: Para acciones exitosas ("Location creada", "Review eliminada", etc.)
- **Confirm dialogs**: Para acciones destructivas (eliminar location, eliminar review)
- **Optimistic updates**: Para toggle de favorito, like, etc.

---

## 📡 Axios Configuration (Cliente HTTP)

```typescript
// lib/axios.ts
import axios from 'axios';

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'https://localhost:5113',
  headers: { 'Content-Type': 'application/json' },
});

// Request interceptor: attach JWT
api.interceptors.request.use(config => {
  const token = useAuthStore.getState().accessToken;
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// Response interceptor: handle 401 → try refresh → retry
api.interceptors.response.use(
  response => response,
  async error => {
    const original = error.config;
    if (error.response?.status === 401 && !original._retry) {
      original._retry = true;
      const { userId, refreshToken, setTokens, logout } = useAuthStore.getState();
      if (refreshToken && userId) {
        try {
          const { data } = await axios.post(`${BASE}/api/auth/refresh-token`, {
            userId, refreshToken
          });
          setTokens(data.accessToken, data.refreshToken);
          original.headers.Authorization = `Bearer ${data.accessToken}`;
          return api(original);
        } catch {
          logout();
        }
      }
      logout();
    }
    return Promise.reject(error);
  }
);

export default api;
```

---

## 🧩 Estructura de Archivos del Proyecto Frontend

```
rmr-locations-frontend/
├── index.html
├── package.json
├── tsconfig.json
├── vite.config.ts
├── tailwind.config.ts
├── postcss.config.js
├── public/
│   ├── favicon.ico
│   └── og-image.png
├── src/
│   ├── main.tsx                    # Entry point
│   ├── App.tsx                     # Router + Layout
│   ├── index.css                   # Tailwind directives + globals
│   ├── lib/
│   │   ├── axios.ts               # Axios instance + interceptors
│   │   ├── utils.ts               # cn(), formatDate(), etc.
│   │   └── constants.ts           # API_BASE_URL, enums, etc.
│   ├── types/
│   │   ├── api.ts                 # Todos los tipos de respuesta de API
│   │   ├── models.ts              # Interfaces de dominio
│   │   └── forms.ts              # Zod schemas para formularios
│   ├── stores/
│   │   ├── auth-store.ts          # Zustand: user, tokens, login/logout/register
│   │   ├── location-store.ts      # Zustand: locations, filters, pagination
│   │   └── ui-store.ts            # Zustand: theme, sidebar, modals
│   ├── hooks/
│   │   ├── use-auth.ts            # Hook de autenticación
│   │   ├── use-locations.ts       # React Query wrapper para locations
│   │   ├── use-categories.ts      # React Query wrapper para categories
│   │   ├── use-tags.ts           # React Query wrapper para tags
│   │   ├── use-reviews.ts        # React Query wrapper para reviews
│   │   ├── use-media.ts          # React Query wrapper para media
│   │   ├── use-collections.ts    # React Query wrapper para collections
│   │   └── use-geolocation.ts    # Hook para obtener ubicación del navegador
│   ├── components/
│   │   ├── ui/                    # shadcn/ui components (Button, Card, etc.)
│   │   ├── layout/
│   │   │   ├── Navbar.tsx
│   │   │   ├── Footer.tsx
│   │   │   ├── Sidebar.tsx
│   │   │   ├── Breadcrumbs.tsx
│   │   │   └── AppShell.tsx
│   │   ├── auth/
│   │   │   ├── LoginForm.tsx
│   │   │   ├── RegisterForm.tsx
│   │   │   ├── ChangePasswordDialog.tsx
│   │   │   └── ProtectedRoute.tsx
│   │   ├── locations/
│   │   │   ├── LocationCard.tsx
│   │   │   ├── LocationGrid.tsx
│   │   │   ├── LocationForm.tsx
│   │   │   ├── LocationDetail.tsx
│   │   │   ├── LocationMap.tsx
│   │   │   ├── LocationPicker.tsx
│   │   │   ├── NearbyMap.tsx
│   │   │   ├── SearchBar.tsx
│   │   │   ├── SearchRadiusSlider.tsx
│   │   │   ├── StatusBadge.tsx
│   │   │   └── OpeningHoursEditor.tsx
│   │   ├── categories/
│   │   │   ├── CategoryCard.tsx
│   │   │   ├── CategoryTree.tsx
│   │   │   ├── CategoryForm.tsx
│   │   │   └── CategoryIconPicker.tsx
│   │   ├── tags/
│   │   │   ├── TagCloud.tsx
│   │   │   ├── TagBadge.tsx
│   │   │   ├── TagForm.tsx
│   │   │   └── TagSelector.tsx
│   │   ├── reviews/
│   │   │   ├── ReviewList.tsx
│   │   │   ├── ReviewCard.tsx
│   │   │   ├── ReviewForm.tsx
│   │   │   ├── StarRating.tsx
│   │   │   └── RatingDistribution.tsx
│   │   ├── media/
│   │   │   ├── MediaGallery.tsx
│   │   │   ├── MediaCard.tsx
│   │   │   ├── MediaUploader.tsx
│   │   │   └── Lightbox.tsx
│   │   ├── collections/
│   │   │   ├── CollectionCard.tsx
│   │   │   ├── CollectionGrid.tsx
│   │   │   ├── CollectionForm.tsx
│   │   │   ├── CollectionDetail.tsx
│   │   │   ├── AddToCollectionDialog.tsx
│   │   │   └── CollectionItemSortable.tsx
│   │   ├── profile/
│   │   │   ├── ProfileCard.tsx
│   │   │   └── UserReviewsList.tsx
│   │   └── shared/
│   │       ├── EmptyState.tsx
│   │       ├── ErrorState.tsx
│   │       ├── LoadingSkeleton.tsx
│   │       ├── ConfirmDialog.tsx
│   │       ├── Pagination.tsx
│   │       └── ColorPicker.tsx
│   └── pages/
│       ├── HomePage.tsx
│       ├── LoginPage.tsx
│       ├── RegisterPage.tsx
│       ├── ProfilePage.tsx
│       ├── LocationsPage.tsx
│       ├── LocationDetailPage.tsx
│       ├── LocationNewPage.tsx
│       ├── LocationEditPage.tsx
│       ├── CategoriesPage.tsx
│       ├── CategoryDetailPage.tsx
│       ├── TagsPage.tsx
│       ├── TagDetailPage.tsx
│       ├── CollectionsPage.tsx
│       ├── CollectionDetailPage.tsx
│       ├── AdminDashboardPage.tsx
│       ├── SearchResultsPage.tsx
│       └── NotFoundPage.tsx
```

---

## 🧪 Validación de Formularios (Zod Schemas)

```typescript
// Ejemplos de schemas para formularios

const loginSchema = z.object({
  userName: z.string().min(3, "Mínimo 3 caracteres"),
  email: z.string().email("Email inválido"),
  password: z.string().min(6, "Mínimo 6 caracteres"),
});

const locationSchema = z.object({
  name: z.string().min(1, "Requerido").max(200),
  slug: z.string().min(1).max(220).regex(/^[a-z0-9-]+$/, "Solo minúsculas, números y guiones"),
  description: z.string().max(4000).optional(),
  coordinates: z.object({
    type: z.literal("Point"),
    coordinates: z.tuple([z.number(), z.number()])
  }),
  address: z.object({
    name: z.string().optional(),
    street: z.string().optional(),
    exteriorNumber: z.string().optional(),
    neighborhood: z.string().optional(),
    city: z.string().optional(),
    state: z.string().optional(),
    postalCode: z.string().optional(),
    countryCode: z.string().length(2).optional(),
  }),
  status: z.enum(["Draft", "Published", "Archived"]),
  isPublic: z.boolean(),
  websiteUrl: z.string().url().optional().or(z.literal('')),
  phoneNumber: z.string().max(32).optional(),
});

const reviewSchema = z.object({
  rating: z.number().int().min(1).max(5),
  title: z.string().max(200).optional(),
  body: z.string().max(4000).optional(),
  visitedOn: z.string().optional(), // DateOnly
});
```

---

## 🔄 Flujos de Usuario Principales

### Flujo 1: Búsqueda y Descubrimiento
1. Usuario llega a Homepage → ve categorías destacadas y lugares populares
2. Usa barra de búsqueda o navega a `/locations`
3. Filtra por texto, categoría, tags
4. Si da permiso de geolocalización → resultados ordenados por cercanía
5. Si no, puede buscar manualmente con NearbyMap/SearchRadiusSlider
6. Hace clic en una Location → ve detalle completo con mapa, fotos, reviews

### Flujo 2: Registro y Contribución
1. Usuario crea cuenta (Register → Login)
2. Navega a `/locations/new`
3. Completa formulario multi-pestaña (info, dirección, ubicación en mapa, categorías, tags, horarios, fotos)
4. Publica (status: Published)
5. Otros usuarios pueden ver, reseñar y guardar en colecciones

### Flujo 3: Reseñas y Ratings
1. Usuario visita detalle de Location
2. Ve reviews existentes y rating promedio
3. Escribe su propia review (1-5 estrellas + título + cuerpo)
4. El rating promedio se actualiza automáticamente

### Flujo 4: Colecciones
1. Usuario crea colección ("Favoritos CDMX", "Para Visitar")
2. Navega por locations y usa "Guardar" → elige colección
3. En su perfil ve las colecciones con todas las locations guardadas
4. Puede compartir colecciones públicas

---

## 🛡️ Manejo de Roles y Permisos

| Rol | Permisos |
|---|---|
| **Anónimo** | Ver locations, categorías, tags, reviews. Buscar. Registrarse. |
| **User** | Todo lo de anónimo + crear/editar/eliminar sus propias locations, reviews, media, collections. |
| **Admin** | Todo lo de User + admin-only endpoints, gestionar cualquier location/categoría/tag, soft-delete/restore. |

### Componente ProtectedRoute
```typescript
<ProtectedRoute requiredRole="Admin">
  <AdminDashboardPage />
</ProtectedRoute>
```

---

## 📄 Páginas Específicas

### HomePage (`/`)

Secciones (en orden):

1. **Hero** (full-width, imagen de fondo con overlay gradiente)
   - Título: "Descubre Lugares Increíbles"
   - Subtítulo: "Encuentra, reseña y comparte los mejores lugares cerca de ti"
   - Barra de búsqueda con ícono de lupa + botón "Explorar"
   - Si el usuario tiene geolocalización activa: botón "Cerca de mí"

2. **Categorías Populares** (grid responsivo)
   - Fetch `GET /api/category` → mostrar top 8 con iconos y colores
   - Cada tarjeta navega a `/locations?categoryId={id}`

3. **Lugares Destacados** (grid responsivo de LocationCard)
   - Fetch `GET /api/location` → filtrar `averageRating >= 4`, ordenar por rating desc
   - Mostrar top 6-8

4. **Cómo Funciona** (3 columnas)
   - 🔍 Busca — "Encuentra lugares por categoría, ubicación o tags"
   - ⭐ Descubre — "Lee reseñas, ve fotos y conoce los mejores lugares"
   - 📝 Comparte — "Crea tu cuenta, escribe reseñas y arma colecciones"

5. **CTA Final** — "¿Tienes un lugar favorito? Compártelo con la comunidad" + botón "Registrarse"

### NotFoundPage (`*`)

- Ilustración 404
- "La página que buscas no existe"
- Botón "Volver al inicio"

---

## ⚙️ Configuración Inicial del Proyecto

```bash
npm create vite@latest rmr-locations-frontend -- --template react-ts
cd rmr-locations-frontend
npm install react-router-dom zustand @tanstack/react-query axios react-hook-form zod @hookform/resolvers
npm install tailwindcss @tailwindcss/vite postcss
npm install lucide-react sonner leaflet react-leaflet @types/leaflet
npm install -D @types/node
npx shadcn@latest init
npx shadcn@latest add button card input textarea select dialog sheet dropdown-menu tabs badge avatar skeleton command popover slider toast switch checkbox
```

### Variables de Entorno (`.env`)

```env
VITE_API_URL=https://localhost:5113
VITE_APP_NAME=rmrLocations
VITE_DEFAULT_MAP_CENTER=19.4326,-99.1332
VITE_DEFAULT_MAP_ZOOM=12
```

---

## 🚀 Instrucciones para Claude Design

1. **Lee este documento completo** antes de empezar a codificar.
2. **Configura el proyecto** con Vite + React + TypeScript + TailwindCSS como se indica arriba.
3. **Instala todas las dependencias** listadas.
4. **Crea la estructura de archivos** exactamente como se muestra en el árbol de directorios.
5. **Implementa en este orden**:
   a. Tipos de TypeScript (`types/`)
   b. Axios config con interceptors (`lib/axios.ts`)
   c. Stores de Zustand (`stores/`)
   d. Componentes UI de shadcn/ui (`components/ui/`)
   e. Componentes compartidos (EmptyState, ErrorState, LoadingSkeleton, etc.)
   f. Layout (Navbar, Footer, AppShell)
   g. Páginas de Auth (Login, Register)
   h. Páginas principales (HomePage, Locations, Categories, Tags)
   i. Páginas de detalle y formularios
   j. Componentes de Review, Media, Collections
   k. Perfil de usuario y Admin Dashboard
6. **Cada página debe ser completamente responsiva** (mobile-first).
7. **Usa React Query (`@tanstack/react-query`)** para todas las llamadas a la API (caching, revalidación, estados de loading/error).
8. **Usa React Hook Form + Zod** para todos los formularios con validación.
9. **Implementa manejo de errores** en cada componente: loading skeleton, empty state con ilustración, error state con retry.
10. **Prueba que cada página funcione** con la API corriendo en `https://localhost:5113`.

---

## 📝 Notas Importantes para el Frontend

1. ⚠️ **Orden de coordenadas**: El backend usa `[longitude, latitude]` (estándar GeoJSON/NTS). Leaflet usa `[latitude, longitude]`. Siempre convierte al llamar/recibir de la API.

2. ⚠️ **`version` (xmin)**: Campo de concurrencia optimista generado por PostgreSQL. Es de solo lectura. No lo incluyas en bodies de POST/PUT.

3. ⚠️ **`isDeleted`**: Las entidades con soft delete no se devuelven por defecto. Para operaciones de administración que necesiten ver items eliminados, contacta al backend (requiere `.IgnoreQueryFilters()` del lado del servidor).

4. ⚠️ **Rating recalculation**: Después de crear/editar/eliminar una review, el backend recalcula automáticamente `averageRating` y `reviewCount`. El frontend debe revalidar la location (invalidate queries) después de mutaciones de review.

5. ⚠️ **Slug uniqueness**: Los slugs deben ser únicos. Sugiere slugs automáticos basados en el nombre (lowercase, reemplazar espacios con guiones).

6. ⚠️ **DELETE Cascade**: Al eliminar una Location (hard delete), se eliminan en cascada: OpeningHours, LocationCategories, LocationTags, UserFavoriteLocations, CollectionItems, Media y Reviews (estas últimas vía soft delete).

7. ⚠️ **CORS**: La API acepta requests de `localhost:3000`, `localhost:5113`, `localhost:5173` (HTTP y HTTPS). Configura el proxy de Vite si usas otro puerto.

8. ⚠️ **HTTPS**: La API corre en HTTPS con certificado de desarrollo. Para desarrollo local, configura Vite para que acepte certificados autofirmados o usa el proxy de Vite.

---

## 🎯 Resumen de Funcionalidades a Implementar

| # | Funcionalidad | Prioridad |
|---|---|---|
| 1 | Registro, Login, Logout, Refresh Token | 🔴 Crítico |
| 2 | Navbar + Footer + AppShell responsivo | 🔴 Crítico |
| 3 | Homepage completa | 🔴 Crítico |
| 4 | CRUD Locations (listar, ver, crear, editar, eliminar) | 🔴 Crítico |
| 5 | Búsqueda geoespacial (nearby + search) con mapa | 🔴 Crítico |
| 6 | CRUD Categories (listar, ver, crear, editar, eliminar) | 🟡 Alto |
| 7 | CRUD Tags (listar, ver, crear, editar, eliminar) | 🟡 Alto |
| 8 | Reviews (listar, crear, editar, eliminar) | 🟡 Alto |
| 9 | Media Gallery + Upload | 🟡 Alto |
| 10 | Collections (CRUD + add/remove items) | 🟢 Medio |
| 11 | Perfil de usuario (ver info, cambiar password) | 🟢 Medio |
| 12 | Admin Dashboard | 🟢 Medio |
| 13 | Soft delete / Restore (UI admin) | 🟢 Medio |
| 14 | Favoritos (UserFavoriteLocation) | 🔵 Bajo |
| 15 | PATCH para actualizaciones parciales | 🔵 Bajo |

---

## 🔗 API Reference Completa

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| POST | `/api/auth/register` | No | Registrar usuario |
| POST | `/api/auth/login` | No | Iniciar sesión |
| POST | `/api/auth/logout` | Sí | Cerrar sesión |
| POST | `/api/auth/change-password` | Sí | Cambiar contraseña |
| POST | `/api/auth/refresh-token` | No | Renovar token |
| GET | `/api/auth` | Sí | Endpoint autenticado |
| GET | `/api/auth/admin-only` | Admin | Endpoint admin |
| GET | `/api/location` | No | Listar locations |
| GET | `/api/location/{id}` | No | Obtener location |
| POST | `/api/location` | No | Crear location |
| PUT | `/api/location/{id}` | No | Actualizar location |
| DELETE | `/api/location/{id}` | No | Eliminar location (hard) |
| GET | `/api/location/nearby` | No | Búsqueda por cercanía |
| GET | `/api/location/search` | No | Búsqueda combinada |
| POST | `/api/location/{id}/soft-delete` | No | Soft delete + cascada |
| POST | `/api/location/{id}/restore` | No | Restaurar |
| GET | `/api/category` | No | Listar categorías |
| GET | `/api/category/{id}` | No | Obtener categoría |
| POST | `/api/category` | No | Crear categoría |
| PUT | `/api/category/{id}` | No | Actualizar categoría |
| DELETE | `/api/category/{id}` | No | Eliminar categoría |
| GET | `/api/tag` | No | Listar tags |
| GET | `/api/tag/{id}` | No | Obtener tag |
| POST | `/api/tag` | No | Crear tag |
| PUT | `/api/tag/{id}` | No | Actualizar tag |
| DELETE | `/api/tag/{id}` | No | Eliminar tag |
| GET | `/api/review/location/{id}` | No | Reviews por location |
| GET | `/api/review/{id}` | No | Obtener review |
| POST | `/api/review` | No | Crear review |
| PUT | `/api/review/{id}` | No | Actualizar review |
| DELETE | `/api/review/{id}` | No | Eliminar review |
| GET | `/api/media/location/{id}` | No | Media por location |
| GET | `/api/media/{id}` | No | Obtener media |
| POST | `/api/media` | No | Crear media |
| PUT | `/api/media/{id}` | No | Actualizar media |
| DELETE | `/api/media/{id}` | No | Eliminar media |
| GET | `/api/collection/user/{id}` | No | Colecciones por usuario |
| GET | `/api/collection/{id}` | No | Obtener colección |
| POST | `/api/collection` | No | Crear colección |
| PUT | `/api/collection/{id}` | No | Actualizar colección |
| DELETE | `/api/collection/{id}` | No | Eliminar colección |
| POST | `/api/collection/{id}/items` | No | Añadir item |
| DELETE | `/api/collection/{id}/items/{lid}` | No | Quitar item |

**Total: 44 endpoints** para el frontend.
