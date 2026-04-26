using TurneroApp.Application.DTOs.Cliente;
using TurneroApp.Application.Exceptions;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Services;

public class ClienteService
{
    private readonly IClienteRepository _clienteRepository;

    public ClienteService(IClienteRepository clienteRepository)
    {
        _clienteRepository = clienteRepository;
    }

    public async Task<IEnumerable<ClienteResponseDto>> GetAllAsync()
    {
        var clientes = await _clienteRepository.GetAllAsync();
        return clientes.Select(MapToResponse);
    }

    public async Task<IEnumerable<ClienteResponseDto>> GetBySucursalAsync(Guid sucursalId)
    {
        var clientes = await _clienteRepository.GetBySucursalAsync(sucursalId);
        return clientes.Select(MapToResponse);
    }

    public async Task<ClienteResponseDto?> GetByIdAsync(Guid id)
    {
        var cliente = await _clienteRepository.GetByIdAsync(id);
        return cliente is null ? null : MapToResponse(cliente);
    }

    public async Task<ClienteResponseDto?> BuscarPorTelefonoAsync(Guid sucursalId, string telefono)
    {
        var cliente = await _clienteRepository.BuscarPorTelefonoAsync(sucursalId, telefono);
        return cliente is null ? null : MapToResponse(cliente);
    }

    public async Task<ClienteResponseDto?> BuscarPorEmailAsync(Guid sucursalId, string email)
    {
        var cliente = await _clienteRepository.BuscarPorEmailAsync(sucursalId, email);
        return cliente is null ? null : MapToResponse(cliente);
    }

    public async Task<IEnumerable<ClienteResponseDto>> BuscarAsync(Guid sucursalId, string q)
    {
        var resultados = await _clienteRepository.BuscarAsync(sucursalId, q.Trim());
        return resultados.Select(MapToResponse);
    }

    public async Task<IEnumerable<ClienteResponseDto>> GetArchivedBySucursalAsync(Guid sucursalId)
    {
        var clientes = await _clienteRepository.GetArchivedBySucursalAsync(sucursalId);
        return clientes.Select(MapToResponse);
    }

    public async Task<ClienteFichaDto?> GetFichaAsync(Guid clienteId)
        => await _clienteRepository.GetFichaAsync(clienteId);

    public async Task<ClienteResponseDto> CreateAsync(CreateClienteDto dto)
    {
        if (await _clienteRepository.TelefonoExisteEnSucursalAsync(dto.SucursalId, dto.Telefono))
            throw new InvalidOperationException($"Ya existe un cliente con el teléfono {dto.Telefono} en esta sucursal.");

        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            SucursalId = dto.SucursalId,
            Nombre = dto.Nombre,
            Apellido = dto.Apellido,
            Telefono = dto.Telefono,
            Email = dto.Email,
            Observaciones = dto.Observaciones
        };

        await _clienteRepository.AddAsync(cliente);
        await _clienteRepository.SaveChangesAsync();
        return MapToResponse(cliente);
    }

    public async Task<ClienteResponseDto?> UpdateAsync(Guid id, UpdateClienteDto dto)
    {
        var cliente = await _clienteRepository.GetByIdAsync(id);
        if (cliente is null) return null;

        if (await _clienteRepository.TelefonoExisteEnSucursalAsync(cliente.SucursalId, dto.Telefono, excludeId: id))
            throw new InvalidOperationException($"Ya existe un cliente con el teléfono {dto.Telefono} en esta sucursal.");

        cliente.Nombre = dto.Nombre;
        cliente.Apellido = dto.Apellido;
        cliente.Telefono = dto.Telefono;
        cliente.Email = dto.Email;
        cliente.Observaciones = dto.Observaciones;

        _clienteRepository.Update(cliente);
        await _clienteRepository.SaveChangesAsync();
        return MapToResponse(cliente);
    }

    public async Task ArchivarCliente(Guid clienteId)
    {
        var cliente = await _clienteRepository.GetByIdIgnorandoFiltrosAsync(clienteId)
            ?? throw new KeyNotFoundException($"Cliente {clienteId} no encontrado.");
        cliente.Archivar();
        _clienteRepository.Update(cliente);
        await _clienteRepository.SaveChangesAsync();
    }

    public async Task DesarchivarCliente(Guid clienteId)
    {
        var cliente = await _clienteRepository.GetByIdIgnorandoFiltrosAsync(clienteId)
            ?? throw new KeyNotFoundException($"Cliente {clienteId} no encontrado.");
        cliente.Desarchivar();
        _clienteRepository.Update(cliente);
        await _clienteRepository.SaveChangesAsync();
    }

    public async Task EliminarCliente(Guid clienteId)
    {
        var cliente = await _clienteRepository.GetByIdIgnorandoFiltrosAsync(clienteId)
            ?? throw new KeyNotFoundException($"Cliente {clienteId} no encontrado.");

        if (await _clienteRepository.TieneImpactoOperacionalAsync(clienteId))
            throw new ClienteNoSePuedeEliminarException(
                "El cliente tiene turnos con historial operativo o pagos registrados y no puede eliminarse. " +
                "Podés archivarlo en su lugar.");

        // Eliminar turnos sin impacto (PagoRechazado, PendientePago, Cancelados) para liberar la FK.
        await _clienteRepository.EliminarTurnosNoCriticosAsync(clienteId);

        _clienteRepository.Delete(cliente);
        await _clienteRepository.SaveChangesAsync();
    }

    private static ClienteResponseDto MapToResponse(Cliente c) => new()
    {
        Id = c.Id,
        SucursalId = c.SucursalId,
        Nombre = c.Nombre,
        Apellido = c.Apellido,
        Telefono = c.Telefono,
        Email = c.Email,
        Observaciones = c.Observaciones,
        Activo = c.Activo,
        FechaArchivado = c.FechaArchivado,
        TotalTurnos = c.Turnos.Count,
        TieneNoShow = c.Turnos.Any(t => t.Estado == TurneroApp.Domain.Enums.EstadoTurno.NoShow),
        EsFrecuente = c.Turnos.Count > 5,
        UltimaVisita = c.Turnos
            .Where(t => t.FechaHoraInicio <= DateTime.UtcNow)
            .OrderByDescending(t => t.FechaHoraInicio)
            .Select(t => t.FechaHoraInicio)
            .FirstOrDefault() == default ? null
            : c.Turnos
                .Where(t => t.FechaHoraInicio <= DateTime.UtcNow)
                .OrderByDescending(t => t.FechaHoraInicio)
                .Select(t => (DateTime?)t.FechaHoraInicio)
                .FirstOrDefault()
    };
}
