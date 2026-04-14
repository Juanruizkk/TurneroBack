# Slugs de sucursal — URL pública personalizada

## Contexto

Cada sucursal en MetaTurnos tiene una URL pública de la forma:

```
https://metaturnos.com/s/{slug}
```

El slug se genera automáticamente al crear la sucursal y puede ser editado por el owner o un administrativo desde la sección **Ajustes → URL Pública**.

---

## Generación automática

Cuando se crea una sucursal (via `RegisterService` o `SuperAdminService`), el slug se genera en el backend a partir del nombre del negocio usando `SlugHelper.GenerateSlug()`.

### Transformaciones aplicadas

1. Normalización Unicode FormKD: descompone caracteres acentuados (`á → a + combining accent`)
2. Eliminación de marcas diacríticas (NonSpacingMark)
3. Lowercase
4. Reemplazo de todo carácter no `[a-z0-9]` por guión `-`
5. Eliminación de guiones al inicio y al final
6. Si el resultado es vacío: fallback con 8 chars de UUID (`Guid.NewGuid().ToString("N")[..8]`)

### Resolución de colisiones

`SlugHelper.GenerateUniqueSlugAsync` prueba secuencialmente:
- `mi-negocio`
- `mi-negocio-2`, `mi-negocio-3`, … hasta `-99`
- Si todos ocupados: `mi-negocio-{8hex}` (fallback aleatorio)

### Palabras prohibidas

Los siguientes slugs están bloqueados tanto en backend como en frontend:

```
admin, api, login, register, dashboard, app,
planes, pricing, soporte, www, superadmin, publico,
auth, reset, forgot, invitacion, turno, reservar, pago
```

---

## Edición manual del slug

### Flujo completo

```
Usuario edita input en Ajustes → URL Pública
        ↓
Normalización en tiempo real (frontend)
        ↓
Validación local (longitud, regex, palabras prohibidas)
        ↓ (600ms debounce)
GET /api/ajustes/slug-disponible?sucursalId=&slug=
        ↓
Muestra: Disponible ✓ / En uso ✗ / Inválido ✗
        ↓ (usuario hace click en "Guardar slug")
PATCH /api/ajustes/slug?sucursalId=
        ↓
Backend valida + actualiza DB
        ↓
UI actualiza preview y resetea estado
```

---

## API — Endpoints

Todos requieren `[Authorize]` (JWT Bearer).

### `GET /api/ajustes/slug?sucursalId={guid}`

Devuelve el slug actual de la sucursal.

**Response 200:**
```json
{ "slug": "mi-barberia" }
```

---

### `GET /api/ajustes/slug-disponible?sucursalId={guid}&slug={string}`

Verifica si un slug está disponible para ser asignado a la sucursal indicada.
La sucursal indicada en `sucursalId` es excluida de la búsqueda (no compite con su propio slug actual).

**Response 200:**
```json
{ "disponible": true }
```

`disponible: false` puede significar tanto que el slug está tomado por otra sucursal **como** que la validación de formato falló (palabras prohibidas, regex, longitud).

---

### `PATCH /api/ajustes/slug?sucursalId={guid}`

**Body:**
```json
{ "slug": "nuevo-slug" }
```

**Response 200** — devuelve el `SucursalResponseDto` actualizado:
```json
{
  "id": "...",
  "nombre": "Mi Barbería",
  "slug": "nuevo-slug",
  "direccion": "...",
  ...
}
```

**Response 400** — slug inválido o ya en uso:
```json
{ "message": "El slug ya está en uso por otra sucursal." }
```

---

## Validación del slug

Las reglas se aplican tanto en frontend (`SlugEditor.tsx`) como en backend (`SlugHelper.EsSlugValido`):

| Regla | Detalle |
|---|---|
| Longitud mínima | 3 caracteres |
| Longitud máxima | 30 caracteres |
| Caracteres permitidos | `[a-z0-9-]` (solo minúsculas) |
| Formato | Debe empezar y terminar con letra o número (`^[a-z0-9][a-z0-9-]*[a-z0-9]$`) |
| Palabras prohibidas | Ver lista arriba |
| Unicidad | No puede estar en uso por otra sucursal |

---

## Normalización en el frontend

Mientras el usuario escribe, el input aplica automáticamente:

```typescript
function normalizarSlug(input: string): string {
  return input
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')   // elimina diacríticos
    .replace(/[^a-z0-9\s-]/g, '')      // solo letras, números, espacios y guiones
    .replace(/\s+/g, '-')              // espacios → guión
    .replace(/-+/g, '-')               // colapsa guiones múltiples
    .slice(0, 30);
}
```

El usuario nunca ve caracteres inválidos — se eliminan en tiempo real.

---

## Componente `SlugEditor`

**Ubicación:** `frontend/turnero-ui/src/components/admin/SlugEditor.tsx`

**Props:**
```typescript
interface SlugEditorProps {
  sucursalId: string;
}
```

El componente es **auto-contenido**: carga su propio slug inicial, maneja su propio estado de guardado, y tiene su propio botón "Guardar slug" independiente del guardado general de ajustes.

### Estados del check

| Estado | Descripción |
|---|---|
| `idle` | Sin interacción aún |
| `unchanged` | Slug igual al actual (sin cambios) |
| `checking` | Esperando respuesta de disponibilidad (debounce 600ms) |
| `available` | Slug válido y disponible → habilita el botón guardar |
| `taken` | Slug tomado por otra sucursal |
| `invalid` | Falló validación local (no se llama al backend) |

### Preview de URL

La URL se actualiza en tiempo real con el valor actual del input:

```
https://metaturnos.com/s/mi-barberia
```

La base del dominio se lee de `import.meta.env.VITE_PUBLIC_URL` con fallback a `https://metaturnos.com`.

---

## Integración en Ajustes

El `SlugEditor` se renderiza en la tab **"URL Pública"** (índice 4) de la página `/admin/ajustes`.

El guardado del slug es **independiente** del guardado general de ajustes:

- El botón "Guardar cambios" (principal) guarda nombre, horarios, configuración, etc.
- El botón "Guardar slug" (dentro del SlugEditor) solo actualiza el slug via `PATCH /api/ajustes/slug`.

---

## Archivos involucrados

### Backend

| Archivo | Responsabilidad |
|---|---|
| `src/Application/Utilities/SlugHelper.cs` | Generación, normalización y validación de slugs |
| `src/Application/DTOs/Sucursal/ActualizarSlugDto.cs` | DTO de entrada para PATCH |
| `src/Application/Interfaces/ISucursalRepository.cs` | `SlugExisteAsync()` |
| `src/Infrastructure/Repositories/SucursalRepository.cs` | Implementación de `SlugExisteAsync()` |
| `src/Application/Services/SucursalService.cs` | `ObtenerSlugAsync`, `SlugDisponibleAsync`, `ActualizarSlugAsync` |
| `src/Api/Controllers/AjustesController.cs` | Endpoints GET slug, GET slug-disponible, PATCH slug |

### Frontend

| Archivo | Responsabilidad |
|---|---|
| `src/api/ajustes.ts` | `obtenerSlug`, `checkSlugDisponible`, `actualizarSlug` |
| `src/components/admin/SlugEditor.tsx` | Componente de edición con preview, validación y debounce |
| `src/pages/admin/Ajustes.tsx` | Tab "URL Pública" que renderiza `<SlugEditor>` |

---

## Notas de diseño

- **Slug inmutable en creación:** una vez creada la sucursal, el slug solo puede cambiarse manualmente. El backend no actualiza automáticamente el slug si cambia el nombre del negocio (para no romper links existentes).
- **Advertencia al usuario:** el componente muestra un aviso de que cambiar el slug rompe URLs anteriores.
- **Palabras prohibidas sincronizadas:** la lista vive en `SlugHelper.PalabrasProhibidas` (backend) y se replica en `SlugEditor.tsx` (frontend). Si se agregan nuevas palabras, actualizar ambos.
- **Índice único en DB:** la columna `Slug` en la tabla `sucursales` tiene un índice único (`IX_sucursales_slug`), garantizando consistencia a nivel de base de datos independientemente de la lógica de aplicación.
