# Onboarding de sucursal

## Propósito

Al registrar una nueva cuenta, el negocio arranca con datos mínimos (nombre, email, contraseña). Para que pueda operar —recibir turnos, asignar profesionales, cobrar— necesita completar tres pasos de configuración. El sistema los guía sin bloquear el acceso al dashboard.

---

## Flujo general

```
Registro → Dashboard → OnboardingChecklist (visible hasta completar los 3 pasos)
                              │
           ┌──────────────────┼──────────────────┐
           ▼                  ▼                  ▼
     Ajustes          Servicios           Equipo
  (perfil completo)  (1er servicio)   (1er profesional)
```

La checklist se muestra automáticamente hasta que los tres pasos estén completos o el usuario la cierre manualmente. Una vez cerrada (o completada), no vuelve a aparecer en ese navegador.

---

## Backend

### Endpoint

```
GET /api/onboarding?sucursalId={guid}
Authorization: Bearer <token>
```

**Respuesta:**

```json
{
  "perfilCompleto": false,
  "tieneServicio": true,
  "tieneProfesional": false,
  "completado": false
}
```

`completado` es un campo calculado (`true` solo si los tres anteriores son `true`).

### Servicio: `OnboardingService`

Ubicación: `src/Application/Services/OnboardingService.cs`

Depende de:
- `ISucursalRepository` — verifica si `Telefono` y `Direccion` están completados
- `IServicioRepository` — llama a `ContarActivosBySucursalAsync`
- `IUsuarioSucursalRolRepository` — cuenta miembros con rol distinto a owner

#### Lógica de cada check

| Campo | Condición |
|---|---|
| `PerfilCompleto` | `sucursal.Telefono` no vacío **y** `sucursal.Direccion` no vacío |
| `TieneServicio` | `ContarActivosBySucursalAsync(sucursalId) > 0` |
| `TieneProfesional` | Existe al menos 1 `UsuarioSucursalRol` con `EsOwner = false` en la sucursal |

### Controlador: `OnboardingController`

Ubicación: `src/Api/Controllers/OnboardingController.cs`

Ruta: `GET /api/onboarding`
Atributo `[Authorize]` — requiere JWT válido.

### DTO: `OnboardingStatusDto`

Ubicación: `src/Application/DTOs/Onboarding/OnboardingStatusDto.cs`

```csharp
public class OnboardingStatusDto
{
    public bool PerfilCompleto  { get; set; }
    public bool TieneServicio   { get; set; }
    public bool TieneProfesional { get; set; }
    public bool Completado => PerfilCompleto && TieneServicio && TieneProfesional;
}
```

### Registro en Program.cs

```csharp
builder.Services.AddScoped<OnboardingService>();
```

---

## Frontend

### API client

Ubicación: `frontend/turnero-ui/src/api/onboarding.ts`

```typescript
export const onboardingApi = {
  getStatus: (sucursalId: string) =>
    api.get<OnboardingStatus>(`/onboarding?sucursalId=${sucursalId}`),
};
```

### Componente: `OnboardingChecklist`

Ubicación: `frontend/turnero-ui/src/components/admin/OnboardingChecklist.tsx`

Renderizado en `Dashboard.tsx`, entre el header y las stat cards.

#### Comportamiento

1. **Carga inicial**: si `localStorage` ya tiene la clave `onboarding_done_${sucursalId}`, el componente no monta ni hace fetch.
2. **Si ya estaba completo al cargar**: guarda la clave en localStorage y no muestra nada (evita el flash de "ya está listo").
3. **Progreso**: barra horizontal que refleja `completedCount / 3 * 100 %`, animada con `transition-all duration-700`.
4. **Pasos incompletos**: al hacer clic navegan a la ruta correspondiente.
5. **Pasos completados**: deshabilitados, icono de check, texto tachado.
6. **Al completar todos los pasos**: muestra "¡Tu negocio está listo!" con `PartyPopperIcon` durante 2,5 segundos y luego se oculta automáticamente guardando la clave en localStorage.
7. **Botón cerrar (×)**: guarda la clave y oculta el componente en cualquier momento.

#### Pasos definidos

| Clave | Label | Destino |
|---|---|---|
| `perfilCompleto` | Completá el perfil del negocio | `/admin/ajustes` |
| `tieneServicio` | Agregá tu primer servicio | `/admin/servicios` |
| `tieneProfesional` | Invitá a un profesional | `/admin/equipo` |

#### Persistencia con localStorage

La clave de dismiss es por sucursal:

```typescript
const DISMISS_KEY = (sucursalId: string) => `onboarding_done_${sucursalId}`;
```

Esto permite que en un mismo navegador con múltiples cuentas cada sucursal tenga su propio estado.

---

## Consideraciones

- **Sin polling**: el componente solo llama al endpoint una vez al montar. Para que un check se marque en tiempo real, el usuario debe navegar al paso, completarlo, y volver al dashboard (o refrescar).
- **No bloquea el acceso**: la checklist es informativa. Un admin puede ignorarla y operar con funcionalidad parcial.
- **Sin estado servidor**: no hay un campo `onboardingDismissed` en la base de datos. La persistencia es únicamente cliente (localStorage). Si el usuario cambia de navegador o borra el storage, la checklist reaparece pero se ocultará si ya estaba todo completo.
