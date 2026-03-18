namespace TurneroApp.Application.DTOs.Caja;

public class CajaSesionResponseDto
{
    public Guid      Id                    { get; set; }
    public Guid      CajaId                { get; set; }
    public DateTime  FechaApertura         { get; set; }
    public DateTime? FechaCierre           { get; set; }
    public decimal   MontoInicial          { get; set; }
    public decimal?  MontoFinalEsperado    { get; set; }
    public decimal?  MontoFinalReal        { get; set; }
    public decimal?  Diferencia            { get; set; }
    public string    Estado                { get; set; } = string.Empty;
    public string?   UsuarioAperturaNombre { get; set; }
    public string?   UsuarioCierreNombre   { get; set; }
    public string?   ObservacionesApertura { get; set; }
    public string?   ObservacionesCierre   { get; set; }
}
