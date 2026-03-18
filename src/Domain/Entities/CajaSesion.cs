using TurneroApp.Domain.Enums;

namespace TurneroApp.Domain.Entities;

public class CajaSesion
{
    public Guid      Id                      { get; set; }
    public Guid      CajaId                  { get; set; }
    public DateTime  FechaApertura           { get; set; }
    public DateTime? FechaCierre             { get; set; }
    public decimal   MontoInicial            { get; set; }
    public decimal?  MontoFinalEsperado      { get; set; }
    public decimal?  MontoFinalReal          { get; set; }
    public decimal?  Diferencia              { get; set; }
    public EstadoCajaSesion Estado           { get; set; } = EstadoCajaSesion.Abierta;
    public Guid?     UsuarioAperturaId       { get; set; }
    public Guid?     UsuarioCierreId         { get; set; }
    public string?   ObservacionesApertura   { get; set; }
    public string?   ObservacionesCierre     { get; set; }

    // Navigation
    public Caja                        Caja            { get; set; } = null!;
    public Usuario?                    UsuarioApertura { get; set; }
    public Usuario?                    UsuarioCierre   { get; set; }
    public ICollection<MovimientoCaja> Movimientos     { get; set; } = new List<MovimientoCaja>();
}
