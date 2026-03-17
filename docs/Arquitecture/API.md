# API Reference — TurneroApp

Base URL: `http://localhost:5000/api` (desarrollo)

Swagger UI disponible en `http://localhost:5000/swagger` cuando `ASPNETCORE_ENVIRONMENT=Development`.

---

## Autenticación

La mayoría de los endpoints requieren un token JWT en el header `Authorization`:

```
Authorization: Bearer {token}
```

Los endpoints públicos (`/api/publico/*`) y de autenticación (`/api/auth/*`) no requieren token.

El claim `"userId"` dentro del token se usa para identificar al autor en comentarios y adjuntos.

---

## Índice de recursos

| Grupo | Base path |
|---|---|
| [Auth](#auth) | `/api/auth` |
| [Turnos](#turnos) | `/api/turnos` |
| [Comentarios de turno](#comentarios-de-turno) | `/api/turnos/{turnoId}/comentarios` |
| [Adjuntos de turno](#adjuntos-de-turno) | `/api/turnos/{turnoId}/adjuntos` |
| [Clientes](#clientes) | `/api/clientes` |
| [Servicios](#servicios) | `/api/servicios` |
| [Usuarios](#usuarios) | `/api/usuarios` |
| [Sucursales](#sucursales) | `/api/sucursales` |
| [Ajustes](#ajustes) | `/api/ajustes` |
| [Caja](#caja) | `/api/caja` |
| [Público (booking online)](#público-booking-online) | `/api/publico` |
| [SuperAdmin](#superadmin) | `/api/superadmin` |

---

## Auth

### `POST /api/auth/login`

Autentica un usuario y devuelve un token JWT.

**Body:**
```json
{
  "email": "usuario@ejemplo.com",
  "password": "contraseña"
}
```

**Respuestas:**

| Código | Descripción |
|---|---|
| `200` | `LoginResponseDto` con token, rol, nombre, etc. |
| `400` | Validación de modelo fallida |
| `401` | Credenciales inválidas o usuario inactivo |

**`LoginResponseDto`:**
```json
{
  "token": "eyJ...",
  "rol": "Administrativo",
  "nombre": "Juan",
  "apellido": "Ruiz",
  "email": "usuario@ejemplo.com",
  "esSuperAdmin": false,
  "esOwner": false,
  "userId": "guid",
  "sucursalId": "guid"
}
```

---

### `GET /api/auth/invitacion/{token}`

Devuelve información pública del usuario invitado (nombre, sucursal) para mostrar en la pantalla de bienvenida antes de que el usuario establezca su contraseña.

**Respuestas:**

| Código | Descripción |
|---|---|
| `200` | `InfoInvitacionDto` |
| `400` | Token inválido o expirado |

---

### `POST /api/auth/invitacion/aceptar`

Establece la contraseña del usuario invitado y activa su cuenta. Devuelve un token JWT listo para usar.

**Body:**
```json
{
  "token": "token-de-invitacion",
  "password": "nuevaContraseña"
}
```

**Respuestas:**

| Código | Descripción |
|---|---|
| `200` | `LoginResponseDto` (igual que login) |
| `400` | Token inválido, expirado o contraseña no cumple requisitos |

---

## Turnos

### `GET /api/turnos`

Lista todos los turnos. Filtro opcional por sucursal.

**Query params:**

| Param | Tipo | Requerido | Descripción |
|---|---|---|---|
| `sucursalId` | `Guid` | No | Filtra por sucursal |

**Respuesta `200`:** `TurnoResponseDto[]`

---

### `GET /api/turnos/{id}`

Obtiene un turno por su Id.

**Respuestas:**

| Código | Descripción |
|---|---|
| `200` | `TurnoResponseDto` |
| `404` | Turno no encontrado |

---

### `GET /api/turnos/by-token/{token}`

Obtiene un turno por su token de gestión. Usado por clientes para ver/cancelar su turno sin autenticación.

**Respuestas:**

| Código | Descripción |
|---|---|
| `200` | `TurnoResponseDto` |
| `404` | Token no encontrado |

---

### `POST /api/turnos`

Crea un nuevo turno. Valida conflictos de horario con el profesional asignado.

**Body (`CreateTurnoDto`):**
```json
{
  "sucursalId": "guid",
  "clienteId": "guid",
  "servicioId": "guid",
  "profesionalId": "guid",
  "fechaHoraInicio": "2025-06-15T10:00:00",
  "origen": "Administrativo",
  "observaciones": "opcional"
}
```

`origen` puede ser: `Web` | `Administrativo` | `Profesional` | `Caja` | `Manual`

**Comportamiento automático:**
- `FechaHoraFin` se calcula como `FechaHoraInicio + Servicio.DuracionAgendaMin`
- `TokenGestion` se genera como hex de 32 caracteres
- `MontoSena` se calcula según `Servicio.TipoSena` (Fija o Porcentaje)
- `Estado` inicial: `Confirmado` si origen es `Administrativo`, o según `ModoConfirmacionTurnos` de la sucursal si es `Web`

**Respuestas:**

| Código | Descripción |
|---|---|
| `201` | `TurnoResponseDto` creado |
| `400` | Validación fallida o entidades no encontradas |
| `409` | Conflicto de horario con el profesional |

---

### `PUT /api/turnos/{id}`

Actualiza el estado y/o datos de un turno. También permite reprogramar (nueva fecha/hora y/o profesional).

**Body (`UpdateTurnoDto`):**
```json
{
  "estado": "Completado",
  "observaciones": "opcional",
  "fechaHoraInicio": "2025-06-16T11:00:00",
  "profesionalId": "guid"
}
```

`estado` puede ser: `Pendiente` | `Confirmado` | `CanceladoCliente` | `CanceladoStaff` | `Completado` | `NoShow` | `Reprogramado`

Al cancelar (`CanceladoStaff` / `CanceladoCliente`) se envían emails de notificación si el cliente tiene email (no-crítico: si el email falla, el update igual se completa).

**Respuestas:**

| Código | Descripción |
|---|---|
| `200` | `TurnoResponseDto` actualizado |
| `400` | Validación fallida |
| `404` | Turno no encontrado |
| `409` | Conflicto de horario al reprogramar |

---

### `DELETE /api/turnos/{id}`

Elimina un turno físicamente de la base de datos.

**Respuestas:**

| Código | Descripción |
|---|---|
| `204` | Eliminado correctamente |
| `404` | Turno no encontrado |

---

### `TurnoResponseDto`

```json
{
  "id": "guid",
  "sucursalId": "guid",
  "clienteId": "guid",
  "clienteNombre": "Juan Pérez",
  "clienteTelefono": "1234567890",
  "servicioId": "guid",
  "servicioNombre": "Masaje deportivo",
  "profesionalId": "guid",
  "profesionalNombre": "Ana López",
  "fechaHoraInicio": "2025-06-15T10:00:00",
  "fechaHoraFin": "2025-06-15T10:30:00",
  "estado": "Confirmado",
  "origen": "Administrativo",
  "observaciones": null,
  "tokenGestion": "a3f9b2c1d4e5f6a7b8c9d0e1f2a3b4c5",
  "precioServicio": 5000.00,
  "montoSena": 1500.00,
  "totalCobrado": 0.00
}
```

---

## Comentarios de turno

### `GET /api/turnos/{turnoId}/comentarios`

Lista todos los comentarios de un turno, ordenados por fecha ascendente.

**Respuesta `200`:** `ComentarioTurnoResponseDto[]`

---

### `POST /api/turnos/{turnoId}/comentarios`

Agrega un comentario al turno. El `autorUsuarioId` se extrae del claim JWT `"userId"`.

**Body:**
```json
{ "texto": "El cliente llegó 10 minutos tarde." }
```

**Respuestas:**

| Código | Descripción |
|---|---|
| `201` | `ComentarioTurnoResponseDto` |
| `400` | Texto vacío o turno no encontrado |
| `401` | Token JWT inválido o sin claim `userId` |

---

### `DELETE /api/turnos/{turnoId}/comentarios/{comentarioId}`

Elimina un comentario. Solo el autor original puede eliminarlo.

**Respuestas:**

| Código | Descripción |
|---|---|
| `204` | Eliminado |
| `400` | El usuario no es el autor del comentario |
| `401` | Token JWT inválido |
| `404` | Comentario no encontrado |

---

### `ComentarioTurnoResponseDto`

```json
{
  "id": "guid",
  "turnoId": "guid",
  "autorUsuarioId": "guid",
  "autorNombre": "Ana López",
  "texto": "El cliente llegó 10 minutos tarde.",
  "fecha": "2025-06-15T10:35:00Z"
}
```

---

## Adjuntos de turno

Archivos (imágenes y PDFs) vinculados a un turno, almacenados físicamente en Cloudinary bajo la carpeta `TurneroApp/Adjuntos`.

Ver documentación detallada en [`adjuntos-cloudinary.md`](adjuntos-cloudinary.md).

### `GET /api/turnos/{turnoId}/adjuntos`

Lista todos los adjuntos de un turno, ordenados por fecha ascendente.

**Respuesta `200`:** `AdjuntoTurnoResponseDto[]`

---

### `POST /api/turnos/{turnoId}/adjuntos`

Sube un archivo (imagen o PDF) y lo vincula al turno. Primero sube a Cloudinary, luego persiste en la DB.

**Content-Type:** `multipart/form-data`

**Form field:** `file` — el archivo a subir (imágenes: `image/*`, PDFs: `application/pdf`)

> El `Content-Type` **no debe fijarse manualmente** desde el cliente — el browser/HTTP client debe calcularlo automáticamente para incluir el boundary correcto.

**Respuestas:**

| Código | Descripción |
|---|---|
| `201` | `AdjuntoTurnoResponseDto` |
| `400` | Sin archivo, archivo vacío, o fallo en Cloudinary |
| `401` | Token JWT inválido |

---

### `DELETE /api/turnos/{turnoId}/adjuntos/{adjuntoId}`

Elimina el adjunto de Cloudinary y su registro en la DB. El orden garantiza consistencia: si Cloudinary falla, la DB no se modifica.

**Respuestas:**

| Código | Descripción |
|---|---|
| `204` | Eliminado |
| `404` | Adjunto no encontrado |

---

### `AdjuntoTurnoResponseDto`

```json
{
  "id": "guid",
  "turnoId": "guid",
  "url": "https://res.cloudinary.com/demo/image/upload/v1/TurneroApp/Adjuntos/foto_abc123.jpg",
  "publicId": "TurneroApp/Adjuntos/foto_abc123",
  "nombreArchivo": "foto.jpg",
  "tipoRecurso": "image",
  "fecha": "2025-06-15T10:40:00Z"
}
```

`tipoRecurso`: `"image"` para imágenes, `"raw"` para PDFs.

---

## Clientes

### `GET /api/clientes`

Lista clientes con filtros opcionales.

**Query params:**

| Param | Tipo | Descripción |
|---|---|---|
| `sucursalId` | `Guid` | Filtra por sucursal |
| `q` | `string` | Búsqueda por nombre/apellido (requiere `sucursalId`) |
| `telefono` | `string` | Búsqueda exacta por teléfono (requiere `sucursalId`) |
| `email` | `string` | Búsqueda exacta por email (requiere `sucursalId`) |

**Respuesta `200`:** `ClienteResponseDto[]`

---

### `GET /api/clientes/{id}`

**Respuestas:** `200` `ClienteResponseDto` | `404`

---

### `POST /api/clientes`

**Body (`CreateClienteDto`):**
```json
{
  "sucursalId": "guid",
  "nombre": "Juan",
  "apellido": "Pérez",
  "telefono": "1134567890",
  "email": "juan@ejemplo.com",
  "observaciones": "opcional"
}
```

**Respuestas:** `201` `ClienteResponseDto` | `400`

---

### `PUT /api/clientes/{id}`

**Body:** `UpdateClienteDto` (mismo esquema que create, sin `sucursalId`)

**Respuestas:** `200` `ClienteResponseDto` | `400` | `404`

---

### `DELETE /api/clientes/{id}`

**Respuestas:** `204` | `404`

---

## Servicios

### `GET /api/servicios`

**Query params:**

| Param | Tipo | Default | Descripción |
|---|---|---|---|
| `sucursalId` | `Guid` | — | Filtra por sucursal |
| `incluirInactivos` | `bool` | `false` | Incluye servicios desactivados |

**Respuesta `200`:** `ServicioResponseDto[]`

---

### `GET /api/servicios/{id}`

**Respuestas:** `200` `ServicioResponseDto` | `404`

---

### `POST /api/servicios`

**Body (`CreateServicioDto`):**
```json
{
  "sucursalId": "guid",
  "nombre": "Masaje deportivo",
  "descripcion": "Masaje profundo de tejidos",
  "aclaraciones": "Traer ropa cómoda",
  "precio": 5000.00,
  "duracionClienteMin": 60,
  "duracionAgendaMin": 60,
  "requiereSena": true,
  "tipoSena": "Porcentaje",
  "valorSena": 30,
  "modalidadPrestacion": "Presencial",
  "tipoServicio": "Regular"
}
```

`modalidadPrestacion`: `Presencial` | `Maquina` | `Tratamiento` | `Otro`

`tipoServicio`: `Regular` | `Variable`

`tipoSena`: `Fija` | `Porcentaje` | `null`

**Respuestas:** `201` `ServicioResponseDto` | `400`

---

### `PUT /api/servicios/{id}`

**Body:** `UpdateServicioDto` (mismo esquema que create, sin `sucursalId`)

**Respuestas:** `200` `ServicioResponseDto` | `400` | `404`

---

### `DELETE /api/servicios/{id}`

Soft-delete: desactiva el servicio (`activo = false`). Los turnos existentes con ese servicio se mantienen intactos.

**Respuestas:** `204` | `404`

---

## Usuarios

### `GET /api/usuarios?sucursalId={id}`

Lista todos los usuarios de una sucursal. El param `sucursalId` es **requerido**.

**Respuesta `200`:** `UsuarioResponseDto[]`

---

### `GET /api/usuarios/{id}`

**Respuestas:** `200` `UsuarioResponseDto` | `404`

---

### `POST /api/usuarios`

Crea un usuario y le envía un email de invitación para que establezca su contraseña.

**Body (`CreateUsuarioDto`):**
```json
{
  "sucursalId": "guid",
  "nombre": "Ana",
  "apellido": "López",
  "email": "ana@ejemplo.com",
  "telefono": "1198765432",
  "rol": "Profesional",
  "permisos": ["AgendarTurnos", "EditarTurnos"]
}
```

`rol`: `Administrativo` | `Profesional` | `Caja`

`permisos`: `AgendarTurnos` | `EditarTurnos` | `CargarCobranzas` | `VerTelefonoClientes` | `GestionarAjustes`

**Respuestas:**

| Código | Descripción |
|---|---|
| `201` | `UsuarioResponseDto` |
| `400` | Validación fallida |
| `409` | Email ya registrado en la sucursal |

---

### `PUT /api/usuarios/{id}`

Actualiza datos del usuario. Dejar `passwordHash` vacío para no cambiar la contraseña.

**Body (`UpdateUsuarioDto`):**
```json
{
  "nombre": "Ana",
  "apellido": "López",
  "email": "ana@ejemplo.com",
  "telefono": "1198765432",
  "passwordHash": "",
  "rol": "Profesional",
  "estado": "Activo",
  "permisos": ["AgendarTurnos"]
}
```

`estado`: `Activo` | `Inactivo`

**Respuestas:** `200` `UsuarioResponseDto` | `400` | `404` | `409` (email duplicado)

---

### `POST /api/usuarios/{id}/reenviar-invitacion`

Reenvía el email de invitación al usuario. Solo válido si el usuario aún no aceptó la invitación.

**Respuestas:** `204` | `400` (ya activado) | `404`

---

### `DELETE /api/usuarios/{id}`

Soft-delete: cambia el estado a `Inactivo`. No elimina físicamente.

**Respuestas:** `204` | `404`

---

## Sucursales

### `GET /api/sucursales`

**Query params:**

| Param | Default | Descripción |
|---|---|---|
| `soloActivas` | `false` | Solo devuelve sucursales activas |

**Respuesta `200`:** `SucursalResponseDto[]`

---

### `GET /api/sucursales/{id}`

**Respuestas:** `200` `SucursalResponseDto` | `404`

---

### `POST /api/sucursales`

**Body:**
```json
{
  "nombre": "Sucursal Centro",
  "direccion": "Av. Corrientes 1234",
  "telefono": "1123456789",
  "email": "centro@empresa.com"
}
```

**Respuestas:** `201` `SucursalResponseDto` | `400`

---

### `PUT /api/sucursales/{id}`

**Body:** `UpdateSucursalDto` (mismo esquema que create)

**Respuestas:** `200` `SucursalResponseDto` | `400` | `404`

---

### `DELETE /api/sucursales/{id}`

**Respuestas:** `204` | `404`

---

## Ajustes

Requiere `[Authorize]`.

### `GET /api/ajustes?sucursalId={id}`

Obtiene la configuración completa de la sucursal (horarios, modos de confirmación, datos de contacto, etc.).

**Respuesta `200`:** `AjustesSucursalDto`

---

### `PUT /api/ajustes?sucursalId={id}`

Actualiza la configuración de la sucursal.

**Body (`ActualizarAjustesDto`):** incluye todos los campos de `AjustesSucursalDto` (nombre, slogan, fotoUrl, rubros, moneda, dirección, teléfono, email, horarios, tiempos mínimos, modo confirmación, etc.).

**Respuestas:** `200` `AjustesSucursalDto` | `400` | `404`

---

## Caja

### `GET /api/caja/sucursal/{sucursalId}`

Obtiene la caja asociada a la sucursal (cada sucursal tiene exactamente una caja).

**Respuesta `200`:** `CajaResponseDto` (`{ id, sucursalId, nombre, activa }`)

---

### `GET /api/caja/{cajaId}/movimientos`

Lista los movimientos de una caja con filtros opcionales de fecha.

**Query params:**

| Param | Tipo | Descripción |
|---|---|---|
| `desde` | `DateTime` | Filtra desde esta fecha (ISO 8601) |
| `hasta` | `DateTime` | Filtra hasta esta fecha (ISO 8601) |

**Respuesta `200`:** `MovimientoCajaResponseDto[]`

---

### `POST /api/caja/{cajaId}/movimientos`

Registra un nuevo movimiento de caja (cobro, gasto, retiro, ajuste, etc.).

**Body (`CreateMovimientoCajaDto`):**
```json
{
  "cajaId": "guid",
  "usuarioId": "guid",
  "turnoId": "guid-opcional",
  "tipoMovimiento": "CobroTurno",
  "medioPago": "Efectivo",
  "monto": 5000.00,
  "observaciones": "Pago total del turno"
}
```

`tipoMovimiento`: `CobroTurno` | `Gasto` | `Retiro` | `Ajuste` | `IngresoManual`

`medioPago`: `Efectivo` | `MercadoPago` | `Transferencia` | `Tarjeta` | `Otro`

> El `cajaId` del body debe coincidir con el de la URL.

**Respuestas:** `201` `MovimientoCajaResponseDto` | `400` | `404`

---

## Público (booking online)

Endpoints **sin autenticación** para el flujo de reserva de turnos por parte del cliente.

### `GET /api/publico/perfil?sucursalId={id}`

Devuelve el perfil público de la sucursal: nombre, slogan, foto, rubros, dirección, teléfono, email, horarios de atención y días máximos de reserva futura.

**Respuesta `200`:** `PerfilPublicoDto`

---

### `GET /api/publico/slots?sucursalId={id}&fecha={yyyy-MM-dd}&servicioId={id}`

Devuelve los slots horarios disponibles para una fecha y servicio dados. Cada slot incluye la hora y el profesional disponible.

**Query params:**

| Param | Tipo | Requerido | Descripción |
|---|---|---|---|
| `sucursalId` | `Guid` | Sí | Sucursal |
| `fecha` | `string` | Sí | Fecha en formato `yyyy-MM-dd` |
| `servicioId` | `Guid` | Sí | Servicio que se quiere reservar |

**Respuesta `200`:** `SlotDisponibleDto[]`

```json
[
  {
    "hora": "10:00",
    "profesionalId": "guid",
    "profesionalNombre": "Ana López"
  }
]
```

**Respuestas:** `200` | `400` (fecha inválida) | `500` (error interno)

---

## SuperAdmin

Endpoints de administración global del sistema. Permiten gestionar todas las sucursales y sus owners.

### `GET /api/superadmin/sucursales`

Lista todas las sucursales con información del owner.

**Respuesta `200`:** `SucursalSuperAdminDto[]`

---

### `GET /api/superadmin/sucursales/{id}`

**Respuestas:** `200` `SucursalSuperAdminDto` | `404`

---

### `POST /api/superadmin/sucursales`

Crea una nueva sucursal junto con su owner en una sola operación.

**Body (`CrearSucursalConOwnerDto`):**
```json
{
  "nombre": "Nueva Sucursal",
  "direccion": "Calle Falsa 123",
  "telefono": "1100000000",
  "email": "sucursal@empresa.com",
  "ownerNombre": "Carlos",
  "ownerApellido": "García",
  "ownerEmail": "carlos@empresa.com",
  "ownerTelefono": "1100000001",
  "ownerPassword": "ContraseñaSegura123"
}
```

**Respuestas:** `201` `SucursalSuperAdminDto` | `400`

---

### `PUT /api/superadmin/sucursales/{id}`

Actualiza los datos básicos de una sucursal (nombre, dirección, teléfono, email).

**Respuestas:** `200` `SucursalSuperAdminDto` | `400` | `404`

---

### `PATCH /api/superadmin/sucursales/{id}/estado`

Alterna el estado activo/inactivo de una sucursal (toggle).

**Respuestas:** `200` `SucursalSuperAdminDto` | `404`

---

### `SucursalSuperAdminDto`

```json
{
  "id": "guid",
  "nombre": "Sucursal Centro",
  "direccion": "Av. Corrientes 1234",
  "telefono": "1123456789",
  "email": "centro@empresa.com",
  "activa": true,
  "fechaCreacion": "2025-01-15T00:00:00Z",
  "owner": {
    "id": "guid",
    "nombre": "Carlos",
    "apellido": "García",
    "nombreCompleto": "Carlos García",
    "email": "carlos@empresa.com",
    "telefono": "1100000001",
    "estado": "Activo"
  }
}
```

---

## Códigos de error comunes

Todos los errores devuelven un objeto `{ "message": "Descripción del error" }`.

| Código | Significado habitual |
|---|---|
| `400` | Validación de modelo fallida, entidad no encontrada, o error de negocio |
| `401` | Token JWT ausente, inválido, o sin el claim requerido |
| `404` | Recurso no encontrado |
| `409` | Conflicto: duplicado (email de usuario) o conflicto de horario (turnos) |
| `500` | Error interno del servidor |
