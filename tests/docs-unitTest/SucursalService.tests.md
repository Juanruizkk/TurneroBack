# Tests unitarios — SucursalService

## Descripción general

Tests que verifican la lógica de `SucursalService.CreateAsync()` de forma completamente aislada: sin base de datos, sin ninguna dependencia de infraestructura.

**Proyecto:** `tests/TurneroApp.Tests/`
**Framework:** xUnit 2.6 + Moq 4.20 + FluentAssertions 6.12
**Cobertura:** 5 tests — creación de sucursal con trial automático

---

## Contexto

Desde 2026-04-03, `SucursalService.CreateAsync` crea tres entidades en una sola transacción:

1. `Sucursal` — la sucursal en sí
2. `Caja` — caja asociada a la sucursal
3. `SucursalSuscripcion` — suscripción con `Estado = PruebActiva`, `EnPeriodoPrueba = true`

El período de prueba se calcula desde el plan base (`GetPlanBasicoAsync`) usando `DuracionTrialDias`.

---

## Estructura de archivos

```
tests/
├── docs/
│   └── SucursalService.tests.md       ← este archivo
└── TurneroApp.Tests/
    ├── Helpers/
    │   └── SuscripcionTestFactory.cs  ← fábrica de planes (DuracionTrialDias = 14 por defecto)
    └── Services/
        └── SucursalServiceCreateTests.cs   (5 tests)
```

---

## Cómo ejecutar

```bash
cd backend
dotnet test tests/TurneroApp.Tests/TurneroApp.Tests.csproj --verbosity normal
```

Para filtrar solo estos tests:

```bash
dotnet test --filter "FullyQualifiedName~SucursalServiceCreateTests"
```

---

## Dependencias mockeadas

`SucursalService` recibe cuatro dependencias en el constructor:

| Dependencia | Tipo | Qué se mockea |
|---|---|---|
| `ISucursalRepository` | Repositorio | `GetBySlugAsync` → `null` (slug siempre disponible), `AddAsync`, `SaveChangesAsync` |
| `ICajaRepository` | Repositorio | `AddAsync` |
| `IAdjuntoService` | Servicio | No se configura (no se invoca en `CreateAsync`) |
| `ISuscripcionRepository` | Repositorio | `GetPlanBasicoAsync` → plan de prueba; `AddAsync` |

---

## Helpers

### `SuscripcionTestFactory.CrearPlan()`

Crea un `Plan` con `DuracionTrialDias = 14` por defecto. Parámetros opcionales: `maxUsuarios`, `maxTurnosMes`, `maxServicios`, `tieneReportes`, `nombre`.

---

## SucursalServiceCreateTests

### Tabla de casos

| # | Test | Verifica |
|---|---|---|
| 1 | `CreateAsync_CreaSucursalCajaYSuscripcionTrial` | `AddAsync` llamado para `Sucursal`, `Caja` y `SucursalSuscripcion` |
| 2 | `CreateAsync_SuscripcionEsTrial` | `Estado = PruebActiva` y `EnPeriodoPrueba = true` |
| 3 | `CreateAsync_FechaVencimiento_Correcta` | `FechaVencimiento ≈ FechaInicio + DuracionTrialDias` (tolerancia ±1 s) |
| 4 | `CreateAsync_SinPlanBase_LanzaException` | `GetPlanBasicoAsync` retorna `null` → lanza `InvalidOperationException` |
| 5 | `CreateAsync_SaveChangesAsync_LlamadoUnaVez` | Un solo `SaveChangesAsync` al final (todos los `AddAsync` son en memoria) |

### Detalle — test 3 (fecha de vencimiento)

```csharp
var antes = DateTime.UtcNow;
await _sut.CreateAsync(CrearDto());
var despues = DateTime.UtcNow;

suscripcionCapturada.FechaVencimiento
    .Should().BeOnOrAfter(antes.AddDays(plan.DuracionTrialDias))
    .And.BeOnOrBefore(despues.AddDays(plan.DuracionTrialDias));
```

Captura el argumento con un `Callback` de Moq sobre `_suscRepo.Setup(r => r.AddAsync(...))`.

### Detalle — test 4 (sin plan base)

```csharp
_suscRepo.Setup(r => r.GetPlanBasicoAsync()).ReturnsAsync((Plan?)null);

var act = async () => await _sut.CreateAsync(CrearDto());
await act.Should().ThrowAsync<InvalidOperationException>()
    .WithMessage("*plan base*");
```

---

## Reglas de negocio cubiertas

| Regla | Test |
|---|---|
| Siempre se crean sucursal + caja + suscripción en una sola llamada | `CreateAsync_CreaSucursalCajaYSuscripcionTrial` |
| La suscripción nace en `PruebActiva` con `EnPeriodoPrueba = true` | `CreateAsync_SuscripcionEsTrial` |
| `FechaVencimiento = FechaCreacion + Plan.DuracionTrialDias` | `CreateAsync_FechaVencimiento_Correcta` |
| Sin plan base → la sucursal no puede crearse | `CreateAsync_SinPlanBase_LanzaException` |
| Un solo `SaveChangesAsync` al final (transacción atómica) | `CreateAsync_SaveChangesAsync_LlamadoUnaVez` |

---

## Relación con otros docs

- `docs/Arquitecture/planes-superadmin.md` — sección **El trial NO es un plan** — contexto del modelo de suscripción
- `docs/Arquitecture/restricciones-plan.md` — cómo se usa `SucursalSuscripcion` para restringir operaciones
- `tests/docs/ServicioService.tests.md` — validación equivalente de `MaxServicios` en `ServicioService`
