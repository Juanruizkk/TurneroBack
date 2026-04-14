# Suscripciones — Exposición al frontend y validaciones

## Resumen

Esta feature expone el estado de suscripción de una sucursal al panel de administración, valida los límites del plan al crear usuarios, y muestra banners de alerta cuando el período de prueba está por vencer o ya venció.

---

## Arquitectura

```
GET /api/suscripcion?sucursalId={guid}
         │
         ▼
SuscripcionController
         │
         ▼
SuscripcionService.ObtenerAsync()
         │
         ├── ISuscripcionRepository.GetBySucursalAsync()   → Include(Plan)
         └── Transición de estado lazy → Update + Save si venció
```

```
POST /api/usuarios   (crear miembro)
         │
         ▼
UsuarioService.CreateAsync()
         │
         ├── ISuscripcionRepository.GetBySucursalAsync()
         └── IUsuarioSucursalRolRepository.ContarActivosBySucursalAsync()
                    → 400 si activos >= Plan.MaxUsuarios
```

---

## Backend

### Nuevo endpoint

```
GET /api/suscripcion?sucursalId={guid}
Authorization: Bearer {token}
```

**Response `200 OK`:**
```json
{
  "id": "uuid",
  "planNombre": "Básico",
  "planPrecio": 0.00,
  "maxUsuarios": 2,
  "maxTurnosMes": null,
  "estado": "PruebActiva",
  "enPeriodoPrueba": true,
  "fechaVencimiento": "2026-04-11T00:00:00Z",
  "diasRestantes": 14
}
```

**Valores posibles de `estado`:**

| Valor | Descripción |
|---|---|
| `PruebActiva` | Período de prueba en curso |
| `Activa` | Suscripción paga activa |
| `PruebVencida` | Prueba expiró sin convertir |
| `Vencida` | Suscripción paga expiró |
| `Cancelada` | Cancelada manualmente |
| `SinSuscripcion` | No hay registro (sucursales viejas) — valores permisivos |

---

### `SuscripcionService` (`src/Application/Services/SuscripcionService.cs`)

**Método principal:** `ObtenerAsync(Guid sucursalId)`

**Lógica:**
1. Busca la suscripción via `ISuscripcionRepository.GetBySucursalAsync()` (ya incluye el `Plan`).
2. Si no existe → retorna DTO con `Estado = "SinSuscripcion"` y `MaxUsuarios = 999` (permisivo, no bloquea).
3. **Transición de estado lazy:**
   - `PruebActiva` + `DateTime.UtcNow > FechaVencimiento` → pasa a `PruebVencida`
   - `Activa` + `DateTime.UtcNow > FechaVencimiento` → pasa a `Vencida`
   - Guarda el cambio inmediatamente en la DB.
4. Calcula `DiasRestantes = Max(0, ⌈(FechaVencimiento - ahora).TotalDays⌉)`.
5. Mapea a `SuscripcionResponseDto`.

> **Nota:** La transición de estado es lazy — no hay un job programado. Se ejecuta la primera vez que alguien hace un GET después del vencimiento.

---

### `IUsuarioSucursalRolRepository` — nuevo método

```csharp
Task<int> ContarActivosBySucursalAsync(Guid sucursalId);
```

Cuenta los usuarios con estado `Activo` o `PendienteInvitacion` en una sucursal.
Los `Inactivo` **no** se cuentan (no consumen cupo).

---

### `UsuarioService.CreateAsync` — validación de cupo

Antes de crear la membresía, verifica el límite del plan:

```csharp
var suscripcion = await _suscripcionRepo.GetBySucursalAsync(dto.SucursalId);
if (suscripcion is not null)
{
    var activos = await _usrRepository.ContarActivosBySucursalAsync(dto.SucursalId);
    if (activos >= suscripcion.Plan.MaxUsuarios)
        throw new InvalidOperationException(
            $"Tu plan {suscripcion.Plan.Nombre} permite hasta {suscripcion.Plan.MaxUsuarios} " +
            "usuarios activos. Actualizá tu suscripción para agregar más.");
}
```

El controller captura el `InvalidOperationException` y retorna `400 Bad Request` con el mensaje.

---

### Registro en `Program.cs`

```csharp
// Servicio
builder.Services.AddScoped<SuscripcionService>();

// Factory de UsuarioService — se agregó ISuscripcionRepository al final
return new UsuarioService(
    usuarioRepo, usrRepo, sucursalRepo, emailService, frontendUrl,
    sp.GetRequiredService<IHorarioLaboralRepository>(),
    sp.GetRequiredService<IUsuarioServicioRepository>(),
    sp.GetRequiredService<ITurnoRepository>(),
    sp.GetRequiredService<ISuscripcionRepository>(),  // ← nuevo
    logger);
```

---

## Frontend

### Tipo `SuscripcionInfo` (`src/types/index.ts`)

```typescript
export interface SuscripcionInfo {
  id: string;
  planNombre: string;
  planPrecio: number;
  maxUsuarios: number;
  maxTurnosMes: number | null;   // null = ilimitado
  estado: 'PruebActiva' | 'Activa' | 'Vencida' | 'Cancelada' | 'PruebVencida' | 'SinSuscripcion';
  enPeriodoPrueba: boolean;
  fechaVencimiento: string;      // ISO 8601
  diasRestantes: number;
}
```

---

### API client (`src/api/suscripcion.ts`)

```typescript
export const suscripcionApi = {
  getSuscripcion: (sucursalId: string) =>
    api.get<SuscripcionInfo>(`/suscripcion?sucursalId=${sucursalId}`),
};
```

---

### Banner de trial — `AdminLayout.tsx`

Se monta dentro del layout principal, entre el topbar y el `<main>`.

**Comportamiento:**

| Estado | Días restantes | Banner |
|---|---|---|
| `PruebActiva` | > 3 | Amarillo — "Estás en tu período de prueba. Te quedan X días." |
| `PruebActiva` | ≤ 3 | Rojo — misma leyenda con urgencia visual |
| `PruebVencida` | — | Rojo — "Tu período de prueba venció." |
| `Activa` / `SinSuscripcion` | — | No se renderiza |

Siempre incluye un link **"Ver plan"** → `/admin/ajustes` (scrollea a la tab Suscripción).

El fetch se hace una vez al montar el layout (cuando `user.sucursalId` está disponible). Los errores de red se ignoran silenciosamente para no romper el panel.

---

### Tab "Suscripción" — `Ajustes.tsx`

Nueva 6ta tab al final del menú lateral de ajustes.

**Contenido:**

```
Plan actual: [PlanNombre]   [badge de estado coloreado]

Vence: [fecha larga]  (en X días)

┌─────────────────────────────────────────┐
│ Límites del plan                        │
│  Usuarios activos permitidos    X       │
│  Turnos por mes                 X / ∞   │
└─────────────────────────────────────────┘

[Si PruebVencida/Vencida]:
  ⚠ Card roja — "Tu suscripción no está activa"
     → Contactar soporte (mailto:soporte@turneroapp.com)
```

**Estados del badge:**

| Estado | Color |
|---|---|
| `Activa` | Verde |
| `PruebActiva` | Amarillo |
| `PruebVencida` / `Vencida` / `Cancelada` | Rojo |
| `SinSuscripcion` | Gris |

Muestra un **skeleton** animado mientras carga. En caso de error de red muestra "No se pudo cargar la información de suscripción."

---

## Flujo de verificación

1. Registrar sucursal → DB debe tener `SucursalSuscripcion` con `Estado=PruebActiva`, `FechaVencimiento = ahora + 14 días`.
2. `GET /api/suscripcion?sucursalId={id}` → `estado=PruebActiva`, `diasRestantes=14`.
3. Panel admin → banner amarillo visible.
4. Ajustes → tab "Suscripción" → plan Básico, límites y vencimiento.
5. Plan Básico (max 2 usuarios): crear 2 usuarios → intentar crear el 3ro → `400` con mensaje de límite.
6. Cambiar `FechaVencimiento` en DB a fecha pasada → nuevo GET → `estado=PruebVencida`, banner rojo, badge rojo en ajustes.

---

## Archivos modificados / creados

### Backend
| Archivo | Cambio |
|---|---|
| `src/Application/Interfaces/IUsuarioSucursalRolRepository.cs` | + `ContarActivosBySucursalAsync` |
| `src/Infrastructure/Repositories/UsuarioSucursalRolRepository.cs` | Implementación del método |
| `src/Application/Services/SuscripcionService.cs` | **Nuevo** |
| `src/Api/Controllers/SuscripcionController.cs` | **Nuevo** |
| `src/Application/Services/UsuarioService.cs` | + `ISuscripcionRepository`; validación MaxUsuarios en `CreateAsync` |
| `src/Api/Program.cs` | + `SuscripcionService`; + `ISuscripcionRepository` en factory |

### Frontend
| Archivo | Cambio |
|---|---|
| `src/types/index.ts` | + interfaz `SuscripcionInfo` |
| `src/api/suscripcion.ts` | **Nuevo** |
| `src/components/admin/AdminLayout.tsx` | + banner de trial |
| `src/pages/admin/Ajustes.tsx` | + tab "Suscripción" (6ta) |
