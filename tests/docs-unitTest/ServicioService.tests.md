# Tests unitarios — ServicioService

## Descripción general

Tests que verifican la lógica de negocio de `ServicioService.CreateAsync()` de forma completamente aislada: sin base de datos, sin ninguna dependencia de infraestructura.

**Proyecto:** `tests/TurneroApp.Tests/`
**Framework:** xUnit 2.6 + Moq 4.20 + FluentAssertions 6.12
**Cobertura:** 6 tests — validación del estado de suscripción (`SuscripcionGuard`) y del límite de servicios por plan (`MaxServicios`)

---

## Estructura de archivos

```
tests/
├── docs/
│   └── ServicioService.tests.md              ← este archivo
└── TurneroApp.Tests/
    ├── Helpers/
    │   └── SuscripcionTestFactory.cs         ← fábrica de planes y suscripciones
    └── Services/
        ├── ServicioServiceCreateTests.cs     (4 tests — límites por plan)
        └── ServicioServiceSuscripcionTests.cs (2 tests — guard de suscripción)
```

---

## Cómo ejecutar

```bash
cd backend
dotnet test tests/TurneroApp.Tests/TurneroApp.Tests.csproj --verbosity normal
```

Resultado esperado: **165 passed, 0 failed**.

Para filtrar solo estos tests:

```bash
dotnet test --filter "FullyQualifiedName~ServicioService"
```

---

## Dependencias mockeadas

`ServicioService` recibe tres dependencias en el constructor:

| Dependencia | Tipo | Qué se mockea |
|---|---|---|
| `IServicioRepository` | Repositorio | `ContarActivosBySucursalAsync`, `AddAsync`, `SaveChangesAsync` |
| `ISuscripcionRepository` | Repositorio | `GetBySucursalAsync` — devuelve `null` por defecto (sin restricción) |
| `SuscripcionGuard` | Servicio de aplicación | Se construye con `new SuscripcionGuard(_suscripcionRepo.Object)`; comparte el mismo mock de `ISuscripcionRepository` |

---

## Helpers

### `SuscripcionTestFactory`

Clase estática interna con métodos para construir planes y suscripciones de prueba.

| Método | Firma relevante | Descripción |
|---|---|---|
| `CrearPlan(...)` | `int? maxServicios = null` | Crea un `Plan` con los límites dados. `null` = ilimitado |
| `CrearSuscripcion(plan?)` | — | Crea una `SucursalSuscripcion` con el plan asociado |

### Setup por defecto — `SetupCrearOk()`

```csharp
_suscripcionRepo.Setup(r => r.GetBySucursalAsync(It.IsAny<Guid>()))
    .ReturnsAsync((SucursalSuscripcion?)null);   // sin suscripción → sin restricción

_servicioRepo.Setup(r => r.AddAsync(It.IsAny<Servicio>())).Returns(Task.CompletedTask);
_servicioRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
```

---

## ServicioServiceCreateTests — `CreateAsync`

### Límite de servicios por plan

| Test | Escenario | Resultado esperado |
|---|---|---|
| `Crear_SinSuscripcion_PermiteCrear` | `GetBySucursalAsync` devuelve `null` | Crea OK — retorna `ServicioResponseDto` no nulo |
| `Crear_MaxServiciosNull_Ilimitado_Permite` | Plan con `MaxServicios = null` | Crea OK — `null` significa ilimitado |
| `Crear_DebajoDeLimiteServicios_Permite` | 4 servicios activos, límite 5 | Crea OK — `4 < 5` no bloquea |
| `Crear_LimiteServiciosSuperado_LanzaException` | 5 servicios activos, límite 5 | Lanza `InvalidOperationException` con nombre del plan |

### Detalle del test de excepción

```csharp
var plan = SuscripcionTestFactory.CrearPlan(maxServicios: 5, nombre: "Básico");
_servicioRepo.Setup(r => r.ContarActivosBySucursalAsync(sucursalId)).ReturnsAsync(5); // 5 >= 5

await _sut.Invoking(s => s.CreateAsync(dto))
    .Should().ThrowAsync<InvalidOperationException>()
    .WithMessage("*Básico*");
```

El mensaje completo es:
> `"Tu plan Básico permite hasta 5 servicios. Actualizá tu suscripción para agregar más."`

---

## ServicioServiceSuscripcionTests — `CreateAsync`

### Estado de suscripción (`SuscripcionGuard`)

| Test | Escenario | Resultado esperado |
|---|---|---|
| `CreateAsync_SuscripcionVencida_LanzaSuscripcionVencidaException` | Suscripción en estado `PruebVencida` | Lanza `SuscripcionVencidaException` antes de cualquier otra validación |
| `CreateAsync_SuscripcionVencida_NoLlamaAddAsync` | Suscripción en estado `PruebVencida` | `_servicioRepo.AddAsync` nunca es invocado |

---

## Reglas de negocio cubiertas

| Regla | Test |
|---|---|
| Suscripción vencida bloquea la creación | `CreateAsync_SuscripcionVencida_LanzaSuscripcionVencidaException` |
| Suscripción vencida no llega a persistir | `CreateAsync_SuscripcionVencida_NoLlamaAddAsync` |
| Sin suscripción → sin restricción | `Crear_SinSuscripcion_PermiteCrear` |
| `MaxServicios = null` → ilimitado | `Crear_MaxServiciosNull_Ilimitado_Permite` |
| `actuales < limite` → permite | `Crear_DebajoDeLimiteServicios_Permite` |
| `actuales >= limite` → lanza excepción con nombre del plan | `Crear_LimiteServiciosSuperado_LanzaException` |

---

## Relación con otras restricciones del plan

La validación de `MaxServicios` es parte del sistema de límites numéricos por plan. Ver la tabla completa en:

- `docs/Arquitecture/restricciones-plan.md` — sección **Límites numéricos por plan**
- `tests/docs/TurnoService.tests.md` — para la validación equivalente de `MaxTurnosMes`
- `tests/docs/AuthService-UsuarioService.tests.md` — para la validación de `MaxUsuarios`
