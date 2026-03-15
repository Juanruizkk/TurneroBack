# TurneroApp — Documentación de API

**Base URL:** `http://localhost:5000/api`
**Formato:** JSON (`Content-Type: application/json`)
**Swagger (Development):** `http://localhost:5000/swagger`

---

## Índice

1. [Autenticación](#1-autenticación)
2. [Público (sin auth)](#2-público-sin-auth)
3. [Sucursales](#3-sucursales)
4. [Servicios](#4-servicios)
5. [Clientes](#5-clientes)
6. [Turnos](#6-turnos)
7. [Usuarios](#7-usuarios)
8. [Ajustes de Sucursal](#8-ajustes-de-sucursal)
9. [Caja](#9-caja)
10. [Super Admin](#10-super-admin)
11. [Referencia de Enums](#11-referencia-de-enums)

---

## Nota sobre autenticación

Los endpoints marcados con 🔒 requieren el header:

```
Authorization: Bearer <token>
```

El token se obtiene en `POST /api/auth/login`. Los endpoints marcados con 🌐 son **públicos** (no requieren token).

---

## 1. Autenticación

### `POST /api/auth/login` 🌐

Autentica un usuario (staff o SuperAdmin) y devuelve un JWT.

**Body:**

| Campo      | Tipo     | Requerido | Descripción         |
|------------|----------|-----------|---------------------|
| `email`    | `string` | ✅        | Email del usuario   |
| `password` | `string` | ✅        | Contraseña          |

```json
{
  "email": "juan@salon.com",
  "password": "miPassword123"
}
```

**Respuesta 200 OK:**

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "rol": "Administrativo",
  "nombre": "Juan",
  "apellido": "Ruiz",
  "email": "juan@salon.com",
  "esSuperAdmin": false,
  "esOwner": true,
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "sucursalId": "1c4d9bac-f72d-498a-8fa0-6d609a9e04e7"
}
```

> **Nota:** Para el SuperAdmin, `userId` y `sucursalId` son `null`, y `esSuperAdmin` es `true`.

**Respuestas de error:**

| Código | Descripción                              |
|--------|------------------------------------------|
| `400`  | Body inválido o campo faltante           |
| `401`  | Credenciales incorrectas o usuario inactivo |

```json
{ "message": "Credenciales inválidas." }
```

---

### `GET /api/auth/invitacion/{token}` 🌐

Obtiene información pública de una invitación pendiente (para mostrar en la pantalla de bienvenida antes de que el usuario establezca su contraseña).

**Parámetros de ruta:**

| Parámetro | Tipo     | Descripción            |
|-----------|----------|------------------------|
| `token`   | `string` | Token de la invitación |

**Respuesta 200 OK:**

```json
{
  "nombre": "María",
  "apellido": "González",
  "email": "maria@salon.com",
  "sucursalNombre": "Salón Beleza"
}
```

**Respuestas de error:**

| Código | Descripción                                      |
|--------|--------------------------------------------------|
| `400`  | Token inválido, ya utilizado o expirado          |

```json
{ "message": "El token de invitación no es válido o ya fue utilizado." }
```

---

### `POST /api/auth/invitacion/aceptar` 🌐

Acepta una invitación: establece la contraseña del usuario y devuelve un JWT para iniciar sesión directamente.

**Body:**

| Campo           | Tipo     | Requerido | Descripción                       |
|-----------------|----------|-----------|-----------------------------------|
| `token`         | `string` | ✅        | Token recibido por email          |
| `nuevaPassword` | `string` | ✅        | Nueva contraseña (mín. 6 chars)   |

```json
{
  "token": "abc123def456...",
  "nuevaPassword": "miNuevaPassword123"
}
```

**Respuesta 200 OK:** igual que `POST /api/auth/login`.

**Respuestas de error:**

| Código | Descripción                              |
|--------|------------------------------------------|
| `400`  | Token inválido/expirado o password corta |

```json
{ "message": "El token de invitación no es válido o ya fue utilizado." }
```

---

## 2. Público (sin auth)

Estos endpoints no requieren autenticación y son consumidos por el flujo de reserva online del cliente.

### `GET /api/publico/perfil` 🌐

Devuelve el perfil público de una sucursal: nombre, slogan, foto, rubros y horarios de atención.

**Query params:**

| Parámetro    | Tipo   | Requerido | Descripción       |
|--------------|--------|-----------|-------------------|
| `sucursalId` | `Guid` | ✅        | ID de la sucursal |

**Ejemplo:** `GET /api/publico/perfil?sucursalId=1c4d9bac-f72d-498a-8fa0-6d609a9e04e7`

**Respuesta 200 OK:**

```json
{
  "nombre": "Salón Beleza",
  "slogan": "Tu belleza, nuestra pasión",
  "fotoUrl": "https://cdn.ejemplo.com/foto.jpg",
  "rubros": ["Peluquería", "Estética"],
  "moneda": "ARS",
  "direccion": "Av. Corrientes 1234, CABA",
  "telefono": "11 4567-8901",
  "email": "info@salonbeleza.com",
  "horariosAtencion": {
    "Lunes": {
      "abierto": true,
      "franjas": [
        { "apertura": "09:00", "cierre": "13:00" },
        { "apertura": "15:00", "cierre": "19:00" }
      ]
    },
    "Sabado": {
      "abierto": true,
      "franjas": [
        { "apertura": "09:00", "cierre": "13:00" }
      ]
    },
    "Domingo": {
      "abierto": false,
      "franjas": []
    }
  }
}
```

**Respuestas de error:**

| Código | Descripción               |
|--------|---------------------------|
| `404`  | Sucursal no encontrada    |

```json
{ "message": "Sucursal no encontrada." }
```

---

### `GET /api/publico/slots` 🌐

Devuelve los horarios disponibles para una fecha y servicio dados. Cada slot incluye el profesional libre asignado.

**Query params:**

| Parámetro    | Tipo     | Requerido | Descripción                         |
|--------------|----------|-----------|-------------------------------------|
| `sucursalId` | `Guid`   | ✅        | ID de la sucursal                   |
| `fecha`      | `string` | ✅        | Fecha en formato `yyyy-MM-dd`        |
| `servicioId` | `Guid`   | ✅        | ID del servicio a reservar          |

**Ejemplo:** `GET /api/publico/slots?sucursalId=1c4d9bac...&fecha=2026-03-16&servicioId=26740bd8...`

**Respuesta 200 OK:**

```json
[
  { "hora": "09:00", "profesionalId": "a1b2c3d4-..." },
  { "hora": "09:30", "profesionalId": "a1b2c3d4-..." },
  { "hora": "10:00", "profesionalId": "e5f6a7b8-..." },
  { "hora": "11:00", "profesionalId": "a1b2c3d4-..." }
]
```

> Los slots ya ocupados o fuera del horario de atención no se incluyen en la respuesta. Si la fecha es hoy, los slots pasados tampoco aparecen.

**Respuestas de error:**

| Código | Descripción                              |
|--------|------------------------------------------|
| `400`  | Formato de fecha inválido               |
| `500`  | Error interno al calcular disponibilidad |

```json
{ "message": "Formato de fecha inválido. Use yyyy-MM-dd." }
```

---

## 3. Sucursales

### `GET /api/sucursales` 🔒

Obtiene todas las sucursales. Opcionalmente filtra solo las activas.

**Query params:**

| Parámetro     | Tipo   | Requerido | Default | Descripción                     |
|---------------|--------|-----------|---------|---------------------------------|
| `soloActivas` | `bool` | ❌        | `false` | Si `true`, devuelve solo activas|

**Respuesta 200 OK:**

```json
[
  {
    "id": "1c4d9bac-f72d-498a-8fa0-6d609a9e04e7",
    "nombre": "Salón Beleza",
    "direccion": "Av. Corrientes 1234, CABA",
    "telefono": "11 4567-8901",
    "email": "info@salonbeleza.com",
    "activa": true
  }
]
```

---

### `GET /api/sucursales/{id}` 🔒

Obtiene una sucursal por ID.

**Parámetros de ruta:**

| Parámetro | Tipo   | Descripción       |
|-----------|--------|-------------------|
| `id`      | `Guid` | ID de la sucursal |

**Respuesta 200 OK:** objeto sucursal (igual al ítem del array de `GET /api/sucursales`).

**Respuestas de error:**

| Código | Descripción            |
|--------|------------------------|
| `404`  | Sucursal no encontrada |

```json
{ "message": "Sucursal con Id 1c4d9bac-... no encontrada." }
```

---

### `POST /api/sucursales` 🔒

Crea una nueva sucursal.

**Body:**

| Campo       | Tipo     | Requerido | Descripción                |
|-------------|----------|-----------|----------------------------|
| `nombre`    | `string` | ✅        | Nombre del negocio         |
| `direccion` | `string` | ✅        | Dirección física           |
| `telefono`  | `string` | ✅        | Teléfono de contacto       |
| `email`     | `string` | ✅        | Email de la sucursal       |

```json
{
  "nombre": "Salón Nuevo",
  "direccion": "Av. Santa Fe 500, CABA",
  "telefono": "11 5555-0000",
  "email": "nuevo@salon.com"
}
```

**Respuesta 201 Created:** objeto sucursal creada.

**Respuestas de error:**

| Código | Descripción              |
|--------|--------------------------|
| `400`  | Datos inválidos o faltantes |

---

### `PUT /api/sucursales/{id}` 🔒

Actualiza los datos de una sucursal.

**Parámetros de ruta:** `id` (Guid)

**Body:** mismos campos que `POST /api/sucursales`.

**Respuesta 200 OK:** objeto sucursal actualizada.

**Respuestas de error:** `400`, `404`.

---

### `DELETE /api/sucursales/{id}` 🔒

Elimina una sucursal.

**Parámetros de ruta:** `id` (Guid)

**Respuesta 204 No Content.**

**Respuestas de error:** `404`.

---

## 4. Servicios

### `GET /api/servicios` 🔒

Lista todos los servicios. Puede filtrarse por sucursal.

**Query params:**

| Parámetro    | Tipo   | Requerido | Descripción            |
|--------------|--------|-----------|------------------------|
| `sucursalId` | `Guid` | ❌        | Filtra por sucursal    |

**Respuesta 200 OK:**

```json
[
  {
    "id": "26740bd8-03cc-4190-8870-e130bd3c48b1",
    "sucursalId": "1c4d9bac-f72d-498a-8fa0-6d609a9e04e7",
    "nombre": "Corte de pelo",
    "descripcion": "Corte y styling profesional",
    "aclaraciones": "Incluye lavado",
    "precio": 5500.00,
    "duracionClienteMin": 45,
    "duracionAgendaMin": 50,
    "requiereSena": false,
    "tipoSena": null,
    "valorSena": null,
    "modalidadPrestacion": "Presencial",
    "tipoServicio": "Regular",
    "activo": true
  }
]
```

---

### `GET /api/servicios/{id}` 🔒

Obtiene un servicio por ID.

**Parámetros de ruta:** `id` (Guid)

**Respuesta 200 OK:** objeto servicio. **404** si no existe.

---

### `POST /api/servicios` 🔒

Crea un nuevo servicio.

**Body:**

| Campo                 | Tipo                  | Requerido | Descripción                                              |
|-----------------------|-----------------------|-----------|----------------------------------------------------------|
| `sucursalId`          | `Guid`                | ✅        | ID de la sucursal                                        |
| `nombre`              | `string`              | ✅        | Nombre del servicio                                      |
| `descripcion`         | `string`              | ✅        | Descripción visible al cliente                           |
| `aclaraciones`        | `string`              | ✅        | Aclaraciones internas                                    |
| `precio`              | `decimal`             | ✅        | Precio en la moneda configurada                          |
| `duracionClienteMin`  | `int`                 | ✅        | Duración visible al cliente (minutos)                    |
| `duracionAgendaMin`   | `int`                 | ✅        | Duración real que bloquea la agenda (minutos)            |
| `requiereSena`        | `bool`                | ✅        | Si el servicio exige seña al reservar                    |
| `tipoSena`            | `TipoSena \| null`    | ❌        | `"Fija"` o `"Porcentaje"` (requerido si `requiereSena`) |
| `valorSena`           | `decimal \| null`     | ❌        | Monto fijo o porcentaje (requerido si `requiereSena`)    |
| `modalidadPrestacion` | `ModalidadPrestacion` | ✅        | Ver enum                                                 |
| `tipoServicio`        | `TipoServicio`        | ✅        | `"Regular"` o `"Variable"`                              |

```json
{
  "sucursalId": "1c4d9bac-f72d-498a-8fa0-6d609a9e04e7",
  "nombre": "Coloración completa",
  "descripcion": "Coloración de raíces y puntas",
  "aclaraciones": "Traer ropa que pueda mancharse",
  "precio": 12000.00,
  "duracionClienteMin": 120,
  "duracionAgendaMin": 140,
  "requiereSena": true,
  "tipoSena": "Porcentaje",
  "valorSena": 30,
  "modalidadPrestacion": "Presencial",
  "tipoServicio": "Regular"
}
```

**Respuesta 201 Created:** objeto servicio creado.

**Respuestas de error:** `400`.

---

### `PUT /api/servicios/{id}` 🔒

Actualiza un servicio existente.

**Parámetros de ruta:** `id` (Guid)

**Body:** mismos campos que `POST /api/servicios`.

**Respuesta 200 OK:** objeto servicio actualizado. **404** si no existe.

---

### `DELETE /api/servicios/{id}` 🔒

Elimina un servicio.

**Respuesta 204 No Content.** **404** si no existe.

---

## 5. Clientes

### `GET /api/clientes` 🔒

Lista clientes. Soporta múltiples filtros opcionales.

**Query params:**

| Parámetro    | Tipo     | Requerido | Descripción                              |
|--------------|----------|-----------|------------------------------------------|
| `sucursalId` | `Guid`   | ❌        | Filtra por sucursal                      |
| `telefono`   | `string` | ❌        | Busca por teléfono exacto (+ sucursalId) |
| `email`      | `string` | ❌        | Busca por email exacto (+ sucursalId)    |

> Si se combina `sucursalId` + `telefono`, devuelve un array de 0 o 1 elemento.
> Si se combina `sucursalId` + `email`, devuelve un array de 0 o 1 elemento.

**Ejemplo búsqueda por teléfono:**
`GET /api/clientes?sucursalId=1c4d9bac-...&telefono=1145678901`

**Respuesta 200 OK:**

```json
[
  {
    "id": "7b3e1a2c-4f5d-4a6e-8b9c-0d1e2f3a4b5c",
    "sucursalId": "1c4d9bac-f72d-498a-8fa0-6d609a9e04e7",
    "nombre": "Laura",
    "apellido": "Martínez",
    "telefono": "1145678901",
    "email": "laura@email.com",
    "observaciones": null,
    "nombreCompleto": "Laura Martínez"
  }
]
```

---

### `GET /api/clientes/{id}` 🔒

Obtiene un cliente por ID.

**Respuesta 200 OK:** objeto cliente. **404** si no existe.

---

### `POST /api/clientes` 🔒

Crea un nuevo cliente.

**Body:**

| Campo           | Tipo     | Requerido | Descripción                  |
|-----------------|----------|-----------|------------------------------|
| `sucursalId`    | `Guid`   | ✅        | ID de la sucursal            |
| `nombre`        | `string` | ✅        | Nombre del cliente           |
| `apellido`      | `string` | ✅        | Apellido del cliente         |
| `telefono`      | `string` | ✅        | Teléfono (WhatsApp)          |
| `email`         | `string` | ❌        | Email de contacto            |
| `observaciones` | `string` | ❌        | Notas internas               |

```json
{
  "sucursalId": "1c4d9bac-f72d-498a-8fa0-6d609a9e04e7",
  "nombre": "Laura",
  "apellido": "Martínez",
  "telefono": "1145678901",
  "email": "laura@email.com"
}
```

**Respuesta 201 Created:** objeto cliente creado.

---

### `PUT /api/clientes/{id}` 🔒

Actualiza un cliente existente.

**Body:** mismos campos opcionales del DTO cliente.

**Respuesta 200 OK:** objeto cliente actualizado. **404** si no existe.

---

### `DELETE /api/clientes/{id}` 🔒

Elimina un cliente.

**Respuesta 204 No Content.** **404** si no existe.

---

## 6. Turnos

### `GET /api/turnos` 🔒

Lista todos los turnos. Puede filtrarse por sucursal.

**Query params:**

| Parámetro    | Tipo   | Requerido | Descripción         |
|--------------|--------|-----------|---------------------|
| `sucursalId` | `Guid` | ❌        | Filtra por sucursal |

**Respuesta 200 OK:**

```json
[
  {
    "id": "f1e2d3c4-b5a6-7890-abcd-ef1234567890",
    "sucursalId": "1c4d9bac-f72d-498a-8fa0-6d609a9e04e7",
    "clienteId": "7b3e1a2c-4f5d-4a6e-8b9c-0d1e2f3a4b5c",
    "clienteNombre": "Laura Martínez",
    "clienteTelefono": "1145678901",
    "servicioId": "26740bd8-03cc-4190-8870-e130bd3c48b1",
    "servicioNombre": "Corte de pelo",
    "profesionalId": "a1b2c3d4-1234-5678-abcd-ef9876543210",
    "profesionalNombre": "Ana Rodríguez",
    "fechaHoraInicio": "2026-03-16T10:00:00",
    "fechaHoraFin": "2026-03-16T10:50:00",
    "estado": "Pendiente",
    "origen": "Web",
    "observaciones": null,
    "tokenGestion": "a3f8b2e1c7d94f5680ab12cd34ef5678",
    "precioServicio": 5500.00,
    "montoSena": null,
    "totalCobrado": 0.00
  }
]
```

---

### `GET /api/turnos/{id}` 🔒

Obtiene un turno por ID.

**Respuesta 200 OK:** objeto turno. **404** si no existe.

---

### `GET /api/turnos/by-token/{token}` 🌐

Obtiene un turno mediante su token de gestión. Usado por el cliente para consultar o cancelar su propio turno sin necesidad de autenticarse.

**Parámetros de ruta:**

| Parámetro | Tipo     | Descripción                         |
|-----------|----------|-------------------------------------|
| `token`   | `string` | Token de 32 caracteres del turno    |

**Ejemplo:** `GET /api/turnos/by-token/a3f8b2e1c7d94f5680ab12cd34ef5678`

**Respuesta 200 OK:** objeto turno completo.

**Respuestas de error:**

| Código | Descripción                           |
|--------|---------------------------------------|
| `404`  | Turno no encontrado para ese token    |

```json
{ "message": "Turno no encontrado para el token proporcionado." }
```

---

### `POST /api/turnos` 🌐

Crea un nuevo turno. Valida que no exista conflicto de horario con el profesional.

> Este endpoint es consumido tanto por el flujo público de reserva online como por el panel administrativo.

**Body:**

| Campo              | Tipo          | Requerido | Descripción                                      |
|--------------------|---------------|-----------|--------------------------------------------------|
| `sucursalId`       | `Guid`        | ✅        | ID de la sucursal                                |
| `clienteId`        | `Guid`        | ✅        | ID del cliente (debe existir previamente)        |
| `servicioId`       | `Guid`        | ✅        | ID del servicio                                  |
| `profesionalId`    | `Guid`        | ✅        | ID del profesional asignado                      |
| `fechaHoraInicio`  | `datetime`    | ✅        | Fecha y hora de inicio (`yyyy-MM-ddTHH:mm:ss`)   |
| `origen`           | `OrigenTurno` | ✅        | Ver enum (ej: `"Web"`, `"Administrativo"`)       |
| `observaciones`    | `string`      | ❌        | Notas adicionales                                |

> `fechaHoraFin` se calcula automáticamente: `fechaHoraInicio + servicio.duracionAgendaMin`.

```json
{
  "sucursalId": "1c4d9bac-f72d-498a-8fa0-6d609a9e04e7",
  "clienteId": "7b3e1a2c-4f5d-4a6e-8b9c-0d1e2f3a4b5c",
  "servicioId": "26740bd8-03cc-4190-8870-e130bd3c48b1",
  "profesionalId": "a1b2c3d4-1234-5678-abcd-ef9876543210",
  "fechaHoraInicio": "2026-03-16T10:00:00",
  "origen": "Web",
  "observaciones": null
}
```

**Respuesta 201 Created:** objeto turno creado.

**Respuestas de error:**

| Código | Descripción                                                |
|--------|------------------------------------------------------------|
| `400`  | Datos inválidos, cliente o servicio no encontrado          |
| `409`  | El profesional ya tiene un turno en el horario seleccionado |

```json
{ "message": "El profesional ya tiene un turno en el horario seleccionado. Por favor elija otro horario." }
```

---

### `PUT /api/turnos/{id}` 🔒

Actualiza el estado y/o datos de un turno. También permite reprogramar (cambiar fecha/hora y profesional).

**Parámetros de ruta:** `id` (Guid)

**Body:**

| Campo             | Tipo          | Requerido | Descripción                                        |
|-------------------|---------------|-----------|----------------------------------------------------|
| `estado`          | `EstadoTurno` | ✅        | Nuevo estado del turno                             |
| `observaciones`   | `string`      | ❌        | Actualiza las observaciones si se envía            |
| `fechaHoraInicio` | `datetime`    | ❌        | Nueva fecha/hora de inicio (para reprogramar)      |
| `profesionalId`   | `Guid`        | ❌        | Nuevo profesional (para reasignar o reprogramar)   |

```json
{
  "estado": "Confirmado",
  "observaciones": "Cliente confirma por WhatsApp"
}
```

**Ejemplo de reprogramación:**

```json
{
  "estado": "Reprogramado",
  "fechaHoraInicio": "2026-03-18T14:00:00",
  "profesionalId": "a1b2c3d4-1234-5678-abcd-ef9876543210"
}
```

**Respuesta 200 OK:** objeto turno actualizado.

**Respuestas de error:**

| Código | Descripción                                                 |
|--------|-------------------------------------------------------------|
| `400`  | Datos inválidos                                             |
| `404`  | Turno no encontrado                                         |
| `409`  | Conflicto de horario al reprogramar                         |

---

### `DELETE /api/turnos/{id}` 🔒

Elimina físicamente un turno.

**Respuesta 204 No Content.** **404** si no existe.

---

## 7. Usuarios

### `GET /api/usuarios` 🔒

Lista todos los usuarios de una sucursal.

**Query params:**

| Parámetro    | Tipo   | Requerido | Descripción       |
|--------------|--------|-----------|-------------------|
| `sucursalId` | `Guid` | ✅        | ID de la sucursal |

**Respuesta 200 OK:**

```json
[
  {
    "id": "a1b2c3d4-1234-5678-abcd-ef9876543210",
    "sucursalId": "1c4d9bac-f72d-498a-8fa0-6d609a9e04e7",
    "nombre": "Ana",
    "apellido": "Rodríguez",
    "nombreCompleto": "Ana Rodríguez",
    "email": "ana@salon.com",
    "telefono": "1155667788",
    "rol": "Profesional",
    "estado": "Activo",
    "permisos": ["AgendarTurnos", "EditarTurnos"]
  }
]
```

**Respuestas de error:**

| Código | Descripción                          |
|--------|--------------------------------------|
| `400`  | `sucursalId` no enviado o es vacío   |

---

### `GET /api/usuarios/{id}` 🔒

Obtiene un usuario por ID.

**Respuesta 200 OK:** objeto usuario. **404** si no existe.

---

### `POST /api/usuarios` 🔒

Crea un nuevo usuario y envía un email de invitación para que establezca su contraseña.

**Body:**

| Campo       | Tipo           | Requerido | Descripción                       |
|-------------|----------------|-----------|-----------------------------------|
| `sucursalId`| `Guid`         | ✅        | ID de la sucursal                 |
| `nombre`    | `string`       | ✅        | Nombre                            |
| `apellido`  | `string`       | ✅        | Apellido                          |
| `email`     | `string`       | ✅        | Email (único por sucursal)        |
| `telefono`  | `string`       | ✅        | Teléfono                          |
| `rol`       | `RolUsuario`   | ✅        | Ver enum                          |
| `permisos`  | `PermisoSistema[]` | ❌   | Lista de permisos granulares      |

```json
{
  "sucursalId": "1c4d9bac-f72d-498a-8fa0-6d609a9e04e7",
  "nombre": "Carlos",
  "apellido": "López",
  "email": "carlos@salon.com",
  "telefono": "1144556677",
  "rol": "Profesional",
  "permisos": ["AgendarTurnos"]
}
```

**Respuesta 201 Created:** objeto usuario. El estado inicial es `PendienteInvitacion`.

**Respuestas de error:**

| Código | Descripción                             |
|--------|-----------------------------------------|
| `400`  | Datos inválidos                         |
| `409`  | Ya existe un usuario con ese email en la sucursal |

```json
{ "message": "Ya existe un usuario con el email carlos@salon.com en esta sucursal." }
```

---

### `PUT /api/usuarios/{id}` 🔒

Actualiza los datos de un usuario. Reemplaza completamente la lista de permisos.

**Parámetros de ruta:** `id` (Guid)

**Body:**

| Campo      | Tipo               | Requerido | Descripción                                       |
|------------|--------------------|-----------|---------------------------------------------------|
| `nombre`   | `string`           | ✅        |                                                   |
| `apellido` | `string`           | ✅        |                                                   |
| `email`    | `string`           | ✅        |                                                   |
| `telefono` | `string`           | ✅        |                                                   |
| `rol`      | `RolUsuario`       | ✅        |                                                   |
| `estado`   | `EstadoUsuario`    | ✅        |                                                   |
| `permisos` | `PermisoSistema[]` | ✅        | Lista completa. Enviar `[]` para quitar todos     |

```json
{
  "nombre": "Carlos",
  "apellido": "López",
  "email": "carlos@salon.com",
  "telefono": "1144556677",
  "rol": "Administrativo",
  "estado": "Activo",
  "permisos": ["AgendarTurnos", "EditarTurnos", "CargarCobranzas"]
}
```

**Respuesta 200 OK:** objeto usuario actualizado.

**Respuestas de error:** `400`, `404`, `409`.

---

### `POST /api/usuarios/{id}/reenviar-invitacion` 🔒

Reenvía el email de invitación a un usuario con estado `PendienteInvitacion`. Regenera el token de invitación.

**Parámetros de ruta:** `id` (Guid)

**Respuesta 204 No Content.**

**Respuestas de error:**

| Código | Descripción                                          |
|--------|------------------------------------------------------|
| `400`  | El usuario ya está activo (no necesita invitación)   |
| `404`  | Usuario no encontrado                                |

---

### `DELETE /api/usuarios/{id}` 🔒

**Soft delete:** cambia el estado del usuario a `Inactivo`. No elimina el registro físicamente.

**Respuesta 204 No Content.** **404** si no existe.

---

## 8. Ajustes de Sucursal

### `GET /api/ajustes` 🔒

Obtiene todos los ajustes de configuración de una sucursal: identidad, contacto, horarios de atención y parámetros operativos de reservas.

**Query params:**

| Parámetro    | Tipo   | Requerido |
|--------------|--------|-----------|
| `sucursalId` | `Guid` | ✅        |

**Respuesta 200 OK:**

```json
{
  "nombre": "Salón Beleza",
  "slogan": "Tu belleza, nuestra pasión",
  "fotoUrl": "https://cdn.ejemplo.com/foto.jpg",
  "rubros": ["Peluquería", "Estética"],
  "moneda": "ARS",
  "direccion": "Av. Corrientes 1234, CABA",
  "telefono": "11 4567-8901",
  "email": "info@salonbeleza.com",
  "horariosAtencion": {
    "Lunes":    { "abierto": true,  "franjas": [{ "apertura": "09:00", "cierre": "13:00" }, { "apertura": "15:00", "cierre": "19:00" }] },
    "Martes":   { "abierto": true,  "franjas": [{ "apertura": "09:00", "cierre": "13:00" }, { "apertura": "15:00", "cierre": "19:00" }] },
    "Miercoles":{ "abierto": true,  "franjas": [{ "apertura": "09:00", "cierre": "13:00" }, { "apertura": "15:00", "cierre": "19:00" }] },
    "Jueves":   { "abierto": true,  "franjas": [{ "apertura": "09:00", "cierre": "13:00" }, { "apertura": "15:00", "cierre": "19:00" }] },
    "Viernes":  { "abierto": true,  "franjas": [{ "apertura": "09:00", "cierre": "13:00" }, { "apertura": "15:00", "cierre": "19:00" }] },
    "Sabado":   { "abierto": true,  "franjas": [{ "apertura": "09:00", "cierre": "13:00" }] },
    "Domingo":  { "abierto": false, "franjas": [] }
  },
  "minutosAnticipacionMinimaReserva": 60,
  "diasMaximosReservaFutura": 30,
  "minutosBufferEntreTurnos": 10,
  "permiteCancelacionCliente": true,
  "horasMinimasCancelacion": 24,
  "enviaRecordatorioEmail": false,
  "enviaRecordatorioWhatsapp": false
}
```

**Respuestas de error:** `404`.

---

### `PUT /api/ajustes` 🔒

Actualiza todos los ajustes de una sucursal de forma atómica.

**Query params:**

| Parámetro    | Tipo   | Requerido |
|--------------|--------|-----------|
| `sucursalId` | `Guid` | ✅        |

**Body:**

| Campo                              | Tipo                           | Requerido | Restricciones            |
|------------------------------------|--------------------------------|-----------|--------------------------|
| `nombre`                           | `string`                       | ✅        | máx. 200 chars           |
| `slogan`                           | `string \| null`               | ❌        | máx. 300 chars           |
| `fotoUrl`                          | `string \| null`               | ❌        | máx. 500 chars           |
| `rubros`                           | `string[]`                     | ❌        |                          |
| `moneda`                           | `string`                       | ✅        | máx. 10 chars (`"ARS"`)  |
| `direccion`                        | `string`                       | ✅        | máx. 500 chars           |
| `telefono`                         | `string`                       | ✅        | máx. 50 chars            |
| `email`                            | `string`                       | ✅        | máx. 200 chars           |
| `horariosAtencion`                 | `object`                       | ❌        | Ver estructura de ejemplo|
| `minutosAnticipacionMinimaReserva` | `int`                          | ✅        | 0–10080                  |
| `diasMaximosReservaFutura`         | `int`                          | ✅        | 1–365                    |
| `minutosBufferEntreTurnos`         | `int`                          | ✅        | 0–120                    |
| `permiteCancelacionCliente`        | `bool`                         | ✅        |                          |
| `horasMinimasCancelacion`          | `int`                          | ✅        | 0–720                    |
| `enviaRecordatorioEmail`           | `bool`                         | ✅        |                          |
| `enviaRecordatorioWhatsapp`        | `bool`                         | ✅        |                          |

**Respuesta 200 OK:** objeto ajustes actualizado.

**Respuestas de error:** `400`, `404`.

---

## 9. Caja

### `GET /api/caja/sucursal/{sucursalId}` 🔒

Obtiene la caja asociada a una sucursal. Cada sucursal tiene exactamente una caja.

**Parámetros de ruta:**

| Parámetro    | Tipo   | Descripción       |
|--------------|--------|-------------------|
| `sucursalId` | `Guid` | ID de la sucursal |

**Respuesta 200 OK:**

```json
{
  "id": "c9d8e7f6-5432-10ab-cdef-fedcba987654",
  "sucursalId": "1c4d9bac-f72d-498a-8fa0-6d609a9e04e7",
  "nombre": "Caja Principal",
  "activa": true
}
```

**Respuestas de error:** `404`.

---

### `GET /api/caja/{cajaId}/movimientos` 🔒

Lista los movimientos de una caja. Soporta filtro por rango de fechas.

**Parámetros de ruta:**

| Parámetro | Tipo   | Descripción    |
|-----------|--------|----------------|
| `cajaId`  | `Guid` | ID de la caja  |

**Query params:**

| Parámetro | Tipo       | Requerido | Descripción                          |
|-----------|------------|-----------|--------------------------------------|
| `desde`   | `datetime` | ❌        | Fecha inicio del filtro (ISO 8601)   |
| `hasta`   | `datetime` | ❌        | Fecha fin del filtro (ISO 8601)      |

**Ejemplo:** `GET /api/caja/c9d8e7f6-.../movimientos?desde=2026-03-01&hasta=2026-03-31`

**Respuesta 200 OK:**

```json
[
  {
    "id": "11223344-5566-7788-99aa-bbccddeeff00",
    "cajaId": "c9d8e7f6-5432-10ab-cdef-fedcba987654",
    "usuarioId": "a1b2c3d4-1234-5678-abcd-ef9876543210",
    "turnoId": "f1e2d3c4-b5a6-7890-abcd-ef1234567890",
    "tipoMovimiento": "CobroTurno",
    "medioPago": "Efectivo",
    "monto": 5500.00,
    "observaciones": "Cobro turno Laura Martínez",
    "fecha": "2026-03-16T10:55:00"
  }
]
```

---

### `POST /api/caja/{cajaId}/movimientos` 🔒

Registra un nuevo movimiento en la caja (cobro, gasto, retiro, etc.).

**Parámetros de ruta:** `cajaId` (Guid)

**Body:**

| Campo           | Tipo                  | Requerido | Descripción                               |
|-----------------|-----------------------|-----------|-------------------------------------------|
| `cajaId`        | `Guid`                | ✅        | Debe coincidir con el de la URL           |
| `usuarioId`     | `Guid`                | ✅        | ID del usuario que registra el movimiento |
| `turnoId`       | `Guid \| null`        | ❌        | Vincula el movimiento a un turno          |
| `tipoMovimiento`| `TipoMovimientoCaja`  | ✅        | Ver enum                                  |
| `medioPago`     | `MedioPago`           | ✅        | Ver enum                                  |
| `monto`         | `decimal`             | ✅        | Monto del movimiento                      |
| `observaciones` | `string \| null`      | ❌        |                                           |

```json
{
  "cajaId": "c9d8e7f6-5432-10ab-cdef-fedcba987654",
  "usuarioId": "a1b2c3d4-1234-5678-abcd-ef9876543210",
  "turnoId": "f1e2d3c4-b5a6-7890-abcd-ef1234567890",
  "tipoMovimiento": "CobroTurno",
  "medioPago": "MercadoPago",
  "monto": 5500.00,
  "observaciones": "Pago online"
}
```

**Respuesta 201 Created:** objeto movimiento creado.

**Respuestas de error:**

| Código | Descripción                                       |
|--------|---------------------------------------------------|
| `400`  | Datos inválidos o `cajaId` del body ≠ URL         |
| `404`  | Caja o usuario no encontrado                      |

---

## 10. Super Admin

> Estos endpoints gestionan el sistema a nivel global (multi-tenant). Deben protegerse adicionalmente en producción mediante el token del SuperAdmin.

### `GET /api/superadmin/sucursales` 🔒

Lista todas las sucursales con información del owner.

**Respuesta 200 OK:**

```json
[
  {
    "id": "1c4d9bac-f72d-498a-8fa0-6d609a9e04e7",
    "nombre": "Salón Beleza",
    "direccion": "Av. Corrientes 1234, CABA",
    "telefono": "11 4567-8901",
    "email": "info@salonbeleza.com",
    "activa": true,
    "fechaCreacion": "2026-03-13T14:29:24",
    "owner": {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "nombre": "Juan",
      "apellido": "Ruiz",
      "nombreCompleto": "Juan Ruiz",
      "email": "juan@salon.com",
      "telefono": "1133445566",
      "estado": "Activo"
    }
  }
]
```

---

### `GET /api/superadmin/sucursales/{id}` 🔒

Obtiene una sucursal específica con su owner.

**Respuesta 200 OK:** objeto `SucursalSuperAdminDto`. **404** si no existe.

---

### `POST /api/superadmin/sucursales` 🔒

Crea una nueva sucursal junto con su usuario owner. El owner queda en estado `PendienteInvitacion` hasta que establezca su contraseña.

**Body:**

| Campo           | Tipo     | Requerido | Descripción                           |
|-----------------|----------|-----------|---------------------------------------|
| `nombre`        | `string` | ✅        | Nombre del negocio (máx. 200)         |
| `direccion`     | `string` | ✅        | Dirección (máx. 500)                  |
| `telefono`      | `string` | ✅        | Teléfono (máx. 50)                    |
| `email`         | `string` | ✅        | Email de la sucursal (máx. 200)       |
| `ownerNombre`   | `string` | ✅        | Nombre del owner (máx. 100)           |
| `ownerApellido` | `string` | ✅        | Apellido del owner (máx. 100)         |
| `ownerEmail`    | `string` | ✅        | Email del owner (máx. 200)            |
| `ownerTelefono` | `string` | ✅        | Teléfono del owner (máx. 50)          |
| `ownerPassword` | `string` | ✅        | Contraseña inicial del owner          |

```json
{
  "nombre": "Nuevo Salón",
  "direccion": "Av. Rivadavia 9999, CABA",
  "telefono": "11 9999-0000",
  "email": "contacto@nuevosalon.com",
  "ownerNombre": "Pedro",
  "ownerApellido": "García",
  "ownerEmail": "pedro@nuevosalon.com",
  "ownerTelefono": "1177889900",
  "ownerPassword": "Password123!"
}
```

**Respuesta 201 Created:** objeto `SucursalSuperAdminDto`.

**Respuestas de error:** `400`.

---

### `PUT /api/superadmin/sucursales/{id}` 🔒

Actualiza los datos de una sucursal (solo datos de la sucursal, no del owner).

**Body:** `nombre`, `direccion`, `telefono`, `email` (strings requeridos).

**Respuesta 200 OK:** objeto actualizado. **404** si no existe.

---

### `PATCH /api/superadmin/sucursales/{id}/estado` 🔒

Alterna el estado activo/inactivo de una sucursal (toggle).

**Respuesta 200 OK:** objeto con el nuevo estado.

```json
{
  "id": "1c4d9bac-...",
  "nombre": "Salón Beleza",
  "activa": false,
  ...
}
```

**Respuestas de error:** `404`.

---

## 11. Referencia de Enums

Los enums se serializan como **strings** en todos los endpoints (no como números enteros), gracias al `JsonStringEnumConverter` configurado globalmente.

### `EstadoTurno`

| Valor               | Descripción                              |
|---------------------|------------------------------------------|
| `Pendiente`         | Turno creado, a confirmar                |
| `Confirmado`        | Turno confirmado por el negocio          |
| `CanceladoCliente`  | Cancelado por el cliente                 |
| `CanceladoStaff`    | Cancelado por el staff                   |
| `Completado`        | Turno realizado                          |
| `NoShow`            | El cliente no se presentó               |
| `Reprogramado`      | Turno movido a otra fecha/hora           |

### `OrigenTurno`

| Valor           | Descripción                        |
|-----------------|------------------------------------|
| `Web`           | Reserva online del cliente         |
| `Administrativo`| Cargado desde el panel admin       |
| `Profesional`   | Cargado por el profesional         |
| `Caja`          | Cargado desde el módulo de caja    |
| `Manual`        | Carga manual genérica              |

### `RolUsuario`

| Valor           | Descripción                                  |
|-----------------|----------------------------------------------|
| `Administrativo`| Acceso completo al panel de gestión          |
| `Profesional`   | Puede ver y gestionar su propia agenda       |
| `Caja`          | Acceso al módulo de cobros y caja            |

### `EstadoUsuario`

| Valor                | Descripción                                      |
|----------------------|--------------------------------------------------|
| `PendienteInvitacion`| Invitado pero no ha establecido su contraseña    |
| `Activo`             | Usuario activo                                   |
| `Inactivo`           | Soft-deleted, no puede iniciar sesión            |

### `PermisoSistema`

| Valor                | Descripción                                      |
|----------------------|--------------------------------------------------|
| `AgendarTurnos`      | Puede crear nuevos turnos                        |
| `EditarTurnos`       | Puede modificar o cancelar turnos                |
| `CargarCobranzas`    | Puede registrar movimientos en caja              |
| `VerTelefonoClientes`| Puede ver el teléfono de los clientes            |

### `TipoSena`

| Valor        | Descripción                              |
|--------------|------------------------------------------|
| `Fija`       | Monto fijo en pesos                      |
| `Porcentaje` | Porcentaje sobre el precio del servicio  |

### `ModalidadPrestacion`

| Valor        | Descripción        |
|--------------|--------------------|
| `Presencial` | Atención en local  |
| `Maquina`    | Con equipamiento   |
| `Tratamiento`| Tratamiento largo  |
| `Otro`       | Otro tipo          |

### `TipoServicio`

| Valor      | Descripción                                  |
|------------|----------------------------------------------|
| `Regular`  | Precio fijo                                  |
| `Variable` | El precio puede variar por sesión            |

### `TipoMovimientoCaja`

| Valor          | Descripción                    |
|----------------|--------------------------------|
| `CobroTurno`   | Cobro asociado a un turno      |
| `Gasto`        | Egreso del negocio             |
| `Retiro`       | Retiro de efectivo             |
| `Ajuste`       | Ajuste de caja                 |
| `IngresoManual`| Ingreso sin turno asociado     |

### `MedioPago`

| Valor          | Descripción         |
|----------------|---------------------|
| `Efectivo`     | Efectivo            |
| `MercadoPago`  | Mercado Pago        |
| `Transferencia`| Transferencia bancaria |
| `Tarjeta`      | Tarjeta de débito/crédito |
| `Otro`         | Otro medio          |

---

*Documentación generada el 2026-03-14. Versión de API: 1.0.*
