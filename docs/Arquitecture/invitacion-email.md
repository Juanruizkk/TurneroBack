# Flujo de invitación por email — Documentación técnica

Sistema de incorporación de usuarios al equipo de una sucursal mediante un enlace de invitación enviado por email.

---

## Índice

1. [Descripción general](#1-descripción-general)
2. [Flujo completo](#2-flujo-completo)
3. [Entidad: Usuario](#3-entidad-usuario)
4. [Endpoints](#4-endpoints)
5. [Servicios involucrados](#5-servicios-involucrados)
6. [Plantilla de email](#6-plantilla-de-email)
7. [Configuración SMTP](#7-configuración-smtp)
8. [Estados del usuario](#8-estados-del-usuario)
9. [Reglas de negocio](#9-reglas-de-negocio)
10. [Diagrama de secuencia](#10-diagrama-de-secuencia)
11. [Errores posibles](#11-errores-posibles)

---

## 1. Descripción general

Cuando un owner o administrador crea un nuevo usuario en su sucursal, **el sistema no pide contraseña**. En su lugar:

1. Genera un token único de invitación.
2. Envía un email al nuevo usuario con un enlace personalizado.
3. El usuario hace clic en el enlace, establece su contraseña y queda activo en el sistema.

Este flujo garantiza que la contraseña siempre la define el propio usuario, sin que el administrador la conozca.

---

## 2. Flujo completo

```
Administrador                  Backend                       Nuevo usuario
     │                             │                               │
     │  POST /api/usuarios         │                               │
     │ ──────────────────────────► │                               │
     │                             │  Genera InvitacionToken       │
     │                             │  Estado = PendienteInvitacion │
     │                             │  FechaExpiracion = now + 7d   │
     │                             │  Guarda en DB                 │
     │                             │                               │
     │                             │  Envía email con enlace       │
     │                             │ ─────────────────────────────►│
     │  201 Created                │                               │
     │ ◄────────────────────────── │                               │
     │                             │                               │
     │                             │      (usuario abre el enlace) │
     │                             │                               │
     │                             │  GET /api/auth/invitacion/:token
     │                             │ ◄─────────────────────────────│
     │                             │  { nombre, sucursal, rol }    │
     │                             │ ─────────────────────────────►│
     │                             │                               │
     │                             │  POST /api/auth/invitacion/aceptar
     │                             │  { token, nuevaPassword }     │
     │                             │ ◄─────────────────────────────│
     │                             │  Valida token y vencimiento   │
     │                             │  Estado = Activo              │
     │                             │  InvitacionToken = null       │
     │                             │  PasswordHash = nuevaPassword │
     │                             │  Retorna JWT                  │
     │                             │ ─────────────────────────────►│
     │                             │                               │  ← ya logueado
```

---

## 3. Entidad: Usuario

Campos agregados para el flujo de invitación:

| Campo | Tipo | Descripción |
|---|---|---|
| `InvitacionToken` | `string?` | Token UUID sin guiones, único por índice parcial (solo cuando no es null). Se borra al aceptar. |
| `FechaExpiracionInvitacion` | `DateTime?` | Fecha límite para aceptar (7 días desde la creación). Se borra al aceptar. |

### Configuración Fluent API

```csharp
builder.Property(u => u.InvitacionToken)
    .HasMaxLength(100)
    .IsRequired(false);

builder.Property(u => u.FechaExpiracionInvitacion)
    .HasColumnType("timestamptz")
    .IsRequired(false);

// Índice parcial: único solo cuando el token no es null
builder.HasIndex(u => u.InvitacionToken)
    .IsUnique()
    .HasFilter("\"invitacion_token\" IS NOT NULL");
```

> El índice parcial es clave: múltiples usuarios pueden tener `null` sin violar unicidad, pero ninguno puede compartir el mismo token activo.

---

## 4. Endpoints

### `POST /api/usuarios`
Crea el usuario e inicia el flujo de invitación.

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

> ⚠️ `passwordHash` ya **no forma parte del DTO** de creación. La contraseña la define el invitado.

**Response `201 Created`:**
```json
{
  "id": "a1b2c3d4-...",
  "nombre": "Laura",
  "apellido": "Méndez",
  "email": "laura@ejemplo.com",
  "rol": "Profesional",
  "estado": "PendienteInvitacion",
  "permisos": ["AgendarTurnos", "VerTelefonoClientes"]
}
```

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
  "rol": "Profesional"
}
```

**Errores:**

| Código | Causa |
|---|---|
| `400` | Token no existe o ya fue utilizado |
| `400` | Token vencido |

---

### `POST /api/auth/invitacion/aceptar`
Activa la cuenta y retorna un JWT para que el usuario quede logueado inmediatamente.

**Request:**
```json
{
  "token": "a3f2e1b4c5d6e7f8...",
  "nuevaPassword": "MiContrasena123!"
}
```

**Validaciones:**
- `token`: requerido
- `nuevaPassword`: requerido, mínimo 6 caracteres, máximo 200

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
Regenera el token (invalida el anterior) y reenvía el email. Solo disponible para usuarios en estado `PendienteInvitacion`.

**Response `204 No Content`**

**Errores:**

| Código | Causa |
|---|---|
| `400` | El usuario no está en estado `PendienteInvitacion` |
| `404` | Usuario no encontrado |

---

## 5. Servicios involucrados

### `UsuarioService` — Application layer

Responsabilidades en el flujo de invitación:

```csharp
// Al crear usuario
var token = Guid.NewGuid().ToString("N"); // ej: "a3f2e1b4c5d6e7f8a9b0c1d2e3f4a5b6"

usuario.InvitacionToken           = token;
usuario.FechaExpiracionInvitacion = DateTime.UtcNow.AddDays(7);
usuario.Estado                    = EstadoUsuario.PendienteInvitacion;
usuario.PasswordHash              = string.Empty;

// Después de guardar en DB, enviar email
var link = $"{_frontendUrl}/invitacion/{token}";
await _emailService.EnviarInvitacionAsync(email, nombreCompleto, sucursal.Nombre, link);
```

> Si el envío del email falla, el usuario igual queda creado en DB. El administrador puede reenviar la invitación manualmente.

---

### `InvitacionService` — Application layer

Responsabilidades al aceptar:

```csharp
public async Task<UsuarioAutenticadoDto> AceptarAsync(AceptarInvitacionDto dto)
{
    var usuario = await _usuarioRepo.GetByInvitacionTokenAsync(dto.Token);
    // Validaciones: token existe, estado PendienteInvitacion, no vencido

    usuario.PasswordHash              = dto.NuevaPassword; // TODO: BCrypt
    usuario.Estado                    = EstadoUsuario.Activo;
    usuario.InvitacionToken           = null;
    usuario.FechaExpiracionInvitacion = null;

    // Guardar y retornar UsuarioAutenticadoDto (sin token JWT — lo genera la capa Api)
}
```

---

### `IEmailService` / `EmailService` — Infrastructure layer

```
Application/Interfaces/IEmailService.cs   ← contrato
Infrastructure/Services/EmailService.cs   ← implementación con MailKit
```

La interfaz define dos métodos:
- `EnviarInvitacionAsync(...)` — primera invitación
- `EnviarReenvioInvitacionAsync(...)` — reenvío (mismo template, distinto asunto)

La implementación usa **MailKit** con STARTTLS en el puerto 587.

---

### `JwtService` — Api layer

Luego de que `InvitacionService` activa al usuario, `AuthController` llama a `JwtService.GenerarRespuesta(usuario)` para emitir el JWT y devolver la sesión completa.

---

## 6. Plantilla de email

Email HTML responsivo con:
- Cabecera con color primario `#B4697B`
- Saludo personalizado con nombre del invitado
- Nombre de la sucursal destacado
- Botón CTA "Aceptar invitación"
- Enlace de texto como respaldo
- Aviso de vencimiento (7 días)
- Footer con mensaje de seguridad

**Asuntos:**

| Caso | Asunto |
|---|---|
| Primera invitación | `Tu invitación para unirte a {nombreSucursal}` |
| Reenvío | `Nuevo enlace de invitación — {nombreSucursal}` |

---

## 7. Configuración SMTP

En `appsettings.json`:

```json
"Smtp": {
  "Host": "smtp.gmail.com",
  "Port": "587",
  "Username": "noreply@tudominio.com",
  "Password": "tu-app-password",
  "FromEmail": "noreply@tudominio.com",
  "FromName": "TurneroApp"
}
```

### Gmail — App Password

1. Activar **verificación en dos pasos** en la cuenta Google.
2. Ir a **Seguridad → Contraseñas de aplicaciones**.
3. Generar una contraseña para "Correo / Windows".
4. Usar esa contraseña de 16 caracteres en `Smtp:Password`.

### Otros proveedores comunes

| Proveedor | Host | Puerto |
|---|---|---|
| Gmail | `smtp.gmail.com` | `587` |
| Outlook / Hotmail | `smtp.office365.com` | `587` |
| SendGrid | `smtp.sendgrid.net` | `587` |
| Brevo (Sendinblue) | `smtp-relay.brevo.com` | `587` |
| Mailgun | `smtp.mailgun.org` | `587` |

### Producción — variable de entorno

En producción nunca poner credenciales en `appsettings.json`. Usar variables de entorno:

```bash
# Docker / servidor
SMTP__Host=smtp.sendgrid.net
SMTP__Port=587
SMTP__Username=apikey
SMTP__Password=SG.xxxxxxxxxxxx
```

> .NET lee automáticamente `SMTP__Host` como `Smtp:Host` (doble guión bajo = separador de sección).

---

## 8. Estados del usuario

```
                    ┌─────────────────────┐
       crear        │                     │
  ────────────────► │  PendienteInvitacion│ ──── reenviar invitacion (regenera token)
                    │                     │         (mismo estado, nuevo token)
                    └──────────┬──────────┘
                               │
                               │  aceptar invitación
                               │  (token válido + nueva password)
                               ▼
                    ┌─────────────────────┐
                    │       Activo        │ ──── soft delete ──► Inactivo
                    └─────────────────────┘
```

| Estado | Badge | Descripción |
|---|---|---|
| `PendienteInvitacion` | 🟡 naranja | Usuario creado, email enviado, aún no aceptó |
| `Activo` | 🟢 verde | Invitación aceptada, puede iniciar sesión |
| `Inactivo` | ⚫ gris | Dado de baja (soft delete) |

---

## 9. Reglas de negocio

| Regla | Detalle |
|---|---|
| **Sin contraseña en creación** | El DTO `CreateUsuarioDto` no incluye `passwordHash`. La contraseña la define el invitado. |
| **Vencimiento de 7 días** | Si el token vence, el admin debe usar "Reenviar invitación" para generar uno nuevo. |
| **Token de un solo uso** | Al aceptar, `InvitacionToken` se pone en `null`. Intentar usar el mismo token otra vez devuelve error. |
| **Token único activo** | El índice parcial en DB garantiza que no pueden existir dos tokens iguales activos simultáneamente. |
| **Reenvío regenera token** | El reenvío invalida el token anterior y crea uno nuevo con 7 días renovados. |
| **Email no bloquea creación** | Si el envío SMTP falla, el usuario igual queda en DB. Se puede reenviar manualmente. |
| **Login bloqueado** | Un usuario en `PendienteInvitacion` no puede hacer login. El `AuthService` solo acepta usuarios `Activo`. |
| **Email único por sucursal** | Se valida antes de crear. Un mismo email puede existir en distintas sucursales (owner multi-sucursal). |

---

## 10. Diagrama de secuencia

```
Admin Frontend          UsuariosController       UsuarioService        IEmailService        DB
      │                        │                       │                     │               │
      │  POST /api/usuarios    │                       │                     │               │
      │ ──────────────────────►│                       │                     │               │
      │                        │  CreateAsync(dto)     │                     │               │
      │                        │ ─────────────────────►│                     │               │
      │                        │                       │  GetByEmailAsync    │               │
      │                        │                       │ ──────────────────────────────────►│
      │                        │                       │  null (email libre) │               │
      │                        │                       │ ◄──────────────────────────────────│
      │                        │                       │                     │               │
      │                        │                       │  token = NewGuid()  │               │
      │                        │                       │  AddAsync(usuario)  │               │
      │                        │                       │ ──────────────────────────────────►│
      │                        │                       │  SaveChangesAsync   │               │
      │                        │                       │ ──────────────────────────────────►│
      │                        │                       │                     │               │
      │                        │                       │  EnviarInvitacionAsync              │
      │                        │                       │ ───────────────────►│               │
      │                        │                       │                     │  SMTP send    │
      │                        │                       │                     │ ─────────────►│
      │                        │  201 + UsuarioDto     │                     │               │
      │ ◄──────────────────────│ ◄─────────────────────│                     │               │
      │                        │                       │                     │               │

Usuario Frontend        AuthController          InvitacionService            DB
      │                        │                       │                     │
      │  GET /invitacion/token (frontend)              │                     │
      │  GET /api/auth/invitacion/{token}              │                     │
      │ ──────────────────────►│                       │                     │
      │                        │  ObtenerInfoAsync     │                     │
      │                        │ ─────────────────────►│                     │
      │                        │                       │  GetByToken         │
      │                        │                       │ ───────────────────►│
      │                        │                       │  Usuario            │
      │                        │                       │ ◄───────────────────│
      │                        │  InfoInvitacionDto    │                     │
      │ ◄──────────────────────│ ◄─────────────────────│                     │
      │                        │                       │                     │
      │  POST /api/auth/invitacion/aceptar             │                     │
      │  { token, nuevaPassword }                      │                     │
      │ ──────────────────────►│                       │                     │
      │                        │  AceptarAsync(dto)    │                     │
      │                        │ ─────────────────────►│                     │
      │                        │                       │  GetByToken         │
      │                        │                       │ ───────────────────►│
      │                        │                       │  Valida estado y    │
      │                        │                       │  vencimiento        │
      │                        │                       │  Estado = Activo    │
      │                        │                       │  Token = null       │
      │                        │                       │  Password = nueva   │
      │                        │                       │  SaveChanges        │
      │                        │                       │ ───────────────────►│
      │                        │                       │  UsuarioAutenticado │
      │                        │ ◄─────────────────────│                     │
      │                        │  JwtService.Generar   │                     │
      │                        │  LoginResponseDto     │                     │
      │  JWT + sesión          │                       │                     │
      │ ◄──────────────────────│                       │                     │
```

---

## 11. Errores posibles

### Al crear usuario

| Error | HTTP | Mensaje |
|---|---|---|
| Email ya existe en la sucursal | `409 Conflict` | `"Ya existe un usuario con el email '...' en esta sucursal."` |
| Sucursal no encontrada | `400 Bad Request` | `"No se encontró la sucursal con id ..."` |
| Permisos inválidos | `400 Bad Request` | `"Los siguientes permisos no son válidos: ..."` |

### Al consultar/aceptar token

| Error | HTTP | Mensaje |
|---|---|---|
| Token no existe | `400 Bad Request` | `"El enlace de invitación no es válido."` |
| Token ya fue usado | `400 Bad Request` | `"Esta invitación ya fue utilizada."` |
| Token vencido | `400 Bad Request` | `"El enlace de invitación venció. Pedile al administrador que reenvíe la invitación."` |

### Al reenviar

| Error | HTTP | Mensaje |
|---|---|---|
| Usuario no en estado pendiente | `400 Bad Request` | `"Solo se puede reenviar la invitación a usuarios pendientes."` |
| Usuario no encontrado | `404 Not Found` | `"No se encontró el usuario con id ..."` |

---

## Pendiente / TODO

- [ ] **Hashing de contraseña**: reemplazar comparación directa por `BCrypt.Verify` / `BCrypt.HashPassword` (paquete `BCrypt.Net-Next`).
- [ ] **Rate limiting** en `POST /api/auth/invitacion/aceptar` para evitar fuerza bruta sobre tokens.
- [ ] **Email de bienvenida** luego de activar la cuenta (opcional, post-activación).
- [ ] **Expiración configurable** desde `appsettings.json` (`Invitacion:ExpirationDays`).
- [ ] **Modo desarrollo**: implementar `ConsoleEmailService` que loguee el link en consola en lugar de enviar SMTP real.
