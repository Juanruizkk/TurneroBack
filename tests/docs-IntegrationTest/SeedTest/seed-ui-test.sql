-- ============================================================
-- SEED: Datos de prueba para UI — TurneroAPP
-- Ejecutar contra la DB de desarrollo (puerto 5433)
-- Fecha: 2026-04-10
-- Passwords: Admin123!
-- ============================================================

-- ─── LIMPIEZA (des-comentar para resetear) ──────────────────
-- DELETE FROM sucursal_suscripciones
--   WHERE "SucursalId" IN (
--     'a1111111-0000-0000-0000-000000000001',
--     'a2222222-0000-0000-0000-000000000001');
-- DELETE FROM configuraciones_sucursal
--   WHERE "SucursalId" IN (
--     'a1111111-0000-0000-0000-000000000001',
--     'a2222222-0000-0000-0000-000000000001');
-- DELETE FROM usuario_sucursal_roles
--   WHERE "SucursalId" IN (
--     'a1111111-0000-0000-0000-000000000001',
--     'a2222222-0000-0000-0000-000000000001');
-- DELETE FROM turnos
--   WHERE "SucursalId" IN (
--     'a1111111-0000-0000-0000-000000000001',
--     'a2222222-0000-0000-0000-000000000001');
-- DELETE FROM clientes
--   WHERE "SucursalId" IN (
--     'a1111111-0000-0000-0000-000000000001',
--     'a2222222-0000-0000-0000-000000000001');
-- DELETE FROM servicios
--   WHERE "SucursalId" IN (
--     'a1111111-0000-0000-0000-000000000001',
--     'a2222222-0000-0000-0000-000000000001');
-- DELETE FROM sucursales
--   WHERE "Id" IN (
--     'a1111111-0000-0000-0000-000000000001',
--     'a2222222-0000-0000-0000-000000000001');
-- DELETE FROM usuarios
--   WHERE "Id" IN (
--     'd1111111-0000-0000-0000-000000000001',
--     'd2222222-0000-0000-0000-000000000001');
-- DELETE FROM planes
--   WHERE "Id" IN (
--     'b1111111-0000-0000-0000-000000000001',
--     'b2222222-0000-0000-0000-000000000001');


-- ════════════════════════════════════════════════════════════
-- VERIFICAR NOMBRES DE TABLA (ejecutar si hay dudas)
-- ════════════════════════════════════════════════════════════
-- SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY tablename;


-- ════════════════════════════════════════════════════════════
-- 1. PLANES
-- ════════════════════════════════════════════════════════════

INSERT INTO planes (
    "Id", "Nombre", "Descripcion",
    "MaxUsuarios", "MaxTurnosMes", "MaxServicios",
    "PrecioMensual", "EsActivo", "OrdenDisplay",
    "DuracionTrialDias", "TieneReportes"
) VALUES
    -- Plan A: límites generosos — el problema es el estado vencido
    (
        'b1111111-0000-0000-0000-000000000001',
        'Profesional',
        'Agenda ilimitada, hasta 5 profesionales y 20 servicios.',
        5, 200, 20,
        15000.00, true, 2, 14, true
    ),
    -- Plan B: límites bajos para disparar alertas en la UI
    (
        'b2222222-0000-0000-0000-000000000001',
        'Starter',
        'Ideal para comenzar. Hasta 2 profesionales, 3 servicios y 10 turnos/mes.',
        2, 10, 3,
        4500.00, true, 1, 14, false
    )
ON CONFLICT ("Id") DO NOTHING;


-- ════════════════════════════════════════════════════════════
-- 2. USUARIOS (owners)
-- ════════════════════════════════════════════════════════════
-- NOTA: PasswordHash es BCrypt de "Admin123!" con cost=11
-- Si el sistema rechaza este hash, regenerarlo con:
--   BCrypt.Net.BCrypt.HashPassword("Admin123!")

INSERT INTO usuarios (
    "Id", "Nombre", "Apellido", "Email", "Telefono",
    "PasswordHash", "FechaCreacion"
) VALUES
    (
        'd1111111-0000-0000-0000-000000000001',
        'Laura', 'Gómez',
        'laura.vencida@test.com', '1155550001',
        '$2a$11$K7nJBDIoKvqMI8YKHO.XzeGpU0.aRlOJ0lRFXLHlbqJ7NVMW3QFNG',
        NOW()
    ),
    (
        'd2222222-0000-0000-0000-000000000001',
        'Marcos', 'Torres',
        'marcos.limites@test.com', '1155550002',
        '$2a$11$K7nJBDIoKvqMI8YKHO.XzeGpU0.aRlOJ0lRFXLHlbqJ7NVMW3QFNG',
        NOW()
    )
ON CONFLICT ("Id") DO NOTHING;


-- ════════════════════════════════════════════════════════════
-- 3. SUCURSALES
-- ════════════════════════════════════════════════════════════

INSERT INTO sucursales (
    "Id", "Nombre", "Slug", "Slogan", "Moneda",
    "Rubros", "Direccion", "Telefono", "Email",
    "Activa", "FechaCreacion"
) VALUES
    (
        'a1111111-0000-0000-0000-000000000001',
        'Estética La Perla (Plan Vencido)',
        'estetica-la-perla-test',
        'Tu belleza, nuestra pasión',
        'ARS',
        '["Estética", "Uñas"]'::jsonb,
        'Av. Corrientes 1234, CABA',
        '1144440001',
        'laperla@test.com',
        true,
        NOW()
    ),
    (
        'a2222222-0000-0000-0000-000000000001',
        'Peluquería Torres (Plan Starter)',
        'peluqueria-torres-test',
        'Cortes con estilo',
        'ARS',
        '["Peluquería"]'::jsonb,
        'Av. Santa Fe 567, CABA',
        '1144440002',
        'torres@test.com',
        true,
        NOW()
    )
ON CONFLICT ("Id") DO NOTHING;


-- ════════════════════════════════════════════════════════════
-- 4. SUSCRIPCIONES
-- ════════════════════════════════════════════════════════════
-- EstadoSuscripcion: Activa=1, Vencida=2

INSERT INTO sucursal_suscripciones (
    "Id", "SucursalId", "PlanId",
    "Estado", "FechaInicio", "FechaVencimiento", "EnPeriodoPrueba"
) VALUES
    -- Sucursal A: venció hace 15 días → ver banners de bloqueo
    (
        'c1111111-0000-0000-0000-000000000001',
        'a1111111-0000-0000-0000-000000000001',
        'b1111111-0000-0000-0000-000000000001',
        2,
        NOW() - INTERVAL '45 days',
        NOW() - INTERVAL '15 days',
        false
    ),
    -- Sucursal B: activa, vence en 20 días → ver alertas de cuota
    (
        'c2222222-0000-0000-0000-000000000001',
        'a2222222-0000-0000-0000-000000000001',
        'b2222222-0000-0000-0000-000000000001',
        1,
        NOW() - INTERVAL '10 days',
        NOW() + INTERVAL '20 days',
        false
    )
ON CONFLICT ("Id") DO NOTHING;


-- ════════════════════════════════════════════════════════════
-- 5. USUARIO_SUCURSAL_ROLES (owners)
-- ════════════════════════════════════════════════════════════
-- RolUsuario: Administrativo=0 | EstadoUsuario: Activo=1

INSERT INTO usuario_sucursal_roles (
    "Id", "UsuarioId", "SucursalId",
    "Rol", "Estado", "EsOwner", "FechaCreacion"
) VALUES
    (
        'e1111111-0000-0000-0000-000000000001',
        'd1111111-0000-0000-0000-000000000001',
        'a1111111-0000-0000-0000-000000000001',
        0, 1, true, NOW()
    ),
    (
        'e2222222-0000-0000-0000-000000000001',
        'd2222222-0000-0000-0000-000000000001',
        'a2222222-0000-0000-0000-000000000001',
        0, 1, true, NOW()
    )
ON CONFLICT ("Id") DO NOTHING;


-- ════════════════════════════════════════════════════════════
-- 6. CONFIGURACIONES_SUCURSAL
-- ════════════════════════════════════════════════════════════
-- ModoConfirmacionTurnos: Automatico=0
-- Columna JSON: "HorariosAtencion" (sin el sufijo Json)

INSERT INTO configuraciones_sucursal (
    "Id", "SucursalId",
    "MinutosAnticipacionMinimaReserva", "DiasMaximosReservaFutura",
    "MinutosBufferEntreTurnos", "PermiteCancelacionCliente",
    "HorasMinimasCancelacion", "EnviaRecordatorioEmail",
    "EnviaRecordatorioWhatsapp", "ModoConfirmacionTurnos",
    "HorariosAtencion"
) VALUES
    (
        'f1111111-0000-0000-0000-000000000001',
        'a1111111-0000-0000-0000-000000000001',
        60, 30, 0, true, 24, false, false, 0,
        '{
          "Lunes":     {"Abierto": true,  "Franjas": [{"Apertura": "09:00", "Cierre": "18:00"}]},
          "Martes":    {"Abierto": true,  "Franjas": [{"Apertura": "09:00", "Cierre": "18:00"}]},
          "Miercoles": {"Abierto": true,  "Franjas": [{"Apertura": "09:00", "Cierre": "18:00"}]},
          "Jueves":    {"Abierto": true,  "Franjas": [{"Apertura": "09:00", "Cierre": "18:00"}]},
          "Viernes":   {"Abierto": true,  "Franjas": [{"Apertura": "09:00", "Cierre": "18:00"}]},
          "Sabado":    {"Abierto": true,  "Franjas": [{"Apertura": "09:00", "Cierre": "13:00"}]},
          "Domingo":   {"Abierto": false, "Franjas": []}
        }'::jsonb
    ),
    (
        'f2222222-0000-0000-0000-000000000001',
        'a2222222-0000-0000-0000-000000000001',
        60, 30, 0, true, 24, false, false, 0,
        '{
          "Lunes":     {"Abierto": true,  "Franjas": [{"Apertura": "09:00", "Cierre": "18:00"}]},
          "Martes":    {"Abierto": true,  "Franjas": [{"Apertura": "09:00", "Cierre": "18:00"}]},
          "Miercoles": {"Abierto": true,  "Franjas": [{"Apertura": "09:00", "Cierre": "18:00"}]},
          "Jueves":    {"Abierto": true,  "Franjas": [{"Apertura": "09:00", "Cierre": "18:00"}]},
          "Viernes":   {"Abierto": true,  "Franjas": [{"Apertura": "09:00", "Cierre": "18:00"}]},
          "Sabado":    {"Abierto": false, "Franjas": []},
          "Domingo":   {"Abierto": false, "Franjas": []}
        }'::jsonb
    )
ON CONFLICT ("Id") DO NOTHING;


-- ════════════════════════════════════════════════════════════
-- 7. SERVICIOS — solo Sucursal B (al tope: MaxServicios=3)
-- ════════════════════════════════════════════════════════════
-- ModalidadPrestacion: Presencial=0 | TipoServicio: Regular=0

INSERT INTO servicios (
    "Id", "SucursalId",
    "Nombre", "Descripcion", "Aclaraciones",
    "Precio", "DuracionClienteMin", "DuracionAgendaMin",
    "RequiereSena", "Activo",
    "ModalidadPrestacion", "TipoServicio"
) VALUES
    (
        'srv1-2222-0000-0000-000000000001',
        'a2222222-0000-0000-0000-000000000001',
        'Corte de cabello', '', '', 3500.00, 30, 30, false, true, 0, 0
    ),
    (
        'srv2-2222-0000-0000-000000000002',
        'a2222222-0000-0000-0000-000000000001',
        'Coloración completa', '', '', 8000.00, 90, 90, true, true, 0, 0
    ),
    (
        'srv3-2222-0000-0000-000000000003',
        'a2222222-0000-0000-0000-000000000001',
        'Peinado', '', '', 2500.00, 45, 45, false, true, 0, 0
    )
    -- TOPE ALCANZADO: MaxServicios=3
ON CONFLICT ("Id") DO NOTHING;


-- ════════════════════════════════════════════════════════════
-- 8. TURNOS — Sucursal B: 4 de 10 usados este mes
--    (para ver la barra de progreso del límite mensual)
-- ════════════════════════════════════════════════════════════

-- Cliente mínimo requerido para FK de turnos
INSERT INTO clientes (
    "Id", "SucursalId", "Nombre", "Apellido", "Telefono"
) VALUES (
    'cli1-2222-0000-0000-000000000001',
    'a2222222-0000-0000-0000-000000000001',
    'Ana', 'García', '1166660001'
)
ON CONFLICT ("Id") DO NOTHING;

-- EstadoTurno: Confirmado=1 | OrigenTurno: Administrativo=0
-- ProfesionalId apunta a usuarios.Id del owner (d2222222...)
-- NOTA: si FK de ProfesionalId apunta a usuario_sucursal_roles.Id en lugar de
--       usuarios.Id, cambiar ProfesionalId a 'e2222222-0000-0000-0000-000000000001'

INSERT INTO turnos (
    "Id", "SucursalId", "ClienteId", "ServicioId", "ProfesionalId",
    "FechaHoraInicio", "FechaHoraFin",
    "Estado", "Origen", "TokenGestion",
    "PrecioServicio", "TotalCobrado", "FechaCreacion"
) VALUES
    (
        'trn1-2222-0000-0000-000000000001',
        'a2222222-0000-0000-0000-000000000001',
        'cli1-2222-0000-0000-000000000001',
        'srv1-2222-0000-0000-000000000001',
        'd2222222-0000-0000-0000-000000000001',
        DATE_TRUNC('month', NOW()) + INTERVAL '1 day 10:00:00',
        DATE_TRUNC('month', NOW()) + INTERVAL '1 day 10:30:00',
        1, 0, 'token-test-0001', 3500.00, 3500.00, NOW()
    ),
    (
        'trn2-2222-0000-0000-000000000002',
        'a2222222-0000-0000-0000-000000000001',
        'cli1-2222-0000-0000-000000000001',
        'srv1-2222-0000-0000-000000000001',
        'd2222222-0000-0000-0000-000000000001',
        DATE_TRUNC('month', NOW()) + INTERVAL '2 day 11:00:00',
        DATE_TRUNC('month', NOW()) + INTERVAL '2 day 11:30:00',
        1, 0, 'token-test-0002', 3500.00, 3500.00, NOW()
    ),
    (
        'trn3-2222-0000-0000-000000000003',
        'a2222222-0000-0000-0000-000000000001',
        'cli1-2222-0000-0000-000000000001',
        'srv1-2222-0000-0000-000000000001',
        'd2222222-0000-0000-0000-000000000001',
        DATE_TRUNC('month', NOW()) + INTERVAL '3 day 09:00:00',
        DATE_TRUNC('month', NOW()) + INTERVAL '3 day 09:30:00',
        1, 0, 'token-test-0003', 3500.00, 0.00, NOW()
    ),
    (
        'trn4-2222-0000-0000-000000000004',
        'a2222222-0000-0000-0000-000000000001',
        'cli1-2222-0000-0000-000000000001',
        'srv1-2222-0000-0000-000000000001',
        'd2222222-0000-0000-0000-000000000001',
        DATE_TRUNC('month', NOW()) + INTERVAL '4 day 15:00:00',
        DATE_TRUNC('month', NOW()) + INTERVAL '4 day 15:30:00',
        1, 0, 'token-test-0004', 3500.00, 0.00, NOW()
    )
ON CONFLICT ("Id") DO NOTHING;


-- ════════════════════════════════════════════════════════════
-- VERIFICACIÓN FINAL
-- ════════════════════════════════════════════════════════════

SELECT
    s."Nombre"               AS sucursal,
    p."Nombre"               AS plan,
    p."MaxTurnosMes"         AS max_turnos,
    p."MaxServicios"         AS max_servicios,
    p."MaxUsuarios"          AS max_usuarios,
    sus."Estado"             AS estado_suscripcion,
    sus."FechaVencimiento"   AS vence
FROM sucursales s
JOIN sucursal_suscripciones sus ON sus."SucursalId" = s."Id"
JOIN planes p ON p."Id" = sus."PlanId"
WHERE s."Id" IN (
    'a1111111-0000-0000-0000-000000000001',
    'a2222222-0000-0000-0000-000000000001'
);
