using TurneroApp.Application.DTOs.Cliente;
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

    public async Task<ClienteResponseDto> CreateAsync(CreateClienteDto dto)
    {
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

        cliente.Nombre = dto.Nombre;
        cliente.Apellido = dto.Apellido;
        cliente.Telefono = dto.Telefono;
        cliente.Email = dto.Email;
        cliente.Observaciones = dto.Observaciones;

        _clienteRepository.Update(cliente);
        await _clienteRepository.SaveChangesAsync();
        return MapToResponse(cliente);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var cliente = await _clienteRepository.GetByIdAsync(id);
        if (cliente is null) return false;

        _clienteRepository.Delete(cliente);
        await _clienteRepository.SaveChangesAsync();
        return true;
    }

    private static ClienteResponseDto MapToResponse(Cliente c) => new()
    {
        Id = c.Id,
        SucursalId = c.SucursalId,
        Nombre = c.Nombre,
        Apellido = c.Apellido,
        Telefono = c.Telefono,
        Email = c.Email,
        Observaciones = c.Observaciones
    };
}
