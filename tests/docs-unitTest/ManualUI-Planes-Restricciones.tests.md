# Tests manuales UI — CRUD de Planes y restricciones por suscripción

## Descripción general

Guía de testing manual para verificar desde la UI que el CRUD de Planes (panel SuperAdmin) y el sistema de restricciones por suscripción (guard 402/403) funcionan correctamente.

**Bloques cubiertos:**
1. CRUD de Planes desde el panel SuperAdmin
2. Bloqueo de creación de contenido por suscripción vencida (HTTP 402)
3. Feature gate de Reportes por plan (HTTP 403)
4. Interacción entre suscripción vencida y feature gate

---

## Sección 0 — Prerequisitos

Antes de comenzar, verificar que el entorno está levantado:

```bash
# 1. Base de datos
docker compose up -d

# 2. API con hot reload
dotnet watch run --project src/Api

# 3. Frontend
npm run dev   # en el proyecto React
```

**Requerimientos de datos:**
- Cuenta superadmin disponible para login
- Al menos una sucursal con un usuario owner registrado
- Los 3 planes base sembrados en DB (`Básico`, `Pro`, `Enterprise`)

---

## Sección 1 — CRUD de Planes (SuperAdmin)

**Objetivo:** verificar que el panel `/superadmin/planes` permite gestionar planes sin tocar la DB directamente.

---

### TC-01: Ver lista de planes

1. Login como superadmin → navegar a `/superadmin/planes`
2. **Resultado esperado:** tabla con al menos 3 planes (`Básico`, `Pro`, `Enterprise`) ordenados por `OrdenDisplay`
3. Verificar que las columnas presentes son: Orden, Nombre, MaxUsuarios, MaxTurnosMes, Precio, Trial, Reportes, Estado
4. Los planes `Pro` y `Enterprise` deben mostrar "Ilimitado" en la columna MaxTurnosMes (cuando el valor es `null`)
5. Los planes `Pro` y `Enterprise` deben mostrar un badge de "Reportes" activo (p. ej., badge púrpura)

---

### TC-02: Crear plan nuevo

1. Click en botón "Nuevo plan" → se abre modal de creación
2. Completar campos:
   - Nombre: `Starter`
   - MaxUsuarios: `5`
   - MaxTurnosMes: `100`
   - Precio: `2999`
   - DuracionTrialDias: `7`
   - TieneReportes: OFF
3. Click "Guardar"
4. **Resultado esperado:**
   - Toast de éxito visible
   - Plan `Starter` aparece en la tabla sin recargar la página
   - Stats card "Total" incrementó en 1

---

### TC-03: Editar plan existente

1. Click en ícono de edición (lápiz) en la fila `Básico`
2. **Resultado esperado:** modal se abre con los valores actuales precargados
3. Cambiar `DuracionTrialDias` de `14` a `7`
4. Click "Guardar"
5. **Resultado esperado:** fila actualizada en tabla sin recargar; el valor de Trial refleja `7 días`

---

### TC-04: Toggle activo/inactivo

1. Click en ícono de ojo (o toggle) en la fila `Starter` (creado en TC-02)
2. **Resultado esperado:** badge cambia de "Activo" a "Inactivo"
3. Click de nuevo → vuelve a "Activo"
4. Verificar que las stats cards "Activos" e "Inactivos" se actualizan con cada toggle

---

### TC-05: DuracionTrialDias afecta nuevas suscripciones

Prerequisito: plan `Básico` tiene `DuracionTrialDias = 7` (ajustado en TC-03).

1. Registrar una sucursal nueva desde `/register`, eligiendo el plan `Básico`
2. Verificar que la suscripción generada tiene `FechaVencimiento = hoy + 7 días` (no 14):

```sql
SELECT fecha_vencimiento FROM suscripciones_sucursal ORDER BY created_at DESC LIMIT 1;
```

**Resultado esperado:** `fecha_vencimiento` = fecha actual + 7 días.

---

## Sección 2 — Restricciones por suscripción vencida (HTTP 402)

**Objetivo:** verificar que una sucursal con suscripción vencida no puede crear contenido nuevo, pero puede operar normalmente en acciones de solo lectura y consulta.

**Setup general para cambiar estado en DB:**

```bash
docker exec turnero_db psql -U postgres -d turnero -c \
  "UPDATE suscripciones_sucursal SET estado = <N> WHERE sucursal_id = '<GUID>';"
```

| Valor | Estado          |
|-------|-----------------|
| `0`   | PruebaActiva    |
| `1`   | Activa          |
| `2`   | Vencida         |
| `3`   | Cancelada       |
| `4`   | PruebaVencida   |

---

### TC-06: Estado PruebaVencida — bloquea creación

1. Cambiar estado a `4` (PruebaVencida) para la sucursal de prueba
2. Login con usuario de esa sucursal
3. Intentar **crear un turno nuevo**
4. **Resultado esperado:** mensaje de error "Tu período de prueba venció. Activá tu suscripción para continuar creando contenido."
5. Intentar **agregar un servicio nuevo** → mismo bloqueo
6. Intentar **invitar un usuario** → mismo bloqueo

---

### TC-07: Estado Vencida — bloquea creación

1. Cambiar estado a `2` (Vencida)
2. Repetir las mismas acciones que TC-06
3. **Resultado esperado:** mensaje "Tu suscripción venció. Renovála para continuar."

---

### TC-08: Estado Cancelada — bloquea creación

1. Cambiar estado a `3` (Cancelada)
2. Repetir las mismas acciones que TC-06
3. **Resultado esperado:** mensaje "Tu suscripción fue cancelada. Renovála para continuar."

---

### TC-09: Suscripción vencida — acceso libre (lo que SÍ debe funcionar)

Con estado `4` (PruebaVencida) activo, verificar que estas acciones NO generan error:

1. **Login** → funciona
2. **Ver dashboard** → funciona
3. **Ver agenda** → lista turnos existentes sin error 402
4. **Ver clientes** → funciona
5. **Registrar cobro de turno existente** → funciona
6. **Editar datos del perfil** → funciona

**Resultado esperado:** ninguna de estas acciones retorna error 402; la restricción aplica solo a creación de nuevo contenido.

---

### TC-10: Restaurar estado activo

1. Cambiar estado a `1` (Activa):

```bash
docker exec turnero_db psql -U postgres -d turnero -c \
  "UPDATE suscripciones_sucursal SET estado = 1 WHERE sucursal_id = '<GUID>';"
```

2. Crear un turno nuevo → **Resultado esperado:** funciona normalmente, sin mensajes de error

---

## Sección 3 — Feature gate de Reportes (HTTP 403)

**Objetivo:** verificar que la sección de reportes solo es accesible para sucursales con un plan que tenga `TieneReportes = true`.

---

### TC-11: Plan Básico — reportes bloqueados

Prerequisito: la sucursal de prueba tiene suscripción con plan `Básico` (`TieneReportes = false`).

1. Login con usuario de esa sucursal
2. Navegar a `/admin/reportes`
3. **Resultado esperado:** pantalla de upgrade ("Los reportes no están incluidos en el plan Básico."), sin datos ni error genérico
4. Verificar que el mensaje incluye el nombre del plan actual y una indicación para hacer upgrade

---

### TC-12: Plan Pro — reportes accesibles

Prerequisito: la sucursal tiene suscripción con plan `Pro` (`TieneReportes = true`).

Para cambiar el plan en DB:

```sql
UPDATE suscripciones_sucursal
SET plan_id = '10000000-0000-0000-0000-000000000002'  -- Pro
WHERE sucursal_id = '<GUID>';
```

1. Navegar a `/admin/reportes`
2. **Resultado esperado:** reportes cargan normalmente (puede mostrar datos vacíos si no hay turnos, pero sin pantalla de bloqueo ni error 403)

---

### TC-13: Habilitar TieneReportes en Básico desde SuperAdmin

1. Login como superadmin → `/superadmin/planes`
2. Click en editar fila `Básico` → activar toggle "Reportes" → Guardar
3. Volver a la UI de la sucursal con plan `Básico` → navegar a `/admin/reportes`
4. **Resultado esperado:** reportes ahora accesibles; desapareció la pantalla de upgrade
5. Volver a SuperAdmin → desactivar toggle "Reportes" en `Básico` para restaurar el estado original

---

## Sección 4 — Interacción entre suscripción vencida y feature gate

### TC-14: Suscripción vencida + plan Pro

1. Sucursal con plan `Pro` y estado `2` (Vencida):

```sql
UPDATE suscripciones_sucursal
SET plan_id = '10000000-0000-0000-0000-000000000002', estado = 2
WHERE sucursal_id = '<GUID>';
```

2. Navegar a `/admin/reportes`
3. **Resultado esperado:** pantalla de suscripción vencida (error 402) — la suscripción vencida tiene prioridad y bloquea el acceso aunque el plan tenga `TieneReportes = true`

> **Nota sobre prioridad de guards:**
> - Sucursal con plan **Pro** + estado **Vencida** → error **402** (suscripción vencida, el plan tenía acceso a reportes)
> - Sucursal con plan **Básico** + estado **Vencida** → error **403** (el plan nunca tuvo acceso a reportes, ese guard tiene prioridad en ReportsController)

---

---

## Sección 5 — Límites numéricos por plan (MaxTurnosMes y MaxServicios)

**Objetivo:** verificar que los límites numéricos definidos en el plan bloquean la creación de contenido cuando se alcanza el cupo, con el mensaje correcto que indica el nombre del plan.

> **Nota:** estos límites se validan en el servicio de aplicación (no en el controller), por lo que el frontend recibe un `400 Bad Request` con `{ "message": "..." }`, no un 402.

---

### TC-15: Límite de turnos mensuales (`MaxTurnosMes`)

**Setup:**

```sql
-- Asignar un plan con MaxTurnosMes bajo para prueba
-- Opción A: editar el plan Básico desde SuperAdmin (MaxTurnosMes = 2)
-- Opción B: directo en DB
UPDATE planes SET max_turnos_mes = 2 WHERE nombre = 'Básico';
```

Asegurarse de que la sucursal de prueba tenga suscripción activa con plan `Básico`.

**Pasos:**

1. Login con usuario de la sucursal de prueba
2. Crear **2 turnos** en el mes actual → ambos deben crearse sin error
3. Intentar crear un **3er turno** en el mismo mes
4. **Resultado esperado:**
   - Error con mensaje: `"Tu plan Básico permite hasta 2 turnos por mes. Actualizá tu suscripción para continuar."`
   - El turno NO se crea
5. Crear el 3er turno en el **mes siguiente** → debe crearse OK (el contador es por mes calendario)

**Restaurar:**

```sql
UPDATE planes SET max_turnos_mes = 200 WHERE nombre = 'Básico';
```

---

### TC-16: Límite de servicios activos (`MaxServicios`)

**Setup:**

```sql
-- Reducir MaxServicios del plan Básico para prueba
UPDATE planes SET max_servicios = 2 WHERE nombre = 'Básico';
```

Asegurarse de que la sucursal de prueba tenga suscripción activa con plan `Básico` y **menos de 2 servicios activos** (desactivar los que sobren si es necesario).

**Pasos:**

1. Login con usuario de la sucursal
2. Crear **2 servicios** → ambos deben crearse sin error
3. Intentar crear un **3er servicio**
4. **Resultado esperado:**
   - Error con mensaje: `"Tu plan Básico permite hasta 2 servicios. Actualizá tu suscripción para agregar más."`
   - El servicio NO se crea
5. Desactivar uno de los servicios existentes (soft-delete) → **no libera el cupo** (el cupo cuenta servicios `Activo = true`)
6. Verificar: con el servicio desactivado el cupo queda en 1 → crear el 3er servicio ahora **sí debe funcionar**

**Restaurar:**

```sql
UPDATE planes SET max_servicios = 5 WHERE nombre = 'Básico';
```

---

### TC-17: Plan con límites `null` — comportamiento ilimitado

**Objetivo:** verificar que con plan `Pro` (límites `null`) no se aplica ningún cupo.

```sql
-- Cambiar la sucursal de prueba al plan Pro
UPDATE suscripciones_sucursal
SET plan_id = '10000000-0000-0000-0000-000000000002'
WHERE sucursal_id = '<GUID>';
```

1. Crear turnos en el mes actual superando la cantidad que bloqueaba en TC-15 → todos deben crearse OK
2. Crear servicios superando la cantidad que bloqueaba en TC-16 → todos deben crearse OK
3. **Resultado esperado:** ningún error de cupo — el plan Pro es ilimitado en turnos y servicios

---

## SQL de referencia rápida

```sql
-- Ver todas las suscripciones con su estado y plan
SELECT
  ss.sucursal_id,
  s.nombre AS sucursal,
  p.nombre AS plan,
  ss.estado,
  ss.fecha_vencimiento
FROM suscripciones_sucursal ss
JOIN sucursales s ON s.id = ss.sucursal_id
JOIN planes p ON p.id = ss.plan_id;

-- Cambiar estado de suscripción (valores: 0=PruebaActiva, 1=Activa, 2=Vencida, 3=Cancelada, 4=PruebaVencida)
UPDATE suscripciones_sucursal SET estado = 4 WHERE sucursal_id = '<GUID>';

-- Cambiar plan de una suscripción
UPDATE suscripciones_sucursal SET plan_id = '<PLAN_GUID>' WHERE sucursal_id = '<GUID>';

-- IDs de planes base sembrados
-- 10000000-0000-0000-0000-000000000001 → Básico
-- 10000000-0000-0000-0000-000000000002 → Pro
-- 10000000-0000-0000-0000-000000000003 → Enterprise

-- Ver todos los planes con sus configuraciones actuales (incluyendo límites numéricos)
SELECT
  id,
  nombre,
  max_usuarios,
  max_turnos_mes,
  max_servicios,
  precio_mensual,
  es_activo,
  duracion_trial_dias,
  tiene_reportes,
  orden_display
FROM planes
ORDER BY orden_display;

-- Contar turnos activos de una sucursal en el mes actual (simula el límite MaxTurnosMes)
SELECT COUNT(*) FROM turnos
WHERE sucursal_id = '<GUID>'
  AND EXTRACT(YEAR FROM fecha_hora_inicio) = EXTRACT(YEAR FROM NOW())
  AND EXTRACT(MONTH FROM fecha_hora_inicio) = EXTRACT(MONTH FROM NOW())
  AND estado NOT IN (4, 5, 6);  -- 4=CanceladoStaff, 5=CanceladoCliente, 6=NoShow

-- Contar servicios activos de una sucursal (simula el límite MaxServicios)
SELECT COUNT(*) FROM servicios
WHERE sucursal_id = '<GUID>' AND activo = true;

-- Verificar fecha de vencimiento del último registro
SELECT fecha_vencimiento FROM suscripciones_sucursal ORDER BY created_at DESC LIMIT 1;
```
