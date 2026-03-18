using TurneroApp.Application.DTOs.Turno;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Tests.Helpers;

internal static class TurnoTestFactory
{
    public static Servicio CrearServicio(
        int duracionAgendaMin = 30,
        decimal precio = 5000m,
        bool requiereSena = false,
        TipoSena? tipoSena = null,
        decimal? valorSena = null)
    {
        return new Servicio
        {
            Id                = Guid.NewGuid(),
            SucursalId        = Guid.NewGuid(),
            Nombre            = "Servicio Test",
            DuracionAgendaMin = duracionAgendaMin,
            Precio            = precio,
            RequiereSena      = requiereSena,
            TipoSena          = tipoSena,
            ValorSena         = valorSena
        };
    }

    public static Cliente CrearCliente(string? email = "cliente@test.com")
    {
        return new Cliente
        {
            Id         = Guid.NewGuid(),
            SucursalId = Guid.NewGuid(),
            Nombre     = "Juan",
            Apellido   = "Perez",
            Telefono   = "1111111111",
            Email      = email
        };
    }

    public static Sucursal CrearSucursal(string email = "sucursal@test.com")
    {
        return new Sucursal
        {
            Id     = Guid.NewGuid(),
            Nombre = "Sucursal Test",
            Email  = email
        };
    }

    public static Sucursal CrearSucursalConConfig(
        ModoConfirmacionTurnos modo = ModoConfirmacionTurnos.Automatico)
    {
        var sucursal = CrearSucursal();
        sucursal.ConfiguracionSucursal = new ConfiguracionSucursal
        {
            ModoConfirmacionTurnos = modo
        };
        return sucursal;
    }

    public static CreateTurnoDto CrearDto(
        Guid? servicioId = null,
        Guid? clienteId = null,
        Guid? sucursalId = null,
        OrigenTurno origen = OrigenTurno.Administrativo)
    {
        return new CreateTurnoDto
        {
            SucursalId      = sucursalId ?? Guid.NewGuid(),
            ClienteId       = clienteId ?? Guid.NewGuid(),
            ServicioId      = servicioId ?? Guid.NewGuid(),
            ProfesionalId   = Guid.NewGuid(),
            FechaHoraInicio = DateTime.UtcNow.AddDays(1),
            Origen          = origen
        };
    }

    public static Turno CrearTurnoConRelaciones(
        Cliente? cliente = null,
        Servicio? servicio = null,
        Guid? sucursalId = null)
    {
        var c = cliente  ?? CrearCliente();
        var s = servicio ?? CrearServicio();
        return new Turno
        {
            Id              = Guid.NewGuid(),
            SucursalId      = sucursalId ?? Guid.NewGuid(),
            ClienteId       = c.Id,
            ServicioId      = s.Id,
            ProfesionalId   = Guid.NewGuid(),
            FechaHoraInicio = DateTime.Now.AddDays(1),
            FechaHoraFin    = DateTime.Now.AddDays(1).AddMinutes(s.DuracionAgendaMin),
            FechaCreacion   = DateTime.Now,
            Estado          = EstadoTurno.Confirmado,
            TokenGestion    = Guid.NewGuid().ToString("N"),
            PrecioServicio  = s.Precio,
            Cliente         = c,
            Servicio        = s
        };
    }
}
