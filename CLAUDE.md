# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Comandos esenciales

```bash
# Levantar solo la DB (desarrollo)
docker compose up -d

# Correr la API con hot reload
dotnet watch run --project src/Api

# Build sin ejecutar
dotnet build TurneroApp.sln

# Generar una migration nueva
dotnet ef migrations add <NombreMigration> --project src/Infrastructure --startup-project src/Api

# Aplicar migrations a la DB
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

**Swagger:** `http://localhost:5000/swagger` (solo disponible cuando el entorno es `Development`)

**Puerto postgres local:** 5433 (el contenedor expone 5433→5432)

## Arquitectura

Solución de 4 proyectos en capas estrictas:

```
Domain → Application → Infrastructure
                  ↑          ↑
                  └──── Api ──┘
```

- **Domain** (`src/Domain`): entidades y enums puros, sin dependencias externas.
- **Application** (`src/Application`): interfaces de repositorios, servicios de aplicación, DTOs. No referencia Infrastructure.
- **Infrastructure** (`src/Infrastructure`): EF Core, AppDbContext, configuraciones Fluent API, implementaciones de repositorios.
- **Api** (`src/Api`): controllers, Program.cs. Referencia Application e Infrastructure para DI.

## Convenciones del proyecto

### Entidades
- ID siempre `Guid`, asignado manualmente (`Guid.NewGuid()`), nunca auto-generado por la DB (`ValueGeneratedNever()`).
- `decimal` para dinero con `HasColumnType("numeric(18,2)")`.
- Enums persistidos como `int` en la DB (`HasConversion<int>()`), excepto `PermisoSistema` que se guarda como `jsonb` (es `[Flags]`).

### Repositorios
- `IGenericRepository<T>` provee `GetByIdAsync`, `GetAllAsync`, `AddAsync`, `Update`, `Delete`, `SaveChangesAsync`.
- Repositorios específicos extienden el genérico con queries propias (ej: `GetBySucursalAsync`, `ExisteConflictoHorarioAsync`).
- Interfaces en `Application/Interfaces`, implementaciones en `Infrastructure/Repositories`.

### Servicios
- Un servicio por agregado. Reciben repositorios por constructor.
- Mapeo entidad↔DTO dentro del servicio con métodos `private static MapToResponse(...)`.
- Los controladores nunca reciben entidades, solo DTOs.
- Errores de negocio se lanzan como `InvalidOperationException` (el controller los puede capturar).

### Configuraciones EF Core
- Cada entidad tiene su clase `IEntityTypeConfiguration<T>` en `Infrastructure/Data/Configurations/`.
- `AppDbContext` aplica todas automáticamente con `ApplyConfigurationsFromAssembly`.
- Relaciones con ciclos usan `OnDelete(DeleteBehavior.Restrict)` para evitar errores de PostgreSQL.

### Registro en Program.cs
Al agregar un nuevo recurso, registrar en este orden en `Program.cs`:
1. `builder.Services.AddScoped<INuevoRepository, NuevoRepository>();`
2. `builder.Services.AddScoped<NuevoService>();`

## Modelo de dominio

**Eje central:** `Sucursal`. Todo (usuarios, servicios, clientes, turnos, caja) pertenece a una sucursal y es independiente entre sucursales.

**Relaciones clave:**
- `Sucursal` 1→1 `Caja`, 1→1 `ConfiguracionSucursal`
- `Turno` referencia `Cliente`, `Servicio`, `Usuario` (como `Profesional`), y `Sucursal`
- `MovimientoCaja` → puede estar asociado a un `Turno` (opcional)
- `PagoTurno` vincula `Turno` ↔ `MovimientoCaja`
- `Usuario.Permisos` es `List<PermisoSistema>` guardado como `jsonb`

**Lógica de negocio en TurnoService:**
- `FechaHoraFin` se calcula automáticamente: `FechaHoraInicio + Servicio.DuracionAgendaMin`
- Antes de crear o reprogramar, valida conflicto de horario con `ExisteConflictoHorarioAsync`
- `TokenGestion` se genera como `Guid.NewGuid().ToString("N")`
- `MontoSena` se calcula según `Servicio.TipoSena` (Fija o Porcentaje sobre `Servicio.Precio`)

## Entorno de desarrollo

- `appsettings.Development.json` usa `Host=localhost;Port=5433` (puerto del contenedor Docker).
- `launchSettings.json` setea `ASPNETCORE_ENVIRONMENT=Development` para que `dotnet run/watch` use Swagger y auto-migrate.
- El auto-migrate (`db.Database.Migrate()`) solo corre en Development al iniciar la API.
- Para producción/staging: `docker compose -f docker-compose.prod.yml up --build`
