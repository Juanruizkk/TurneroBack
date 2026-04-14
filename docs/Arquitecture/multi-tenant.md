# Arquitectura Multi-Tenant

MetaTurnos implementa un modelo multi-tenant donde cada **Sucursal** es un tenant independiente. Un usuario puede pertenecer a múltiples sucursales con distintos roles y permisos en cada una — el mismo patrón que usa Slack con sus workspaces.

---

## Modelo de datos

### Sucursal — el tenant

La `Sucursal` es la unidad central. Todo dato del negocio le pertenece a una sucursal y es completamente independiente entre sucursales:

```
Sucursal
  ├── Servicios
  ├── Clientes
  ├── Turnos
  ├── Caja
  ├── BloqueoAgenda
  ├── ConfiguracionSucursal (1:1)
  └── SucursalSuscripcion (plan activo)
```

### Usuario — identidad global

`Usuario` representa a la persona, no a su rol en un negocio. El email es único en toda la plataforma.

```
Usuario (global)
  ├── Email — único en toda la plataforma
  ├── PasswordHash — una sola contraseña para todas las sucursales
  └── UsuarioSucursalRol (1:N) — membresías por sucursal
        ├── Rol (Administrativo / Profesional / Caja)
        ├── EsOwner (bool)
        ├── Estado (PendienteInvitacion / Activo / Inactivo)
        ├── UsuarioPermiso (1:N) — permisos granulares de ESA membresía
        ├── HorarioLaboral (1:N) — horarios de ESA membresía
        └── UsuarioServicio (1:N) — servicios asignados en ESA membresía
```

**Restricciones clave:**
- Un usuario solo puede tener una membresía por sucursal: `UNIQUE (UsuarioId, SucursalId)`
- Un usuario puede ser Profesional en la Sucursal A y Administrativo en la Sucursal B

---

## Flujo de autenticación

### Login estándar (1 sucursal)

```
POST /api/auth/login { email, password }
  → Verificar credenciales
  → Buscar membresías activas
  → 1 membresía activa → emitir JWT con sucursalId
```

### Login multi-sucursal (2+ sucursales)

```
POST /api/auth/login { email, password }
  → Verificar credenciales
  → Buscar membresías activas
  → N membresías activas → guardar SeleccionToken (10 min)
  → Retornar { requiresSelection: true, opciones: [...] }
  → Frontend muestra selector de sucursal

POST /api/auth/seleccionar-sucursal { seleccionToken, sucursalId }
  → Validar token no expirado
  → Emitir JWT con sucursalId elegido
```

### Super Admin

- Credenciales hardcodeadas en `appsettings.json`
- JWT sin `sucursalId` — puede ver todos los tenants
- Endpoints exclusivos en `SuperAdminController`

---

## JWT y contexto de tenant

El JWT incluye el `sucursalId` del tenant activo en los claims:

```
sub          → usuarioId (guid)
email        → usuario.Email
sucursalId   → guid del tenant activo  ← clave de aislamiento
role         → Administrativo / Profesional / Caja
esOwner      → bool
esSuperAdmin → bool
permiso      → (uno por cada permiso habilitado)
nombre / apellido
```

El token tiene vigencia de 24 horas. Para SignalR, se lee desde query string `?access_token=...`.

---

## Aislamiento de datos

El aislamiento se implementa en tres capas (defensa en profundidad):

### Capa 1 — ITenantContext

`HttpTenantContext` extrae el `sucursalId` del JWT en cada request:

```csharp
// Infrastructure/MultiTenant/HttpTenantContext.cs
public Guid? SucursalId =>
    _httpContext.User.FindFirst("sucursalId") is { } c
        ? Guid.Parse(c.Value)
        : null;
```

Retorna `null` para SuperAdmin y endpoints públicos (sin sucursalId en el token).

### Capa 2 — Query Filters de EF Core

`AppDbContext` aplica filtros automáticos en las entidades principales. Toda query agrega automáticamente `WHERE sucursal_id = {tenantId}`:

| Entidad | Filtro automático |
|---|---|
| `Servicio` | `e.SucursalId == tenantContext.SucursalId` |
| `Cliente` | `e.SucursalId == tenantContext.SucursalId` |
| `Turno` | `e.SucursalId == tenantContext.SucursalId` |
| `Caja` | `e.SucursalId == tenantContext.SucursalId` |
| `BloqueoAgenda` | `e.SucursalId == tenantContext.SucursalId` |
| `UsuarioSucursalRol` | `e.SucursalId == tenantContext.SucursalId` |
| `SucursalSuscripcion` | `e.SucursalId == tenantContext.SucursalId` |

El filtro se desactiva automáticamente cuando `SucursalId` es `null` (SuperAdmin o endpoints públicos).

### Capa 3 — Repositorios

Los repositorios filtran explícitamente por `SucursalId` en sus queries custom, como segunda línea de defensa:

```csharp
// Ejemplo: ClienteRepository
public Task<List<Cliente>> GetBySucursalAsync(Guid sucursalId) =>
    _db.Clientes.Where(c => c.SucursalId == sucursalId).ToListAsync();
```

---

## Roles y permisos

### Roles base

| Rol | Descripción |
|---|---|
| `Administrativo` | Gestión general del negocio |
| `Profesional` | Prestador de servicios (tiene agenda propia) |
| `Caja` | Operaciones de caja y cobranzas |

El flag `EsOwner` identifica al dueño/fundador de la sucursal. Un owner puede hacer todo lo que un Administrativo más operaciones de gestión de sucursal.

### Permisos granulares (UsuarioPermiso)

Los permisos son independientes del rol y se asignan por membresía:

| Permiso | Descripción |
|---|---|
| `AgendarTurnos` | Crear nuevos turnos |
| `EditarTurnos` | Modificar turnos existentes |
| `CargarCobranzas` | Registrar pagos en caja |
| `VerTelefonoClientes` | Ver el teléfono de los clientes |
| `GestionarAjustes` | Ajustes y devoluciones |

Restricción: `UNIQUE (UsuarioSucursalRolId, Permiso)` — no hay permisos duplicados por membresía.

Los permisos llegan en el JWT como claims individuales y se validan en los endpoints correspondientes.

---

## Flujo de invitación

### Nuevo usuario (email no existe en la plataforma)

```
Admin invita email@nuevo.com
  → Se crea Usuario (PasswordHash vacío)
  → Se crea UsuarioSucursalRol (Estado = PendienteInvitacion, InvitacionToken)
  → Email con link /invitacion/{token}

Usuario hace clic → GET /api/auth/invitacion/{token}
  → { yaRegistrado: false } → Frontend muestra formulario de contraseña

POST /api/auth/invitacion/aceptar { token, nuevaPassword }
  → Se guarda el hash de la contraseña
  → Estado → Activo, InvitacionToken → null
  → Retorna JWT (el usuario queda logueado)
```

### Usuario existente (ya tiene cuenta en otra sucursal)

```
Admin invita email@existente.com
  → Usuario global ya existe → NO se crea otro
  → Se crea solo UsuarioSucursalRol (Estado = PendienteInvitacion, InvitacionToken)
  → Email diferente (sin campo de contraseña)

POST /api/auth/invitacion/aceptar { token, nuevaPassword: null }
  → La contraseña existente no se modifica
  → Estado → Activo, InvitacionToken → null
  → Retorna JWT
```

---

## Planes y suscripciones

Cada sucursal tiene una suscripción activa a un plan:

```
Plan
  ├── Nombre, Descripcion
  ├── MaxUsuarios
  ├── MaxTurnosMes (null = ilimitado)
  └── PrecioMensual

SucursalSuscripcion
  ├── SucursalId
  ├── PlanId
  ├── Estado (EstadoSuscripcion)
  ├── FechaInicio / FechaVencimiento
  └── EnPeriodoPrueba (bool)
```

`SucursalSuscripcion` está filtrada por tenant context igual que el resto de las entidades.

---

## Endpoints públicos (sin autenticación)

Estos endpoints no requieren JWT ni tenant activo:

| Endpoint | Descripción |
|---|---|
| `POST /api/auth/register` | Registro: crea sucursal + owner + suscripción trial |
| `POST /api/auth/login` | Login con email/password |
| `POST /api/auth/seleccionar-sucursal` | Selección de sucursal (multi-tenant) |
| `GET /api/auth/invitacion/{token}` | Info de invitación |
| `POST /api/auth/invitacion/aceptar` | Aceptar invitación |
| `POST /api/auth/forgot-password` | Solicitar reset de contraseña |
| `POST /api/auth/reset-password` | Aplicar nuevo password |
| `GET /api/turnos/by-token/{token}` | Consulta pública de turno (sin auth) |
| `GET /api/publico/...` | Booking público (sucursalId en query param, no en JWT) |

---

## Convenciones importantes

- **`UsuarioSucursalRol.Id` es el ID de membresía**, no `Usuario.Id`. Los endpoints `GET|PUT|DELETE /api/usuarios/{id}` reciben este ID.
- **`Turno.ProfesionalId` apunta a `Usuario.Id`** (la persona física), no a `UsuarioSucursalRolId`. Un profesional puede trabajar en varias sucursales.
- **Audit trails** (`MovimientoCaja.UsuarioId`, `ComentarioTurno.AutorUsuarioId`) también apuntan a `Usuario.Id` porque registran quién realizó la acción, no su rol en la sucursal.
- Los **tokens temporales** (`SeleccionToken`, `InvitacionToken`, `TokenRecuperacion`) usan filtered unique indexes — únicos solo cuando no son NULL.

---

## Archivos clave

| Archivo | Descripción |
|---|---|
| `Infrastructure/Data/AppDbContext.cs` | Query filters globales por tenant |
| `Infrastructure/MultiTenant/ITenantContext.cs` | Interfaz del contexto de tenant |
| `Api/Services/HttpTenantContext.cs` | Extrae sucursalId del JWT por request |
| `Api/Services/JwtService.cs` | Construye los claims del token |
| `Application/Services/AuthService.cs` | Lógica de login y selección de sucursal |
| `Application/Services/InvitacionService.cs` | Flujo de invitación |
| `Application/Services/RegisterService.cs` | Registro inicial (sucursal + owner) |
| `Infrastructure/Data/Configurations/UsuarioSucursalRolConfiguration.cs` | Constraints de membresía |

Ver también: `docs/Arquitecture/usuario-sucursal-rol.md` para el detalle del modelo de usuarios.
