# Restricciones de plan y suscripción

## Contexto

MetaTurnos implementa tres tipos de restricciones de acceso basadas en la suscripción de cada sucursal:

1. **Suscripción vencida** — bloquea operaciones que crean contenido (turnos, servicios, usuarios) cuando el trial expiró o la suscripción fue cancelada/vencida.
2. **Feature gate por plan** — ciertas funcionalidades (actualmente Reportes) solo están disponibles en planes que las incluyen explícitamente.
3. **Límites numéricos por plan** — cupos máximos de usuarios, turnos mensuales y servicios activos definidos por cada plan.

Las tres restricciones son **permisivas con sucursales legacy**: si una sucursal no tiene registro en `suscripciones_sucursal`, todas las validaciones pasan libremente.

> **Desde 2026-04-03:** `SucursalService.CreateAsync` crea automáticamente la `SucursalSuscripcion` en estado `PruebActiva` al registrar cada sucursal nueva. El comportamiento permisivo por omisión aplica únicamente a datos pre-existentes en la DB.

---

## Matriz de acceso — suscripción vencida

| Acción | Activa / PruebaActiva | PruebVencida / Vencida / Cancelada |
|---|---|---|
| Login | ✅ | ✅ |
| Ver dashboard | ✅ | ✅ |
| Ver agenda | ✅ | ✅ |
| Ver turnos existentes | ✅ | ✅ |
| Cobrar (caja) | ✅ | ✅ |
| Editar datos propios | ✅ | ✅ |
| **Crear turno nuevo** | ✅ | ❌ 402 |
| **Crear servicio nuevo** | ✅ | ❌ 402 |
| **Invitar / crear usuario** | ✅ | ❌ 402 |
| Ver reportes (plan básico) | ❌ 403 | ❌ 403 |
| Ver reportes (plan Pro/Ent) | ✅ | ❌ 402 |

---

---

## Límites numéricos por plan

Tanto el guard de suscripción vencida como los límites numéricos se validan en los **servicios de aplicación**, antes de persistir la entidad. El orden de validaciones en cada `CreateAsync` es:

```
1. VerificarActivaAsync(sucursalId)  → lanza SuscripcionVencidaException si vencida
2. Límite numérico del plan (MaxServicios / MaxTurnosMes / MaxUsuarios)
3. Lógica de negocio (conflicto horario, email duplicado, etc.)
4. AddAsync + SaveChangesAsync
```

El controller sigue llamando al guard antes de delegar al servicio (defensa en profundidad; el 402 se devuelve sin llegar al service). Si por algún motivo se omitiera el guard en el controller, el servicio lo detecta igualmente.

### Tabla de límites

| Campo en `Plan` | Entidad controlada | Validado en | `null` = |
|---|---|---|---|
| `MaxUsuarios` | `UsuarioSucursalRol` | `UsuarioService.CreateAsync()` | ilimitado |
| `MaxTurnosMes` | `Turno` (del mes calendario) | `TurnoService.CreateAsync()` | ilimitado |
| `MaxServicios` | `Servicio` (activos) | `ServicioService.CreateAsync()` | ilimitado |

### Lógica de validación (patrón uniforme)

```csharp
var suscripcion = await _suscripcionRepo.GetBySucursalAsync(dto.SucursalId);
if (suscripcion?.Plan.MaxXxx is int limite)      // null → patrón no matchea → skip
{
    var usados = await _repo.ContarXxxAsync(...);
    if (usados >= limite)
        throw new InvalidOperationException(
            $"Tu plan {suscripcion.Plan.Nombre} permite hasta {limite} ...")
}
```

- Si no existe suscripción → pasa libremente.
- Si `MaxXxx = null` → `is int limite` no matchea → pasa libremente.
- Si `usados >= limite` → lanza `InvalidOperationException` que el controller captura y devuelve 400.

### Comportamiento esperado por endpoint

| Endpoint | Límite aplicado | Qué se cuenta |
|---|---|---|
| `POST /api/turnos` | `MaxTurnosMes` | Turnos no cancelados de la sucursal en el año/mes del turno nuevo |
| `POST /api/servicios` | `MaxServicios` | Servicios con `Activo = true` de la sucursal |
| `POST /api/usuarios` | `MaxUsuarios` | Membresías activas (`UsuarioSucursalRol`) de la sucursal |

### Estados de turno contabilizados para `MaxTurnosMes`

Se cuentan todos **excepto** los cancelados:

| Estado | ¿Cuenta? |
|---|---|
| `Confirmado` | ✅ |
| `Pendiente` | ✅ |
| `PendientePago` | ✅ |
| `Completado` | ✅ |
| `NoShow` | ❌ excluido |
| `CanceladoCliente` | ❌ excluido |
| `CanceladoStaff` | ❌ excluido |

### Valores en planes base

| Plan | MaxUsuarios | MaxTurnosMes | MaxServicios |
|---|---|---|---|
| Básico | 2 | 200 | 5 |
| Pro | 10 | `null` (ilimitado) | `null` (ilimitado) |
| Enterprise | `null` (ilimitado) | `null` (ilimitado) | `null` (ilimitado) |

> Los valores exactos de los planes se gestionan desde el panel SuperAdmin (`/superadmin/planes`) sin re-deploy.

### Archivos involucrados

| Archivo | Responsabilidad |
|---|---|
| `src/Domain/Entities/Plan.cs` | Campos `MaxUsuarios`, `MaxTurnosMes`, `MaxServicios` |
| `src/Application/Interfaces/ITurnoRepository.cs` | `ContarPorSucursalEnMesAsync(sucursalId, anio, mes)` |
| `src/Infrastructure/Repositories/TurnoRepository.cs` | Implementación: excluye estados cancelados y NoShow |
| `src/Application/Interfaces/IServicioRepository.cs` | `ContarActivosBySucursalAsync(sucursalId)` |
| `src/Infrastructure/Repositories/ServicioRepository.cs` | Implementación: cuenta `Activo = true` |
| `src/Application/Services/TurnoService.cs` | `VerificarActivaAsync` + validación `MaxTurnosMes` en `CreateAsync()` |
| `src/Application/Services/ServicioService.cs` | `VerificarActivaAsync` + validación `MaxServicios` en `CreateAsync()` |
| `src/Application/Services/UsuarioService.cs` | `VerificarActivaAsync` + validación `MaxUsuarios` en `CreateAsync()` |

---

## `SuscripcionGuard`

Servicio scoped (`AddScoped<SuscripcionGuard>`) que centraliza ambas validaciones. Se inyecta por constructor tanto en los **servicios de aplicación** (`TurnoService`, `ServicioService`, `UsuarioService`) como en los **controllers** correspondientes, funcionando como doble capa de defensa.

**Ubicación:** `src/Application/Services/SuscripcionGuard.cs`

```csharp
public class SuscripcionGuard
{
    private static readonly EstadoSuscripcion[] EstadosVencidos =
    [
        EstadoSuscripcion.PruebVencida,
        EstadoSuscripcion.Vencida,
        EstadoSuscripcion.Cancelada,
    ];

    // Bloquea si la suscripción está en estado vencido/cancelado.
    // Lanza SuscripcionVencidaException → controller devuelve 402.
    public async Task VerificarActivaAsync(Guid sucursalId)

    // Bloquea si Plan.TieneReportes == false.
    // Lanza FeatureNoPlanException → controller devuelve 403.
    public async Task VerificarTieneReportesAsync(Guid sucursalId)
}
```

Ambos métodos retornan silenciosamente si no existe registro de suscripción para la sucursal.

---

## Excepciones personalizadas

**Ubicación:** `src/Application/Exceptions/`

```csharp
// → HTTP 402 Payment Required
public class SuscripcionVencidaException : Exception

// → HTTP 403 Forbidden
public class FeatureNoPlanException : Exception
```

El mapeo excepción → código HTTP se realiza manualmente en cada controller (try/catch), no mediante middleware global, para mantener control explícito sobre el mensaje de error retornado.

---

## Patrón en controllers

Todos los endpoints protegidos siguen el mismo patrón:

```csharp
try
{
    await _guard.VerificarActivaAsync(dto.SucursalId);
}
catch (SuscripcionVencidaException ex)
{
    return StatusCode(402, new { message = ex.Message });
}
```

Para feature gates:

```csharp
try
{
    await _guard.VerificarTieneReportesAsync(sucursalId);
}
catch (FeatureNoPlanException ex)
{
    return StatusCode(403, new { message = ex.Message });
}
```

---

## Endpoints protegidos

### HTTP 402 — suscripción vencida

| Controller | Endpoint | Guard call |
|---|---|---|
| `TurnosController` | `POST /api/turnos` | `VerificarActivaAsync(dto.SucursalId)` |
| `ServiciosController` | `POST /api/servicios` | `VerificarActivaAsync(dto.SucursalId)` |
| `UsuariosController` | `POST /api/usuarios` | `VerificarActivaAsync(dto.SucursalId)` |

### HTTP 403 — feature no incluida en el plan

| Controller | Endpoints | Guard call |
|---|---|---|
| `ReportsController` | `GET /api/reportes/resumen`, `/turnos`, `/ingresos` | `VerificarTieneReportesAsync(sucursalId)` |

---

## Feature gate: `TieneReportes`

El acceso a reportes se controla mediante el campo `Plan.TieneReportes` (bool). El SuperAdmin puede activarlo/desactivarlo por plan desde `/superadmin/planes` sin re-deployar.

**Planes base:**

| Plan | TieneReportes |
|---|---|
| Básico | `false` |
| Pro | `true` |
| Enterprise | `true` |

> El valor se estableció con una sentencia `UPDATE` directa al aplicar la migración `AgregaTieneReportes`. No se usa seed data de EF para permitir edición libre desde el panel.

Si en el futuro se agregan más features premium (exportaciones, API access, etc.), el patrón es:
1. Agregar un `bool` a `Plan` (ej: `TieneExportaciones`)
2. Agregar un método `VerificarTieneXxxAsync` en `SuscripcionGuard`
3. Llamarlo desde los controllers correspondientes
4. Exponer el toggle en el modal de edición de planes en el frontend

---

## Mensajes de error por estado

`VerificarActivaAsync` devuelve mensajes específicos según el estado:

| Estado | Mensaje |
|---|---|
| `PruebVencida` | `"Tu período de prueba venció. Activá tu suscripción para continuar creando contenido."` |
| `Cancelada` | `"Tu suscripción fue cancelada. Renovála para continuar."` |
| `Vencida` | `"Tu suscripción venció. Renovála para continuar."` |

`VerificarTieneReportesAsync` incluye el nombre del plan en el mensaje:

> `"Los reportes no están incluidos en el plan Básico. Actualizá tu plan para acceder a esta funcionalidad."`

---

## Frontend — manejo de errores

### `Reportes.tsx`

```typescript
} catch (err) {
  if (err instanceof ApiError && err.status === 403) {
    setPlanInsuficiente(true);
  } else {
    setError('No se pudieron cargar los reportes.');
  }
}
```

Cuando `planInsuficiente === true`, la página reemplaza el contenido con una pantalla de upgrade:

```
Ícono de gráfico bloqueado
"Los reportes están disponibles en el plan Pro"
Descripción del plan Pro y sus beneficios
Botón "Ver planes" → (a definir: enlace a pantalla de suscripción)
```

### Para endpoints con 402

Los controllers de Turnos, Servicios y Usuarios devolverán 402. El frontend de esas secciones debería manejar este caso mostrando un aviso de suscripción vencida. Actualmente la UI muestra el mensaje de error genérico del `catch`; se puede mejorar diferenciando `err.status === 402` en el futuro.

---

## Archivos involucrados

### Backend

| Archivo | Responsabilidad |
|---|---|
| `src/Application/Exceptions/SuscripcionVencidaException.cs` | Excepción para suscripción vencida → 402 |
| `src/Application/Exceptions/FeatureNoPlanException.cs` | Excepción para feature no incluida → 403 |
| `src/Application/Services/SuscripcionGuard.cs` | Servicio con `VerificarActivaAsync` y `VerificarTieneReportesAsync` |
| `src/Application/Services/TurnoService.cs` | Guard inyectado (`SuscripcionGuard?` nullable); llama `VerificarActivaAsync` como paso 1 de `CreateAsync` |
| `src/Application/Services/ServicioService.cs` | Guard inyectado (requerido); llama `VerificarActivaAsync` como paso 1 de `CreateAsync` |
| `src/Application/Services/UsuarioService.cs` | Guard inyectado (requerido); llama `VerificarActivaAsync` como paso 1 de `CreateAsync` |
| `src/Api/Controllers/TurnosController.cs` | Guard en `POST /api/turnos` (primera línea, antes de llamar al service) |
| `src/Api/Controllers/ServiciosController.cs` | Guard en `POST /api/servicios` (primera línea, antes de llamar al service) |
| `src/Api/Controllers/UsuariosController.cs` | Guard en `POST /api/usuarios` (primera línea, antes de llamar al service) |
| `src/Api/Controllers/ReportsController.cs` | Guard en los 3 endpoints de reportes |
| `src/Api/Program.cs` | `AddScoped<SuscripcionGuard>()`; factories de `TurnoService` y `UsuarioService` pasan el guard explícitamente |
| `src/Domain/Entities/Plan.cs` | `TieneReportes bool` |

### Frontend

| Archivo | Responsabilidad |
|---|---|
| `src/pages/admin/Reportes.tsx` | Estado `planInsuficiente`, pantalla de upgrade en 403 |

---

## Notas de diseño

- **Permisivo por omisión.** Las sucursales sin `SucursalSuscripcion` nunca son bloqueadas. Esto permite hacer pruebas locales sin crear suscripciones y garantiza retrocompatibilidad con datos de desarrollo.
- **El guard no se aplica a lecturas.** Las consultas GET (listar turnos, ver agenda, ver clientes) nunca son bloqueadas aunque la suscripción esté vencida. La filosofía es: el negocio puede ver su historial, pero no puede seguir operando.
- **Sin middleware global.** Se prefirió try/catch explícito por controller sobre un `IExceptionFilter` global para mantener trazabilidad directa del flujo y evitar casos edge con otros tipos de `InvalidOperationException`.
- **`ISuscripcionRepository.GetBySucursalAsync`** hace eager loading del `Plan` asociado (`.Include(s => s.Plan)`) para que `VerificarTieneReportesAsync` pueda acceder a `Plan.TieneReportes` sin una segunda query.
