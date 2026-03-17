# Recordatorios de turno por WhatsApp

Sistema de envío **manual** de recordatorios por WhatsApp desde el detalle del turno.
A diferencia de los recordatorios por email (que son automáticos), el staff decide cuándo y cuál enviar.

---

## Índice

1. [Resumen](#1-resumen)
2. [Diferencia con los recordatorios por email](#2-diferencia-con-los-recordatorios-por-email)
3. [Cómo funciona técnicamente](#3-cómo-funciona-técnicamente)
4. [Variables de plantilla](#4-variables-de-plantilla)
5. [Cambios en la base de datos](#5-cambios-en-la-base-de-datos)
6. [Archivos involucrados](#6-archivos-involucrados)
7. [Flujo completo de uso](#7-flujo-completo-de-uso)
8. [Condiciones de visibilidad del botón](#8-condiciones-de-visibilidad-del-botón)
9. [Configuración de plantillas en Ajustes](#9-configuración-de-plantillas-en-ajustes)

---

## 1. Resumen

| Aspecto           | Detalle                                                                 |
|-------------------|-------------------------------------------------------------------------|
| **Tipo**          | Manual (acción del staff)                                               |
| **Disparador**    | Click en "Recordatorio" → dropdown → opción de 24 h o 3 h              |
| **Mecanismo**     | Genera link `wa.me` con mensaje pre-completado y lo abre en nueva pestaña |
| **Plantillas**    | Configuradas por el Owner/Administrador en Ajustes → Configuración       |
| **Sin backend**   | No hay endpoint dedicado al envío. Todo ocurre en el navegador del staff |
| **Condición**     | Toggle "Recordatorio por WhatsApp" habilitado + cliente con teléfono    |

---

## 2. Diferencia con los recordatorios por email

| Aspecto              | Email (automático)                        | WhatsApp (manual)                         |
|----------------------|-------------------------------------------|-------------------------------------------|
| **Quién envía**      | Background service del servidor           | El staff, desde el detalle del turno      |
| **Cuándo se envía**  | Automáticamente en la ventana correcta    | Cuando el staff lo decide                 |
| **Canal de envío**   | SMTP → inbox del cliente                  | Navegador → WhatsApp Web/App del staff    |
| **Requiere SMTP**    | Sí                                        | No                                        |
| **Anti-duplicado**   | Flags `Recordatorio24hEnviado/3hEnviado`  | No (el staff es responsable)              |
| **Configuración**    | Toggle global en Ajustes                  | Toggle + 2 plantillas de texto en Ajustes |

---

## 3. Cómo funciona técnicamente

### Generación del link

```tsx
// frontend/turnero-ui/src/pages/admin/AppointmentDetail.tsx

function generarLinkWhatsApp(turno: Turno, plantilla: string): string {
  const mensaje = plantilla
    .replace(/\{\{ClienteNombre\}\}/g, turno.clienteNombre)
    .replace(/\{\{Servicio\}\}/g,      turno.servicioNombre)
    .replace(/\{\{Fecha\}\}/g,         formatFechaLarga(turno.fechaHoraInicio))
    .replace(/\{\{Hora\}\}/g,          formatHora(turno.fechaHoraInicio));

  // Limpia el teléfono: solo dígitos (asume código de país incluido)
  const telefono = turno.clienteTelefono.replace(/[\s\-\(\)\+]/g, '');

  return `https://wa.me/${telefono}?text=${encodeURIComponent(mensaje)}`;
}
```

### Ejemplo de URL generada

```
https://wa.me/541112345678?text=Hola%20Laura!%20Te%20recordamos%20tu%20turno%20de%20Masajes%20ma%C3%B1ana%20lunes%2017%20de%20marzo%20de%202026%20a%20las%2010%3A00.%20%C2%A1Te%20esperamos!
```

Al abrir esa URL, WhatsApp Web (o la app si el dispositivo es móvil) abre una conversación con el número del cliente y pre-carga el texto del mensaje. El staff lo revisa y lo envía manualmente.

### Formato del número de teléfono

El cliente debe tener su teléfono cargado **con código de país**, sin el `+` inicial. Ejemplos válidos:

| Formato guardado    | Resultado en `wa.me`  |
|---------------------|-----------------------|
| `+54 11 1234-5678`  | `541112345678`        |
| `5491112345678`     | `5491112345678`       |
| `011 1234-5678`     | sin código → puede fallar |

> **Recomendación:** Cargar siempre el teléfono del cliente con código de país (ej: `54` para Argentina, `598` para Uruguay).

---

## 4. Variables de plantilla

Las plantillas admiten cuatro variables que se reemplazan al generar el link:

| Variable             | Se reemplaza por                          | Ejemplo                        |
|----------------------|-------------------------------------------|--------------------------------|
| `{{ClienteNombre}}`  | `turno.clienteNombre`                     | `Laura Martínez`               |
| `{{Servicio}}`       | `turno.servicioNombre`                    | `Masaje descontracturante`     |
| `{{Fecha}}`          | `formatFechaLarga(turno.fechaHoraInicio)` | `lunes 17 de marzo de 2026`    |
| `{{Hora}}`           | `formatHora(turno.fechaHoraInicio)`       | `10:00`                        |

### Ejemplo de plantilla y resultado

**Plantilla 24 h:**
```
Hola {{ClienteNombre}}! Te recordamos tu turno de {{Servicio}} mañana {{Fecha}} a las {{Hora}}. ¡Te esperamos!
```

**Resultado:**
```
Hola Laura Martínez! Te recordamos tu turno de Masaje descontracturante mañana lunes 17 de marzo de 2026 a las 10:00. ¡Te esperamos!
```

---

## 5. Cambios en la base de datos

### Migración: `AgregarPlantillasWhatsApp`

Se agregaron dos columnas opcionales a la tabla `configuraciones_sucursal`:

| Columna               | Tipo              | Nullable | Longitud máx. |
|-----------------------|-------------------|----------|---------------|
| `MensajeWhatsApp24h`  | `varchar(2000)`   | Sí       | 2000 chars    |
| `MensajeWhatsApp3h`   | `varchar(2000)`   | Sí       | 2000 chars    |

Si ambas son `NULL`, el botón de WhatsApp no aparece en la UI aunque el toggle esté activo.

### Entidad de dominio

```csharp
// Domain/Entities/ConfiguracionSucursal.cs
/// <summary>Plantilla de mensaje WhatsApp para recordatorio 24 h antes.</summary>
public string? MensajeWhatsApp24h { get; set; }

/// <summary>Plantilla de mensaje WhatsApp para recordatorio 3 h antes.</summary>
public string? MensajeWhatsApp3h  { get; set; }
```

### Configuración EF Core

```csharp
// Infrastructure/Data/Configurations/ConfiguracionSucursalConfiguration.cs
builder.Property(c => c.MensajeWhatsApp24h).HasMaxLength(2000).IsRequired(false);
builder.Property(c => c.MensajeWhatsApp3h).HasMaxLength(2000).IsRequired(false);
```

---

## 6. Archivos involucrados

| Capa           | Archivo                                                                    | Cambio                                               |
|----------------|----------------------------------------------------------------------------|------------------------------------------------------|
| Domain         | `Domain/Entities/ConfiguracionSucursal.cs`                                 | +2 props string?                                     |
| Infrastructure | `Infrastructure/Data/Configurations/ConfiguracionSucursalConfiguration.cs` | Mapeo `HasMaxLength(2000).IsRequired(false)`         |
| Infrastructure | `Infrastructure/Migrations/…AgregarPlantillasWhatsApp.cs`                  | Nueva migración                                      |
| Application    | `Application/DTOs/Ajustes/AjustesSucursalDto.cs`                           | +2 props `string?`                                   |
| Application    | `Application/DTOs/Ajustes/ActualizarAjustesDto.cs`                         | +2 props `[MaxLength(2000)] string?`                 |
| Application    | `Application/Services/AjustesService.cs`                                   | Mapeo en `ObtenerAsync` y `ActualizarAsync`          |
| Frontend       | `frontend/…/types/index.ts`                                                | +2 props en `AjustesSucursal`                        |
| Frontend       | `frontend/…/pages/admin/Ajustes.tsx`                                       | Textareas + píldoras click-to-insert                 |
| Frontend       | `frontend/…/pages/admin/AppointmentDetail.tsx`                             | Dropdown "Recordatorio" + `generarLinkWhatsApp()`    |

---

## 7. Flujo completo de uso

```
Owner/Admin configura plantillas en Ajustes
│
├─ Sección "Configuración de reservas" → toggle "Recordatorio por WhatsApp" → ON
│
├─ Aparecen dos textareas: "24 h antes" y "3 h antes"
│   └─ Cada textarea tiene píldoras clickeables debajo:
│      {{ClienteNombre}} {{Servicio}} {{Fecha}} {{Hora}}
│      → Click en píldora → inserta variable en la posición del cursor
│
└─ Guardar cambios → se persiste en configuraciones_sucursal

─────────────────────────────────────────────────────────

Staff envía recordatorio desde detalle del turno
│
├─ Abre AppointmentDetail de un turno Confirmado o Pendiente
│
├─ Ve botón verde "Recordatorio ▾" en la barra de acciones
│
├─ Hace clic → dropdown con:
│   ├─ "24 horas antes" → recordatorio del día anterior
│   └─ "3 horas antes"  → recordatorio del mismo día
│      (Solo aparecen las opciones que tienen plantilla configurada)
│
└─ Selecciona una opción
    ├─ Se genera el link wa.me con variables reemplazadas
    ├─ Se abre WhatsApp Web/App en nueva pestaña
    └─ El staff revisa y envía el mensaje al cliente
```

---

## 8. Condiciones de visibilidad del botón

El botón "Recordatorio" solo aparece cuando se cumplen **las tres condiciones**:

```tsx
const mostrarWhatsApp =
  !!ajustes?.enviaRecordatorioWhatsapp &&          // toggle habilitado en ajustes
  !!turno?.clienteTelefono?.trim() &&              // cliente tiene teléfono cargado
  !!(ajustes?.mensajeWhatsApp24h || ajustes?.mensajeWhatsApp3h); // al menos una plantilla existe
```

Dentro del dropdown, cada opción individual se muestra solo si tiene su plantilla configurada:
- `ajustes?.mensajeWhatsApp24h` → muestra "24 horas antes"
- `ajustes?.mensajeWhatsApp3h`  → muestra "3 horas antes"

La barra de acciones completa solo se renderiza para turnos en estado `Confirmado` o `Pendiente`.

---

## 9. Configuración de plantillas en Ajustes

### Píldoras click-to-insert

Cada textarea tiene su propia fila de píldoras debajo. Al hacer clic en una píldora, la variable se inserta en la posición exacta del cursor dentro del textarea correspondiente:

```tsx
// Ajustes.tsx — helper insertAt
const insertAt = (
  ref: React.RefObject<HTMLTextAreaElement>,
  variable: string,
  key: 'mensajeWhatsApp24h' | 'mensajeWhatsApp3h',
) => {
  const el = ref.current;
  const start = el.selectionStart ?? el.value.length;
  const end   = el.selectionEnd   ?? start;
  const next   = el.value.slice(0, start) + variable + el.value.slice(end);
  const cursor = start + variable.length;
  set(key, next);                             // actualiza React state
  requestAnimationFrame(() => {               // restaura foco y cursor tras re-render
    el.focus();
    el.setSelectionRange(cursor, cursor);
  });
};
```

### Plantillas por defecto

Al crear una nueva sucursal (o al resetear el formulario), se pre-cargan estas plantillas:

| Tipo | Plantilla por defecto |
|------|-----------------------|
| 24 h | `Hola {{ClienteNombre}}! Te recordamos tu turno de {{Servicio}} mañana {{Fecha}} a las {{Hora}}. ¡Te esperamos!` |
| 3 h  | `Hola {{ClienteNombre}}! Tu turno de {{Servicio}} es hoy a las {{Hora}}. ¡Te esperamos pronto!` |

Estos valores se guardan como vacíos en la base de datos si el usuario los borra, lo que hace desaparecer la opción correspondiente del dropdown.

---

*Documentación generada el 2026-03-16.*
