using System.ComponentModel.DataAnnotations;

namespace TurneroApp.Application.DTOs.SuperAdmin;

public class CambiarPlanSuperAdminDto
{
    [Required]
    public Guid PlanId { get; set; }
}
