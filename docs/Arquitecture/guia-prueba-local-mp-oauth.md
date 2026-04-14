# Guía de prueba local — OAuth + Pagos MercadoPago con ngrok

Esta guía cubre el flujo completo para probar reservas de turno con seña via MercadoPago en entorno local, incluyendo la conexión OAuth de la sucursal.

**Archivo relacionado:** `docs/Arquitecture/mercadopago-senas.md` — arquitectura completa del módulo

---

## 0. Prerequisitos

- Docker Desktop corriendo
- ngrok instalado y autenticado (`ngrok config add-authtoken <tu-token>`)
- Cuenta en [MercadoPago Developers](https://www.mercadopago.com.ar/developers) con una app creada
- Backend y frontend clonados localmente

---

## 1. Configurar la app en el panel de MP Developers

1. Ir a [https://www.mercadopago.com.ar/developers/panel](https://www.mercadopago.com.ar/developers/panel)
2. Crear una nueva app o editar una existente
3. En la sección **Checkout Pro**: habilitarlo
4. En **URLs de redireccionamiento OAuth**, agregar:
   ```
   https://<ngrok-url>/api/mercadopago/oauth/callback
   ```
   > Aún no tenés la URL de ngrok — volvé a este paso después del paso 3
5. Copiar (pestaña **Credenciales de prueba / TEST**):
   - `Client ID`
   - `Client Secret`
6. En **Notificaciones webhook**, habilitar y copiar el `WebhookSecret`

---

## 2. Levantar la base de datos

```bash
docker compose up -d
```

Verificar que el contenedor de Postgres está corriendo:

```bash
docker ps
```

---

## 3. Exponer el backend con ngrok

```bash
ngrok http 5000
```

Copiar la URL `https` generada, por ejemplo:

```
https://abc123.ngrok-free.app
```

Volver al paso 1 y agregar la redirect URI con esta URL en el panel de MP.

---

## 4. Actualizar `appsettings.Development.json`

Abrir `src/Api/appsettings.Development.json` y completar:

```json
{
  "MercadoPagoOAuth": {
    "ClientId": "<Client ID copiado del panel>",
    "ClientSecret": "<Client Secret copiado del panel>",
    "RedirectUri": "https://<ngrok-url>/api/mercadopago/oauth/callback"
  },
  "MercadoPago": {
    "WebhookSecret": "<WebhookSecret copiado del panel>"
  },
  "App": {
    "BackendUrl": "https://<ngrok-url>"
  }
}
```

> **Nota:** Nunca commitear credenciales reales. El archivo `appsettings.Development.json` debe estar en `.gitignore`.

---

## 5. Levantar el backend

```bash
dotnet watch run --project src/Api
```

Al iniciar, confirmar en los logs que la migración `AddMpCheckoutUrlToTurnos` fue aplicada:

```
Applying migration 'AddMpCheckoutUrlToTurnos'...
```

Si no aparece, verificar que la cadena de conexión apunta al contenedor Docker correcto.

---

## 6. Conectar la sucursal a MercadoPago (OAuth)

### 6a. Obtener JWT del owner de la sucursal

```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "owner@ejemplo.com",
  "password": "tu-password"
}
```

Copiar el `token` de la respuesta.

### 6b. Iniciar el flujo OAuth

```http
GET /api/mercadopago/oauth/start?sucursalId={guid}
Authorization: Bearer <jwt>
```

La respuesta contiene una `redirectUrl`. Abrirla en el browser.

### 6c. Autorizar en MercadoPago

- Iniciar sesión con una **cuenta de vendedor TEST** de MercadoPago (distinta a la cuenta principal)
- Hacer clic en "Permitir"
- MP redirige al callback → la sucursal queda conectada

### 6d. Verificar conexión

```http
GET /api/mercadopago/oauth/estado?sucursalId={guid}
Authorization: Bearer <jwt>
```

Respuesta esperada:

```json
{
  "conectado": true,
  "email": "vendedortest@ejemplo.com"
}
```

---

## 7. Verificar / crear datos de prueba

Antes de crear un turno, confirmar que existen los siguientes datos via API o directamente en la DB:

| Entidad | Condición requerida |
|---------|---------------------|
| Sucursal | `Id` = el guid del paso anterior, con OAuth conectado |
| Caja | Estado = `Abierta`, pertenece a la sucursal |
| Cliente | Tiene `Email` no nulo (para recibir email de confirmación) |
| Servicio | `RequiereSena = true`, `MontoSena > 0` |

Si falta alguno, crearlo via los endpoints correspondientes con un JWT de owner/admin.

---

## 8. Crear un turno (origen Web)

```http
POST /api/turnos
Content-Type: application/json

{
  "sucursalId": "{guid}",
  "clienteId": "{guid}",
  "servicioId": "{guid-servicio-con-sena}",
  "fecha": "2026-04-10",
  "hora": "10:00",
  "origen": "Web"
}
```

**Respuesta esperada:**

```json
{
  "id": "{turnoId}",
  "estado": "PendientePago",
  "montoSena": 1500.00,
  "checkoutUrl": null
}
```

> Si `estado` no es `PendientePago`, verificar que el servicio tiene `RequiereSena=true`.

---

## 9. Iniciar pago

Este endpoint es público (no requiere JWT) porque lo llama el cliente desde la web.

```http
POST /api/pagos/turnos/{turnoId}/iniciar-pago
```

**Respuesta esperada:**

```json
{
  "checkoutUrl": "https://www.mercadopago.com.ar/checkout/v1/redirect?pref_id=..."
}
```

### Probar idempotencia

Llamar al mismo endpoint nuevamente sin haber pagado:

```http
POST /api/pagos/turnos/{turnoId}/iniciar-pago
```

Debe retornar **la misma `checkoutUrl`** sin crear una nueva preferencia en MercadoPago.

---

## 10. Pagar en el sandbox de MercadoPago

1. Abrir la `checkoutUrl` en el browser
2. Iniciar sesión con la **cuenta de comprador TEST** de MercadoPago

### Tarjetas de prueba

| Resultado | Número de tarjeta | CVV | Vencimiento |
|-----------|-------------------|-----|-------------|
| Aprobado (APRO) | 5031 7557 3453 0604 | 123 | 11/25 |
| Rechazado (OTHE) | 5031 7557 3453 0604 | 124 | 11/25 |
| Fondos insuficientes (FUND) | 3711 803032 57522 | 1234 | 11/25 |

> Las tarjetas de prueba completas están en: [https://www.mercadopago.com.ar/developers/es/docs/checkout-pro/additional-content/your-integrations/test/cards](https://www.mercadopago.com.ar/developers/es/docs/checkout-pro/additional-content/your-integrations/test/cards)

---

## 11. Verificar resultado

### API

```http
GET /api/turnos/{turnoId}
Authorization: Bearer <jwt>
```

**Si el pago fue aprobado:**
```json
{
  "estado": "Confirmado"
}
```

**Si el pago fue rechazado:**
```json
{
  "estado": "PagoRechazado"
}
```

### Consultas SQL de verificación

```sql
-- Estado del turno
SELECT id, estado, monto_sena, checkout_url
FROM turnos
WHERE id = '<turnoId>';

-- MovimientoCaja creado (pago aprobado)
SELECT id, monto, medio_pago, usuario_id, concepto
FROM movimientos_caja
WHERE turno_id = '<turnoId>';
-- Esperado: medio_pago = 'MercadoPago', usuario_id IS NULL

-- PagoTurno registrado
SELECT id, monto, estado, concepto, mp_payment_id
FROM pagos_turno
WHERE turno_id = '<turnoId>';
-- Esperado: concepto = 'Sena', estado = 'Aprobado'
```

### Email

Verificar en los logs del backend que se intentó enviar el email al cliente. En entorno local, revisar el servicio de email configurado (Mailtrap u otro SMTP de prueba).

---

## 12. Fallback: verificar pago manualmente (si el webhook no llegó)

Puede ocurrir que ngrok no entregue la notificación a tiempo o que el webhook falle. En ese caso:

### Opción A — verificar vía polling

```http
POST /api/pagos/turnos/{turnoId}/verificar-pago
```

Consulta el estado del pago directamente en la API de MercadoPago y actualiza el turno.

### Opción B — confirmar desde redirect

Si MP redirigió al success URL pero el webhook aún no procesó:

```http
POST /api/pagos/confirmar-redirect?paymentId={mpPaymentId}&turnoId={turnoId}
```

El `paymentId` aparece en la URL de retorno de MercadoPago.

---

## 13. Probar expiración automática

El servicio `TurnoPagoExpirationService` marca como `PagoRechazado` los turnos en `PendientePago` que superaron 30 minutos sin pagar.

### Opción A — esperar (flujo real)

Crear un turno → no pagar → esperar 30 minutos → verificar estado.

### Opción B — forzar en DB (más rápido)

```sql
UPDATE turnos
SET fecha_creacion = NOW() - INTERVAL '31 minutes'
WHERE id = '<turnoId>' AND estado = 'PendientePago';
```

Luego esperar el siguiente ciclo del background service (máx. 1 minuto) y verificar:

```sql
SELECT estado FROM turnos WHERE id = '<turnoId>';
-- Esperado: 'PagoRechazado'
```

---

## 14. Probar desconexión OAuth

### Desconectar la sucursal

```http
DELETE /api/mercadopago/oauth/desconectar?sucursalId={guid}
Authorization: Bearer <jwt>
```

### Verificar que iniciar-pago falla correctamente

Crear un nuevo turno y llamar:

```http
POST /api/pagos/turnos/{nuevoTurnoId}/iniciar-pago
```

**Respuesta esperada:** `400 Bad Request` o `422 Unprocessable Entity` con mensaje indicando que la sucursal no tiene MercadoPago conectado.

---

## Resumen del flujo completo

```
Panel MP → OAuth → Sucursal conectada
                          ↓
Cliente web → POST /turnos (origen=Web) → estado: PendientePago
                          ↓
              POST /iniciar-pago → checkoutUrl
                          ↓
              Browser → MP Checkout → pagar
                          ↓
         Webhook POST /pagos/webhook → procesar
                          ↓
              Turno: Confirmado, MovimientoCaja creado, Email enviado
```
