# Recordatorios de turno por email

Sistema automático que envía recordatorios por email a los clientes antes de sus turnos.

---

## Índice

1. [Resumen](#1-resumen)
2. [Cambios en la base de datos](#2-cambios-en-la-base-de-datos)
3. [Flujo de ejecución](#3-flujo-de-ejecución)
4. [Reglas de negocio](#4-reglas-de-negocio)
5. [Archivos involucrados](#5-archivos-involucrados)
6. [Configuración](#6-configuración)
7. [Comportamiento en desarrollo](#7-comportamiento-en-desarrollo)

---

## 1. Resumen

Dos recordatorios automáticos, enviados en ventanas de tiempo distintas antes del turno:

| Tipo    | Ventana                         | Asunto                                              |
|---------|---------------------------------|-----------------------------------------------------|
| **24 h**| Turno entre *ahora* y *+24 h*   | `Recordatorio: tu turno en {sucursal} es mañana`   |
| **3 h** | Turno entre *ahora* y *+3 h*    | `Recordatorio: tu turno en {sucursal} es en 3 horas`|

Cada turno solo recibe cada recordatorio **una vez**, controlado por flags en la base de datos. El servicio se ejecuta cada **15 minutos** en segundo plano.

---

## 2. Cambios en la base de datos

### Migración: `AddRecordatoriosEmailFlags`

Se agregaron dos columnas booleanas a la tabla `turnos`:

| Columna                   | Tipo      | Default | Descripción                                 |
|---------------------------|-----------|---------|---------------------------------------------|
| `Recordatorio24hEnviado`  | `boolean` | `false` | Marca si ya se envió el recordatorio de 24 h|
| `Recordatorio3hEnviado`   | `boolean` | `false` | Marca si ya se envió el recordatorio de 3 h |

Ambas columnas tienen `NOT NULL DEFAULT false`, por lo que los turnos existentes quedan en `false` automáticamente y recibirán sus recordatorios en el próximo ciclo si corresponde.

---

## 3. Flujo de ejecución

```
Cada 15 minutos
│
├─ Consulta DB: turnos Confirmado/Pendiente con inicio futuro
│   donde (Recordatorio24hEnviado = false AND inicio ≤ ahora+24h)
│      OR (Recordatorio3hEnviado  = false AND inicio ≤ ahora+3h)
│
├─ Por cada turno encontrado:
│   │
│   ├─ Sin email de cliente → saltar
│   │
│   ├─ ¿Inicio ≤ ahora+3h AND Recordatorio3hEnviado = false?
│   │   ├─ Enviar email "en 3 horas"
│   │   ├─ Marcar Recordatorio3hEnviado  = true
│   │   ├─ Marcar Recordatorio24hEnviado = true  ← evita email doble
│   │   └─ Pasar al siguiente turno (skip check de 24 h)
│   │
│   └─ ¿Inicio ≤ ahora+24h AND Recordatorio24hEnviado = false?
│       ├─ Enviar email "mañana"
│       └─ Marcar Recordatorio24hEnviado = true
│
└─ Guardar cambios en DB (UpdateRange + SaveChanges)
```

---

## 4. Reglas de negocio

### Estados contemplados
Solo se procesan turnos en estado `Confirmado` o `Pendiente`. Los cancelados, completados y NoShow se excluyen de la query.

### Clientes sin email
Si `Cliente.Email` es nulo o vacío, el turno se omite silenciosamente. No se lanza error.

### Prioridad 3 h sobre 24 h
Si un turno cae simultáneamente en ambas ventanas (por ejemplo, fue creado con menos de 3 h de antelación), solo se envía el recordatorio de **3 h** y se marcan **ambos** flags como `true`. Esto evita que el cliente reciba dos emails en el mismo ciclo o en el siguiente.

Ejemplo de escenario:

```
Turno creado a las 11:30 para las 13:00 del mismo día.
Ciclo de las 11:45:
  - límite24h = 11:45 + 24h → turno dentro ✓
  - límite3h  = 11:45 + 3h  → turno dentro ✓
  → Solo se envía el de 3 h. Ambos flags → true.
```

### Idempotencia
Los flags garantizan que aunque el servicio reinicie o falle a mitad de ciclo, un turno no recibe el mismo recordatorio dos veces. Si el email se envió pero el `SaveChanges` falló, el próximo ciclo reintentará el envío. Se acepta este comportamiento (at-least-once) a cambio de simplicidad.

### Aislamiento de errores
Un fallo al enviar el email de un turno específico (SMTP caído, timeout, etc.) se loguea como error pero **no cancela el resto del lote**. El flag de ese turno no se marca, por lo que se reintentará en el próximo ciclo.

---

## 5. Archivos involucrados

| Capa           | Archivo                                                             | Cambio                                              |
|----------------|---------------------------------------------------------------------|-----------------------------------------------------|
| Domain         | `Domain/Entities/Turno.cs`                                          | +2 props bool                                       |
| Infrastructure | `Infrastructure/Data/Configurations/TurnoConfiguration.cs`          | Mapeo de columnas con `HasDefaultValue(false)`      |
| Infrastructure | `Infrastructure/Repositories/TurnoRepository.cs`                    | `GetParaRecordatoriosAsync`                         |
| Infrastructure | `Infrastructure/Services/EmailService.cs`                           | `EnviarRecordatorioAsync` + template HTML           |
| Infrastructure | `Infrastructure/Services/ConsoleEmailService.cs`                    | Stub de desarrollo                                  |
| Application    | `Application/Interfaces/ITurnoRepository.cs`                        | Firma de `GetParaRecordatoriosAsync`                |
| Application    | `Application/Interfaces/IEmailService.cs`                           | Firma de `EnviarRecordatorioAsync`                  |
| Api            | `Api/Services/RecordatoriosEmailBackgroundService.cs`               | Nuevo BackgroundService                             |
| Api            | `Api/Program.cs`                                                    | `AddHostedService<RecordatoriosEmailBackgroundService>` |
| Migrations     | `Infrastructure/Migrations/…AddRecordatoriosEmailFlags`             | Nueva migración                                     |

---

## 6. Configuración

No requiere configuración adicional. Usa los mismos valores SMTP ya definidos en `appsettings`:

```json
{
  "Smtp": {
    "Host": "smtp.ejemplo.com",
    "Port": 587,
    "Username": "no-reply@salon.com",
    "Password": "...",
    "FromEmail": "no-reply@salon.com",
    "FromName": "Salón Beleza"
  },
  "App": {
    "FrontendUrl": "https://turneroapp.com"
  }
}
```

`FrontendUrl` se usa para construir el enlace de cancelación incluido en el email:

```
{FrontendUrl}/turno/{TokenGestion}
```

### Cambiar el intervalo de ejecución

El intervalo está definido como constante en `RecordatoriosEmailBackgroundService.cs`:

```csharp
private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(15);
```

Para modificarlo basta con cambiar ese valor y rebuild.

---

## 7. Comportamiento en desarrollo

Cuando `Smtp:Username` no está configurado, el sistema registra automáticamente `ConsoleEmailService` en lugar de `EmailService`. Los recordatorios se imprimen en la consola con este formato:

```
╔══════════════════════════════════════════════════════╗
║      [DEV] EMAIL RECORDATORIO 24 H                   ║
╠══════════════════════════════════════════════════════╣
║  Para:        cliente@email.com
║  Cliente:     Laura Martínez
║  Sucursal:    Salón Beleza
║  Servicio:    Corte de pelo
║  Profesional: Ana Rodríguez
║  Inicio:      17/03/2026 10:00
║  Fin:         10:50
║  Cancelar:    http://localhost:5173/turno/a3f8b2e1...
╚══════════════════════════════════════════════════════╝
```

El nivel de log es `Warning` para que sea visible incluso con la configuración por defecto de `appsettings.Development.json`.

---

*Documentación generada el 2026-03-16.*
