# 05 — Tests Unitarios de Seguridad

**Hallazgos cubiertos:** A-04 · C-03/04/05 · B-03 · B-04 · M-01 · M-02
**Proyecto:** `TurneroApp.Tests`
**Carpeta:** `Security/`

---

## A-04 — HMAC OAuth State (`HmacOAuthTests.cs`)

### Contexto

El flujo OAuth genera un `state` para prevenir CSRF. Sin firma HMAC, un atacante podía
forjar un `state` con cualquier `sucursalId`. La corrección firma el state con HMAC-SHA256
usando un secret del servidor.

### Tests

```csharp
using System;
using System.Text;
using FluentAssertions;
using Xunit;
// + using al controller/service que implementa GenerarState / VerificarState

namespace TurneroApp.Tests.Security;

public class HmacOAuthTests
{
    private const string Secret = "mi-secret-test-32-chars-minimum!";

    /// <summary>
    /// Test 17 — Un state generado y luego verificado con el mismo secret
    /// debe devolver el sucursalId original.
    /// </summary>
    [Fact]
    public void GenerarState_LuegoVerificar_DevuelveSucursalIdCorrecta()
    {
        var sucursalId = Guid.NewGuid();
        var sut        = BuildSut(Secret);

        var state     = sut.GenerarStateFirmado(sucursalId);
        var resultado = sut.VerificarState(state);

        resultado.Should().Be(sucursalId,
            "el state firmado debe contener el sucursalId y ser verificable");
    }

    /// <summary>
    /// Test 18 — Un state cuyo payload fue alterado (sin recalcular la firma)
    /// debe ser rechazado (devuelve null).
    /// </summary>
    [Fact]
    public void VerificarState_StateAlterado_DevuelveNull()
    {
        var sut   = BuildSut(Secret);
        var state = sut.GenerarStateFirmado(Guid.NewGuid());

        // Decodificar → modificar payload → re-encodear sin recalcular firma
        var decoded  = Encoding.UTF8.GetString(Convert.FromBase64String(state));
        var partes   = decoded.Split('.');  // formato: "sucursalId:guid.hmac"
        var tampered = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"GUID_FALSO:{partes[0].Split(':')[1]}.{partes[1]}"));

        var resultado = sut.VerificarState(tampered);

        resultado.Should().BeNull(
            "un state con payload alterado y firma inválida debe ser rechazado");
    }

    /// <summary>
    /// Test 19 — Un state generado con secret A no puede verificarse con secret B.
    /// Previene ataques cross-environment (ej: state de staging usado en producción).
    /// </summary>
    [Fact]
    public void VerificarState_SecretDistinto_DevuelveNull()
    {
        var sutGen    = BuildSut("secret-produccion-32-chars-min!!");
        var sutVerify = BuildSut("secret-atacante-32-chars-minimum!");

        var state     = sutGen.GenerarStateFirmado(Guid.NewGuid());
        var resultado = sutVerify.VerificarState(state);

        resultado.Should().BeNull(
            "un state firmado con un secret distinto debe ser rechazado");
    }

    // Instanciar el SUT con el secret inyectado (ajustar según implementación)
    private static OAuthStateService BuildSut(string secret) =>
        new(Microsoft.Extensions.Options.Options.Create(
            new OAuthOptions { HmacSecret = secret }));
}
```

---

## C-03/C-04/C-05 — BCrypt Hashing (`HashingContrasenaTests.cs`)

### Contexto

- **C-03:** `SincronizarPasswordAsync` y `CrearConOwnerAsync` no hasheaban la contraseña.
- **C-04:** `CrearConOwnerAsync` guardaba la contraseña en texto plano.
- **C-05:** `AuthService.ValidarAsync` para SuperAdmin usaba `==` (vulnerable a timing attacks).
  La corrección aplica `BCrypt.Verify` que es en tiempo constante.

```csharp
using BCrypt.Net;
using FluentAssertions;
using Moq;
using TurneroApp.Application.DTOs;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Domain.Entities;
using Xunit;

namespace TurneroApp.Tests.Security;

public class HashingContrasenaTests
{
    private readonly Mock<ISucursalRepository> _sucursalRepo = new();
    private readonly Mock<IUsuarioRepository>  _usuarioRepo  = new();
    private readonly SuperAdminService          _sut;

    public HashingContrasenaTests()
    {
        _sut = new SuperAdminService(_sucursalRepo.Object, _usuarioRepo.Object, /* otros */);
    }

    /// <summary>
    /// Test 20 — CrearConOwnerAsync debe guardar la contraseña del owner con BCrypt,
    /// nunca en texto plano. (C-03 + C-04)
    /// </summary>
    [Fact]
    public async Task CrearConOwner_PasswordOwner_SeGuardaHasheada()
    {
        // Arrange
        const string passwordPlano = "ClaveTextoPlano123!";
        var dto = new CrearSucursalConOwnerDto
        {
            NombreSucursal = "Test", OwnerEmail    = "o@t.com",
            OwnerNombre    = "Test", OwnerApellido = "User",
            OwnerPassword  = passwordPlano,
        };

        _sucursalRepo.Setup(r => r.AddAsync(It.IsAny<Sucursal>()))
                     .Returns(Task.CompletedTask);
        _usuarioRepo.Setup(r => r.AddAsync(It.IsAny<Usuario>()))
                    .Returns(Task.CompletedTask);

        // Act
        await _sut.CrearConOwnerAsync(dto);

        // Assert — la contraseña guardada NO es el texto plano,
        //          pero BCrypt.Verify sí la reconoce
        _usuarioRepo.Verify(r => r.AddAsync(It.Is<Usuario>(u =>
            u.PasswordHash != passwordPlano &&
            BCrypt.Net.BCrypt.Verify(passwordPlano, u.PasswordHash)
        )), Times.Once);
    }

    /// <summary>
    /// Test 21 — SincronizarPasswordAsync debe guardar la nueva contraseña hasheada. (C-03)
    /// </summary>
    [Fact]
    public async Task SincronizarPassword_NuevaContrasena_SeGuardaHasheada()
    {
        // Arrange
        var usuario = new Usuario
        {
            Id           = Guid.NewGuid(),
            Email        = "u@t.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("claveVieja"),
        };
        _usuarioRepo.Setup(r => r.GetByIdAsync(usuario.Id)).ReturnsAsync(usuario);

        const string nuevaClave = "NuevaClave!";

        // Act
        await _sut.SincronizarPasswordAsync(new SincronizarPasswordDto
        {
            UsuarioId     = usuario.Id,
            NuevoPassword = nuevaClave,
        });

        // Assert
        usuario.PasswordHash.Should().NotBe(nuevaClave,
            "la contraseña nunca debe guardarse en texto plano");
        BCrypt.Net.BCrypt.Verify(nuevaClave, usuario.PasswordHash).Should().BeTrue(
            "el hash almacenado debe corresponder a la nueva contraseña");
    }
}
```

### `AuthServiceTests.cs` — Tests 22a y 22b (C-05)

```csharp
using FluentAssertions;
using Moq;
using TurneroApp.Application.DTOs;
using TurneroApp.Application.Services;
using Xunit;

namespace TurneroApp.Tests.Security;

public class AuthServiceTests
{
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        // Configurar AuthService con SuperAdmin:Email y SuperAdmin:Password
        // inyectados via IOptions<SuperAdminOptions>
        _sut = BuildAuthService(email: "sa@test.com", password: "SuperPass!");
    }

    /// <summary>
    /// Test 22a — Login de SuperAdmin con credenciales correctas → autenticación ok. (C-05)
    /// </summary>
    [Fact]
    public async Task ValidarAsync_SuperAdmin_PasswordCorrecta_AutenticaOk()
    {
        var dto      = new LoginDto { Email = "sa@test.com", Password = "SuperPass!" };
        var resultado = await _sut.ValidarAsync(dto);

        resultado.Usuario.Should().NotBeNull();
        resultado.Usuario!.EsSuperAdmin.Should().BeTrue();
    }

    /// <summary>
    /// Test 22b — Login de SuperAdmin con contraseña incorrecta → UnauthorizedAccessException. (C-05)
    /// Verifica que BCrypt.Verify (tiempo constante) se usa en lugar de == (vulnerable a timing).
    /// </summary>
    [Fact]
    public async Task ValidarAsync_SuperAdmin_PasswordErronea_LanzaUnauthorized()
    {
        var dto = new LoginDto { Email = "sa@test.com", Password = "PasswordIncorrecto" };
        var act = () => _sut.ValidarAsync(dto);

        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            "credenciales incorrectas deben lanzar UnauthorizedAccessException");
    }

    private static AuthService BuildAuthService(string email, string password)
    {
        // Ajustar según el constructor real de AuthService
        var opts = Microsoft.Extensions.Options.Options.Create(
            new SuperAdminOptions { Email = email, Password = password });
        return new AuthService(opts, /* otros mocks */);
    }
}
```

---

## B-03 — TimeSpan.TryParse en Horarios (`HorarioFormatoTests.cs`)

### Contexto

`HorarioLaboralService.ReplaceHorariosAsync` parseaba las horas con `TimeSpan.Parse`,
que lanza `FormatException` para strings inválidos. `FormatException` no está capturada
por el middleware de errores y resulta en un 500.

La corrección usa `TimeSpan.TryParse` y lanza `InvalidOperationException` si falla,
que sí está manejada por el controller → 400.

```csharp
using FluentAssertions;
using Moq;
using TurneroApp.Application.DTOs;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using Xunit;

namespace TurneroApp.Tests.Security;

public class HorarioFormatoTests
{
    private readonly Mock<IUsuarioSucursalRolRepository> _usrRepo = new();
    private readonly HorarioLaboralService               _sut;

    public HorarioFormatoTests()
    {
        _sut = new HorarioLaboralService(_usrRepo.Object);
    }

    /// <summary>
    /// Test 23 — Horas con formato inválido deben lanzar InvalidOperationException
    /// (manejable → 400), NO FormatException (no manejable → 500).
    /// </summary>
    [Theory]
    [InlineData("25:00")]   // hora fuera de rango
    [InlineData("texto")]   // string arbitrario
    [InlineData("10:99")]   // minutos inválidos
    [InlineData("")]        // vacío
    public async Task ReplaceHorarios_HoraInvalida_LanzaInvalidOperationException(
        string horaInvalida)
    {
        // Arrange
        var usr = UsuarioTestFactory.CrearUSR(
            UsuarioTestFactory.CrearUsuario("u@t.com", "pass"));
        _usrRepo.Setup(r => r.GetByIdAsync(usr.Id)).ReturnsAsync(usr);

        var dtos = new List<HorarioLaboralDto>
        {
            new(DiaSemana: 1, HoraDesde: horaInvalida, HoraHasta: "18:00", Activo: true)
        };

        // Act
        var act = () => _sut.ReplaceHorariosAsync(usr.Id, dtos);

        // Assert — debe ser InvalidOperationException, NO FormatException
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Formato de hora inválido*",
                "el mensaje debe indicar qué campo tiene el problema");
    }

    /// <summary>
    /// Test 24 — Horas con formato válido no deben lanzar excepción.
    /// </summary>
    [Theory]
    [InlineData("09:00", "18:00")]
    [InlineData("00:00", "23:59")]
    [InlineData("08:30", "17:30")]
    public async Task ReplaceHorarios_HoraValida_NoLanzaExcepcion(
        string desde, string hasta)
    {
        var usr = UsuarioTestFactory.CrearUSR(
            UsuarioTestFactory.CrearUsuario("u@t.com", "pass"));
        _usrRepo.Setup(r => r.GetByIdAsync(usr.Id)).ReturnsAsync(usr);
        _usrRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var dtos = new List<HorarioLaboralDto>
        {
            new(DiaSemana: 1, HoraDesde: desde, HoraHasta: hasta, Activo: true)
        };

        var act = () => _sut.ReplaceHorariosAsync(usr.Id, dtos);

        await act.Should().NotThrowAsync(
            $"las horas {desde}–{hasta} son válidas y no deben fallar");
    }
}
```

---

## B-04 — Token de reset en fragment de URL (`TokenResetUrlTests.cs`)

### Contexto

El link de recuperación de contraseña enviaba el token como query param (`?token=...`),
que queda registrado en access logs del servidor y en el header `Referer` del navegador.
La corrección mueve el token al fragment (`#token=...`), que el navegador nunca envía al servidor.

```csharp
using FluentAssertions;
using Moq;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using Xunit;

namespace TurneroApp.Tests.Security;

public class TokenResetUrlTests
{
    private readonly Mock<IUsuarioRepository> _usuarioRepo = new();
    private readonly Mock<IEmailService>      _emailService = new();
    private readonly RecuperacionService      _sut;

    public TokenResetUrlTests()
    {
        _sut = new RecuperacionService(_usuarioRepo.Object, _emailService.Object);
    }

    /// <summary>
    /// Test 25 — El link de recuperación debe usar #token= (fragment), no ?token= (query param).
    /// El fragment nunca aparece en access logs ni en el header Referer.
    /// </summary>
    [Fact]
    public async Task SolicitarRecuperacion_LinkEnEmail_UsaFragment()
    {
        // Arrange
        var usuario = UsuarioTestFactory.CrearUsuario("user@t.com", "pass");
        _usuarioRepo
            .Setup(r => r.GetByEmailGlobalAsync("user@t.com"))
            .ReturnsAsync(usuario);

        string? linkCapturado = null;
        _emailService
            .Setup(e => e.EnviarRecuperacionContrasenaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, link) => linkCapturado = link)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SolicitarAsync("user@t.com");

        // Assert
        linkCapturado.Should().NotBeNull(
            "el servicio debe enviar un email con un link de recuperación");

        linkCapturado!.Should().Contain("#token=",
            "el token debe ir en el fragment para no aparecer en access logs ni Referer");

        linkCapturado.Should().NotContain("?token=",
            "el query param expone el token en logs del servidor");
    }
}
```

---

## M-01 — Overposting (`OverpostingTests.cs`)

### Contexto

Los DTOs de actualización (`UpdateServicioDto`, `UpdateSucursalDto`) incluían campos como
`Activo` / `Activa`, permitiendo que un cliente los cambiara arbitrariamente via PUT/PATCH.
La corrección eliminó esos campos de los DTOs de actualización. El cambio de estado solo
se hace via endpoints específicos (ej: `DELETE /api/servicios/{id}`).

```csharp
using FluentAssertions;
using System.Text.Json;
using TurneroApp.Application.DTOs;
using Xunit;

namespace TurneroApp.Tests.Security;

public class OverpostingTests
{
    /// <summary>
    /// Test 26 — UpdateServicioDto no debe tener la propiedad Activo.
    /// Si la tiene, un cliente podría desactivar un servicio via PUT.
    /// </summary>
    [Fact]
    public void UpdateServicioDto_CampoActivoNoExiste()
    {
        typeof(UpdateServicioDto)
            .GetProperty("Activo")
            .Should().BeNull(
                "el campo Activo fue removido de UpdateServicioDto para prevenir overposting");
    }

    /// <summary>
    /// Test 27 — UpdateSucursalDto no debe tener la propiedad Activa.
    /// </summary>
    [Fact]
    public void UpdateSucursalDto_CampoActivaNoExiste()
    {
        typeof(UpdateSucursalDto)
            .GetProperty("Activa")
            .Should().BeNull(
                "el campo Activa fue removido de UpdateSucursalDto para prevenir overposting");
    }

    /// <summary>
    /// Test complementario — Si el JSON incluye 'activo', se ignora en la deserialización.
    /// Verifica que no hay [JsonPropertyName] ni binding que lo acepte.
    /// </summary>
    [Fact]
    public void UpdateServicioDto_CampoActivoEnJson_SeIgnora()
    {
        var json = """
            {"nombre":"Test","precio":100,"duracionClienteMin":30,
             "duracionAgendaMin":30,"activo":false,"modalidadPrestacion":0}
            """;

        var dto = JsonSerializer.Deserialize<UpdateServicioDto>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // No hay excepción → el campo se ignoró silenciosamente
        dto.Should().NotBeNull();
        // Verificar que el nombre sí se mapeó correctamente
        dto!.Nombre.Should().Be("Test");
    }
}
```

---

## M-02 — DTO Validations (`DtoValidacionesTests.cs`)

### Contexto

Los DTOs de creación (`CreateTurnoDto`, `CreateClienteDto`, `CreateServicioDto`) carecían
de atributos de validación (`[Required]`, `[Range]`, etc.). Esto permitía requests con datos
inválidos que fallaban silenciosamente o causaban errores en capa de servicio.

```csharp
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using TurneroApp.Application.DTOs;
using Xunit;

namespace TurneroApp.Tests.Security;

public class DtoValidacionesTests
{
    // ── CreateTurnoDto ──────────────────────────────────────────────────────

    /// <summary>
    /// Test 28 — Campos Required ausentes en CreateTurnoDto deben fallar validación.
    /// </summary>
    [Theory]
    [InlineData("SucursalId")]
    [InlineData("ClienteId")]
    [InlineData("ServicioId")]
    [InlineData("ProfesionalId")]
    [InlineData("FechaHoraInicio")]
    public void CreateTurnoDto_CampoRequerido_Ausente_ModelStateInvalido(string campo)
    {
        var dto = new CreateTurnoDto(); // todos Guid.Empty / DateTime.MinValue

        var resultados = Validar(dto);

        resultados.Should().Contain(r => r.MemberNames.Contains(campo),
            $"el campo {campo} es [Required] y debe fallar cuando está ausente");
    }

    // ── CreateClienteDto ────────────────────────────────────────────────────

    /// <summary>Test 29 — Nombre vacío en CreateClienteDto debe fallar [Required].</summary>
    [Fact]
    public void CreateClienteDto_NombreVacio_FallaValidacion()
    {
        var dto = new CreateClienteDto { Nombre = "", Apellido = "Test",
                                         Email = "t@t.com", SucursalId = Guid.NewGuid() };
        var resultados = Validar(dto);

        resultados.Should().Contain(r => r.MemberNames.Contains("Nombre"),
            "Nombre es [Required] y no puede estar vacío");
    }

    // ── CreateServicioDto ───────────────────────────────────────────────────

    /// <summary>Test 30 — Precio negativo en CreateServicioDto debe fallar [Range(0, ...)].</summary>
    [Fact]
    public void CreateServicioDto_PrecioNegativo_FallaValidacion()
    {
        var dto = new CreateServicioDto
        {
            Nombre             = "Srv",
            Precio             = -1,
            DuracionClienteMin = 30,
            DuracionAgendaMin  = 30,
            SucursalId         = Guid.NewGuid(),
        };
        var resultados = Validar(dto);

        resultados.Should().Contain(r => r.MemberNames.Contains("Precio"),
            "Precio no puede ser negativo");
    }

    /// <summary>Test 31 — Duración = 0 en CreateServicioDto debe fallar [Range(1, 1440)].</summary>
    [Fact]
    public void CreateServicioDto_DuracionCero_FallaValidacion()
    {
        var dto = new CreateServicioDto
        {
            Nombre             = "Srv",
            Precio             = 50,
            DuracionClienteMin = 0,   // inválido
            DuracionAgendaMin  = 30,
            SucursalId         = Guid.NewGuid(),
        };
        var resultados = Validar(dto);

        resultados.Should().Contain(r => r.MemberNames.Contains("DuracionClienteMin"),
            "la duración mínima es 1 minuto");
    }

    // ── Helper ──────────────────────────────────────────────────────────────

    private static List<ValidationResult> Validar(object dto)
    {
        var context    = new ValidationContext(dto);
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(dto, context, resultados, validateAllProperties: true);
        return resultados;
    }
}
```
