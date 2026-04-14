namespace TurneroApp.Application.DTOs.Reportes;

public class ReporteTurnosDto
{
    public int Total          { get; set; }
    public int Completados    { get; set; }
    public int Cancelados     { get; set; }
    public int NoShow         { get; set; }
    public int Pendientes     { get; set; }
    public int PendientePago  { get; set; }
}

public class ReporteIngresosDto
{
    public decimal TotalCobrado      { get; set; }
    public decimal TotalEfectivo     { get; set; }
    public decimal TotalTransferencia{ get; set; }
    public decimal TotalTarjeta      { get; set; }
    public decimal TotalMercadoPago  { get; set; }
    public List<IngresosPorDiaDto> PorDia { get; set; } = [];
}

public class IngresosPorDiaDto
{
    public DateOnly Fecha  { get; set; }
    public decimal  Monto  { get; set; }
}

public class ReporteServicioDto
{
    public Guid   ServicioId   { get; set; }
    public string Nombre       { get; set; } = string.Empty;
    public int    CantidadTurnos { get; set; }
    public decimal Ingresos    { get; set; }
}

public class ReporteProfesionalDto
{
    public Guid   ProfesionalId  { get; set; }
    public string Nombre         { get; set; } = string.Empty;
    public int    CantidadTurnos { get; set; }
    public decimal Ingresos      { get; set; }
}

public class ReporteClientesDto
{
    public int TotalNuevos      { get; set; }
    public int TotalRecurrentes { get; set; }
    public int TotalUnicos      { get; set; }
}

public class ReporteResumenDto
{
    public ReporteTurnosDto     Turnos       { get; set; } = new();
    public ReporteIngresosDto   Ingresos     { get; set; } = new();
    public List<ReporteServicioDto>    TopServicios    { get; set; } = [];
    public List<ReporteProfesionalDto> TopProfesionales{ get; set; } = [];
    public ReporteClientesDto   Clientes     { get; set; } = new();
}
