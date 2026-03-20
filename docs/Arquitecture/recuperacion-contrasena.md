# Recuperación de contraseña

## Descripción general

Flujo de dos pasos que permite a un usuario registrado restablecer su contraseña sin intervención del administrador:

1. El usuario solicita un enlace de recuperación → el sistema lo genera y lo envía por email.
2. El usuario abre el enlace, ingresa una nueva contraseña → el sistema la persiste y redirige al login.

El flujo es **backwards-compatible**: los usuarios que nunca lo usen no se ven afectados.

---

## Flujo completo

```
Usuario                  Frontend              Backend              Email
  │                         │                     │                   │
  │  Abre /forgot-password  │                     │                   │
  │────────────────────────>│                     │                   │
  │  Ingresa email          │                     │                   │
  │  Clic "Enviar enlace"   │                     │                   │
  │────────────────────────>│ POST /auth/forgot-  │                   │
  │                         │      password       │                   │
  │                         │────────────────────>│                   │
  │                         │                     │ Busca usuario     │
  │                         │                     │ (si no existe:    │
  │                         │                     │  responde 200     │
  │                         │                     │  silencioso)      │
  │                         │                     │                   │
  │                         │                     │ Genera token      │
  │                         │                     │ Guarda token +    │
  │                         │                     │ expiración (UTC+1h│
  │                         │                     │────────────────────>
  │                         │                     │  Email con link   │
  │                         │ 200 OK              │                   │
  │                         │<────────────────────│                   │
  │  Mensaje genérico       │                     │                   │
  │<────────────────────────│                     │                   │
  │                         │                     │                   │
  │  Abre link del email    │                     │                   │
  │  /reset-password?token= │                     │                   │
  │────────────────────────>│                     │                   │
  │  Ingresa nueva pass     │                     │                   │
  │  Clic "Guardar"         │                     │                   │
  │────────────────────────>│ POST /auth/reset-   │                   │
  │                         │      password       │                   │
  │                         │────────────────────>│                   │
  │                         │                     │ Busca por token   │
  │                         │                     │ Valida expiración │
  │                         │                     │ Persiste nueva    │
  │                         │                     │ contraseña        │
  │                         │                     │ Limpia token      │
  │                         │ 204 No Content      │                   │
  │                         │<────────────────────│                   │
  │  Redirige a /login      │                     │                   │
  │<────────────────────────│                     │                   │
```

---

## Backend

### Cambios en dominio

**`src/Domain/Entities/Usuario.cs`** — dos campos nuevos (nullable):

| Campo | Tipo | Descripción |
|---|---|---|
| `TokenRecuperacion` | `string?` | Token de un solo uso generado con `Guid.NewGuid().ToString("N")` |
| `TokenRecuperacionExpiracion` | `DateTime?` | Timestamp UTC de expiración (ahora + 1 hora) |

Ambos se limpian (`null`) después de un restablecimiento exitoso, haciendo el token de un solo uso.

### Migración EF Core

```
AgregaRecuperacionContrasena
```

Agrega las dos columnas a la tabla `usuarios` y un índice único parcial sobre `TokenRecuperacion` (solo cuando no es `NULL`) equivalente al patrón ya usado por `InvitacionToken`.

```sql
-- Equivalente generado por EF
ALTER TABLE usuarios ADD COLUMN "TokenRecuperacion" varchar(100);
ALTER TABLE usuarios ADD COLUMN "TokenRecuperacionExpiracion" timestamptz;
CREATE UNIQUE INDEX ... ON usuarios ("TokenRecuperacion")
    WHERE "TokenRecuperacion" IS NOT NULL;
```

Para aplicar:

```bash
# Detener dotnet watch primero
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

### Servicio de aplicación

**`src/Application/Services/RecuperacionContrasenaService.cs`**

| Método | Descripción |
|---|---|
| `SolicitarAsync(email)` | Busca el usuario por email. Si no existe, retorna sin hacer nada (sin revelar su existencia). Si existe, genera el token, lo persiste y envía el email. |
| `RestablecerAsync(token, nuevaContrasena)` | Busca el usuario por token. Lanza `InvalidOperationException` si el token no existe o expiró. Si es válido, persiste la nueva contraseña y limpia el token. |

**Consideración de seguridad:** `SolicitarAsync` siempre retorna sin error aunque el email no exista. El endpoint devuelve 200 con un mensaje genérico en ambos casos, evitando enumerar qué emails están registrados.

### Endpoints

#### `POST /api/auth/forgot-password`

Solicita el envío del enlace de recuperación.

**Request body:**
```json
{ "email": "usuario@ejemplo.com" }
```

**Respuestas:**

| Código | Cuándo |
|---|---|
| `200 OK` | Siempre (independientemente de si el email existe) |
| `400 Bad Request` | Email con formato inválido o campo vacío |

**Response body (200):**
```json
{ "message": "Si el email está registrado, recibirás un enlace en tu casilla." }
```

---

#### `POST /api/auth/reset-password`

Valida el token y establece la nueva contraseña.

**Request body:**
```json
{
  "token": "a3f1b2c4d5e6...",
  "nuevaContrasena": "MiNuevaPass123"
}
```

**Restricciones de validación:**
- `token`: requerido, no vacío.
- `nuevaContrasena`: requerido, mínimo 8 caracteres.

**Respuestas:**

| Código | Cuándo |
|---|---|
| `204 No Content` | Contraseña actualizada correctamente |
| `400 Bad Request` | Token inválido, token expirado, o validación de campos fallida |

**Response body (400 — token expirado):**
```json
{ "message": "El enlace de recuperación venció. Solicitá uno nuevo desde la pantalla de inicio de sesión." }
```

**Response body (400 — token inválido):**
```json
{ "message": "El enlace de recuperación no es válido." }
```

### Email

Se agrega `EnviarRecuperacionContrasenaAsync` a `IEmailService` e implementado en:

- **`EmailService`** (SMTP real): HTML con el mismo estilo del sistema (header rosado, botón CTA, enlace de texto alternativo, footer con aviso de seguridad).
- **`ConsoleEmailService`** (desarrollo): imprime el link en la consola con formato de caja similar a los otros emails de dev.

**Asunto del email:** `Recuperación de contraseña — TurneroApp`

El enlace incluido en el email tiene la forma:
```
{App:FrontendUrl}/reset-password?token={token}
```

`App:FrontendUrl` se lee de `appsettings.json`, ya configurado en el sistema.

---

## Frontend

### Nuevas páginas

#### `/forgot-password` — `ForgotPassword.tsx`

Misma estructura visual que `/login` (panel de branding izquierdo + formulario derecho).

**Estados de la página:**

| Estado | Qué se muestra |
|---|---|
| Inicial | Input de email + botón "Enviar enlace" |
| Enviando | Botón con spinner |
| Enviado (éxito o email inexistente) | Ícono de check verde + mensaje genérico con el email ingresado + link "Volver al login" |

El catch del `try/catch` también lleva al estado "enviado" para no revelar si el email existe.

#### `/reset-password?token=...` — `ResetPassword.tsx`

**Estados de la página:**

| Estado | Qué se muestra |
|---|---|
| Sin `?token` en URL | Pantalla de error con link a `/forgot-password` |
| Formulario | Dos inputs de contraseña (con toggle mostrar/ocultar) + validación en tiempo real |
| Token expirado (error del backend) | Pantalla con ícono ámbar + link a `/forgot-password` |
| Éxito | Pantalla con ícono verde, mensaje de confirmación, redirige a `/login` después de 2 segundos |

**Validaciones en tiempo real (cliente):**
- Mínimo 8 caracteres
- Ambas contraseñas deben coincidir

La detección de token expirado se hace inspeccionando el mensaje de error del backend: si contiene `"venció"`, `"expiró"` o `"válido"`, se muestra la pantalla de expiración en lugar del mensaje de error inline.

### Login.tsx — cambios

1. **Link "¿Olvidaste tu contraseña?"** agregado a la derecha del label del campo contraseña, apuntando a `/forgot-password`.
2. **Banner de éxito** (verde) que aparece si la navegación a `/login` incluye el estado `{ passwordReseteado: true }` en el historial (enviado desde `ResetPassword` al redirigir).

### api/auth.ts

Nuevo archivo con las tres llamadas de autenticación centralizadas:

```typescript
authApi.login(email, password)           // POST /auth/login
authApi.forgotPassword(email)            // POST /auth/forgot-password
authApi.resetPassword(token, pass)       // POST /auth/reset-password
```

---

## Archivos modificados / creados

### Backend

| Archivo | Tipo |
|---|---|
| `src/Domain/Entities/Usuario.cs` | Modificado |
| `src/Infrastructure/Data/Configurations/UsuarioConfiguration.cs` | Modificado |
| `src/Application/Interfaces/IUsuarioRepository.cs` | Modificado |
| `src/Infrastructure/Repositories/UsuarioRepository.cs` | Modificado |
| `src/Application/Interfaces/IEmailService.cs` | Modificado |
| `src/Infrastructure/Services/EmailService.cs` | Modificado |
| `src/Infrastructure/Services/ConsoleEmailService.cs` | Modificado |
| `src/Application/DTOs/Auth/ForgotPasswordDto.cs` | Nuevo |
| `src/Application/DTOs/Auth/ResetPasswordDto.cs` | Nuevo |
| `src/Application/Services/RecuperacionContrasenaService.cs` | Nuevo |
| `src/Api/Controllers/AuthController.cs` | Modificado |
| `src/Api/Program.cs` | Modificado |
| `src/Infrastructure/Migrations/AgregaRecuperacionContrasena.cs` | Nuevo |

### Frontend

| Archivo | Tipo |
|---|---|
| `src/api/auth.ts` | Nuevo |
| `src/pages/public/ForgotPassword.tsx` | Nuevo |
| `src/pages/public/ResetPassword.tsx` | Nuevo |
| `src/App.tsx` | Modificado |
| `src/pages/public/Login.tsx` | Modificado |

---

## Consideraciones de seguridad

| Aspecto | Decisión |
|---|---|
| Enumeración de emails | El endpoint `forgot-password` siempre devuelve 200 sin importar si el email existe |
| Unicidad del token | Índice único en DB sobre `TokenRecuperacion` (solo cuando no es NULL) |
| Expiración | 1 hora desde la generación (UTC) |
| Token de un solo uso | Se limpia (`null`) al completar el restablecimiento exitoso |
| Hashing de contraseña | Sigue el mismo patrón del resto del sistema (plain text con TODO de BCrypt pendiente) |

> **Nota:** El sistema actualmente no implementa BCrypt (hay un TODO en `InvitacionService` y `RecuperacionContrasenaService`). Cuando se implemente hashing real, el restablecimiento de contraseña se actualizará automáticamente junto con el resto del flujo de autenticación.
