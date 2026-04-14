# Modelo de usuario multi-sucursal (UsuarioSucursalRol)

Documento de referencia para la arquitectura de identidad y membresía de usuarios, introducida en la migración `20260325180642_UsuarioSucursalRol`.

---

## Índice

1. [Problema que resuelve](#1-problema-que-resuelve)
2. [Nueva arquitectura](#2-nueva-arquitectura)
3. [Entidades del dominio](#3-entidades-del-dominio)
4. [Flujo de login](#4-flujo-de-login)
5. [Flujo de invitación](#5-flujo-de-invitación)
6. [Repositorios](#6-repositorios)
7. [Impacto en tablas relacionadas](#7-impacto-en-tablas-relacionadas)
8. [Migración de datos](#8-migración-de-datos)
9. [Convenciones que cambiaron](#9-convenciones-que-cambiaron)

---

## 1. Problema que resuelve

El modelo original tenía `Usuario` con `SucursalId` embebido: **una fila de usuario por sucursal**. Si el mismo email pertenecía a dos sucursales, existían dos filas distintas con contraseñas independientes. Esto generaba:

- Contraseñas duplicadas e inconsistentes (cambiar la contraseña en una sucursal no la cambiaba en la otra).
- Lógica de invitación incorrecta: un usuario ya registrado recibía el flujo de "crear contraseña" al ser invitado a una segunda sucursal.
- Unicidad de email rota conceptualmente: el mismo correo era "dos personas distintas".

---

## 2. Nueva arquitectura

Inspirada en el modelo de Slack: **una identidad global por email, múltiples membresías por workspace (sucursal)**.

```
Usuario (identidad global)
  ├── Id, Email (único global)
  ├── PasswordHash, Nombre, Apellido, Telefono
  ├── TokenRecuperacion, SeleccionToken
  └── FechaCreacion

UsuarioSucursalRol (membresía por sucursal)
  ├── Id, UsuarioId (FK → Usuario), SucursalId (FK → Sucursal)
  ├── Rol, EsOwner, Estado
  ├── InvitacionToken, FechaExpiracionInvitacion
  └── FechaCreacion
  Unique: (UsuarioId, SucursalId)

UsuarioPermiso    → FK: UsuarioSucursalRolId
HorarioLaboral    → FK: UsuarioSucursalRolId
UsuarioServicio   → FK: UsuarioSucursalRolId
```

### Diagrama de relaciones

```
sucursales ──────────────────────────────────────────┐
                                                      │
usuarios ──┐                                          │
           │   usuario_sucursal_roles                 │
           └── UsuarioId  ──────────── SucursalId ───┘
               Id (PK)
               Rol, EsOwner, Estado
               InvitacionToken
               │
               ├──► usuario_permisos (UsuarioSucursalRolId)
               ├──► horarios_laborales (UsuarioSucursalRolId)
               └──► usuario_servicios (UsuarioSucursalRolId)
```

---

## 3. Entidades del dominio

### `Usuario` (solo identidad global)

```csharp
public class Usuario
{
    public Guid    Id          { get; set; }
    public string  Email       { get; set; }
    public string  PasswordHash { get; set; }
    public string  Nombre      { get; set; }
    public string  Apellido    { get; set; }
    public string? Telefono    { get; set; }

    // Recuperación de contraseña
    public string?   TokenRecuperacion           { get; set; }
    public DateTime? TokenRecuperacionExpiracion { get; set; }

    // Selección de sucursal en login multi-sucursal
    public string?   SeleccionToken           { get; set; }
    public DateTime? SeleccionTokenExpiracion { get; set; }

    public DateTime FechaCreacion { get; set; }

    // Navegación
    public ICollection<UsuarioSucursalRol> Sucursales { get; set; } = [];
}
```

> `SucursalId`, `Rol`, `EsOwner`, `Estado`, `InvitacionToken` y `FechaExpiracionInvitacion` **ya no existen** en esta entidad.

### `UsuarioSucursalRol` (membresía)

```csharp
public class UsuarioSucursalRol
{
    public Guid           Id                       { get; set; }
    public Guid           UsuarioId                { get; set; }
    public Guid           SucursalId               { get; set; }
    public RolUsuario     Rol                      { get; set; }
    public bool           EsOwner                  { get; set; }
    public EstadoUsuario  Estado                   { get; set; }
    public string?        InvitacionToken          { get; set; }
    public DateTime?      FechaExpiracionInvitacion { get; set; }
    public DateTime       FechaCreacion            { get; set; }

    public Usuario         Usuario  { get; set; } = null!;
    public Sucursal        Sucursal { get; set; } = null!;

    public ICollection<UsuarioPermiso>   Permisos         { get; set; } = [];
    public ICollection<HorarioLaboral>   HorariosLaborales { get; set; } = [];
    public ICollection<UsuarioServicio>  Servicios         { get; set; } = [];
}
```

### Configuración EF Core destacada

```csharp
// Unique: un usuario puede tener solo una membresía por sucursal
builder.HasIndex(u => new { u.UsuarioId, u.SucursalId }).IsUnique();

// Token de invitación único solo cuando no es null
builder.HasIndex(u => u.InvitacionToken)
    .IsUnique()
    .HasFilter("\"InvitacionToken\" IS NOT NULL");

// Usuario global: email único en toda la plataforma
builder.HasIndex(u => u.Email).IsUnique();  // en UsuarioConfiguration

// Cascades: borrar una membresía borra sus permisos/horarios/servicios
builder.HasMany(u => u.Permisos).WithOne(...).OnDelete(DeleteBehavior.Cascade);
```

---

## 4. Flujo de login

El `AuthService.ValidarAsync` implementa el siguiente árbol de decisión:

```
Email + Password
       │
       ▼
  ¿Usuario existe (global)?
       │ No → error credenciales
       │ Sí
       ▼
  ¿PasswordHash coincide?
       │ No → error credenciales
       │ Sí
       ▼
  ¿Cuántos UsuarioSucursalRol activos tiene?
       │
       ├── 0 → error (cuenta sin sucursales activas)
       │
       ├── 1 → LOGIN DIRECTO
       │        Genera JWT con datos del único USR
       │        Devuelve LoginResponseDto con token
       │
       └── >1 → SELECCIÓN DE SUCURSAL
                Guarda SeleccionToken en Usuario (expira 10 min)
                Devuelve lista de opciones { sucursalId, sucursalNombre, rol }
                Frontend muestra <SucursalSelector />
```

### Segunda llamada: selección de sucursal

```
POST /api/auth/seleccionar-sucursal
{ seleccionToken: "...", sucursalId: "..." }
       │
       ▼
  Busca Usuario por SeleccionToken
  Valida expiración
  Busca UsuarioSucursalRol por (usuario.Id, sucursalId)
  Limpia SeleccionToken del Usuario
  Genera JWT con datos de ese USR
```

> **Cambio clave respecto al modelo anterior:** el DTO ahora usa `sucursalId` en lugar de `usuarioId`, porque con un único Usuario global no alcanza el ID del usuario para identificar cuál sucursal seleccionó.

### Contenido del JWT / sesión

El JWT incluye claims del **Usuario** (nombre, email) y del **UsuarioSucursalRol** elegido (rol, esOwner, sucursalId, permisos). La sesión en el frontend almacena todo en `localStorage` bajo la clave `turnero_session`.

---

## 5. Flujo de invitación

Existen dos variantes según si el email invitado ya tiene cuenta global o no.

### Variante A — Usuario nuevo

```
Admin crea usuario con email nuevo
       │
       ▼
  UsuarioService.CreateAsync
  ¿Existe Usuario global con ese email?  → No
       │
       ▼
  Crear Usuario (PasswordHash = "")
  Crear UsuarioSucursalRol (Estado = PendienteInvitacion, InvitacionToken = NewGuid)
  Crear UsuarioPermiso por cada permiso asignado (FK → USR.Id)
  Enviar email: EnviarInvitacionAsync (con link a /invitacion/{token})
```

```
Usuario abre /invitacion/{token}
       │
       ▼
  GET /api/auth/invitacion/{token}
  Busca UsuarioSucursalRol por InvitacionToken
  Retorna: { nombre, apellido, email, nombreSucursal, rol, yaRegistrado: false }
       │
       ▼
  Frontend muestra formulario de contraseña
       │
       ▼
  POST /api/auth/invitacion/aceptar
  { token, nuevaPassword: "MiClave123" }
       │
       ▼
  InvitacionService.AceptarAsync
  Setea Usuario.PasswordHash
  Setea USR.Estado = Activo
  Limpia InvitacionToken
  Retorna JWT → usuario queda logueado
```

### Variante B — Usuario ya registrado (mismo email, otra sucursal)

```
Admin crea usuario con email existente
       │
       ▼
  UsuarioService.CreateAsync
  ¿Existe Usuario global con ese email?  → Sí
  ¿Ya tiene membresía en esta sucursal?  → No → continuar
       │
       ▼
  NO crear nuevo Usuario (ya existe)
  Crear UsuarioSucursalRol (Estado = PendienteInvitacion, InvitacionToken = NewGuid)
  Crear UsuarioPermiso por cada permiso (FK → nuevo USR.Id)
  Enviar email: EnviarInvitacionUsuarioExistenteAsync
    → Template diferente: "Tu cuenta ya existe. Solo aceptá la invitación."
    → Sin mención de contraseña
```

```
Usuario abre /invitacion/{token}
       │
       ▼
  GET /api/auth/invitacion/{token}
  Retorna: { ..., yaRegistrado: true }
       │
       ▼
  Frontend muestra card "Tu cuenta ya existe"
  con botón "Aceptar invitación y entrar" (sin formulario de contraseña)
       │
       ▼
  POST /api/auth/invitacion/aceptar
  { token, nuevaPassword: null }
       │
       ▼
  InvitacionService.AceptarAsync
  Verifica que Usuario.PasswordHash ya esté seteado (no vacío)
  NO modifica PasswordHash
  Setea USR.Estado = Activo
  Limpia InvitacionToken
  Retorna JWT
```

### DTO actualizado

```csharp
public class AceptarInvitacionDto
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [MinLength(6), MaxLength(200)]
    public string? NuevaPassword { get; set; }  // null para usuarios ya registrados
}
```

### Respuesta de `GET /api/auth/invitacion/{token}` actualizada

```json
{
  "nombre": "Laura",
  "apellido": "Méndez",
  "email": "laura@ejemplo.com",
  "nombreSucursal": "Sede Palermo",
  "rol": "Profesional",
  "yaRegistrado": false
}
```

---

## 6. Repositorios

### `IUsuarioSucursalRolRepository` (nuevo)

```csharp
Task<UsuarioSucursalRol?> GetByIdAsync(Guid id);
Task<UsuarioSucursalRol?> GetByInvitacionTokenAsync(string token);
Task<IEnumerable<UsuarioSucursalRol>> GetBySucursalAsync(Guid sucursalId);
Task<IEnumerable<UsuarioSucursalRol>> GetByUsuarioAsync(Guid usuarioId);
Task<UsuarioSucursalRol?> GetByUsuarioYSucursalAsync(Guid usuarioId, Guid sucursalId);
Task AddAsync(UsuarioSucursalRol rol);
void Update(UsuarioSucursalRol rol);
Task SaveChangesAsync();
```

Todos los métodos incluyen `.Include(u => u.Usuario).Include(u => u.Sucursal).Include(u => u.Permisos)`.

### `IUsuarioRepository` (simplificado)

Métodos **eliminados**:
- `GetBySucursalAsync` → ahora está en `IUsuarioSucursalRolRepository`
- `GetByEmailAsync(Guid sucursalId, string email)` → reemplazado por global
- `GetByInvitacionTokenAsync` → movido a `IUsuarioSucursalRolRepository`
- `GetByIdConPermisosAsync` → los permisos se cargan desde USR

Métodos **actualizados**:
- `GetByEmailAsync(string email)` → busca globalmente, retorna `Task<Usuario?>`
- `GetByEmailGlobalAsync(string email)` → idem (alias semántico)
- `GetBySeleccionTokenAsync(string token)` → retorna `Task<Usuario?>` (singular)

---

## 7. Impacto en tablas relacionadas

### Tablas con FK cambiada a `UsuarioSucursalRolId`

| Tabla | FK anterior | FK nueva |
|---|---|---|
| `usuario_permisos` | `UsuarioId` | `UsuarioSucursalRolId` |
| `horarios_laborales` | `UsuarioId` | `UsuarioSucursalRolId` |
| `usuario_servicios` | `UsuarioId` | `UsuarioSucursalRolId` |

**Implicancia:** los permisos, horarios y servicios son **por membresía**, no por persona. El mismo profesional puede tener horarios distintos en cada sucursal.

### Tablas que mantienen FK a `usuarios.Id`

| Tabla | Columna | Motivo |
|---|---|---|
| `turnos` | `ProfesionalId` | El profesional de un turno es una persona (Usuario), no una membresía |
| `movimientos_caja` | `UsuarioId` | Auditoría: quién registró el movimiento |
| `comentarios_turno` | `AutorUsuarioId` | Auditoría: quién escribió el comentario |
| `adjuntos_turno` | `AutorUsuarioId` | Auditoría: quién subió el adjunto |
| `bloqueos_agenda` | `UsuarioId` | El bloqueo es del profesional-persona |

### Compatibilidad en repositorios de HorarioLaboral y UsuarioServicio

Para no romper `TurnoService` y `PublicoService` (que trabajan con `UsuarioId`), los métodos de lectura siguen aceptando `usuarioId` y hacen el join internamente:

```csharp
// HorarioLaboralRepository
public async Task<IEnumerable<HorarioLaboral>> GetByUsuarioIdAsync(Guid usuarioId)
    => await _context.HorariosLaborales
        .Where(h => h.UsuarioSucursalRol.UsuarioId == usuarioId)
        .ToListAsync();
```

Solo `ReplaceAsync` recibe `usuarioSucursalRolId` (llamado desde `UsuarioService`, que tiene el USR disponible).

---

## 8. Migración de datos

Migración: `20260325180642_UsuarioSucursalRol`

El `Up()` opera en este orden para garantizar integridad:

1. Drop FKs antiguas (horarios, permisos, servicios, usuarios→sucursales)
2. Drop índices viejos en `usuarios`
3. Agregar `FechaCreacion` a `usuarios`
4. Crear tabla `usuario_sucursal_roles`
5. **INSERT**: poblar USR desde `usuarios` (mientras `SucursalId`, `Rol`, etc. aún existen)
6. **UPDATE**: redirigir `usuario_permisos`, `horarios_laborales`, `usuario_servicios` de `UsuarioId` a `USR.Id`
7. Renombrar columnas (`UsuarioId` → `UsuarioSucursalRolId`) — con valores ya correctos
8. Drop columnas viejas de `usuarios` (`SucursalId`, `Rol`, `EsOwner`, `Estado`, `InvitacionToken`, `FechaExpiracionInvitacion`)
9. **Deduplicar**: bloque PL/pgSQL que por cada email duplicado elige un Usuario canónico, redirige todas las FKs y borra los duplicados
10. Crear índice único en `usuarios.Email`
11. Crear índices en `usuario_sucursal_roles`
12. Agregar nuevas FKs

> **Paso 9 es crítico**: en la base de datos vieja podía haber el mismo email en múltiples filas de `usuarios`. Antes de crear el índice único de email, el bloque PL/pgSQL elige la fila con el `Id` más bajo como canónica, actualiza las referencias en `turnos`, `movimientos_caja`, `comentarios_turno`, `adjuntos_turno` y `bloqueos_agenda`, y luego borra las filas duplicadas.

---

## 9. Convenciones que cambiaron

| Antes | Ahora |
|---|---|
| `UsuarioRepository.GetBySucursalAsync(sucursalId)` | `UsuarioSucursalRolRepository.GetBySucursalAsync(sucursalId)` |
| `UsuarioService.GetByIdAsync(usuarioId)` — id del Usuario | `UsuarioService.GetByIdAsync(usrId)` — id del USR |
| `MapToResponse(Usuario u)` | `MapToResponse(UsuarioSucursalRol usr)` — lee `usr.Usuario.*` para datos personales |
| `PUT /api/usuarios/{usuarioId}` — id del Usuario | `PUT /api/usuarios/{usrId}` — id del USR |
| `SeleccionarSucursalDto.UsuarioId` | `SeleccionarSucursalDto.SucursalId` |
| `authApi.seleccionarSucursal(token, usuarioId)` | `authApi.seleccionarSucursal(token, sucursalId)` |
| Email único por sucursal | Email único global |
| `InvitacionToken` en tabla `usuarios` | `InvitacionToken` en tabla `usuario_sucursal_roles` |
| `AceptarInvitacionDto.NuevaPassword` obligatorio | `NuevaPassword` opcional (null si ya registrado) |
