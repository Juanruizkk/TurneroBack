# Flujo de invitación por email — Documentación técnica

Sistema de incorporación de usuarios al equipo de una sucursal mediante un enlace de invitación enviado por email.

> **Actualizado**: este documento refleja el modelo multi-sucursal (`UsuarioSucursalRol`). Ver [`usuario-sucursal-rol.md`](./usuario-sucursal-rol.md) para el contexto arquitectural completo.

---

## Índice

1. [Descripción general](#1-descripción-general)
2. [Variante A — Usuario nuevo](#2-variante-a--usuario-nuevo)
3. [Variante B — Usuario ya registrado](#3-variante-b--usuario-ya-registrado)
4. [Entidad: UsuarioSucursalRol](#4-entidad-usuariosucursalrol)
5. [Endpoints](#5-endpoints)
6. [Servicios involucrados](#6-servicios-involucrados)
7. [Plantillas de email](#7-plantillas-de-email)
8. [Estados del usuario](#8-estados-del-usuario)
9. [Reglas de negocio](#9-reglas-de-negocio)
10. [Diagrama de secuencia](#10-diagrama-de-secuencia)
11. [Errores posibles](#11-errores-posibles)

---

## 1. Descripción general

Cuando un owner o administrador crea un nuevo usuario en su sucursal, **el sistema no pide contraseña**. En su lugar:

1. Genera un token único de invitación en `UsuarioSucursalRol`.
2. Envía un email al invitado con un enlace personalizado.
3. El invitado hace clic en el enlace.
   - **Si es primera vez en la plataforma**: establece su contraseña y queda activo.
   - **Si ya tiene cuenta**: solo acepta la invitación (sin ingresar contraseña) y queda activo en la nueva sucursal.

Este flujo garantiza que la contraseña siempre la define el propio usuario y que las cuentas existentes no se duplican.

---

## 2. Variante A — Usuario nuevo

El email invitado **no existe** en la tabla `usuarios`.

```
Administrador                  Backend                            Nuevo usuario
     │                             │                                    │
     │  POST /api/usuarios         │                                    │
     │ ──────────────────────────► │                                    │
     │                             │  ¿Email existe globalmente? → No   │
     │                             │  Crear Usuario (PasswordHash="")   │
     │                             │  Crear UsuarioSucursalRol          │
     │                             │    Estado=PendienteInvitacion      │
     │                             │    InvitacionToken=NewGuid         │
     │                             │    FechaExpiracion=now+7d          │
     │                             │  Crear UsuarioPermiso (× n permisos)
     │                             │  Enviar email "primera invitación" │
     │  201 Created                │ ─────────────────────────────────► │
     │ ◄────────────────────────── │                                    │
     │                             │     (usuario abre el enlace)       │
     │                             │                                    │
     │                             │  GET /api/auth/invitacion/{token}  │
     │                             │ ◄──────────────────────────────────│
     │                             │  { ..., yaRegistrado: false }      │
     │                             │ ─────────────────────────────────► │
     │                             │   (frontend muestra form password) │
     │                             │                                    │
     │                             │  POST /api/auth/invitacion/aceptar │
     │                             │  { token, nuevaPassword: "..." }   │
     │                             │ ◄──────────────────────────────────│
     │                             │  Usuario.PasswordHash = nueva      │
     │                             │  USR.Estado = Activo               │
     │                             │  USR.InvitacionToken = null        │
     │                             │  Retorna JWT                       │
     │                             │ ─────────────────────────────────► │ ← logueado
```

---

## 3. Variante B — Usuario ya registrado

El email invitado **ya existe** en la tabla `usuarios` (tiene cuenta en otra sucursal).

```
Administrador                  Backend                          Usuario existente
     │                             │                                    │
     │  POST /api/usuarios         │                                    │
     │ ──────────────────────────► │                                    │
     │                             │  ¿Email existe globalmente? → Sí   │
     │                             │  ¿Ya tiene USR en esta sucursal?→No│
     │                             │  NO crear nuevo Usuario            │
     │                             │  Crear UsuarioSucursalRol          │
     │                             │    Estado=PendienteInvitacion      │
     │                             │    InvitacionToken=NewGuid         │
     │                             │  Crear UsuarioPermiso (× n permisos)
     │                             │  Enviar email "ya registrado"      │
     │  201 Created                │ ─────────────────────────────────► │
     │ ◄────────────────────────── │                                    │
     │                             │     (usuario abre el enlace)       │
     │                             │                                    │
     │                             │  GET /api/auth/invitacion/{token}  │
     │                             │ ◄──────────────────────────────────│
     │                             │  { ..., yaRegistrado: true }       │
     │                             │ ─────────────────────────────────► │
     │                             │  (frontend muestra botón "Aceptar")│
     │                             │                                    │
     │                             │  POST /api/auth/invitacion/aceptar │
     │                             │  { token, nuevaPassword: null }    │
     │                             │ ◄──────────────────────────────────│
     │                             │  (NO modifica PasswordHash)        │
     │                             │  USR.Estado = Activo               │
     │                             │  USR.InvitacionToken = null        │
     │                             │  Retorna JWT                       │
     │                             │ ─────────────────────────────────► │ ← logueado
```

---

## 4. Entidad: UsuarioSucursalRol

Los campos de invitación ya no están en `Usuario` sino en `UsuarioSucursalRol`:

| Campo | Tipo | Descripción |
|---|---|---|
| `InvitacionToken` | `string?` | Token UUID sin guiones. Índice parcial único (solo cuando no es null). Se limpia al aceptar. |
| `FechaExpiracionInvitacion` | `DateTime?` | Vencimiento (7 días desde creación). Se limpia al aceptar. |
| `Estado` | `EstadoUsuario` | `PendienteInvitacion` hasta que se acepte, luego `Activo`. |

### Configuración Fluent API

```csharp
// En UsuarioSucursalRolConfiguration.cs
builder.Property(u => u.InvitacionToken)
    .HasMaxLength(100)
    .IsRequired(false);

// Índice parcial: único solo cuando el token no es null
builder.HasIndex(u => u.InvitacionToken)
    .IsUnique()
    .HasFilter("\"InvitacionToken\" IS NOT NULL");
```

---

## 5. Endpoints

### `POST /api/usuarios`

Crea al usuario (o solo la membresía si ya existe) e inicia el flujo de invitación.

**Request:**
```json
{
  "sucursalId": "47b55896-b045-4dc2-b896-e542364b9916",
  "nombre": "Laura",
  "apellido": "Méndez",
  "email": "laura@ejemplo.com",
  "telefono": "11 4567 8901",
  "rol": "Profesional",
  "permisos": ["AgendarTurnos", "VerTelefonoClientes"]
}
```

**Response `201 Created`:**
```json
{
  "id": "a1b2c3d4-...",
  "nombre": "Laura",
  "apellido": "Méndez",
  "email": "laura@ejemplo.com",
  "rol": "Profesional",
  "estado": "PendienteInvitacion",
  "esOwner": false,
  "permisos": ["AgendarTurnos", "VerTelefonoClientes"]
}
```

> `id` es el `UsuarioSucursalRol.Id`, no el `Usuario.Id`.

---

### `GET /api/auth/invitacion/{token}`

Devuelve información pública del token para mostrar en la pantalla de bienvenida. **No requiere autenticación.**

**Response `200 OK`:**
```json
{
  "nombre": "Laura",
  "apellido": "Méndez",
  "email": "laura@ejemplo.com",
  "nombreSucursal": "Belleza Studio - Palermo",
  "rol": "Profesional",
  "yaRegistrado": false
}
```

`yaRegistrado: true` cuando el `Usuario.PasswordHash` ya tiene valor (el usuario ya activó su cuenta en otra sucursal).

**Errores:**

| Código | Causa |
|---|---|
| `400` | Token no existe, ya fue utilizado, o está vencido |

---

### `POST /api/auth/invitacion/aceptar`

Activa la membresía y retorna un JWT.

**Request — usuario nuevo:**
```json
{
  "token": "a3f2e1b4c5d6e7f8...",
  "nuevaPassword": "MiContrasena123!"
}
```

**Request — usuario ya registrado:**
```json
{
  "token": "a3f2e1b4c5d6e7f8...",
  "nuevaPassword": null
}
```

**Validaciones:**
- `token`: requerido
- `nuevaPassword`: opcional; si se envía, mínimo 6 caracteres

**Response `200 OK`:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "rol": "Profesional",
  "nombre": "Laura",
  "apellido": "Méndez",
  "email": "laura@ejemplo.com",
  "esSuperAdmin": false,
  "esOwner": false,
  "userId": "a1b2c3d4-...",
  "sucursalId": "47b55896-..."
}
```

---

### `POST /api/usuarios/{id}/reenviar-invitacion`

Regenera el token (invalida el anterior) y reenvía el email correspondiente (variante A o B según si tiene contraseña). Solo disponible para usuarios en estado `PendienteInvitacion`.

**Response `204 No Content`**

---

## 6. Servicios involucrados

### `UsuarioService` — Application layer

```csharp
public async Task<UsuarioResponseDto> CreateAsync(CreateUsuarioDto dto)
{
    var emailNorm = dto.Email.ToLowerInvariant().Trim();
    var usuarioExistente = await _usuarioRepo.GetByEmailAsync(emailNorm);

    if (usuarioExistente is not null)
    {
        // Variante B: solo crear USR (no duplicar Usuario)
        var usrExistente = await _usrRepository.GetByUsuarioYSucursalAsync(
            usuarioExistente.Id, dto.SucursalId);
        if (usrExistente is not null)
            throw new InvalidOperationException("El usuario ya pertenece a esta sucursal.");

        var nuevoUsr = new UsuarioSucursalRol { ... InvitacionToken = token };
        await _usrRepository.AddAsync(nuevoUsr);
        // Crear permisos con FK → nuevoUsr.Id
        await _emailService.EnviarInvitacionUsuarioExistenteAsync(...);
        return MapToResponse(nuevoUsr);
    }

    // Variante A: crear Usuario + USR
    var usuario = new Usuario { PasswordHash = "", Email = emailNorm, ... };
    var usr = new UsuarioSucursalRol { UsuarioId = usuario.Id, ... InvitacionToken = token };
    await _usuarioRepo.AddAsync(usuario);
    await _usrRepository.AddAsync(usr);
    await _emailService.EnviarInvitacionAsync(...);
    return MapToResponse(usr);
}
```

### `InvitacionService` — Application layer

```csharp
public async Task<UsuarioAutenticadoDto> AceptarAsync(AceptarInvitacionDto dto)
{
    var usr = await _usrRepo.GetByInvitacionTokenAsync(dto.Token)
        ?? throw new InvalidOperationException("El enlace de invitación no es válido.");

    if (usr.FechaExpiracionInvitacion < DateTime.UtcNow)
        throw new InvalidOperationException("El enlace venció. Pedile al administrador que reenvíe la invitación.");

    if (dto.NuevaPassword is not null)
    {
        // Variante A
        usr.Usuario.PasswordHash = dto.NuevaPassword; // TODO: BCrypt
    }
    else
    {
        // Variante B — verificar que ya tiene contraseña
        if (string.IsNullOrEmpty(usr.Usuario.PasswordHash))
            throw new InvalidOperationException("Debés establecer una contraseña.");
    }

    usr.Estado           = EstadoUsuario.Activo;
    usr.InvitacionToken  = null;
    usr.FechaExpiracionInvitacion = null;

    _usrRepo.Update(usr);
    await _usrRepo.SaveChangesAsync();
    return MapToAutenticado(usr.Usuario, usr);
}
```

### `IEmailService` — dos métodos de invitación

```csharp
// Variante A: usuario nuevo — incluye instrucciones de contraseña
Task EnviarInvitacionAsync(
    string destinatarioEmail, string destinatarioNombre,
    string nombreSucursal, string linkInvitacion);

// Variante B: usuario existente — solo acepta la membresía
Task EnviarInvitacionUsuarioExistenteAsync(
    string destinatarioEmail, string destinatarioNombre,
    string nombreSucursal, string linkInvitacion);
```

---

## 7. Plantillas de email

### Variante A — Primera invitación

- Asunto: `Tu invitación para unirte a {nombreSucursal}`
- Cuerpo: saludo, nombre de sucursal, instrucción de establecer contraseña, botón "Aceptar invitación", aviso vencimiento 7 días.

### Variante B — Usuario ya registrado

- Asunto: `Te invitaron a unirte a {nombreSucursal}`
- Cuerpo: "Tu correo ya está registrado en TurneroApp. Solo necesitás aceptar la invitación para unirte a **{sucursal}**." Sin mención de contraseña. Botón "Aceptar invitación".

---

## 8. Estados del usuario

El estado vive en `UsuarioSucursalRol.Estado`, no en `Usuario`.

```
                    ┌─────────────────────┐
       crear        │                     │
  ────────────────► │  PendienteInvitacion│ ──── reenviar invitacion
                    │                     │         (regenera token, mismo estado)
                    └──────────┬──────────┘
                               │
                               │  aceptar invitación
                               ▼
                    ┌─────────────────────┐
                    │       Activo        │ ──── soft delete ──► Inactivo
                    └─────────────────────┘
```

| Estado | Descripción |
|---|---|
| `PendienteInvitacion` | Membresía creada, email enviado, aún no aceptó |
| `Activo` | Invitación aceptada, puede iniciar sesión en esta sucursal |
| `Inactivo` | Dado de baja (soft delete sobre la membresía, no sobre el Usuario global) |

---

## 9. Reglas de negocio

| Regla | Detalle |
|---|---|
| **Sin contraseña en creación** | El DTO no incluye contraseña. La define el invitado. |
| **Vencimiento de 7 días** | Si vence, el admin reenvía la invitación para renovar el token. |
| **Token de un solo uso** | Al aceptar, `InvitacionToken` se pone en `null`. Reusar el token devuelve error. |
| **Token único activo** | Índice parcial en DB garantiza unicidad cuando no es null. |
| **No duplicar Usuario** | Si el email ya existe globalmente, solo se crea `UsuarioSucursalRol`. |
| **Un USR por sucursal** | El índice unique `(UsuarioId, SucursalId)` impide duplicados. |
| **Email no bloquea creación** | Si el envío falla, la membresía igual queda en DB. Se puede reenviar. |
| **Login bloqueado en pendiente** | `AuthService` solo acepta USR con `Estado = Activo`. |
| **Soft delete es por membresía** | Inactivar un usuario en sucursal A no afecta su acceso a sucursal B. |

---

## 10. Diagrama de secuencia

```
Admin Frontend     UsuariosController    UsuarioService       IEmailService       DB
      │                    │                   │                    │              │
      │  POST /api/usuarios│                   │                    │              │
      │ ──────────────────►│                   │                    │              │
      │                    │  CreateAsync(dto) │                    │              │
      │                    │ ─────────────────►│                    │              │
      │                    │                   │  GetByEmailAsync   │              │
      │                    │                   │ ─────────────────────────────────►│
      │                    │                   │  null / usuario    │              │
      │                    │                   │ ◄─────────────────────────────────│
      │                    │                   │                    │              │
      │                    │                   │  AddAsync(usr)     │              │
      │                    │                   │ ─────────────────────────────────►│
      │                    │                   │  EnviarInvitacion* │              │
      │                    │                   │ ──────────────────►│              │
      │                    │  201 + USRDto     │                    │  email send  │
      │ ◄──────────────────│ ◄─────────────────│                    │             │
```

---

## 11. Errores posibles

### Al crear usuario

| Error | HTTP | Mensaje |
|---|---|---|
| Email ya existe en esta sucursal | `409 Conflict` | `"El usuario ya pertenece a esta sucursal."` |
| Sucursal no encontrada | `400 Bad Request` | `"No se encontró la sucursal con id ..."` |

### Al consultar/aceptar token

| Error | HTTP | Mensaje |
|---|---|---|
| Token no existe o ya fue usado | `400 Bad Request` | `"El enlace de invitación no es válido."` |
| Token vencido | `400 Bad Request` | `"El enlace venció. Pedile al administrador que reenvíe la invitación."` |
| `nuevaPassword` null pero sin contraseña previa | `400 Bad Request` | `"Debés establecer una contraseña."` |

---

## Pendiente / TODO

- [ ] **Hashing de contraseña**: reemplazar asignación directa por `BCrypt.HashPassword` (paquete `BCrypt.Net-Next`).
- [ ] **Rate limiting** en `POST /api/auth/invitacion/aceptar` para evitar fuerza bruta sobre tokens.
- [ ] **Expiración configurable** desde `appsettings.json` (`Invitacion:ExpirationDays`).
