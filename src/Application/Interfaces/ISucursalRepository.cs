using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Interfaces;

public interface ISucursalRepository : IGenericRepository<Sucursal>
{
    Task<IEnumerable<Sucursal>> GetActivasAsync();

    /// <summary>Todas las sucursales incluyendo el usuario owner (EsOwner = true).</summary>
    Task<IEnumerable<Sucursal>> GetAllConOwnerAsync();

    /// <summary>Una sucursal por Id incluyendo el usuario owner.</summary>
    Task<Sucursal?> GetByIdConOwnerAsync(Guid id);

    /// <summary>Una sucursal por Id incluyendo su ConfiguracionSucursal.</summary>
    Task<Sucursal?> GetByIdConConfiguracionAsync(Guid id);

    /// <summary>Busca una sucursal por su slug único. Incluye ConfiguracionSucursal.</summary>
    Task<Sucursal?> GetBySlugAsync(string slug);

    /// <summary>
    /// Devuelve true si ya existe una sucursal con ese slug.
    /// Si se pasa <paramref name="excluirSucursalId"/>, esa sucursal se ignora
    /// (útil al editar el slug propio).
    /// </summary>
    Task<bool> SlugExisteAsync(string slug, Guid? excluirSucursalId = null);
}
