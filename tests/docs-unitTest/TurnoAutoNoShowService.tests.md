# Tests unitarios — TurnoAutoNoShowService

## Descripción general

Tests que verifican el comportamiento del BackgroundService que marca automáticamente como `NoShow` los turnos `Confirmado` o `Pendiente` cuya `FechaHoraFin` sea anterior a la medianoche del día actual.

Los tests son completamente aislados: sin base de datos, sin acceso a disco, sin dependencias de infraestructura. Todo el scope de DI (scopeFactory → scope → serviceProvider → repositorio) se simula con mocks.

**Proyecto:** `tests/TurneroApp.Tests/`
**Archivo de tests:** `tests/TurneroApp.Tests/Services/TurnoAutoNoShowServiceTests.cs`
**Framework:** xUnit 2.6 + Moq 4.20 + FluentAssertions 6.12
**Cobertura:** 11 tests sobre `EjecutarAsync` (método de negocio interno del servicio)

---

## Estructura de archivos

```
tests/
├── docs/
│   ├── TurnoService.tests.md
│   └── TurnoAutoNoShowService.tests.md    ← este archivo
└── TurneroApp.Tests/
    ├── TurneroApp.Tests.csproj
    ├── Helpers/
    │   └── TurnoTestFactory.cs            ← fábrica de entidades de prueba
    └── Services/
        ├── TurnoServiceCreateTests.cs
        ├── TurnoServiceUpdateTests.cs
        ├── TurnoServiceComentariosTests.cs
        ├── AdjuntoTurnoServiceTests.cs
        └── TurnoAutoNoShowServiceTests.cs  ← nueva suite
```

---

## Requisito previo: detener la API antes de correr los tests

> **Importante en Windows:** `dotnet watch run` mantiene un file lock sobre el `TurneroApp.Api.dll`
> mientras el proceso esté activo. MSBuild no puede recompilar ese dll durante el lock,
> por lo que `dotnet test` fallará con `MSB3027` si la API está corriendo.

**En CI/CD** (GitHub Actions, Azure DevOps, etc.) la API no está corriendo durante los tests, por lo que este paso no aplica.

---

## Cómo ejecutar

### Flujo completo (primera vez o cuando cambia código)

```bash
# 1. Detener dotnet watch run (Ctrl+C en la terminal de la API)

# 2. Ir al directorio backend
cd "C:/JUAN RUIZ/TurneroAPP/backend"

# 3. Correr solo los tests de esta suite
dotnet test tests/TurneroApp.Tests/TurneroApp.Tests.csproj \
  --filter "TurnoAutoNoShowServiceTests" \
  --verbosity normal

# 4. Volver a levantar la API
dotnet watch run --project src/Api
```

Resultado esperado: **11 passed, 0 failed** en menos de 2 segundos (solo esta suite).

### Correr toda la suite de tests

```bash
dotnet test tests/TurneroApp.Tests/TurneroApp.Tests.csproj --verbosity normal
```

Resultado esperado: **83 passed, 0 failed** (43 TurnoService + 11 TurnoAutoNoShow + 16 PagoTurnoService + 11 TurnoPagoExpiration + 2 restantes = total aproximado).

### Filtros útiles por categoría

```bash
# Solo los tests de scope de DI
dotnet test tests/TurneroApp.Tests/TurneroApp.Tests.csproj \
  --filter "FullyQualifiedName~TurnoAutoNoShowServiceTests&FullyQualifiedName~Scope"

# Solo los tests de manejo de errores
dotnet test tests/TurneroApp.Tests/TurneroApp.Tests.csproj \
  --filter "FullyQualifiedName~TurnoAutoNoShowServiceTests&FullyQualifiedName~Excepcion"

# Solo los tests de estado NoShow
dotnet test tests/TurneroApp.Tests/TurneroApp.Tests.csproj \
  --filter "FullyQualifiedName~TurnoAutoNoShowServiceTests&FullyQualifiedName~Turnos"
```

### Con reporte de cobertura (requiere coverlet)

```bash
dotnet test tests/TurneroApp.Tests/TurneroApp.Tests.csproj \
  --filter "TurnoAutoNoShowServiceTests" \
  --collect:"XPlat Code Coverage"
```

---

## Cambios realizados en código de producción

Para poder testear el método interno sin bloquear el bucle del BackgroundService
(que puede esperar hasta 24 h antes de la primera ejecución), se realizaron
tres cambios mínimos:

| Archivo | Cambio |
|---|---|
| `src/Api/Services/TurnoAutoNoShowService.cs` | `private async Task EjecutarAsync` → `internal async Task EjecutarAsync` |
| `src/Api/TurneroApp.Api.csproj` | Agrega `[assembly: InternalsVisibleTo("TurneroApp.Tests")]` vía MSBuild attribute |
| `tests/TurneroApp.Tests/TurneroApp.Tests.csproj` | Agrega `<ProjectReference>` al proyecto Api con `UseAppHost=false` |

> `UseAppHost=false` evita que MSBuild intente copiar el `.exe` del host al compilar
> la referencia desde el proyecto de tests, reduciendo el impacto del file lock en Windows.

---

## Por qué no se usa `StartAsync`

`BackgroundService.StartAsync` invoca `ExecuteAsync`, que tiene este flujo:

```csharp
while (!stoppingToken.IsCancellationRequested)
{
    var espera = proximas3AM - DateTime.Now; // puede ser hasta ~24 horas
    await Task.Delay(espera, stoppingToken); // ← bloquea hasta las 3:00 AM
    await EjecutarAsync(stoppingToken);
}
```

Incluso cancelando el token en 50 ms, el test solo verificaría que el loop termina,
nunca que `EjecutarAsync` se ejecuta. Por eso se invoca `EjecutarAsync` directamente
como método `internal`.

---

## Mocks configurados

El constructor de la clase de tests configura la cadena completa que el servicio
recorre al hacer `_scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ITurnoRepository>()`:

```
Mock<IServiceScopeFactory>
  └─ CreateScope() → Mock<IServiceScope>
        └─ ServiceProvider → Mock<IServiceProvider>
              └─ GetService(typeof(ITurnoRepository)) → Mock<ITurnoRepository>
```

`GetRequiredService<T>()` es una extension method que internamente llama
`IServiceProvider.GetService(typeof(T))`, por lo que el mock de `IServiceProvider`
intercepta la llamada correctamente.

| Mock | Tipo | Qué se mockea |
|---|---|---|
| `IServiceScopeFactory` | DI | `CreateScope()` devuelve el scope mock |
| `IServiceScope` | DI | `ServiceProvider` + `Dispose()` |
| `IServiceProvider` | DI | `GetService(typeof(ITurnoRepository))` |
| `ITurnoRepository` | Repositorio | `GetConfirmadosPendientesAntesDeAsync`, `UpdateRange`, `SaveChangesAsync` |
| `ILogger<TurnoAutoNoShowService>` | Logging | Sin verificaciones (el logger no es el SUT) |

---

## Descripción de los tests

### Sin turnos pendientes

| Test | Qué verifica |
|---|---|
| `Ejecutar_SinTurnosPendientes_NoLlamaAUpdateRange` | Si el repo retorna lista vacía, `UpdateRange` nunca se llama |
| `Ejecutar_SinTurnosPendientes_NoLlamaASaveChanges` | Si el repo retorna lista vacía, `SaveChangesAsync` nunca se llama |

### Estado de turnos

| Test | Qué verifica |
|---|---|
| `Ejecutar_ConTurnosPendientes_ActualizaEstadoANoShow` | Turnos `Confirmado` y `Pendiente` pasan a `EstadoTurno.NoShow` |

### Llamadas al repositorio

| Test | Qué verifica |
|---|---|
| `Ejecutar_ConTurnosPendientes_LlamaUpdateRangeUnaVez` | Con 3 turnos retornados, `UpdateRange` se llama exactamente 1 vez |
| `Ejecutar_ConTurnosPendientes_LlamaSaveChangesUnaVez` | `SaveChangesAsync` se llama exactamente 1 vez (una transacción) |
| `Ejecutar_ConTurnosPendientes_UpdateRangeRecibeTodosLosTurnos` | El argumento de `UpdateRange` contiene todos los turnos retornados por el repo |

### Fecha límite

| Test | Qué verifica |
|---|---|
| `Ejecutar_FechaLimite_EsMedianocheDeDiaActual` | `GetConfirmadosPendientesAntesDeAsync` recibe `DateTime.Today` con `TimeOfDay == 00:00:00` |

### Manejo de errores

| Test | Qué verifica |
|---|---|
| `Ejecutar_CuandoRepoLanzaExcepcion_NoPropagarElError` | Si el repo lanza, el método completa sin re-lanzar (el `try/catch` interno absorbe el error) |
| `Ejecutar_CuandoSaveChangesLanzaExcepcion_NoPropagarElError` | Si `SaveChangesAsync` lanza, el método completa sin re-lanzar |

### Ciclo de vida del scope

| Test | Qué verifica |
|---|---|
| `Ejecutar_SiempreCreaExactamenteUnScope` | `CreateScope()` se llama exactamente 1 vez por ejecución |
| `Ejecutar_DisposeDelScopeSeLlamaAlFinalizar` | El scope se libera al finalizar gracias al `using var` |
| `Ejecutar_CuandoExcepcion_ElScopeSigueDisposed` | Incluso cuando hay excepción, el `using` garantiza que `Dispose()` se llame |

---

## Reglas de negocio cubiertas

| Regla de negocio | Test(s) que la cubren |
|---|---|
| Solo se marcan turnos `Confirmado` o `Pendiente` (el filtro lo hace el repo) | Todos los tests de "Con turnos pendientes" |
| La fecha límite es la medianoche del día actual (excluye los turnos de hoy) | `Ejecutar_FechaLimite_EsMedianocheDeDiaActual` |
| Si no hay turnos que marcar, no se persiste nada | `NoLlamaAUpdateRange`, `NoLlamaASaveChanges` |
| Todos los turnos del lote se actualizan en una sola llamada | `LlamaUpdateRangeUnaVez`, `LlamaSaveChangesUnaVez` |
| Un error en la DB no derrumba el host (BackgroundService sigue vivo) | `CuandoRepoLanzaExcepcion_NoPropagarElError`, `CuandoSaveChangesLanzaExcepcion_NoPropagarElError` |
| El scope de DI siempre se libera (evita memory leaks) | `DisposeDelScopeSeLlamaAlFinalizar`, `CuandoExcepcion_ElScopeSigueDisposed` |

---

## Comportamiento no cubierto (fuera de alcance unitario)

| Comportamiento | Por qué no se testea aquí |
|---|---|
| El servicio se ejecuta a las 3:00 AM | Depende de `DateTime.Now` y `Task.Delay` reales — requeriría test de integración con tiempo simulado |
| El loop reinicia después de cada ejecución | Idéntico motivo: el delay es de infrastructure real |
| Los logs se emiten correctamente | El logger se mockea pero no se verifica; los logs son observabilidad, no lógica de negocio |
