# Usuarios Demo — Zen Massage Center

Sucursal: **Zen Massage Center** (`slug: zen-massage-center`)
Contraseña de todos los usuarios: `Demo123!`

---

## Owner / Profesional

| Campo     | Valor                          |
|-----------|-------------------------------|
| Nombre    | Adrián Sosa                   |
| Email     | adrian.sosa@zenmassage.com    |
| Rol       | Profesional                   |
| EsOwner   | Sí                            |
| Permisos  | Todos (implícito por Owner)   |
| Horario   | Lun–Vie 09:00–18:00           |

---

## Administrativo

| Campo     | Valor                          |
|-----------|-------------------------------|
| Nombre    | Laura Gómez                   |
| Email     | laura.gomez@zenmassage.com    |
| Rol       | Administrativo                |
| EsOwner   | No                            |
| Permisos  | Todos (implícito por rol)     |
| Horario   | Lun–Vie 09:00–18:00           |

---

## Profesional

| Campo     | Valor                                                    |
|-----------|----------------------------------------------------------|
| Nombre    | Martina López                                            |
| Email     | martina.lopez@zenmassage.com                             |
| Rol       | Profesional                                              |
| EsOwner   | No                                                       |
| Permisos  | AgendarTurnos, EditarTurnos, VerTelefonoClientes         |
| Horario   | Lun–Vie 09:00–18:00                                      |
| Servicios | Masaje Relajante, Masaje Descontracturante, Masaje con Piedras Calientes |

---

## Caja

| Campo     | Valor                                    |
|-----------|------------------------------------------|
| Nombre    | Diego Fernández                          |
| Email     | diego.fernandez@zenmassage.com           |
| Rol       | Caja                                     |
| EsOwner   | No                                       |
| Permisos  | CargarCobranzas (implícito por rol)      |
| Horario   | Lun–Vie 09:00–18:00                      |

---

## Notas

- **Owner** no es un rol separado. Es el flag `EsOwner = true` sobre cualquier rol. Otorga acceso total a la sucursal.
- **Administrativo** tiene acceso total implícito igual que Owner, pero sin poder gestionar la suscripción.
- **Profesional** sin `CargarCobranzas` no ve la sección de Caja ni el tab de Pagos en los turnos.
- **Caja** tiene acceso implícito a cobranzas sin necesitar el permiso explícito asignado.
