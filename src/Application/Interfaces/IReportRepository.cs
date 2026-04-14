using TurneroApp.Application.DTOs.Reportes;

namespace TurneroApp.Application.Interfaces;

public interface IReportRepository
{
    Task<ReporteTurnosDto>            GetTurnosAsync(Guid sucursalId, DateTime desde, DateTime hasta);
    Task<ReporteIngresosDto>          GetIngresosAsync(Guid sucursalId, DateTime desde, DateTime hasta);
    Task<List<ReporteServicioDto>>    GetTopServiciosAsync(Guid sucursalId, DateTime desde, DateTime hasta, int top = 10);
    Task<List<ReporteProfesionalDto>> GetTopProfesionalesAsync(Guid sucursalId, DateTime desde, DateTime hasta, int top = 10);
    Task<ReporteClientesDto>          GetClientesAsync(Guid sucursalId, DateTime desde, DateTime hasta);
}
