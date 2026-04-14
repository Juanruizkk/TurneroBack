using FluentAssertions;
using TurneroApp.Domain.Enums;
using TurneroApp.IntegrationTests.Helpers;
using TurneroApp.IntegrationTests.Infrastructure;
using Xunit;

namespace TurneroApp.IntegrationTests.Services;

/// <summary>
/// Tests de integración para PublicoService.ObtenerSlotsAsync.
/// Verifican que el algoritmo de generación de slots interactúa correctamente
/// con la DB real: horarios, conflictos, duración, buffer y horario laboral.
///
/// Fecha fija de prueba: lunes 2027-03-01 (siempre futuro → pasa filtro de anticipación).
/// Fecha cerrada:        domingo 2027-02-28 (cerrado en el horario por defecto).
/// </summary>
public class SlotsDisponiblesIntegrationTests : IntegrationTestBase
{
    public SlotsDisponiblesIntegrationTests(PostgresFixture fixture) : base(fixture) { }

    // Lunes 2027-03-01 → abierto en horario por defecto (09:00-13:00 y 15:00-19:00)
    private static readonly DateOnly LunesFuturo = new(2027, 3, 1);

    // Domingo 2027-02-28 → cerrado en horario por defecto
    private static readonly DateOnly DomingoCerrado = new(2027, 2, 28);

    // ─── Test 1: Happy path ───────────────────────────────────────────────────

    [Fact]
    public async Task ObtenerSlotsAsync_SinTurnosNiConflictos_RetornaSlotsPorCadaProfesional()
    {
        // Arrange
        // Usamos horario estrecho (09:00-11:00) para tener un conteo determinístico:
        // duracion=30min → slots: 09:00, 09:30, 10:00, 10:30 = 4 slots por profesional
        var horarioJson = SeedHelper.HorarioJson("Lunes", "09:00", "11:00");

        var sucursal = await SeedHelper.SeedSucursalAsync(Db);
        await SeedHelper.SeedConfiguracionAsync(Db, sucursal.Id, horariosJson: horarioJson, anticipacionMin: 0);

        var servicio = await SeedHelper.SeedServicioAsync(Db, sucursal.Id, duracionAgendaMin: 30);

        var prof1 = await SeedHelper.SeedProfesionalAsync(Db);
        var prof2 = await SeedHelper.SeedProfesionalAsync(Db);
        await SeedHelper.SeedUsuarioSucursalRolAsync(Db, sucursal.Id, prof1.Id);
        await SeedHelper.SeedUsuarioSucursalRolAsync(Db, sucursal.Id, prof2.Id);

        var sut = ServiceFactory.BuildPublicoService(Db);

        // Act
        var slots = await sut.ObtenerSlotsAsync(sucursal.Slug, LunesFuturo, servicio.Id);

        // Assert — 4 slots × 2 profesionales = 8
        slots.Should().HaveCount(8);
        slots.Select(s => s.Hora).Distinct()
             .Should().BeEquivalentTo(["09:00", "09:30", "10:00", "10:30"],
                "debe haber un slot en cada media hora dentro de la franja 09:00-11:00");
        slots.Select(s => s.ProfesionalId).Distinct()
             .Should().HaveCount(2, "cada profesional debe tener sus propios slots");
    }

    // ─── Test 2: Turno existente bloquea el slot del profesional ─────────────

    [Fact]
    public async Task ObtenerSlotsAsync_ConTurnoQueBloquea_ExcluyeSlotDelProfesionalOcupado()
    {
        // Arrange
        var horarioJson = SeedHelper.HorarioJson("Lunes", "09:00", "10:30");

        var sucursal = await SeedHelper.SeedSucursalAsync(Db);
        await SeedHelper.SeedConfiguracionAsync(Db, sucursal.Id, horariosJson: horarioJson, anticipacionMin: 0);

        var cliente  = await SeedHelper.SeedClienteAsync(Db, sucursal.Id);
        var servicio = await SeedHelper.SeedServicioAsync(Db, sucursal.Id, duracionAgendaMin: 30);
        var prof     = await SeedHelper.SeedProfesionalAsync(Db);
        await SeedHelper.SeedUsuarioSucursalRolAsync(Db, sucursal.Id, prof.Id);

        // Turno existente: 09:00 – 09:30 → bloquea el slot de las 09:00
        var turnoInicio = new DateTime(2027, 3, 1, 9, 0, 0, DateTimeKind.Unspecified);
        await SeedHelper.SeedTurnoAsync(Db, sucursal.Id, cliente.Id, servicio.Id, prof.Id,
                                        turnoInicio, duracionMin: 30);

        var sut = ServiceFactory.BuildPublicoService(Db);

        // Act
        var slots = await sut.ObtenerSlotsAsync(sucursal.Slug, LunesFuturo, servicio.Id);

        // Assert — el slot 09:00 está bloqueado; 09:30 y 10:00 siguen libres
        slots.Should().NotContain(s => s.Hora == "09:00" && s.ProfesionalId == prof.Id.ToString(),
            "el profesional ya tiene un turno a las 09:00");
        slots.Select(s => s.Hora).Should().Contain("09:30").And.Contain("10:00");
    }

    // ─── Test 3: Duración larga reduce la cantidad de slots ──────────────────

    [Fact]
    public async Task ObtenerSlotsAsync_DuracionServicioLarga_ReduceSlotsDisponibles()
    {
        // Arrange — franja 09:00-10:30, servicio de 60 min
        // 09:00+60=10:00 ≤ 10:30 → slot 09:00 ✓
        // 10:00+60=11:00 > 10:30 → stop
        // Total esperado: 1 slot
        var horarioJson = SeedHelper.HorarioJson("Lunes", "09:00", "10:30");

        var sucursal = await SeedHelper.SeedSucursalAsync(Db);
        await SeedHelper.SeedConfiguracionAsync(Db, sucursal.Id, horariosJson: horarioJson, anticipacionMin: 0);

        var servicio = await SeedHelper.SeedServicioAsync(Db, sucursal.Id, duracionAgendaMin: 60);
        var prof     = await SeedHelper.SeedProfesionalAsync(Db);
        await SeedHelper.SeedUsuarioSucursalRolAsync(Db, sucursal.Id, prof.Id);

        var sut = ServiceFactory.BuildPublicoService(Db);

        // Act
        var slots = await sut.ObtenerSlotsAsync(sucursal.Slug, LunesFuturo, servicio.Id);

        // Assert
        slots.Should().HaveCount(1);
        slots.Single().Hora.Should().Be("09:00");
    }

    // ─── Test 4: Buffer entre turnos espacía los slots ────────────────────────

    [Fact]
    public async Task ObtenerSlotsAsync_ConBuffer_AgregaEspacioEntreSlots()
    {
        // Arrange — franja 09:00-10:30, servicio 30 min, buffer 15 min
        // Sin buffer: 09:00, 09:30, 10:00
        // Con buffer: 09:00 → cursor += 30+15 = 09:45 → 09:45 → cursor += 45 = 10:30 → stop
        // Total esperado: 2 slots (09:00 y 09:45)
        var horarioJson = SeedHelper.HorarioJson("Lunes", "09:00", "10:30");

        var sucursal = await SeedHelper.SeedSucursalAsync(Db);
        await SeedHelper.SeedConfiguracionAsync(Db, sucursal.Id,
            horariosJson: horarioJson, buffer: 15, anticipacionMin: 0);

        var servicio = await SeedHelper.SeedServicioAsync(Db, sucursal.Id, duracionAgendaMin: 30);
        var prof     = await SeedHelper.SeedProfesionalAsync(Db);
        await SeedHelper.SeedUsuarioSucursalRolAsync(Db, sucursal.Id, prof.Id);

        var sut = ServiceFactory.BuildPublicoService(Db);

        // Act
        var slots = await sut.ObtenerSlotsAsync(sucursal.Slug, LunesFuturo, servicio.Id);

        // Assert
        slots.Should().HaveCount(2);
        slots.Select(s => s.Hora).Should().BeEquivalentTo(["09:00", "09:45"],
            "el buffer de 15 min debe sumar al cursor junto con la duración");
    }

    // ─── Test 5: Día cerrado retorna lista vacía ──────────────────────────────

    [Fact]
    public async Task ObtenerSlotsAsync_DiaCerrado_RetornaListaVacia()
    {
        // Arrange — sin ConfiguracionSucursal → usa horario por defecto
        // El horario por defecto tiene Domingo cerrado
        var sucursal = await SeedHelper.SeedSucursalAsync(Db);
        var servicio = await SeedHelper.SeedServicioAsync(Db, sucursal.Id, duracionAgendaMin: 30);
        var prof     = await SeedHelper.SeedProfesionalAsync(Db);
        await SeedHelper.SeedUsuarioSucursalRolAsync(Db, sucursal.Id, prof.Id);

        var sut = ServiceFactory.BuildPublicoService(Db);

        // Act — domingo cerrado
        var slots = await sut.ObtenerSlotsAsync(sucursal.Slug, DomingoCerrado, servicio.Id);

        // Assert
        slots.Should().BeEmpty("el domingo está cerrado en el horario por defecto");
    }

    // ─── Test 6: HorarioLaboral del profesional filtra slots fuera de su rango

    [Fact]
    public async Task ObtenerSlotsAsync_HorarioLaboralConfigurado_ExcluyeSlotsFueraDeRangoDelProfesional()
    {
        // Arrange
        // Horario de sucursal: Lunes 09:00-13:00 y 15:00-19:00 (horario estrecho para el test)
        // HorarioLaboral del profesional: Lunes 12:00-13:00
        // → slots 09:00-11:30 excluidos (fuera del rango del prof)
        // → slots 12:00 y 12:30 incluidos (dentro del rango)
        // → slots 15:00-18:30 excluidos (HoraHasta=13:00 < 15:30)
        // Total esperado: 2 slots
        var horarioJson = SeedHelper.HorarioJson("Lunes", "09:00", "13:00");

        var sucursal = await SeedHelper.SeedSucursalAsync(Db);
        await SeedHelper.SeedConfiguracionAsync(Db, sucursal.Id, horariosJson: horarioJson, anticipacionMin: 0);

        var servicio = await SeedHelper.SeedServicioAsync(Db, sucursal.Id, duracionAgendaMin: 30);
        var prof     = await SeedHelper.SeedProfesionalAsync(Db);
        var usr = await SeedHelper.SeedUsuarioSucursalRolAsync(Db, sucursal.Id, prof.Id);

        // HorarioLaboral: solo trabaja lunes de 12:00 a 13:00
        await SeedHelper.SeedHorarioLaboralAsync(Db, usr.Id,
            DayOfWeek.Monday,
            desde: TimeSpan.FromHours(12),
            hasta: TimeSpan.FromHours(13));

        var sut = ServiceFactory.BuildPublicoService(Db);

        // Act
        var slots = await sut.ObtenerSlotsAsync(sucursal.Slug, LunesFuturo, servicio.Id);

        // Assert
        slots.Should().HaveCount(2,
            "solo los slots dentro del horario laboral 12:00-13:00 deben estar disponibles");
        slots.Select(s => s.Hora).Should().BeEquivalentTo(["12:00", "12:30"]);
        slots.Should().AllSatisfy(s =>
            s.ProfesionalId.Should().Be(prof.Id.ToString()));
    }
}
