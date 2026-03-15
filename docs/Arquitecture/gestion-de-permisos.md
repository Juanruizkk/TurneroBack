# Gestión de Permisos

Documento de referencia para el sistema de control de acceso de TurneroApp.

---

## Modelo de acceso

El sistema tiene dos capas de control de acceso que coexisten:

1. **Rol** (`RolUsuario`): define el perfil base del usuario. Determina a qué secciones puede acceder por defecto.
2. **Permisos granulares** (`PermisoSistema`): lista de capacidades adicionales que se asignan individualmente sobre el rol base.

Un usuario tiene acceso a una funcionalidad si su **rol** lo permite O si tiene el **permiso granular** correspondiente. Los administradores (`esOwner` o rol `Administrativo`) tienen acceso completo sin necesitar permisos individuales.

---

## Roles (`RolUsuario`)

Definidos en `src/Domain/Enums/RolUsuario.cs`. Persistidos como `int` en la DB.

| Valor | Nombre          | Descripción                                                                 |
|-------|-----------------|-----------------------------------------------------------------------------|
| `0`   | `Administrativo`| Acceso completo al panel. Equivalente funcional al Owner para la operación diaria |
| `1`   | `Profesional`   | Accede a su agenda, puede ver y gestionar sus propios turnos                |
| `2`   | `Caja`          | Accede al módulo de caja y cobranzas                                        |

Adicionalmente existe el flag `esOwner` (booleano en la entidad `Usuario`) que otorga el nivel más alto de acceso, incluyendo operaciones de gestión de sucursal.

---

## Permisos granulares (`PermisoSistema`)

Definidos en `src/Domain/Enums/PermisoSistema.cs`. Almacenados como lista de enteros en la tabla `usuario_permisos` (una fila por permiso por usuario).

| Valor | Nombre                | Qué desbloquea                                                                 |
|-------|-----------------------|--------------------------------------------------------------------------------|
| `0`   | `AgendarTurnos`       | Permite crear nuevos turnos desde el panel admin                               |
| `1`   | `EditarTurnos`        | Permite modificar el estado, horario y profesional de turnos existentes        |
| `2`   | `CargarCobranzas`     | Permite registrar pagos y movimientos en caja                                  |
| `3`   | `VerTelefonoClientes` | Permite ver el teléfono de los clientes en la ficha de cliente                 |
| `4`   | `GestionarAjustes`    | Permite acceder a la sección Ajustes del panel y modificar la configuración de la sucursal |

> **Nota de persistencia**: el campo `Permisos` en la entidad `Usuario` es `List<PermisoSistema>` y se guardaba originalmente como `jsonb`. Fue migrado a tabla separada `usuario_permisos` para permitir queries eficientes. La migración correspondiente es `AddUsuarioPermisosTable`.

---

## Función `tienePermiso`

Definida en `frontend/turnero-ui/src/contexts/AuthContext.tsx`.

```typescript
export function tienePermiso(user: SessionUser | null, permiso: PermisoSistema): boolean {
  if (!user) return false;
  if (user.esSuperAdmin || user.esOwner || user.rol === 'Administrativo') return true;
  return (user.permisos ?? []).includes(permiso);
}
```

**Regla clave**: `esSuperAdmin`, `esOwner` y rol `Administrativo` siempre devuelven `true` independientemente del permiso consultado. Solo para otros roles se verifica la lista de permisos individuales.

---

## Control de acceso por sección (frontend)

### Nav lateral — `AdminLayout.tsx`

El menú lateral filtra los ítems según el usuario:

| Ítem de nav   | Condición de visibilidad                                              |
|---------------|-----------------------------------------------------------------------|
| Equipo        | `esAdmin` (Owner o Administrativo)                                    |
| Ajustes       | `esAdmin` **o** `tienePermiso(user, 'GestionarAjustes')`              |
| Resto         | Visible para todos los usuarios autenticados                          |

```typescript
section.items.filter((item) => {
  if (item.path === '/admin/usuarios') return esAdmin;
  if (item.path === '/admin/ajustes') return esAdmin || tienePermiso(user, 'GestionarAjustes');
  return true;
})
```

### Página Ajustes — `Ajustes.tsx`

Además del filtro de nav, la propia página verifica acceso al montarse y redirige si no tiene permiso:

```typescript
const tieneAcceso = user?.esOwner || user?.rol === 'Administrativo'
  || tienePermiso(user, 'GestionarAjustes');

useEffect(() => {
  if (!tieneAcceso) navigate('/admin');
}, [tieneAcceso]);
```

Esto protege contra acceso directo por URL (`/admin/ajustes`), no solo a través del nav.

### Página Equipo — `Usuarios.tsx`

Solo accesible para `esAdmin`. No usa permiso granular.

---

## Gestión de permisos desde el panel

Los permisos se asignan en la sección **Equipo** (`/admin/usuarios`). Al crear o editar un usuario, el admin puede marcar/desmarcar cada permiso con checkboxes.

El array de permisos disponibles (`TODOS_LOS_PERMISOS`) y sus etiquetas (`PERMISO_LABELS`) están definidos en `Usuarios.tsx`:

```typescript
const PERMISO_LABELS: Record<PermisoSistema, string> = {
  AgendarTurnos:       'Agendar turnos',
  EditarTurnos:        'Editar turnos',
  CargarCobranzas:     'Cargar cobranzas',
  VerTelefonoClientes: 'Ver teléfonos',
  GestionarAjustes:    'Gestionar ajustes',
};
```

Al guardar, los permisos se envían como array de strings en `ActualizarUsuarioDto.permisos`.

---

## Endpoints REST

### `PUT /api/usuarios/{id}`

Acepta `permisos: PermisoSistema[]` en el body. El backend elimina los permisos existentes del usuario y reinserta los nuevos.

### `GET /api/usuarios/{sucursalId}`

Devuelve los usuarios con su lista de permisos actual. El frontend los usa para inicializar los checkboxes al editar.

---

## Escenarios de verificación

| Rol          | `GestionarAjustes` asignado | Nav muestra "Ajustes" | Puede acceder a `/admin/ajustes` |
|--------------|-----------------------------|-----------------------|----------------------------------|
| Owner        | —                           | Sí                    | Sí                               |
| Administrativo | —                         | Sí                    | Sí                               |
| Profesional  | No                          | No                    | No (redirige a `/admin`)         |
| Profesional  | Sí                          | Sí                    | Sí                               |
| Caja         | No                          | No                    | No (redirige a `/admin`)         |
| Caja         | Sí                          | Sí                    | Sí                               |

---

## Extensión: agregar un nuevo permiso

1. **Backend** — `src/Domain/Enums/PermisoSistema.cs`: agregar el nuevo valor al enum con el siguiente entero disponible.
2. **Frontend tipos** — `src/types/index.ts`: agregar el string literal al union type `PermisoSistema`.
3. **Frontend UI** — `src/pages/admin/Usuarios.tsx`: agregar entrada en `PERMISO_LABELS` y `TODOS_LOS_PERMISOS`.
4. **Frontend guards** — Agregar la verificación con `tienePermiso(user, 'NuevoPermiso')` en los componentes que deban restringirse.

No se requiere migración de base de datos: los permisos nuevos simplemente no aparecerán en la tabla `usuario_permisos` hasta que se asignen.
