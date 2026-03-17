# Adjuntos de Turno — Integración con Cloudinary

Módulo que permite subir y eliminar archivos (imágenes y PDFs) asociados a un turno, almacenados en Cloudinary bajo una carpeta controlada desde el código.

---

## Arquitectura en capas

```
Api (Controller)
    │  IFormFile → stream + metadata
    ▼
Application (AdjuntoTurnoService)
    │  orquesta DB + cloud
    ├──► IAdjuntoTurnoRepository  (persistencia)
    └──► IAdjuntoService          (almacenamiento en la nube)
                │
Infrastructure  │
    ├── AdjuntoTurnoRepository  → AppDbContext
    └── CloudinaryService       → Cloudinary SDK
```

### Interfaces en Application

**`IAdjuntoService`** — `src/Application/Interfaces/IAdjuntoService.cs`

```csharp
Task<(string Url, string PublicId, string TipoRecurso)> SubirArchivoAsync(
    Stream stream, string fileName, string contentType);

Task EliminarArchivoAsync(string publicId, string tipoRecurso);
```

**`IAdjuntoTurnoRepository`** — `src/Application/Interfaces/IAdjuntoTurnoRepository.cs`

```csharp
Task<List<AdjuntoTurno>> GetByTurnoIdAsync(Guid turnoId);
Task<AdjuntoTurno?>      GetByIdAsync(Guid id);
Task                     AddAsync(AdjuntoTurno adjunto);
void                     Delete(AdjuntoTurno adjunto);
Task                     SaveChangesAsync();
```

---

## Entidad `AdjuntoTurno`

Archivo: `src/Domain/Entities/AdjuntoTurno.cs`

| Propiedad       | Tipo       | Columna DB     | Descripción                                              |
|-----------------|------------|----------------|----------------------------------------------------------|
| `Id`            | `Guid`     | `Id`           | Identificador único, asignado manualmente                |
| `TurnoId`       | `Guid`     | `TurnoId`      | FK al turno al que pertenece (cascade delete)            |
| `AutorUsuarioId`| `Guid`     | `AutorUsuarioId`| FK al usuario que subió el archivo (restrict delete)    |
| `Url`           | `string`   | `UrlArchivo`   | URL segura (`https://`) de Cloudinary (máx. 2000 chars)  |
| `PublicId`      | `string`   | `PublicId`     | Identificador único en Cloudinary (necesario para borrar)|
| `NombreArchivo` | `string`   | `NombreArchivo`| Nombre original del archivo subido (máx. 500 chars)      |
| `TipoRecurso`   | `string`   | `TipoRecurso`  | `"image"` para imágenes, `"raw"` para PDFs (máx. 20)    |
| `Fecha`         | `DateTime` | `Fecha`        | Momento de la subida (UTC)                               |

> **Nota de columna:** La propiedad C# se llama `Url` pero en la DB la columna se llama `UrlArchivo` (mapping con `HasColumnName`) para mantener compatibilidad con la migración original.

### Relaciones

- `Turno` — muchos adjuntos por turno, eliminación en cascada
- `Autor` (Usuario) — restrict delete, el usuario puede eliminarse por separado

---

## CloudinaryService

Archivo: `src/Infrastructure/Services/CloudinaryService.cs`

Implementa `IAdjuntoService` usando el SDK oficial **CloudinaryDotNet**.

### Carpeta de almacenamiento

Todos los archivos se suben a:

```
TurneroApp/Adjuntos/
```

Este valor está hardcodeado como constante `Folder = "TurneroApp/Adjuntos"` en el servicio. Es el requisito clave que permite organizar assets en el plan gratuito de Cloudinary con una API Key global de entorno.

### Subida de archivos

```csharp
// Para imágenes (jpg, png, webp, etc.)
var params = new ImageUploadParams
{
    File           = new FileDescription(fileName, stream),
    Folder         = "TurneroApp/Adjuntos",
    UseFilename    = true,
    UniqueFilename = true,
};

// Para PDFs (contentType == "application/pdf")
var params = new RawUploadParams
{
    File           = new FileDescription(fileName, stream),
    Folder         = "TurneroApp/Adjuntos",
    UseFilename    = true,
    UniqueFilename = true,
};
```

El tipo de recurso Cloudinary se determina automáticamente por `contentType`:

| `contentType`       | Tipo Cloudinary | `TipoRecurso` en DB |
|---------------------|-----------------|----------------------|
| `application/pdf`   | `RawUploadParams` | `"raw"`            |
| Cualquier otro      | `ImageUploadParams` | `"image"`        |

### Eliminación de archivos

El borrado físico en Cloudinary requiere especificar el `ResourceType` correcto. Por eso se almacena `TipoRecurso` en la DB:

```csharp
var resourceType = tipoRecurso == "raw" ? ResourceType.Raw : ResourceType.Image;
var deleteParams = new DeletionParams(publicId) { ResourceType = resourceType };
await _cloudinary.DestroyAsync(deleteParams);
```

### Configuración

Los valores se leen desde `appsettings.json`:

```json
"Cloudinary": {
  "CloudName": "...",
  "ApiKey":    "...",
  "ApiSecret": "..."
}
```

El cliente Cloudinary se inicializa con `Api.Secure = true` para garantizar que todas las URLs generadas sean `https://`.

---

## AdjuntoTurnoService

Archivo: `src/Application/Services/AdjuntoTurnoService.cs`

Orquesta las operaciones de la capa de aplicación.

### `SubirAsync`

1. Llama a `IAdjuntoService.SubirArchivoAsync` → obtiene `(Url, PublicId, TipoRecurso)`.
2. Crea entidad `AdjuntoTurno` con todos los campos.
3. Persiste en DB y retorna `AdjuntoTurnoResponseDto`.

**Orden de operaciones:** primero se sube a Cloudinary, luego se guarda en DB. Si falla la subida, la DB no se toca. Si falla la DB después de la subida, el archivo queda huérfano en Cloudinary (aceptable para la fase actual).

### `EliminarAsync`

1. Busca el adjunto en DB.
2. Si no existe → retorna `false`.
3. Llama a `IAdjuntoService.EliminarArchivoAsync` con `PublicId` y `TipoRecurso`.
4. Elimina el registro de DB.

**Orden de operaciones:** primero borra de Cloudinary, luego de la DB. Si falla el borrado en Cloudinary, la DB no se modifica (el adjunto permanece registrado y se puede reintentar).

### DTO de respuesta

```csharp
public class AdjuntoTurnoResponseDto
{
    Guid     Id;
    Guid     TurnoId;
    string   Url;           // URL segura de Cloudinary
    string   PublicId;      // para uso interno / debug
    string   NombreArchivo; // nombre original del archivo
    string   TipoRecurso;   // "image" | "raw"
    DateTime Fecha;
}
```

---

## Endpoints REST

Definidos en `src/Api/Controllers/TurnosController.cs`.

| Método     | Ruta                                         | Body                  | Respuesta              |
|------------|----------------------------------------------|-----------------------|------------------------|
| `GET`      | `/api/turnos/{turnoId}/adjuntos`             | —                     | `AdjuntoTurnoResponseDto[]` |
| `POST`     | `/api/turnos/{turnoId}/adjuntos`             | `multipart/form-data` field `file` | `AdjuntoTurnoResponseDto` (201) |
| `DELETE`   | `/api/turnos/{turnoId}/adjuntos/{adjuntoId}` | —                     | `204 No Content`       |

### Detalles del POST

- `Content-Type: multipart/form-data`
- El archivo se recibe como `IFormFile file` via `[FromForm]`.
- El `userId` del autor se extrae del claim JWT `"userId"`.
- Si no hay archivo o está vacío → `400 Bad Request`.
- Si Cloudinary falla → `400 Bad Request` con mensaje de error.

### Ejemplo de llamada con `curl`

```bash
# Subir imagen
curl -X POST "http://localhost:5000/api/turnos/{turnoId}/adjuntos" \
  -H "Authorization: Bearer {token}" \
  -F "file=@foto.jpg"

# Eliminar
curl -X DELETE "http://localhost:5000/api/turnos/{turnoId}/adjuntos/{adjuntoId}" \
  -H "Authorization: Bearer {token}"
```

---

## Frontend

### API client

**`src/api/client.ts`** — función `requestForm`

Para peticiones `multipart/form-data` **no se fija `Content-Type`**: se omite para que el browser calcule el boundary correcto automáticamente.

```typescript
// Correcto — el browser pone el boundary
api.postForm<AdjuntoTurno>(`/turnos/${turnoId}/adjuntos`, formData)

// Incorrecto — nunca hacer esto para FormData
fetch(url, { headers: { 'Content-Type': 'multipart/form-data' } })
```

### API de turnos

**`src/api/turnos.ts`**

```typescript
listarAdjuntos: (turnoId: string) =>
    api.get<AdjuntoTurno[]>(`/turnos/${turnoId}/adjuntos`),

subirAdjunto: (turnoId: string, formData: FormData) =>
    api.postForm<AdjuntoTurno>(`/turnos/${turnoId}/adjuntos`, formData),

eliminarAdjunto: (turnoId: string, adjuntoId: string) =>
    api.delete<void>(`/turnos/${turnoId}/adjuntos/${adjuntoId}`),
```

### Tipo TypeScript

**`src/types/index.ts`**

```typescript
export interface AdjuntoTurno {
  id: string;
  turnoId: string;
  url: string;
  publicId: string;
  nombreArchivo: string;
  tipoRecurso: 'image' | 'raw';
  fecha: string; // ISO 8601
}
```

### Pestaña Adjuntos — `AppointmentDetail.tsx`

La pestaña se carga de forma **lazy** al hacer click: el `useEffect` solo ejecuta `listarAdjuntos` cuando `activeTab === 'adjuntos'`.

**Estados del componente:**

| Estado            | Tipo                  | Descripción                           |
|-------------------|-----------------------|---------------------------------------|
| `adjuntos`        | `AdjuntoTurno[]`      | Lista cargada del servidor            |
| `loadingAdjuntos` | `boolean`             | Spinner de carga inicial              |
| `subiendoAdjunto` | `boolean`             | Spinner dentro del botón "Subir"      |
| `errorAdjunto`    | `string`              | Mensaje de error visible al usuario   |

**Flujo de subida:**

```typescript
const handleSubirArchivo = async (e: ChangeEvent<HTMLInputElement>) => {
  const file = e.target.files?.[0];
  const formData = new FormData();
  formData.append('file', file);              // key 'file' coincide con [FromForm]
  const nuevo = await turnosApi.subirAdjunto(id, formData);
  setAdjuntos((prev) => [...prev, nuevo]);    // actualización optimista local
  e.target.value = '';                        // reset para re-subir el mismo archivo
};
```

**Tipos de archivo aceptados:**

```html
<input type="file" accept="image/*,application/pdf" />
```

**Icono en la lista:**
- PDFs (`tipoRecurso === 'raw'` o extensión `.pdf`) → `FileIcon`
- Imágenes → `ImageIcon`
- Botón externo → `ExternalLinkIcon` (abre URL de Cloudinary en nueva pestaña)
- Botón eliminar → aparece en hover con `group-hover:opacity-100`

---

## Migración de base de datos

Migración: `AgregarAdjuntosCloudinary`

Operaciones sobre la tabla `adjuntos_turno`:

| Operación | Columna         | Detalle                                 |
|-----------|-----------------|-----------------------------------------|
| `ALTER`   | `UrlArchivo`    | `varchar(1000)` → `varchar(2000)`       |
| `ADD`     | `PublicId`      | `varchar(500) NOT NULL DEFAULT ''`      |
| `ADD`     | `NombreArchivo` | `varchar(500) NOT NULL DEFAULT ''`      |
| `ADD`     | `TipoRecurso`   | `varchar(20) NOT NULL DEFAULT 'image'`  |
| `DROP`    | `Descripcion`   | Campo eliminado (ya no está en la entidad) |

Para aplicar:

```bash
cd backend
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

---

## Registro en Program.cs

```csharp
// Repositorio
builder.Services.AddScoped<IAdjuntoTurnoRepository, AdjuntoTurnoRepository>();

// Cloudinary (implementa IAdjuntoService, lee config desde appsettings)
builder.Services.AddScoped<IAdjuntoService, CloudinaryService>();

// Servicio de aplicación
builder.Services.AddScoped<AdjuntoTurnoService>();
```

---

## Decisiones de diseño

| Decisión | Razón |
|---|---|
| `Folder = "TurneroApp/Adjuntos"` hardcodeado | Plan gratuito de Cloudinary con API Key global — sin carpeta fija, todos los assets caerían en el root del account |
| Guardar `TipoRecurso` en DB | La API de Cloudinary exige `ResourceType` correcto al borrar; sin él, el borrado de PDFs falla silenciosamente |
| `IAdjuntoService` acepta `Stream` (no `IFormFile`) | `IFormFile` es de ASP.NET Core — la interfaz en Application no puede depender de ese namespace |
| Cloudinary antes de DB al borrar | Garantiza consistencia: si borramos de DB primero y Cloudinary falla, el archivo queda huérfano sin registro para borrarlo después |
