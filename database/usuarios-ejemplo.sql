-- =====================================================================
-- ERP Modular - Usuario MySQL de la aplicacion (PLANTILLA)
-- =====================================================================
-- La aplicacion NO debe conectarse como root. Este script crea un usuario
-- que solo puede leer y escribir datos (sin DDL) y que NO puede modificar
-- ni borrar las tablas inmutables (auditoria, historiales).
--
-- Como usarlo:
--   1. Copie este archivo fuera del repositorio (no versione contrasenas).
--   2. Reemplace <CONTRASENA_SEGURA> por una contrasena real.
--   3. Ejecutelo con un usuario administrador, despues de database.sql:
--        mysql -u root -p -e "source ruta/usuarios.sql"
--   4. Configure la cadena de conexion de la API con este usuario:
--        dotnet user-secrets set "ConnectionStrings:Erp" "Server=localhost;Port=3306;Database=erpgt;User ID=erpgt_app;Password=..." --project backend/src/ERP.Api
--
-- Los privilegios se otorgan TABLA POR TABLA: MySQL no permite quitarle una
-- tabla a un GRANT hecho sobre toda la base. Cada script de database/updates/
-- que cree tablas debe incluir sus GRANT correspondientes.
-- =====================================================================

CREATE USER IF NOT EXISTS 'erpgt_app'@'localhost' IDENTIFIED BY '<CONTRASENA_SEGURA>';

-- ----- Tablas editables: los registros de negocio nunca se borran, se desactivan -----
GRANT SELECT, INSERT, UPDATE ON erpgt.configuracion      TO 'erpgt_app'@'localhost';
GRANT SELECT, INSERT, UPDATE ON erpgt.empresa            TO 'erpgt_app'@'localhost';
GRANT SELECT, INSERT, UPDATE ON erpgt.sucursal           TO 'erpgt_app'@'localhost';
GRANT SELECT, INSERT, UPDATE ON erpgt.politica_password  TO 'erpgt_app'@'localhost';
GRANT SELECT, INSERT, UPDATE ON erpgt.usuario            TO 'erpgt_app'@'localhost';
GRANT SELECT, INSERT, UPDATE ON erpgt.token_recuperacion TO 'erpgt_app'@'localhost';
GRANT SELECT, INSERT, UPDATE ON erpgt.refresh_token      TO 'erpgt_app'@'localhost';
GRANT SELECT, INSERT, UPDATE ON erpgt.rol                TO 'erpgt_app'@'localhost';
GRANT SELECT, INSERT, UPDATE ON erpgt.modulo             TO 'erpgt_app'@'localhost';
GRANT SELECT, INSERT, UPDATE ON erpgt.modulo_empresa     TO 'erpgt_app'@'localhost';

-- ----- Asignaciones: la aplicacion las reemplaza (borra e inserta) al guardar -----
GRANT SELECT, INSERT, DELETE ON erpgt.rol_permiso      TO 'erpgt_app'@'localhost';
GRANT SELECT, INSERT, DELETE ON erpgt.usuario_rol      TO 'erpgt_app'@'localhost';
GRANT SELECT, INSERT, DELETE ON erpgt.usuario_permiso  TO 'erpgt_app'@'localhost';
GRANT SELECT, INSERT, DELETE ON erpgt.usuario_empresa  TO 'erpgt_app'@'localhost';
GRANT SELECT, INSERT, DELETE ON erpgt.usuario_sucursal TO 'erpgt_app'@'localhost';

-- Contador de la cache de autorizacion: solo se lee y se incrementa.
GRANT SELECT, UPDATE ON erpgt.seguridad_version TO 'erpgt_app'@'localhost';

-- ----- Catalogos que solo cambian por script: solo lectura -----
GRANT SELECT ON erpgt.permiso         TO 'erpgt_app'@'localhost';
GRANT SELECT ON erpgt.menu            TO 'erpgt_app'@'localhost';
GRANT SELECT ON erpgt.esquema_version TO 'erpgt_app'@'localhost';

-- ----- Tablas INMUTABLES: solo insercion y lectura (sin UPDATE ni DELETE) -----
GRANT SELECT, INSERT ON erpgt.auditoria          TO 'erpgt_app'@'localhost';
GRANT SELECT, INSERT ON erpgt.auditoria_acceso   TO 'erpgt_app'@'localhost';
GRANT SELECT, INSERT ON erpgt.intento_login      TO 'erpgt_app'@'localhost';
GRANT SELECT, INSERT ON erpgt.password_historial TO 'erpgt_app'@'localhost';

-- Fase 0 no tiene procedimientos almacenados. Cuando existan (etapa 1A):
--   GRANT EXECUTE ON PROCEDURE erpgt.sp_siguiente_correlativo TO 'erpgt_app'@'localhost';
