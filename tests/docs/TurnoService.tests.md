# Tests unitarios — TurnoService y AdjuntoTurnoService

## Descripción general

Tests que verifican la lógica de negocio de `TurnoService` y `AdjuntoTurnoService` de forma completamente aislada: sin base de datos, sin SMTP, sin Cloudinary, sin ninguna dependencia de infraestructura.

**Proyecto:** `tests/TurneroApp.Tests/`
**Framework:** xUnit 2.6 + Moq 4.20 + FluentAssertions 6.12
**Cobertura:** 40 tests — `CreateAsync`, `UpdateEstadoAsync`, `CrearComentarioAsync`, `EliminarComentarioAsync`, `ListarAsync`, `SubirAsync`, `EliminarAsync`

---

## Estructura de archivos

```
tests/
├── docs/
│   └── TurnoService.tests.md          ← este archivo
└── TurneroApp.Tests/
    ├── TurneroApp.Tests.csproj
    ├── Helpers/
    │   └── TurnoTestFactory.cs        ← fábrica de entidades de prueba
    └── Services/
        ├── TurnoServiceCreateTests.cs          (15 tests)
        ├── TurnoServiceUpdateTests.cs          (8 tests)
        ├── TurnoServiceComentariosTests.cs     (6 tests)
        └── AdjuntoTurnoServiceTests.cs         (11 tests)
```

---

## Cómo ejecutar

```bash
cd backend
dotnet test tests/TurneroApp.Tests/TurneroApp.Tests.csproj --verbosity normal
```

Resultado esperado: **40 passed, 0 failed** en ~3 segundos.

---

## Dependencias mockeadas

### TurnoService (6 mocks)

Todos los tests instancian `TurnoService` con mocks de sus 6 dependencias:

| Dependencia | Tipo | Qué se mockea |
|---|---|---|
| `ITurnoRepository` | Repositorio | CRUD, conflictos, carga con relaciones |
| `IServicioRepository` | Repositorio | Búsqueda por Id |
| `IClienteRepository` | Repositorio | Búsqueda por Id |
| `ISucursalRepository` | Repositorio | Config de confirmación, datos para emails |
| `IEmailService` | Servicio externo | Envío de confirmaciones y cancelaciones |
| `IComentarioTurnoRepository` | Repositorio | CRUD de comentarios |

### AdjuntoTurnoService (2 mocks)

| Dependencia | Tipo | Qué se mockea |
|---|---|---|
| `IAdjuntoTurnoRepository` | Repositorio | CRUD de adjuntos |
| `IAdjuntoService` | Servicio externo | Subida y eliminación en Cloudinary |

---

## Helpers — `TurnoTestFactory`

Clase estática interna con métodos para construir entidades con valores por defecto sensatos. Evita repetición en cada test.

| Método | Descripción |
|---|---|
| `CrearServicio(...)` | `Servicio` con duración 30 min, precio $5000, sin seña |
| `CrearCliente(email?)` | `Cliente` con email opcional, por defecto `cliente@test.com` |
| `CrearSucursal(email?)` | `Sucursal` básica sin configuración |
| `CrearSucursalConConfig(modo)` | `Sucursal` con `ConfiguracionSucursal` y modo de confirmación |
| `CrearDto(...)` | `CreateTurnoDto` con todos los Ids generados y fecha futura |
| `CrearTurnoConRelaciones(...)` | `Turno` con `Cliente` y `Servicio` cargados (simula carga EF) |

---

## TurnoServiceCreateTests — `CreateAsync`

Patrón de cada test: **Arrange / Act / Assert** con mocks de repositorios.

### Tests de validación

| Test | Qué verifica |
|---|---|
| `Create_ServicioNoEncontrado_LanzaException` | Si `GetByIdAsync(servicioId)` retorna `null`, lanza `InvalidOperationException` |
| `Create_ClienteNoEncontrado_LanzaException` | Si servicio OK pero `GetByIdAsync(clienteId)` retorna `null`, lanza `InvalidOperationException` |
| `Create_ConflictoHorario_LanzaException` | Si `ExisteConflictoHorarioAsync` retorna `true`, lanza `InvalidOperationException` con mensaje que contiene "horario" |

### Tests de cálculo

| Test | Qué verifica |
|---|---|
| `Create_CalculaFechaHoraFin_Correctamente` | `FechaHoraFin == FechaHoraInicio + DuracionAgendaMin` (probado con 45 min) |
| `Create_SinSena_MontoSenaEsNull` | `RequiereSena = false` → `MontoSena == null` |
| `Create_SenaFija_MontoSenaIgualAlValor` | `TipoSena.Fija`, `ValorSena = 2000` → `MontoSena == 2000` |
| `Create_SenaPorcentaje_CalculaCorrectamente` | `TipoSena.Porcentaje`, precio = $10.000, porcentaje = 30% → `MontoSena == 3000` |

### Tests de estado inicial

| Test | Qué verifica |
|---|---|
| `Create_OrigenWeb_ModoManual_EstadoPendiente` | Origen `Web` + modo `Manual` → `Estado == Pendiente` |
| `Create_OrigenWeb_ModoAutomatico_EstadoConfirmado` | Origen `Web` + modo `Automatico` → `Estado == Confirmado` |
| `Create_OrigenAdministrativo_SiempreConfirmado` | Origen `Administrativo` → `Estado == Confirmado` aunque el modo sea `Manual` |

### Tests de token y happy path

| Test | Qué verifica |
|---|---|
| `Create_SinConflicto_RetornaDto` | Happy path completo: retorna `TurnoResponseDto` con `Id` no vacío |
| `Create_TokenGestion_EsHexDe32Caracteres` | `TokenGestion.Length == 32` y solo contiene `[0-9a-f]` (resultado de `Guid.ToString("N")`) |

### Tests de email (no-crítico)

| Test | Qué verifica |
|---|---|
| `Create_ClienteConEmail_EnviaEmailConfirmacion` | Si el cliente tiene email, `EnviarConfirmacionTurnoAsync` se llama exactamente 1 vez |
| `Create_ClienteSinEmail_NoEnviaEmail` | Si `cliente.Email == null`, `EnviarConfirmacionTurnoAsync` nunca se llama |
| `Create_EmailFalla_NoLanzaException` | Si el email service lanza una excepción, `CreateAsync` igual completa sin error (el email es no-crítico) |

---

## TurnoServiceUpdateTests — `UpdateEstadoAsync`

### Tests básicos

| Test | Qué verifica |
|---|---|
| `Update_TurnoNoEncontrado_RetornaNull` | Si `GetByIdConRelacionesAsync` retorna `null`, el método retorna `null` |
| `Update_CambiaEstado_Correctamente` | El DTO devuelto refleja el nuevo `Estado` (ej: `Completado`) |

### Tests de cancelación y emails

| Test | Qué verifica |
|---|---|
| `Update_CanceladoStaff_EnviaEmailAlCliente` | Estado `CanceladoStaff` → `EnviarCancelacionStaffAsync` se llama 1 vez |
| `Update_CanceladoCliente_EnviaEmailAlClienteYAlProfesional` | Estado `CanceladoCliente` → `EnviarCancelacionClienteAsync` + `EnviarAlertaLiberacionAgendaAsync` ambos llamados 1 vez |
| `Update_EmailCancelacion_Falla_NoLanzaException` | Si el email de cancelación lanza excepción, el método igual completa (email no-crítico) |

### Tests de reprogramación

| Test | Qué verifica |
|---|---|
| `Update_Reprogramar_RecalculaFechaHoraFin` | Al pasar `FechaHoraInicio + ProfesionalId`, la nueva `FechaHoraFin == nuevaFecha + duracion` |
| `Update_Reprogramar_ConConflicto_LanzaException` | Si `ExisteConflictoHorarioAsync` retorna `true` al reprogramar, lanza `InvalidOperationException` |
| `Update_SoloProfesional_SinNuevaFecha_CambiaProf` | Solo `ProfesionalId` en el dto (sin nueva fecha) → actualiza el profesional sin tocar las fechas |

---

## TurnoServiceComentariosTests — `CrearComentarioAsync` / `EliminarComentarioAsync`

### CrearComentario

| Test | Qué verifica |
|---|---|
| `CrearComentario_TextoVacio_LanzaException` (`""`) | Texto vacío lanza `InvalidOperationException` |
| `CrearComentario_TextoVacio_LanzaException` (`"   "`) | Texto solo espacios lanza `InvalidOperationException` |
| `CrearComentario_TurnoNoExiste_LanzaException` | Si `GetByIdAsync(turnoId)` retorna `null`, lanza `InvalidOperationException` |
| `CrearComentario_TurnoExiste_CreaNuevoComentario` | Happy path: retorna `ComentarioTurnoResponseDto` con `TurnoId` correcto |

### EliminarComentario

| Test | Qué verifica |
|---|---|
| `EliminarComentario_DeOtroAutor_LanzaException` | Si `comentario.AutorUsuarioId != autorId`, lanza `InvalidOperationException` |
| `EliminarComentario_NoExiste_RetornaFalse` | Si `GetByIdAsync(comentarioId)` retorna `null`, retorna `false` |

---

---

## AdjuntoTurnoServiceTests — `ListarAsync` / `SubirAsync` / `EliminarAsync`

Patrón de cada test: **Arrange / Act / Assert** con mocks de `IAdjuntoTurnoRepository` e `IAdjuntoService` (Cloudinary).

### ListarAsync

| Test | Qué verifica |
|---|---|
| `Listar_SinAdjuntos_RetornaListaVacia` | Si el repositorio retorna lista vacía, el resultado también es vacío |
| `Listar_ConAdjuntos_MapeoEsCorrecto` | Todos los campos del `AdjuntoTurno` se mapean correctamente al DTO (`Id`, `TurnoId`, `Url`, `PublicId`, `NombreArchivo`, `TipoRecurso`) |

### SubirAsync

| Test | Qué verifica |
|---|---|
| `Subir_Imagen_RetornaDtoConDatosDeCloudinary` | El DTO devuelto refleja la `Url`, `PublicId` y `TipoRecurso` retornados por Cloudinary |
| `Subir_Pdf_TipoRecursoEsRaw` | Con `contentType = "application/pdf"`, el DTO tiene `TipoRecurso == "raw"` |
| `Subir_PersisteDespuesDeCloudinary` | El orden de llamadas es estrictamente: `SubirArchivoAsync` → `AddAsync` → `SaveChangesAsync` |
| `Subir_CloudinaryFalla_NoPersiste` | Si `SubirArchivoAsync` lanza excepción, `AddAsync` y `SaveChangesAsync` nunca se llaman |

### EliminarAsync

| Test | Qué verifica |
|---|---|
| `Eliminar_AdjuntoNoExiste_RetornaFalse` | Si `GetByIdAsync` retorna `null`, el método retorna `false` y Cloudinary nunca es llamado |
| `Eliminar_AdjuntoExiste_RetornaTrue` | Happy path: el método retorna `true` |
| `Eliminar_LlamaCloudinaryConPublicIdYTipoRecursoCorrectos` | Cloudinary recibe exactamente el `PublicId` y `TipoRecurso` almacenados en la entidad |
| `Eliminar_BorraDbDespuesDeCloudinary` | El orden es estrictamente: `EliminarArchivoAsync` → `SaveChangesAsync` |
| `Eliminar_CloudinaryFalla_NoEliminaDeDb` | Si Cloudinary lanza excepción, `Delete` y `SaveChangesAsync` nunca se llaman (el registro queda en DB para reintentar) |

---

## Reglas de negocio cubiertas

| Regla | Test(s) |
|---|---|
| Conflicto de horario impide crear o reprogramar | `Create_ConflictoHorario`, `Update_Reprogramar_ConConflicto` |
| `FechaHoraFin` se calcula automáticamente | `Create_CalculaFechaHoraFin`, `Update_Reprogramar_RecalculaFechaHoraFin` |
| Seña fija vs. seña porcentaje | `Create_SenaFija`, `Create_SenaPorcentaje` |
| Estado inicial depende del origen y modo de confirmación | `Create_OrigenWeb_ModoManual`, `Create_OrigenWeb_ModoAutomatico`, `Create_OrigenAdministrativo` |
| `TokenGestion` es un hex de 32 caracteres | `Create_TokenGestion` |
| Emails son no-críticos (nunca bloquean la operación) | `Create_EmailFalla`, `Update_EmailCancelacion_Falla` |
| `CanceladoCliente` notifica al profesional o a la sucursal | `Update_CanceladoCliente_EnviaEmailAlClienteYAlProfesional` |
| Solo el autor puede eliminar su comentario | `EliminarComentario_DeOtroAutor_LanzaException` |
| Cloudinary se llama antes de persistir en DB (subida) | `Subir_PersisteDespuesDeCloudinary` |
| Si Cloudinary falla al subir, la DB no se toca | `Subir_CloudinaryFalla_NoPersiste` |
| El tipo de recurso (`image`/`raw`) se preserva y se usa al borrar | `Subir_Pdf_TipoRecursoEsRaw`, `Eliminar_LlamaCloudinaryConPublicIdYTipoRecursoCorrectos` |
| Cloudinary se borra antes que la DB (eliminación) | `Eliminar_BorraDbDespuesDeCloudinary` |
| Si Cloudinary falla al borrar, el registro queda en DB | `Eliminar_CloudinaryFalla_NoEliminaDeDb` |
