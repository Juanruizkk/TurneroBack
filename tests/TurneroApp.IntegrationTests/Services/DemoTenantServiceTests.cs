using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TurneroApp.Application.Exceptions;
using TurneroApp.Infrastructure.Services;
using TurneroApp.IntegrationTests.Infrastructure;
using Xunit;

namespace TurneroApp.IntegrationTests.Services;

public class DemoTenantServiceTests(PostgresFixture fixture) : IntegrationTestBase(fixture)
{
    private DemoTenantService CrearSvc(int maxPorIp = 3, int maxGlobal = 100) =>
        new(Db, ttlMinutos: 30, maxPorIp: maxPorIp, maxGlobal: maxGlobal);

    [Fact]
    public async Task CrearAsync_crea_sucursal_demo_con_dataset_completo()
    {
        var svc = CrearSvc();

        var res = await svc.CrearAsync("1.1.1.1");

        var suc = await Db.Sucursales.IgnoreQueryFilters().SingleAsync(s => s.Id == res.SucursalId);
        suc.EsDemo.Should().BeTrue();
        suc.DemoExpiraEn.Should().NotBeNull();
        suc.DemoExpiraEn!.Value.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(30), TimeSpan.FromMinutes(1));
        suc.DemoIp.Should().Be("1.1.1.1");

        res.Usuarios.Should().HaveCount(3);
        res.Usuarios.Should().ContainSingle(u => u.EsOwner);

        (await Db.Servicios.IgnoreQueryFilters().CountAsync(s => s.SucursalId == res.SucursalId)).Should().Be(3);
        (await Db.Clientes.IgnoreQueryFilters().CountAsync(c => c.SucursalId == res.SucursalId)).Should().Be(6);
        (await Db.Turnos.IgnoreQueryFilters().CountAsync(t => t.SucursalId == res.SucursalId)).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CrearAsync_dos_veces_no_colisiona_por_email()
    {
        var svc = CrearSvc();
        var a = await svc.CrearAsync("1.1.1.1");
        var b = await svc.CrearAsync("2.2.2.2");
        a.SucursalId.Should().NotBe(b.SucursalId);

        var emails = await Db.Usuarios.IgnoreQueryFilters().Select(u => u.Email).ToListAsync();
        emails.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task CrearAsync_cuarta_demo_misma_ip_lanza_limite()
    {
        var svc = CrearSvc();
        await svc.CrearAsync("9.9.9.9");
        await svc.CrearAsync("9.9.9.9");
        await svc.CrearAsync("9.9.9.9");

        var act = async () => await svc.CrearAsync("9.9.9.9");
        await act.Should().ThrowAsync<DemoLimiteException>();
    }

    [Fact]
    public async Task CrearAsync_respeta_tope_global()
    {
        var svc = CrearSvc(maxPorIp: 100, maxGlobal: 2);
        await svc.CrearAsync("1.0.0.1");
        await svc.CrearAsync("1.0.0.2");

        var act = async () => await svc.CrearAsync("1.0.0.3");
        await act.Should().ThrowAsync<DemoLimiteException>();
    }

    [Fact]
    public async Task TeardownAsync_borra_todo_y_no_toca_otras_sucursales()
    {
        var svc = CrearSvc();
        var demo = await svc.CrearAsync("1.1.1.1");
        var otra = await svc.CrearAsync("2.2.2.2");

        await svc.TeardownAsync(demo.SucursalId);

        (await Db.Sucursales.IgnoreQueryFilters().AnyAsync(s => s.Id == demo.SucursalId)).Should().BeFalse();
        (await Db.Turnos.IgnoreQueryFilters().AnyAsync(t => t.SucursalId == demo.SucursalId)).Should().BeFalse();
        (await Db.Servicios.IgnoreQueryFilters().AnyAsync(s => s.SucursalId == demo.SucursalId)).Should().BeFalse();
        (await Db.Clientes.IgnoreQueryFilters().AnyAsync(c => c.SucursalId == demo.SucursalId)).Should().BeFalse();
        (await Db.Cajas.IgnoreQueryFilters().AnyAsync(c => c.SucursalId == demo.SucursalId)).Should().BeFalse();
        (await Db.UsuarioSucursalRoles.IgnoreQueryFilters().AnyAsync(r => r.SucursalId == demo.SucursalId)).Should().BeFalse();

        // Los usuarios de la demo borrada ya no existen (eran huérfanos tras el teardown)
        var emailsDemoBorrada = demo.Usuarios.Select(u => u.Email).ToList();
        (await Db.Usuarios.IgnoreQueryFilters().AnyAsync(u => emailsDemoBorrada.Contains(u.Email))).Should().BeFalse();

        // La otra demo sigue intacta
        (await Db.Sucursales.IgnoreQueryFilters().AnyAsync(s => s.Id == otra.SucursalId)).Should().BeTrue();
        (await Db.Turnos.IgnoreQueryFilters().AnyAsync(t => t.SucursalId == otra.SucursalId)).Should().BeTrue();
    }

    [Fact]
    public async Task LimpiarVencidasAsync_borra_solo_vencidas()
    {
        var svc = CrearSvc();
        var viva  = await svc.CrearAsync("1.1.1.1");
        var vieja = await svc.CrearAsync("2.2.2.2");

        // Forzar vencimiento de 'vieja'
        var sucVieja = await Db.Sucursales.IgnoreQueryFilters().SingleAsync(s => s.Id == vieja.SucursalId);
        sucVieja.DemoExpiraEn = DateTime.UtcNow.AddMinutes(-1);
        await Db.SaveChangesAsync();

        var borradas = await svc.LimpiarVencidasAsync();

        borradas.Should().Be(1);
        (await Db.Sucursales.IgnoreQueryFilters().AnyAsync(s => s.Id == vieja.SucursalId)).Should().BeFalse();
        (await Db.Sucursales.IgnoreQueryFilters().AnyAsync(s => s.Id == viva.SucursalId)).Should().BeTrue();
    }
}
