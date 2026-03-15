namespace TurneroApp.Domain.Enums;

/// <summary>
/// Permisos granulares que se asignan individualmente a cada usuario,
/// independientemente de su rol base.
/// Almacenados como filas en la tabla usuario_permisos (no como JSON).
/// </summary>
public enum PermisoSistema
{
    AgendarTurnos       = 0,
    EditarTurnos        = 1,
    CargarCobranzas     = 2,
    VerTelefonoClientes = 3,
    GestionarAjustes    = 4
}
