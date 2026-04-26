# Tests unitarios — Seguridad

Tests unitarios que verifican las correcciones de seguridad aplicadas en las Fases 1–4 del hardening.

**Proyecto:** `tests/TurneroApp.Tests/`
**Carpeta:** `Security/`
**Framework:** xUnit 2.6 + Moq 4.20 + FluentAssertions 6.12

---

## Archivos y hallazgos cubiertos

| Archivo | Tests | Hallazgo |
|---|---|---|
| `OverpostingTests.cs` | 3 | M-01 — DTOs sin campos sensibles |
| `DtoValidacionesTests.cs` | 8 | M-02 — DataAnnotations en DTOs |
| `HashingContrasenaTests.cs` | 2 | C-03/C-04 — BCrypt en SuperAdminService |
| `HorarioFormatoTests.cs` | 6 | B-03 — `TimeSpan.TryParse` en horarios |
| `TokenResetUrlTests.cs` | 2 | B-04 — Token de reset en fragment (`#`) |

---

## `OverpostingTests` — M-01

### Contexto

Los DTOs de actualización (`UpdateServicioDto`, `UpdateSucursalDto`) no deben incluir los campos `Activo` / `Activa`. Si los incluyeran, un cliente podría desactivar entidades vía `PUT` sin pasar por los endpoints específicos de borrado lógico.

### Test 26 — `UpdateServicioDto` sin propiedad `Activo`

```
UpdateServicioDto_PropiedadActivoNoExiste
```

Usa reflection: `typeof(UpdateServicioDto).GetProperty("Activo")`.

**Assert:** El resultado es `null` — la propiedad no existe en el DTO.

---

### Test 27 — `UpdateSucursalDto` sin propiedad `Activa`

```
UpdateSucursalDto_PropiedadActivaNoExiste
```

**Assert:** `typeof(UpdateSucursalDto).GetProperty("Activa") == null`.

---

### Test complementario — JSON con campo `activo` es ignorado

```
UpdateServicioDto_CampoActivoEnJson_SeIgnora
```

Deserializa un JSON que incluye `"activo": false` en un `UpdateServicioDto`.

**Assert:**
- El DTO se deserializa sin errores
- Los campos válidos (`Nombre`, etc.) se mapean correctamente
- El campo `activo` se ignora silenciosamente (no hay binding)

---

## `DtoValidacionesTests` — M-02

### Contexto

Los DTOs de creación tienen atributos `[Required]`, `[MaxLength]`, `[Range]` que ASP.NET Core valida antes de invocar el controller. Estos tests verifican que las anotaciones estén correctamente definidas usando `Validator.TryValidateObject` directamente.

> **Nota sobre `[Required]` en tipos no-nulos:** Para campos `Guid` y `DateTime` (tipos de valor no-nulos), `[Required]` no es validable via DataAnnotations — esa validación ocurre en el model binding de ASP.NET Core. Los tests se enfocan en campos `string` y tipos numéricos.

### Tests de `CreateTurnoDto`

| Test | Campo | Regla | Condición |
|---|---|---|---|
| Test 28 | `Observaciones` | `[MaxLength(500)]` | 501 caracteres → falla |
| Complementario | `Observaciones` | `[MaxLength(500)]` | 500 caracteres → pasa |

---

### Tests de `CreateClienteDto`

| Test | Campo | Regla | Condición |
|---|---|---|---|
| Test 29 | `Nombre` | `[Required]` | Vacío `""` → falla |
| Complementario | `Apellido` | `[Required]` | Vacío `""` → falla |

---

### Tests de `CreateServicioDto`

| Test | Campo | Regla | Condición |
|---|---|---|---|
| Test 30 | `Precio` | `[Range(0, 9999999.99)]` | `-1` → falla |
| Test 31 | `DuracionClienteMin` | `[Range(1, 1440)]` | `0` → falla |
| Test 31b | `DuracionAgendaMin` | `[Range(1, 1440)]` | `0` → falla |

---

## `HashingContrasenaTests` — C-03 / C-04

### Contexto

`SuperAdminService` debe hashear contraseñas con BCrypt antes de persistirlas. Antes del fix, las contraseñas podían guardarse en texto plano.

**SUT:** `SuperAdminService` (instanciado con 5 repositorios mockeados)

**Patrón técnico — Callback para capturar la entidad:**
Los métodos `BCrypt.Net.BCrypt.Verify(password, hash)` tienen parámetros opcionales (work factor). Las expression trees de Moq no permiten llamadas con parámetros opcionales, por lo que la verificación se hace fuera del `It.Is<T>`:

```csharp
Usuario? ownerCapturado = null;

_usuarioRepo.Setup(r => r.AddAsync(It.IsAny<Usuario>()))
    .Callback<Usuario>(u => ownerCapturado = u)
    .Returns(Task.CompletedTask);

// Luego, en el assert:
BCrypt.Net.BCrypt.Verify(passwordPlano, ownerCapturado!.PasswordHash)
    .Should().BeTrue();
```

---

### Test 20 — C-04: `CrearConOwnerAsync` hashea la contraseña del owner

```
CrearConOwner_PasswordOwner_SeGuardaHasheada
```

**Arrange:** `CrearSucursalConOwnerDto` con `OwnerPassword = "ClaveTextoPlano123!"`. Mocks configurados para `AddAsync` de `Sucursal` y `Usuario` (el segundo captura el owner via Callback).

**Act:** `_sut.CrearConOwnerAsync(dto)`

**Assert:**
- `ownerCapturado.PasswordHash ≠ "ClaveTextoPlano123!"` — no en texto plano
- `BCrypt.Verify("ClaveTextoPlano123!", ownerCapturado.PasswordHash) == true` — hash válido

---

### Test 21 — C-03: `SincronizarPasswordAsync` hashea la nueva contraseña

```
SincronizarPassword_NuevaContrasena_SeGuardaHasheada
```

**Arrange:** `Usuario` con un hash BCrypt de una clave vieja. `GetByEmailGlobalAsync` devuelve ese usuario.

**Act:** `_sut.SincronizarPasswordAsync("u@t.com", "NuevaClave!")`

**Assert:**
- `usuario.PasswordHash ≠ "NuevaClave!"` — no en texto plano
- `BCrypt.Verify("NuevaClave!", usuario.PasswordHash) == true`

---

## `HorarioFormatoTests` — B-03

### Contexto

`UsuarioService.ReplaceHorariosAsync` usa `TimeSpan.TryParse` para parsear los strings de hora. Antes del fix usaba `TimeSpan.Parse`, que lanzaba `FormatException` para inputs inválidos. `FormatException` no es capturada por los controllers → producía `500 Internal Server Error`. Con `TryParse`, se lanza `InvalidOperationException` (manejable → `400 Bad Request`).

**SUT:** `UsuarioService` (instanciado con 11 dependencias mockeadas)

**Detalle técnico — enumeración lazy:**
`ReplaceHorariosAsync` usa `dtos.Select(d => { if (!TryParse) throw; ... })` — un LINQ lazy. El mock de `_horarioRepo.ReplaceAsync` por defecto retorna `Task.CompletedTask` sin enumerar la secuencia, por lo que la excepción no se dispara. El mock se configura con un `Callback` que llama `.ToList()` para forzar la evaluación:

```csharp
_horarioRepo
    .Setup(r => r.ReplaceAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<HorarioLaboral>>()))
    .Returns<Guid, IEnumerable<HorarioLaboral>>((usrId, horarios) =>
    {
        horarios.ToList(); // fuerza la enumeración → dispara la excepción
        return Task.CompletedTask;
    });
```

---

### Test 23 — Horas inválidas en `HoraDesde` → `InvalidOperationException`

```
ReplaceHorarios_HoraInvalida_LanzaInvalidOperationException [Theory]
```

**InlineData:** `"25:00"` (hora fuera de rango), `"texto"` (string arbitrario), `"10:99"` (minutos inválidos), `""` (vacío).

**Assert:**
- Lanza `InvalidOperationException` (no `FormatException`)
- Mensaje contiene `"Formato de hora inválido"`

---

### Test 23b — Hora inválida en `HoraHasta` → `InvalidOperationException`

```
ReplaceHorarios_HoraHastaInvalida_LanzaInvalidOperationException
```

Misma verificación pero para el campo `HoraHasta`. Confirma que la validación aplica a ambos campos del DTO.

---

### Test 24 — Horas válidas no lanzan excepción

```
ReplaceHorarios_HoraValida_NoLanzaExcepcion [Theory]
```

**InlineData:** `("09:00", "18:00")`, `("00:00", "23:59")`, `("08:30", "17:30")`.

**Assert:** `NotThrowAsync()` — el servicio procesa horas bien formateadas sin error.

---

## `TokenResetUrlTests` — B-04

### Contexto

El link de recuperación de contraseña enviado por email debe usar el fragment HTTP (`#token=...`) y no un query param (`?token=...`). Los fragments nunca son enviados al servidor por el browser, por lo que no aparecen en access logs ni en el header `Referer` hacia terceros (analytics, CDN).

**SUT:** `RecuperacionContrasenaService` configurado con base URL `"https://app.metaturnos.com"`.

---

### Test 25 — El link de recuperación usa `#token=`

```
SolicitarRecuperacion_LinkEnEmail_UsaFragment
```

**Arrange:** `IEmailService` mockeado con un `Callback<string, string, string>` que captura el tercer argumento (el link).

**Act:** `_sut.SolicitarAsync("user@t.com")`

**Assert:**
- `linkCapturado.Contains("#token=")` → usa fragment
- `!linkCapturado.Contains("?token=")` → no usa query param

---

### Test 25b — Email no existente → no envía email (respuesta silenciosa)

```
SolicitarRecuperacion_EmailNoExistente_NoEnviaEmail
```

`GetByEmailGlobalAsync` retorna `null`.

**Assert:** `_emailService.Verify(EnviarRecuperacionContrasenaAsync, Times.Never)` — el servicio no revela si el email está registrado o no.
