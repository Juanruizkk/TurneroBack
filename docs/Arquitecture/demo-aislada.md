# Demo Aislada por Visitante

Documentación del diseño y funcionamiento del sistema de demostración pública efímera y aislada de MetaTurnos.

---

## Objetivo

Permitir que cualquier visitante de la Landing pruebe la aplicación completa en producción en su **propia sucursal efímera**, sin compartir datos con otros visitantes ni con sucursales reales, con un tiempo de vida (TTL) de 30 minutos y borrado automático.

---

## Modelo de Aislamiento

Cada sesión de demo genera una instancia de `Sucursal` completamente nueva:
- `EsDemo`: `true` (campo booleano en la tabla `sucursales`).
- `DemoExpiraEn`: Fecha y hora de expiración (`DateTime.UtcNow.AddMinutes(30)`).

No existe reutilización de datos entre visitantes. Cada request a `POST /api/demo/crear` clona el dataset demo ("Zen Massage Center") con datos independientes.

---

## Flujo Operativo

```
Landing (Botón "Probar Demo")
   │
   ▼ POST /api/demo/crear
┌───────────────────────────────────────────────┐
│ Backend:                                      │
│ 1. Verifica Rate Limit por IP (máx. 3 activas)│
│ 2. Verifica Cupo Global (máx. 100 activas)    │
│ 3. Clona Sucursal + Servicios + Clientes      │
│ 4. Genera emails únicos por instancia (hash)  │
│ 5. Pre-mintea 4 JWTs (Owner, Admin, Prof, Caja│
└───────────────────────────────────────────────┘
   │
   ▼ Retorna { sucursalId, expiraEn, tokens }
Frontend:
   └── Selector de Rol inmediato (Owner / Admin / Profesional / Caja)
       sin requerir contraseña.
```

---

## Ciclo de Vida y Teardown Ordenado

### DemoCleanupBackgroundService
Un `BackgroundService` registrado en `Program.cs` se ejecuta periódicamente (cada 5 minutos) y busca todas las sucursales donde `EsDemo == true` y `DemoExpiraEn < DateTime.UtcNow`.

### Orden de Eliminación (DeleteBehavior.Restrict)
Dado que el dominio utiliza `DeleteBehavior.Restrict` para prevenir errores de cascada accidental en PostgreSQL, el borrado debe realizarse en estricto orden topológico inverso:

1. `PagosTurno`
2. `MovimientosCaja`
3. `Turnos`
4. `UsuarioServicios`, `UsuariosPermisos`, `HorariosLaborales`
5. `UsuarioSucursalRoles`
6. `Clientes`
7. `Servicios`
8. `CajaSesiones`, `Cajas`
9. `ConfiguracionSucursal`
10. `Usuarios` (solo los correspondientes a la instancia efímera)
11. `Sucursal`

Esta lógica está encapsulada en `DemoTenantService.TeardownAsync(sucursalId)` para garantizar que tanto el worker en segundo plano como los tests unitarios e integrados puedan reutilizarla.

---

## Reglas de Seguridad y Anti-Abuso

1. **Emails únicos por instancia:** La entidad `Usuario` exige unicidad global en `Email`. El seeder efímero genera sufijos derivados del `SucursalId` (ej. `adrian.sosa+a1b2c3@zenmassage.com`).
2. **Supresión de emails salientes:** `RecordatoriosEmailBackgroundService` y cualquier despacho vía Resend ignora automáticamente las sucursales con `EsDemo = true`, garantizando que ninguna acción en modo demo envíe correos electrónicos reales.
3. **Rate Limiting:**
   - Máximo **3 instancias demo activas por IP**.
   - Cupo global máximo de **100 instancias demo activas simultáneas**.
   - Ante excesos, responde `429 Too Many Requests`.

