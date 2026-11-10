using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;
using TurneroApp.Infrastructure.Repositories;
using TurneroApp.Infrastructure.Services;
using TurneroApp.IntegrationTests.Infrastructure;
using Xunit;

namespace TurneroApp.IntegrationTests.Services;

public class RecordatoriosDemoExclusionTests(PostgresFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task GetParaRecordatoriosAsync_excluye_turnos_de_sucursales_demo()
    {
        // Crear una demo y forzar un turno confirmado dentro de la ventana de 3 h.
        var demoSvc = new DemoTenantService(Db, ttlMinutos: 30, maxPorIp: 100, maxGlobal: 100);
        var demo = await demoSvc.CrearAsync("1.1.1.1");

        var ahora  = DateTime.Now;
        var turnoDemo = await Db.Turnos.IgnoreQueryFilters()
            .Where(t => t.SucursalId == demo.SucursalId)
            .OrderBy(t => t.FechaHoraInicio)
            .FirstAsync();

        turnoDemo.Estado = EstadoTurno.Confirmado;
        turnoDemo.FechaHoraInicio = DateTime.SpecifyKind(ahora.AddHours(1), DateTimeKind.Unspecified);
        turnoDemo.FechaHoraFin = turnoDemo.FechaHoraInicio.AddMinutes(45);
        turnoDemo.Recordatorio3hEnviado = false;
        turnoDemo.Recordatorio24hEnviado = false;
        await Db.SaveChangesAsync();

        var repo = new TurnoRepository(Db);
        var turnos = await repo.GetParaRecordatoriosAsync(ahora, ahora.AddHours(24), ahora.AddHours(3));

        turnos.Should().NotContain(t => t.SucursalId == demo.SucursalId);
    }
}
