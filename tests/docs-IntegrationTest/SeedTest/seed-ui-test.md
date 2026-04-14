# Seed UI Test — TurneroAPP

Script SQL para insertar datos de prueba en la DB de desarrollo y verificar el comportamiento de la UI ante escenarios de planes y límites.

---

## Escenarios

| | Sucursal A | Sucursal B |
|---|---|---|
| Nombre | Estética La Perla | Peluquería Torres |
| Plan | Profesional | Starter |
| Estado suscripción | **Vencida** (hace 15 días) | Activa (vence en 20 días) |
| MaxUsuarios | 5 | **2** (límite alcanzado) |
| MaxTurnosMes | 200 | **10** (4 usados → barra de progreso) |
| MaxServicios | 20 | **3** (3 creados → límite alcanzado) |
| Owner email | laura.vencida@test.com | marcos.limites@test.com |
| Password | Admin123! | Admin123! |

### Qué esperar en la UI

**Sucursal A (plan vencido):**
- Banner/bloqueo de acceso en `/admin`
- Funcionalidades restringidas hasta renovar suscripción

**Sucursal B (límites al tope):**
- `/admin/servicios` → 3 servicios listados, botón "Agregar" deshabilitado
- Contador de turnos del mes: 4/10 (barra de progreso visible)
- Sección usuarios: 2/2 (sin posibilidad de agregar más)

---

## Ejecución

```bash
psql -h localhost -p 5433 -U postgres -d turnero -f seed-ui-test.sql
```

O abrir el archivo en pgAdmin y ejecutar contra la DB de desarrollo.

---

## Cómo testear cada escenario

El panel admin obtiene el sucursalId directamente del JWT al loguearse.
No se necesita tocar el .env para probar distintas sucursales en el admin.

**Escenario A — plan vencido:**
1. Ir a /login
2. Email: laura.vencida@test.com / Password: Admin123!
3. Esperar banners de bloqueo en /admin

**Escenario B — límites al tope:**
1. Ir a /login
2. Email: marcos.limites@test.com / Password: Admin123!
3. Ir a /admin/servicios → 3 servicios, botón "Agregar" deshabilitado
4. Revisar contador de turnos del mes: 4/10

## ¿Qué hace VITE_SUCURSAL_ID entonces?

Es un fallback solo para la página pública de booking (BookingFlow, ruta /s/:slug).
En producción no existe — la URL con el slug de la sucursal determina qué sucursal mostrar.
Para testear el admin, es irrelevante.

---

## Troubleshooting

**Si algún INSERT falla por nombre de tabla**, verificar los nombres reales:
```sql
SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY tablename;
```

**Si el login rechaza Admin123!**, el hash BCrypt del script puede no coincidir con el cost del proyecto. Generar uno nuevo:
```csharp
BCrypt.Net.BCrypt.HashPassword("Admin123!") // cost por defecto del proyecto
```
Reemplazar el valor de `"PasswordHash"` en los dos INSERT de `usuarios`.

**Si los turnos fallan por FK en `ProfesionalId`**, puede ser que la columna apunte a `usuario_sucursal_roles.Id` en lugar de `usuarios.Id`. Cambiar a:
```
'e2222222-0000-0000-0000-000000000001'  -- Id del rol del owner de Sucursal B
```

**Si `configuraciones_sucursal` falla**, puede que la columna JSON se llame `"HorariosAtencionJson"` en lugar de `"HorariosAtencion"`. Ajustar el nombre de columna en el INSERT.

---

## IDs de referencia rápida

| Entidad | ID |
|---|---|
| Plan Profesional | `b1111111-0000-0000-0000-000000000001` |
| Plan Starter | `b2222222-0000-0000-0000-000000000001` |
| Usuario Laura (owner A) | `d1111111-0000-0000-0000-000000000001` |
| Usuario Marcos (owner B) | `d2222222-0000-0000-0000-000000000001` |
| Sucursal A (plan vencido) | `a1111111-0000-0000-0000-000000000001` |
| Sucursal B (límites tope) | `a2222222-0000-0000-0000-000000000001` |
| Suscripción A | `c1111111-0000-0000-0000-000000000001` |
| Suscripción B | `c2222222-0000-0000-0000-000000000001` |

---

## Limpieza

El script incluye sentencias DELETE comentadas al inicio. Des-comentarlas y ejecutar para resetear los datos de prueba sin afectar el resto de la DB.
