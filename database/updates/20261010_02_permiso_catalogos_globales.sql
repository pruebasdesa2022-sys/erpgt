-- =====================================================================
-- ERP Modular - Actualizacion 0.2.0 -> 0.2.1 : permiso de catalogos globales
-- =====================================================================
-- Para bases YA instaladas con la version 0.2.0. Equivale a lo que cambia
-- database.sql 0.2.1 respecto de la 0.2.0; en una instalacion limpia NO se ejecuta.
--
-- Se ejecuta UNA sola vez, con un usuario con privilegios de DDL (no el de la app):
--   mysql -u <usuario> -p --default-character-set=utf8mb4 -e "source database/updates/20261010_02_permiso_catalogos_globales.sql"
--
-- Que hace:
--   * Crea el permiso CATALOGOS_GLOBALES_EDITAR y lo concede SOLO al rol ADMINISTRADOR.
--     Las monedas y los tipos de documento son catalogos globales (los comparten todas
--     las empresas): alterarlos desde una empresa afectaria a las demas.
--   * Retira los permisos CATALOGOS_MONEDAS_CREAR / _EDITAR / _ELIMINAR y
--     CATALOGOS_TIPOSDOCUMENTO_EDITAR, que el nuevo permiso reemplaza, junto con sus
--     asignaciones a roles y a usuarios.
--
-- La primera instruccion registra el script en esquema_version: si ya se aplico,
-- falla por clave duplicada y no se ejecuta nada mas. No cambia tablas, por lo que no
-- requiere privilegios nuevos para el usuario de la aplicacion.
-- =====================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_0900_ai_ci;

USE erpgt;

INSERT INTO esquema_version (version, script, aplicado_por)
VALUES ('0.2.1', '20261010_02_permiso_catalogos_globales.sql', CURRENT_USER());

START TRANSACTION;

INSERT INTO permiso (modulo_id, codigo, recurso, accion, nombre)
SELECT m.id, 'CATALOGOS_GLOBALES_EDITAR', 'GLOBALES', 'EDITAR',
       'Administrar los catálogos globales (monedas y tipos de documento)'
FROM modulo m
WHERE m.codigo = 'CATALOGOS';

-- Retiro de los permisos que el nuevo reemplaza (primero sus asignaciones).
DELETE rp FROM rol_permiso rp
JOIN permiso p ON p.id = rp.permiso_id
WHERE p.codigo IN ('CATALOGOS_MONEDAS_CREAR', 'CATALOGOS_MONEDAS_EDITAR', 'CATALOGOS_MONEDAS_ELIMINAR',
                   'CATALOGOS_TIPOSDOCUMENTO_EDITAR');

DELETE up FROM usuario_permiso up
JOIN permiso p ON p.id = up.permiso_id
WHERE p.codigo IN ('CATALOGOS_MONEDAS_CREAR', 'CATALOGOS_MONEDAS_EDITAR', 'CATALOGOS_MONEDAS_ELIMINAR',
                   'CATALOGOS_TIPOSDOCUMENTO_EDITAR');

DELETE FROM permiso
WHERE codigo IN ('CATALOGOS_MONEDAS_CREAR', 'CATALOGOS_MONEDAS_EDITAR', 'CATALOGOS_MONEDAS_ELIMINAR',
                 'CATALOGOS_TIPOSDOCUMENTO_EDITAR');

-- Solo el administrador lo recibe; cualquier otro rol debe pedirlo de forma explicita.
INSERT INTO rol_permiso (rol_id, permiso_id)
SELECT r.id, p.id
FROM rol r
JOIN permiso p ON p.codigo = 'CATALOGOS_GLOBALES_EDITAR'
WHERE r.codigo = 'ADMINISTRADOR';

-- Invalida la cache de autorizacion de las instancias en marcha.
UPDATE seguridad_version SET version = version + 1, modificado_en = UTC_TIMESTAMP(6) WHERE id = 1;

COMMIT;

-- =====================================================================
-- Fin de la actualizacion 0.2.1
-- =====================================================================
