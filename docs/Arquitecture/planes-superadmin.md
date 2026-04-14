# CRUD de Planes — SuperAdmin

## Contexto

Los planes definen los límites operativos de cada suscripción: cuántos usuarios puede tener una sucursal, cuántos turnos puede tomar por mes, qué funcionalidades avanzadas tiene disponibles, y cuántos días de prueba gratuita otorga al registrarse.

El SuperAdmin puede crear, editar y activar/desactivar planes desde el panel sin necesidad de re-deployar ni tocar la base de datos directamente.

No se implementa Delete: un plan con suscripciones activas no puede borrarse. Solo se puede desactivar (`EsActivo = false`).

---

## El trial NO es un plan

El período de prueba es un **estado de facturación** (`EnPeriodoPrueba = true`, `Estado = PruebActiva`) en `SucursalSuscripcion`. No es un tier distinto: la sucursal usa los límites del plan Básico durante el trial.

Lo que sí controla el plan es **cuántos días dura la prueba** mediante `DuracionTrialDias`. Al crear una sucursal, `SucursalService.CreateAsync` obtiene el plan base y calcula la fecha de vencimiento:

```csharp
// SucursalService.cs — CreateAsync
var plan = await _suscripcionRepository.GetPlanBasicoAsync()
    ?? throw new InvalidOperationException("No existe un plan base activo...");

await _suscripcionRepository.AddAsync(new SucursalSuscripcion
{
    Estado           = EstadoSuscripcion.PruebActiva,
    EnPeriodoPrueba  = true,
    FechaInicio      = ahora,
    FechaVencimiento = ahora.AddDays(plan.DuracionTrialDias),
});
```

`DuracionTrialDias = 0` bloquea la creación de la sucursal con `InvalidOperationException` (el plan base debe tener al menos 1 día de trial).

> **Nota:** Antes de 2026-04-03, la suscripción se creaba de forma lazy en `SuscripcionService.ObtenerAsync` al primer acceso. Desde esa fecha toda sucursal nueva nace con su `SucursalSuscripcion` en estado `PruebActiva`.

---

## Entidad `Plan`

```csharp
// src/Domain/Entities/Plan.cs
public class Plan
{
    public Guid    Id                { get; set; }
    public string  Nombre            { get; set; }
    public string? Descripcion       { get; set; }
    public int     MaxUsuarios       { get; set; }          // cuántos miembros activos puede tener la sucursal
    public int?    MaxTurnosMes      { get; set; }          // null = ilimitado
    public decimal PrecioMensual     { get; set; }
    public bool    EsActivo          { get; set; } = true;
    public int     OrdenDisplay      { get; set; }          // orden visual en listas
    public int     DuracionTrialDias { get; set; } = 14;    // días de prueba al registrarse
    public bool    TieneReportes     { get; set; } = false; // acceso al módulo de reportes

    public ICollection<SucursalSuscripcion> Suscripciones { get; set; }
}
```

Tabla en DB: `planes`

---

## Planes base (seed original)

Los 3 planes fueron insertados por la migración `AddPlanesYSuscripciones`. El `HasData` fue eliminado de `PlanConfiguration.cs` en la migración `QuitarSeedPlanes` para evitar conflictos con ediciones futuras.

| ID | Nombre | MaxUsuarios | MaxTurnosMes | Precio | Trial | Reportes |
|---|---|---|---|---|---|---|
| `10000000-...-0001` | Básico | 2 | 200 | $0 | 14 días | No |
| `10000000-...-0002` | Pro | 10 | ilimitado | $0 | 14 días | Sí |
| `10000000-...-0003` | Enterprise | 2.147.483.647 | ilimitado | $0 | 14 días | Sí |

> `TieneReportes` se estableció con una sentencia `UPDATE` directa al aplicar la migración `AgregaTieneReportes`, no con seed data de EF.

---

## API — Endpoints

Todos los endpoints están bajo `/api/superadmin/planes` y no requieren autenticación especial más allá del JWT de sesión (acceso solo desde el panel SuperAdmin en el frontend).

```
GET    /api/superadmin/planes              → 200 PlanDto[]
GET    /api/superadmin/planes/{id}         → 200 PlanDto | 404
POST   /api/superadmin/planes              → 201 PlanDto
PUT    /api/superadmin/planes/{id}         → 200 PlanDto | 404
PATCH  /api/superadmin/planes/{id}/estado  → 200 PlanDto | 404
```

### GET `/api/superadmin/planes`

Retorna todos los planes (activos e inactivos) ordenados por `OrdenDisplay`.

**Response `200`:**
```json
[
  {
    "id": "10000000-0000-0000-0000-000000000001",
    "nombre": "Básico",
    "descripcion": "Ideal para comenzar.",
    "maxUsuarios": 2,
    "maxTurnosMes": 200,
    "precioMensual": 0.00,
    "esActivo": true,
    "ordenDisplay": 1,
    "duracionTrialDias": 14,
    "tieneReportes": false
  }
]
```

### POST `/api/superadmin/planes`

**Body** (`CrearPlanDto`):
```json
{
  "nombre": "Pro",
  "descripcion": "Para negocios en crecimiento.",
  "maxUsuarios": 10,
  "maxTurnosMes": null,
  "precioMensual": 4999.00,
  "esActivo": true,
  "ordenDisplay": 2,
  "duracionTrialDias": 14,
  "tieneReportes": true
}
```

`maxTurnosMes: null` significa ilimitado.

**Response `201`** — el plan creado con su `id` asignado.

**Response `400`** — validación fallida (nombre requerido, maxUsuarios < 1, etc.).

### PUT `/api/superadmin/planes/{id}`

Mismo body que POST. Reemplaza todos los campos del plan.

**Response `404`:**
```json
{ "message": "Plan no encontrado." }
```

### PATCH `/api/superadmin/planes/{id}/estado`

Invierte `EsActivo` (toggle). No requiere body.

---

## DTOs

**`PlanDto`** (response):
```csharp
public Guid    Id                { get; set; }
public string  Nombre            { get; set; }
public string? Descripcion       { get; set; }
public int     MaxUsuarios       { get; set; }
public int?    MaxTurnosMes      { get; set; }   // null = ilimitado
public decimal PrecioMensual     { get; set; }
public bool    EsActivo          { get; set; }
public int     OrdenDisplay      { get; set; }
public int     DuracionTrialDias { get; set; }
public bool    TieneReportes     { get; set; }
```

**`CrearPlanDto`** / **`ActualizarPlanDto`** (misma estructura):
```csharp
[Required][MaxLength(100)] string  Nombre
[MaxLength(300)]           string? Descripcion
[Range(1, int.MaxValue)]   int     MaxUsuarios
                           int?    MaxTurnosMes
[Range(0, 9999999)]        decimal PrecioMensual
                           bool    EsActivo
[Range(0, 9999)]           int     OrdenDisplay
[Range(0, 365)]            int     DuracionTrialDias
                           bool    TieneReportes
```

---

## `ISuscripcionRepository` — métodos del panel SuperAdmin

```csharp
// Todos los planes (activos e inactivos), ordenados por OrdenDisplay
Task<IEnumerable<Plan>> GetTodosLosPlanesAsync();

// Agregar plan nuevo al contexto
Task AddPlanAsync(Plan plan);

// Marcar plan como modificado en el contexto
void UpdatePlan(Plan plan);
```

`SaveChangesAsync()` es heredado de `IGenericRepository<SucursalSuscripcion>` y se llama desde el servicio.

---

## `SuperAdminService` — métodos de planes

| Método | Descripción |
|---|---|
| `ListarPlanesAsync()` | Retorna todos los planes via `GetTodosLosPlanesAsync()` |
| `ObtenerPlanAsync(Guid id)` | Busca por id, retorna `null` si no existe |
| `CrearPlanAsync(CrearPlanDto)` | Crea plan con `Guid.NewGuid()`, persiste y retorna DTO |
| `ActualizarPlanAsync(Guid, ActualizarPlanDto)` | Actualiza todos los campos; lanza si no existe |
| `ToggleActivoPlanAsync(Guid)` | Invierte `EsActivo`; lanza si no existe |

Todos los métodos comparten el mapeo privado `MapPlanToDto(Plan p) → PlanDto`.

---

## Migraciones

| Migración | Fecha | Descripción |
|---|---|---|
| `AddPlanesYSuscripciones` | 2026-03-26 | Crea tabla `planes` + `suscripciones_sucursal` + seed de 3 planes |
| `QuitarSeedPlanes` | 2026-03-28 | No-op en DB. Elimina `HasData` del snapshot para permitir edición libre de planes |
| `AgregaDuracionTrialDias` | 2026-03-28 | Agrega columna `DuracionTrialDias integer NOT NULL DEFAULT 14` |
| `AgregaTieneReportes` | 2026-03-28 | Agrega columna `TieneReportes boolean NOT NULL DEFAULT false`. Pro y Enterprise se actualizaron a `true` vía SQL |

---

## Frontend

### Ruta

```
/superadmin/planes
```

Protegida con `<ProtectedRoute soloSuperAdmin>`. Accesible desde el nav item **"Planes"** (`CreditCardIcon`) en `SuperAdminLayout`.

### Tipos (`src/types/index.ts`)

```typescript
export interface Plan {
  id: string;
  nombre: string;
  descripcion?: string;
  maxUsuarios: number;
  maxTurnosMes: number | null;   // null = ilimitado
  precioMensual: number;
  esActivo: boolean;
  ordenDisplay: number;
  duracionTrialDias: number;
  tieneReportes: boolean;
}

export interface CrearPlanDto {
  nombre: string;
  descripcion?: string;
  maxUsuarios: number;
  maxTurnosMes: number | null;
  precioMensual: number;
  esActivo: boolean;
  ordenDisplay: number;
  duracionTrialDias: number;
  tieneReportes: boolean;
}

export type ActualizarPlanDto = CrearPlanDto;
```

### API client (`src/api/superadmin.ts`)

```typescript
export const planesApi = {
  listar:       ()                                   => api.get<Plan[]>('/superadmin/planes'),
  obtener:      (id: string)                        => api.get<Plan>(`/superadmin/planes/${id}`),
  crear:        (dto: CrearPlanDto)                 => api.post<Plan>('/superadmin/planes', dto),
  actualizar:   (id: string, dto: ActualizarPlanDto) => api.put<Plan>(`/superadmin/planes/${id}`, dto),
  toggleActivo: (id: string)                        => api.patch<Plan>(`/superadmin/planes/${id}/estado`, {}),
};
```

### Página `Planes.tsx`

**Ruta:** `src/pages/superadmin/Planes.tsx`

Componente único sin subrutas. Estructura visual:

```
Header "Planes" + botón "Nuevo plan"
Stats cards: Total | Activos | Inactivos
Tabla (ordenada por OrdenDisplay):
  Orden | Nombre | MaxUsuarios | MaxTurnosMes | Precio/mes | Trial | Reportes | Estado | Acciones
Acciones por fila:
  Editar (ícono lápiz) → abre modal en modo edición
  Toggle activo/inactivo (ojo / ojo-tachado)
Modal create/edit (AnimatePresence overlay):
  - Nombre *
  - Descripción
  - Max Usuarios *  |  Orden display
  - Max Turnos/mes  [✓ Ilimitado]
  - Precio mensual ($)
  - Días de prueba gratuita (0 = sin trial)
  - Toggle Activo  |  Toggle Reportes
```

**Columnas de la tabla:**

| Columna | Display |
|---|---|
| Orden | `#1`, `#2`… |
| Nombre | texto + descripción truncada en gris |
| MaxUsuarios | número |
| MaxTurnosMes | número o badge "Ilimitado" |
| Precio/mes | `Gratis` / `$4.999` |
| Trial | `14d` / `—` |
| Reportes | badge púrpura "Sí" / `—` |
| Estado | badge verde "Activo" / gris "Inactivo" |
| Acciones | editar + toggle |

---

## Archivos involucrados

### Backend

| Archivo | Cambio |
|---|---|
| `src/Domain/Entities/Plan.cs` | + `DuracionTrialDias`, `TieneReportes` |
| `src/Application/DTOs/SuperAdmin/PlanDto.cs` | **Nuevo** |
| `src/Application/DTOs/SuperAdmin/CrearPlanDto.cs` | **Nuevo** |
| `src/Application/DTOs/SuperAdmin/ActualizarPlanDto.cs` | **Nuevo** |
| `src/Application/Interfaces/ISuscripcionRepository.cs` | + `GetTodosLosPlanesAsync`, `AddPlanAsync`, `UpdatePlan` |
| `src/Infrastructure/Repositories/SuscripcionRepository.cs` | Implementación de los 3 métodos |
| `src/Infrastructure/Data/Configurations/PlanConfiguration.cs` | Removido `HasData`; config de `DuracionTrialDias` y `TieneReportes` |
| `src/Application/Services/SuperAdminService.cs` | + `ISuscripcionRepository`; + 5 métodos de planes + `MapPlanToDto` |
| `src/Api/Controllers/SuperAdminController.cs` | + 5 endpoints bajo `/api/superadmin/planes` |
| `src/Api/Program.cs` | + `ISuscripcionRepository` en factory de `SuperAdminService`; + `SuscripcionGuard` |
| `src/Application/Services/SucursalService.cs` | `CreateAsync` inyecta `ISuscripcionRepository`, crea `SucursalSuscripcion` con `PruebActiva` |
| `src/Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Removido seed; + `DuracionTrialDias`, `TieneReportes` |

### Frontend

| Archivo | Cambio |
|---|---|
| `src/types/index.ts` | + interfaces `Plan`, `CrearPlanDto`, `ActualizarPlanDto` |
| `src/api/superadmin.ts` | + `planesApi` |
| `src/pages/superadmin/Planes.tsx` | **Nuevo** — página completa con tabla y modal |
| `src/components/superadmin/SuperAdminLayout.tsx` | + nav item "Planes" |
| `src/App.tsx` | + ruta `/superadmin/planes` |

---

## Notas de diseño

- **No se borra, se desactiva.** Un plan inactivo no aparece en la pantalla de suscripción del cliente, pero sus suscripciones activas siguen funcionando con los límites que tenía.
- **`OrdenDisplay` controla la jerarquía.** El plan con menor `OrdenDisplay` activo se considera el plan "básico" y se asigna al registrar una nueva sucursal (`GetPlanBasicoAsync` toma el primero por orden).
- **`TieneReportes` se controla por plan.** Si en el futuro se agregan más features premium (exportaciones, API access, etc.), el patrón es agregar un `bool` al `Plan` y validarlo desde `SuscripcionGuard`. Ver `docs/Arquitecture/restricciones-plan.md`.

---

## Verificación

1. `GET /api/superadmin/planes` → lista los 3 planes base
2. `POST /api/superadmin/planes` → crear plan nuevo → aparece en tabla
3. `PUT /api/superadmin/planes/{id}` → cambiar `DuracionTrialDias` de Básico a 7 → registrar sucursal nueva → trial dura 7 días
4. `PUT /api/superadmin/planes/{id}` → activar `TieneReportes` en Básico → sucursal con plan Básico puede ver reportes
5. `PATCH /api/superadmin/planes/{id}/estado` → toggle → badge cambia en tabla
6. Panel `/superadmin/planes` → columna "Reportes" muestra badge púrpura para Pro y Enterprise
