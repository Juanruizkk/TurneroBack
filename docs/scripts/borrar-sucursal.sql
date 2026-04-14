-- =============================================================================
-- SCRIPT: Borrar una sucursal y todos sus datos relacionados
-- =============================================================================
-- USO: Reemplazá el valor de sucursal_id con el ID de la sucursal a eliminar.
--
-- ADVERTENCIA: Esta operación es IRREVERSIBLE. Hacer un backup antes de ejecutar
-- en cualquier entorno que no sea local.
--
-- Puerto local Docker: 5433
-- Conectar con: psql -h localhost -p 5433 -U postgres -d turnero
-- =============================================================================
DO $$
DECLARE
    v_sucursal_id UUID := '67a517a3-4177-4a59-8fbe-17f34e5ccd13';
BEGIN
    RAISE NOTICE 'Iniciando borrado de sucursal %', v_sucursal_id;

    IF NOT EXISTS (
        SELECT 1
        FROM sucursales
        WHERE "Id" = v_sucursal_id
    ) THEN
        RAISE EXCEPTION 'No existe la sucursal con Id %', v_sucursal_id;
    END IF;

    -- 1. Adjuntos de turno
    DELETE FROM adjuntos_turno
    WHERE "TurnoId" IN (
        SELECT "Id"
        FROM turnos
        WHERE "SucursalId" = v_sucursal_id
    );
    RAISE NOTICE '  adjuntos_turno eliminados';

    -- 2. Comentarios de turno
    DELETE FROM comentarios_turno
    WHERE "TurnoId" IN (
        SELECT "Id"
        FROM turnos
        WHERE "SucursalId" = v_sucursal_id
    );
    RAISE NOTICE '  comentarios_turno eliminados';

    -- 3. Pagos de turno
    DELETE FROM pagos_turno
    WHERE "TurnoId" IN (
        SELECT "Id"
        FROM turnos
        WHERE "SucursalId" = v_sucursal_id
    );
    RAISE NOTICE '  pagos_turno eliminados';

    -- 4. Movimientos de caja
    DELETE FROM movimientos_caja
    WHERE "CajaId" IN (
        SELECT "Id"
        FROM cajas
        WHERE "SucursalId" = v_sucursal_id
    );
    RAISE NOTICE '  movimientos_caja eliminados';

    -- 5. Sesiones de caja
    DELETE FROM caja_sesiones
    WHERE "CajaId" IN (
        SELECT "Id"
        FROM cajas
        WHERE "SucursalId" = v_sucursal_id
    );
    RAISE NOTICE '  caja_sesiones eliminadas';

    -- 6. Bloqueos de agenda
    DELETE FROM bloqueos_agenda
    WHERE "SucursalId" = v_sucursal_id;
    RAISE NOTICE '  bloqueos_agenda eliminados';

    -- 7. Turnos
    DELETE FROM turnos
    WHERE "SucursalId" = v_sucursal_id;
    RAISE NOTICE '  turnos eliminados';

    -- 8. Permisos de usuario
    DELETE FROM usuario_permisos
    WHERE "UsuarioSucursalRolId" IN (
        SELECT "Id"
        FROM usuario_sucursal_roles
        WHERE "SucursalId" = v_sucursal_id
    );
    RAISE NOTICE '  usuario_permisos eliminados';

    -- 9. Horarios laborales
    DELETE FROM horarios_laborales
    WHERE "UsuarioSucursalRolId" IN (
        SELECT "Id"
        FROM usuario_sucursal_roles
        WHERE "SucursalId" = v_sucursal_id
    );
    RAISE NOTICE '  horarios_laborales eliminados';

    -- 10. Servicios por usuario
    DELETE FROM usuario_servicios
    WHERE "UsuarioSucursalRolId" IN (
        SELECT "Id"
        FROM usuario_sucursal_roles
        WHERE "SucursalId" = v_sucursal_id
    );
    RAISE NOTICE '  usuario_servicios eliminados';

    -- 11. Membresías de usuario
    DELETE FROM usuario_sucursal_roles
    WHERE "SucursalId" = v_sucursal_id;
    RAISE NOTICE '  usuario_sucursal_roles eliminados';

    -- 12. Servicios
    DELETE FROM servicios
    WHERE "SucursalId" = v_sucursal_id;
    RAISE NOTICE '  servicios eliminados';

    -- 13. Clientes
    DELETE FROM clientes
    WHERE "SucursalId" = v_sucursal_id;
    RAISE NOTICE '  clientes eliminados';

    -- 14. Cajas
    DELETE FROM cajas
    WHERE "SucursalId" = v_sucursal_id;
    RAISE NOTICE '  cajas eliminadas';

    -- 15. Configuración
    DELETE FROM configuraciones_sucursal
    WHERE "SucursalId" = v_sucursal_id;
    RAISE NOTICE '  configuraciones_sucursal eliminadas';

    -- 16. Suscripción
    DELETE FROM sucursal_suscripciones
    WHERE "SucursalId" = v_sucursal_id;
    RAISE NOTICE '  sucursal_suscripciones eliminadas';

    -- 17. Sucursal
    DELETE FROM sucursales
    WHERE "Id" = v_sucursal_id;
    RAISE NOTICE '  sucursal eliminada';

    -- 18. Usuarios sin membresías en ninguna sucursal (huérfanos)
    --     Aplica al owner si no pertenece a ninguna otra sucursal.
    DELETE FROM usuarios u
    WHERE NOT EXISTS (
        SELECT 1 FROM usuario_sucursal_roles usr WHERE usr."UsuarioId" = u."Id"
    );
    RAISE NOTICE '  usuarios huerfanos eliminados';

    RAISE NOTICE 'Borrado de sucursal % completado.', v_sucursal_id;
END $$;
