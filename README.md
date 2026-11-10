# TurneroApp — Backend (API .NET 8)

API REST del sistema de gestión de turnos **MetaTurnos**: agenda, clientes, servicios, caja y reservas online para estéticas y peluquerías.

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql&logoColor=white)
![EF Core](https://img.shields.io/badge/EF%20Core-8-512BD4)
![License](https://img.shields.io/badge/license-MIT-green)

> Proyecto open source. Parte de un ecosistema de 3 repos:
> **[TurneroBack](https://github.com/Juanruizkk/TurneroBack)** (este) · [TurneroFront](https://github.com/Juanruizkk/TurneroFront) · [turnero-devops](https://github.com/Juanruizkk/turnero-devops)

---

## ¿Qué hace?

- **Reserva online sin cuenta** — el cliente elige servicio, profesional y horario, y gestiona su turno con un token público.
- **Agenda por profesional** con validación de conflictos de horario y cálculo automático de duración/fin del turno.
- **Multi-sucursal con roles tipo Slack** — una identidad global, membresías por sucursal, con rol, estado y permisos granulares.
- **Caja y reportes** — movimientos, pagos, señas y dashboards.
- **Notificaciones** — emails transaccionales (Resend) y recordatorios automáticos 24h/3h.
- **Tiempo real** — actualizaciones de agenda vía SignalR.
- **Observabilidad** — endpoint `/metrics` (Prometheus) listo para scraping.

## Stack

| Capa | Tecnología |
|------|-----------|
| Runtime | .NET 8 |
| ORM | Entity Framework Core 8 |
| Base de datos | PostgreSQL 16 |
| Auth | JWT |
| Tiempo real | SignalR |
| Emails | Resend |
| Imágenes | Cloudinary |
| Métricas | prometheus-net.AspNetCore |

## Arquitectura

Clean Architecture en 4 capas con dependencias estrictas:

```
Domain → Application → Infrastructure
                  ↑          ↑
                  └──── Api ──┘
```

- **Domain** — entidades y enums puros, sin dependencias externas.
- **Application** — interfaces de repositorios, servicios de aplicación, DTOs.
- **Infrastructure** — EF Core, `AppDbContext`, configuraciones Fluent API, repositorios.
- **Api** — controllers y `Program.cs`.

Convenciones clave: IDs `Guid` asignados manualmente, dinero como `numeric(18,2)`, enums como `int` en DB (y string en la API), un servicio por agregado, errores de negocio como `InvalidOperationException`.

## Cómo correrlo

Requisitos: [.NET 8 SDK](https://dotnet.microsoft.com/download) y [Docker](https://www.docker.com/).

```bash
# 1. Levantar la base de datos
docker compose up -d

# 2. Configurar los secrets locales (no van al repo)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5433;Database=turnero;Username=postgres;Password=TU_PASSWORD" \
  --project src/Api
dotnet user-secrets set "Jwt:Secret" "una-clave-de-minimo-32-caracteres" --project src/Api
dotnet user-secrets set "SuperAdmin:Password" "TuPasswordSuperAdmin!" --project src/Api

# 3. Correr la API (aplica migrations automáticamente en Development)
dotnet watch run --project src/Api
```

- **API:** http://localhost:5000/api
- **Swagger:** http://localhost:5000/swagger (solo en Development)

> Las credenciales nunca van en `appsettings.json`. En desarrollo se usan [user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets); en producción, variables de entorno (ver `.env.prod.example`).

## Migrations

```bash
# Crear una migration
dotnet ef migrations add <Nombre> --project src/Infrastructure --startup-project src/Api

# Aplicarla
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

## Tests

```bash
dotnet test                                    # todos
dotnet test tests/TurneroApp.Tests             # unit tests
dotnet test tests/TurneroApp.IntegrationTests  # integración (TestContainers + PostgreSQL real)
```

## CI/CD

Cada push dispara el pipeline de GitHub Actions (6 jobs): build + unit tests → integration tests → SonarCloud → SBOM CycloneDX → Snyk (NuGet + Docker) → deploy. Ver [turnero-devops](https://github.com/Juanruizkk/turnero-devops) para el detalle de la infraestructura.

## Licencia

MIT.
