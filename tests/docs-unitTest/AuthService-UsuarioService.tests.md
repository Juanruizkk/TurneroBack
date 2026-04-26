# Tests unitarios — AuthService y UsuarioService

## Descripción general

Tests que verifican la lógica de autenticación y alta de usuarios de forma completamente aislada: sin base de datos, sin SMTP, sin tokens JWT reales, sin ninguna dependencia de infraestructura.

**Proyecto:** `tests/TurneroApp.Tests/`
**Framework:** xUnit 2.6 + Moq 4.20 + FluentAssertions 6.12
**Cobertura:** `AuthServiceTests` (7 — incluye C-05) + `UsuarioServiceCreateTests` (8 — incluye límites de plan y guard de suscripción) + `UsuarioServiceDeleteTests` + `UsuarioServiceSuscripcionTests` (2)

---

## Estructura de archivos

```
tests/
├── docs/
│   └── AuthService-UsuarioService.tests.md    ← este archivo
└── TurneroApp.Tests/
    ├── Helpers/
    │   ├── TurnoTestFactory.cs
    │   └── UsuarioTestFactory.cs              ← nuevo helper (entidades de usuarios)
    └── Services/
        ├── AuthServiceTests.cs                  (6 tests)
        ├── UsuarioServiceCreateTests.cs         (8 tests — incluye límites de plan)
        ├── UsuarioServiceDeleteTests.cs         (tests de baja)
        └── UsuarioServiceSuscripcionTests.cs    (2 tests — guard de suscripción)
```

---

## Cómo ejecutar

```bash
cd "C:/JUAN RUIZ/TurneroAPP/backend"

# Solo los tests de esta suite
dotnet test tests/TurneroApp.Tests/TurneroApp.Tests.csproj \
  --filter "FullyQualifiedName~AuthServiceTests|FullyQualifiedName~UsuarioServiceCreateTests"

# Todo el proyecto de tests
dotnet test tests/TurneroApp.Tests/TurneroApp.Tests.csproj
```

Resultado esperado: **165 passed, 0 failed**.

> **Nota Windows:** Si `dotnet watch run` está activo, detenerlo con Ctrl+C antes de correr los tests (el proceso bloquea el DLL de `TurneroApp.Api`).

---

## Problema técnico clave: nav props null en tests

### El problema

En `UsuarioService.CreateAsync`, el código construye un `UsuarioSucursalRol` en memoria y luego llama a `MapToResponse(usr)`, que accede a `usr.Usuario.Nombre` y `usr.Sucursal.Nombre`. En producción, EF Core asigna esas nav props automáticamente por change tracking. En tests con mocks, ese mecanismo no existe: `usr.Usuario` es `null` y el test lanza `NullReferenceException`.

### La solución: Moq `Callback`

Se usa `Callback<T>` en el `Setup` de `AddAsync` para inyectar las nav props antes de que `MapToResponse` sea invocado:

```csharp
Usuario? usuarioCapturado = null;

_usuarioRepo.Setup(r => r.AddAsync(It.IsAny<Usuario>()))
    .Callback<Usuario>(u => usuarioCapturado = u)
    .Returns(Task.CompletedTask);

_usrRepo.Setup(r => r.AddAsync(It.IsAny<UsuarioSucursalRol>()))
    .Callback<UsuarioSucursalRol>(usr =>
    {
        usr.Usuario  = usuarioCapturado!;  // wires nav prop antes de MapToResponse
        usr.Sucursal = sucursal;
    })
    .Returns(Task.CompletedTask);
```

El callback de `_usrRepo.AddAsync` referencia `usuarioCapturado`, que ya fue asignado por el callback de `_usuarioRepo.AddAsync` (llamado primero en `CreateAsync`). El orden de callbacks sigue el orden de ejecución del código de producción.

---

## Helper — `UsuarioTestFactory`

Clase estática interna análoga a `TurnoTestFactory`, que construye entidades de usuario con todas las nav props correctamente hydratadas.

| Método | Descripción |
|---|---|
| `CrearUsuario(email?, password?)` | `Usuario` con `Id = NewGuid()`, `Email`, `PasswordHash = password` |
| `CrearUSR(usuario?, estado?, sucursalId?, rol?)` | `UsuarioSucursalRol` con `Usuario`, `Sucursal` y `Permisos = []` ya asignados |
| `CrearSucursal(nombre?)` | `Sucursal` mínima con `Id = NewGuid()` y `Nombre` |
| `CrearCreateDto(sucursalId?, email?)` | `CreateUsuarioDto` válido con `Permisos = []` y `Rol = Profesional` |

---

## Mocks — `AuthServiceTests`

`AuthService` recibe 4 parámetros en el constructor: dos repositorios y las credenciales del Super Admin como `string`.

| Dependencia | Tipo | Qué se mockea |
|---|---|---|
| `IUsuarioRepository` | Repositorio | `GetByEmailAsync`, `Update`, `SaveChangesAsync` |
| `IUsuarioSucursalRolRepository` | Repositorio | `GetByUsuarioAsync` |

Las credenciales SA (`"sa@test.com"` / `"SuperPass!"`) se pasan directamente como strings al constructor del SUT.

---

## Tests: `AuthServiceTests` — `ValidarAsync` (6 tests)

### Caso Super Admin

| Test | Qué verifica |
|---|---|
| `ValidarAsync_SuperAdmin_CredencialesCorrectas_RetornaDirectamente` | Email y password coinciden con la config SA → `resultado.Usuario!.EsSuperAdmin == true`; ningún repositorio es consultado |
| `ValidarAsync_SuperAdmin_PasswordIncorrecta_LanzaUnauthorized` *(C-05)* | Email SA correcto pero password incorrecto → lanza `UnauthorizedAccessException`. Verifica que el servicio usa BCrypt (tiempo constante) y no el operador `==` (vulnerable a timing attacks) |

### Casos de credenciales inválidas

| Test | Qué verifica |
|---|---|
| `ValidarAsync_EmailNoExiste_LanzaUnauthorized` | `GetByEmailAsync` retorna `null` → lanza `UnauthorizedAccessException` con mensaje que contiene `"Credenciales"` |
| `ValidarAsync_PasswordIncorrecta_LanzaUnauthorized` | Email existe pero `PasswordHash ≠ dto.Password` → lanza `UnauthorizedAccessException` |

### Casos de sucursales activas

| Test | Qué verifica |
|---|---|
| `ValidarAsync_SinSucursalesActivas_LanzaUnauthorized` | Credenciales correctas pero `GetByUsuarioAsync` retorna `[]` → lanza con mensaje `"no tiene sucursales activas"` |
| `ValidarAsync_UnaSucursalActiva_RetornaLoginDirecto` | 1 USR activo → `RequiereSeleccion == false`, `Usuario != null`, `Email` y `Rol` correctos |
| `ValidarAsync_MultiplesSucursalesActivas_RetornaSeleccionConToken` | 2 USRs activos → `RequiereSeleccion == true`, `Opciones.Count == 2`, `SeleccionToken` no vacío; se verifica que `Update` y `SaveChangesAsync` fueron llamados |

---

## Mocks — `UsuarioServiceCreateTests`

`UsuarioService` recibe 11 parámetros en el constructor.

| Dependencia | Tipo | Qué se mockea |
|---|---|---|
| `IUsuarioRepository` | Repositorio | `GetByEmailAsync`, `AddAsync` |
| `IUsuarioSucursalRolRepository` | Repositorio | `GetByUsuarioYSucursalAsync`, `AddAsync`, `SaveChangesAsync`, `ContarActivosBySucursalAsync` |
| `ISucursalRepository` | Repositorio | `GetByIdAsync` |
| `IEmailService` | Servicio externo | `EnviarInvitacionAsync`, `EnviarInvitacionUsuarioExistenteAsync` |
| `IHorarioLaboralRepository` | Repositorio | No invocado en `CreateAsync`; mock vacío requerido por el constructor |
| `IUsuarioServicioRepository` | Repositorio | No invocado en `CreateAsync`; mock vacío requerido por el constructor |
| `ISuscripcionRepository` | Repositorio | `GetBySucursalAsync` — devuelve `null` por defecto (sin restricción) |
| `SuscripcionGuard` | Servicio de aplicación | Se construye con `new SuscripcionGuard(_suscripcionRepo.Object)`; comparte el mismo mock |
| `ILogger<UsuarioService>` | Logging | `Mock.Of<ILogger<UsuarioService>>()` — sin verificaciones |

---

## Tests: `UsuarioServiceSuscripcionTests` — `CreateAsync` (2 tests)

### Estado de suscripción (`SuscripcionGuard`)

| Test | Escenario | Resultado esperado |
|---|---|---|
| `CreateAsync_SuscripcionVencida_LanzaSuscripcionVencidaException` | Suscripción en estado `PruebVencida` | Lanza `SuscripcionVencidaException` antes de validar permisos o límites |
| `CreateAsync_SuscripcionVencida_NoLlamaAddAsync` | Suscripción en estado `PruebVencida` | `_usrRepo.AddAsync` nunca es invocado |

---

## Tests: `UsuarioServiceCreateTests` — `CreateAsync` (8 tests)

### Usuario nuevo (email no existe)

| Test | Qué verifica |
|---|---|
| `CreateAsync_EmailNuevo_CreaUsuarioYUSR` | `_usuarioRepo.AddAsync` se llama 1 vez; `_usrRepo.AddAsync` se llama 1 vez |
| `CreateAsync_EmailNuevo_EnviaEmailEstandar` | `EnviarInvitacionAsync` se llama 1 vez; `EnviarInvitacionUsuarioExistenteAsync` nunca se llama |
| `CreateAsync_EmailNuevo_PasswordHashEsVacio` | El `Usuario` creado tiene `PasswordHash == string.Empty` (la contraseña la elige el usuario al aceptar la invitación) |

### Usuario existente (email ya registrado)

| Test | Qué verifica |
|---|---|
| `CreateAsync_EmailExistente_SoloCreaUSR_YEnviaEmailExistente` | `_usuarioRepo.AddAsync` nunca se llama; `EnviarInvitacionUsuarioExistenteAsync` se llama 1 vez; `EnviarInvitacionAsync` nunca se llama |
| `CreateAsync_EmailExistente_YaMiembroDeSucursal_LanzaException` | `GetByUsuarioYSucursalAsync` retorna USR existente → lanza `InvalidOperationException` con mensaje `"ya es miembro"` |

---

## Reglas de negocio cubiertas

| Regla | Test(s) |
|---|---|
| Suscripción vencida bloquea antes de validar permisos | `CreateAsync_SuscripcionVencida_LanzaSuscripcionVencidaException` |
| Suscripción vencida no llega a persistir membresía | `CreateAsync_SuscripcionVencida_NoLlamaAddAsync` |
| El Super Admin bypasea toda la lógica de repositorios | `ValidarAsync_SuperAdmin_CredencialesCorrectas_*` |
| SA con contraseña incorrecta lanza `UnauthorizedAccessException` *(C-05)* | `ValidarAsync_SuperAdmin_PasswordIncorrecta_LanzaUnauthorized` |
| Credenciales incorrectas siempre lanzan `UnauthorizedAccessException` | `ValidarAsync_EmailNoExiste`, `ValidarAsync_PasswordIncorrecta` |
| Sin sucursales activas no se puede iniciar sesión | `ValidarAsync_SinSucursalesActivas` |
| 1 sucursal → login directo sin paso extra | `ValidarAsync_UnaSucursalActiva` |
| 2+ sucursales → selección con token temporal (guardado en DB) | `ValidarAsync_MultiplesSucursalesActivas` |
| Sin suscripción → sin restricción de usuarios | `CreateAsync_SinSuscripcion_PermiteCrearUsuario` |
| `usados < MaxUsuarios` → permite crear | `CreateAsync_ConPlanActivo_DebajoDeLimite_PermiteCrear` |
| `usados >= MaxUsuarios` → lanza excepción con nombre del plan | `CreateAsync_ConPlanActivo_LimiteSuperado_LanzaException` |
| Usuario nuevo: identidad global + membresía creadas; email de invitación estándar | `CreateAsync_EmailNuevo_*` |
| `PasswordHash` vacío al crear; el usuario lo elige al aceptar la invitación | `CreateAsync_EmailNuevo_PasswordHashEsVacio` |
| Usuario existente: solo se crea la membresía; email diferenciado para usuarios ya registrados | `CreateAsync_EmailExistente_SoloCreaUSR_*` |
| No se permite duplicar membresías en la misma sucursal | `CreateAsync_EmailExistente_YaMiembroDeSucursal_LanzaException` |
