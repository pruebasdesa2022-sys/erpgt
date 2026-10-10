-- =====================================================================
-- ERP Modular (Guatemala) - Script maestro de instalacion limpia
-- =====================================================================
-- FUENTE OFICIAL del esquema. Se ejecuta UNA sola vez sobre un servidor
-- donde la base `erpgt` no existe. Nunca se re-ejecuta sobre una base
-- con datos: los cambios posteriores van en database/updates/.
--
-- Version del esquema : 0.1.0 (Fase 0 - Fundamentos)
-- Motor               : MySQL 8.0.16 o superior (compatible con 8.4 LTS)
-- Ejecutar con        : un usuario con privilegios de DDL (no el de la app)
--   PowerShell : mysql -u <usuario> -p --default-character-set=utf8mb4 -e "source database/database.sql"
--   cmd / bash : mysql -u <usuario> -p --default-character-set=utf8mb4 < database/database.sql
--   (en PowerShell no usar `Get-Content ... | mysql`: altera las tildes)
--
-- Convenciones (CLAUDE.md seccion 7):
--   * snake_case en espanol, sin tildes ni enie en identificadores.
--   * PK `id BIGINT UNSIGNED AUTO_INCREMENT`; FK `<tabla>_id`, siempre con indice.
--   * Fechas/horas tecnicas en UTC, DATETIME(6). El backend fija la sesion en UTC.
--   * creado_por / modificado_por guardan el id del usuario SIN llave foranea
--     (existen en todas las tablas, incluso en las que se crean antes que
--     `usuario`). NULL = registro creado por el sistema o por este script.
--   * Estados y tipos: VARCHAR + CHECK (no ENUM), para poder ampliarlos con
--     un script incremental sin reconstruir la tabla.
--   * Baja logica con `activo`. Los codigos de registros inactivos NO se
--     reutilizan, por eso los UNIQUE son simples.
--   * Orden de secciones por dependencia de FK: una seccion no referencia
--     tablas de una seccion posterior. Las excepciones van al final, en
--     RELACIONES DIFERIDAS.
--
-- Secciones: BASE - CONFIGURACION - ORGANIZACION - SEGURIDAD - AUDITORIA -
--            VISTAS - PROCEDIMIENTOS - FUNCIONES - TRIGGERS - SEEDS -
--            RELACIONES DIFERIDAS
-- =====================================================================

-- El archivo esta en UTF-8 y los seeds llevan tildes: la conexion debe leerlo asi.
SET NAMES utf8mb4 COLLATE utf8mb4_0900_ai_ci;

-- ===== BASE =====

CREATE DATABASE erpgt
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_0900_ai_ci;

USE erpgt;

-- Registro de scripts aplicados (este y los de database/updates/).
CREATE TABLE esquema_version (
  id            BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  version       VARCHAR(20)     NOT NULL,
  script        VARCHAR(200)    NOT NULL,
  aplicado_en   DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  aplicado_por  VARCHAR(100)    NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_esquema_version_script (script)
) ENGINE=InnoDB COMMENT='Scripts de esquema aplicados a esta base';


-- ===== CONFIGURACION =====

-- Configuracion de negocio por clave. empresa_id NULL = valor global.
-- Los parametros tienen vigencia: nunca se sobrescribe un valor historico.
CREATE TABLE configuracion (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  empresa_id      BIGINT UNSIGNED NULL,
  -- UNIQUE no detecta duplicados cuando hay NULL; esta columna lo resuelve.
  empresa_clave   BIGINT UNSIGNED GENERATED ALWAYS AS (IFNULL(empresa_id, 0)) STORED,
  clave           VARCHAR(100)    NOT NULL,
  valor           VARCHAR(2000)   NOT NULL,
  tipo_dato       VARCHAR(20)     NOT NULL DEFAULT 'TEXTO',
  descripcion     VARCHAR(300)    NULL,
  vigente_desde   DATE            NOT NULL,
  vigente_hasta   DATE            NULL,
  activo          TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_configuracion_clave (empresa_clave, clave, vigente_desde),
  KEY ix_configuracion_empresa (empresa_id),
  CONSTRAINT ck_configuracion_tipo_dato
    CHECK (tipo_dato IN ('TEXTO', 'ENTERO', 'DECIMAL', 'BOOLEANO', 'FECHA', 'JSON')),
  CONSTRAINT ck_configuracion_vigencia
    CHECK (vigente_hasta IS NULL OR vigente_hasta >= vigente_desde)
) ENGINE=InnoDB COMMENT='Parametros de negocio con vigencia, globales o por empresa';

-- Contador que el backend incrementa al cambiar roles, permisos o estado de
-- modulos; invalida la cache de autorizacion. Tabla de una sola fila (id = 1),
-- por eso no usa AUTO_INCREMENT.
CREATE TABLE seguridad_version (
  id             TINYINT UNSIGNED NOT NULL,
  version        BIGINT UNSIGNED  NOT NULL DEFAULT 1,
  modificado_en  DATETIME(6)      NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (id),
  CONSTRAINT ck_seguridad_version_fila_unica CHECK (id = 1)
) ENGINE=InnoDB COMMENT='Contador de cambios de autorizacion (fila unica)';


-- ===== ORGANIZACION =====
-- Fase 0: empresa y sucursal minimas. Bodega, caja, departamento y centro de
-- costo se agregan en la etapa 1A.

CREATE TABLE empresa (
  id                BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  codigo            VARCHAR(20)     NOT NULL,
  razon_social      VARCHAR(200)    NOT NULL,
  nombre_comercial  VARCHAR(200)    NOT NULL,
  nit               VARCHAR(20)     NOT NULL,
  direccion         VARCHAR(300)    NULL,
  telefono          VARCHAR(30)     NULL,
  correo            VARCHAR(150)    NULL,
  activo            TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila      INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en         DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por        BIGINT UNSIGNED NULL,
  modificado_en     DATETIME(6)     NULL,
  modificado_por    BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_empresa_codigo (codigo)
) ENGINE=InnoDB COMMENT='Empresas (multiempresa)';

CREATE TABLE sucursal (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  empresa_id      BIGINT UNSIGNED NOT NULL,
  codigo          VARCHAR(20)     NOT NULL,
  nombre          VARCHAR(150)    NOT NULL,
  direccion       VARCHAR(300)    NULL,
  telefono        VARCHAR(30)     NULL,
  correo          VARCHAR(150)    NULL,
  es_principal    TINYINT(1)      NOT NULL DEFAULT 0,
  activo          TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  -- Tambien sirve de indice para la FK a empresa.
  UNIQUE KEY uq_sucursal_codigo (empresa_id, codigo),
  -- Listado paginado y busqueda por nombre dentro de la empresa.
  KEY ix_sucursal_empresa_nombre (empresa_id, nombre),
  CONSTRAINT fk_sucursal_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id)
) ENGINE=InnoDB COMMENT='Sucursales de cada empresa';


-- ===== SEGURIDAD =====

-- Politica de contrasenas vigente (el backend lee la fila activa).
-- expira_dias = 0 significa que la contrasena no expira.
CREATE TABLE politica_password (
  id                     BIGINT UNSIGNED   NOT NULL AUTO_INCREMENT,
  longitud_minima        TINYINT UNSIGNED  NOT NULL,
  requiere_mayuscula     TINYINT(1)        NOT NULL DEFAULT 1,
  requiere_minuscula     TINYINT(1)        NOT NULL DEFAULT 1,
  requiere_digito        TINYINT(1)        NOT NULL DEFAULT 1,
  requiere_simbolo       TINYINT(1)        NOT NULL DEFAULT 1,
  historial_cantidad     TINYINT UNSIGNED  NOT NULL,
  expira_dias            SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  max_intentos_fallidos  TINYINT UNSIGNED  NOT NULL,
  bloqueo_minutos        SMALLINT UNSIGNED NOT NULL,
  recuperacion_minutos   SMALLINT UNSIGNED NOT NULL,
  activo                 TINYINT(1)        NOT NULL DEFAULT 1,
  version_fila           INT UNSIGNED      NOT NULL DEFAULT 1,
  creado_en              DATETIME(6)       NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por             BIGINT UNSIGNED   NULL,
  modificado_en          DATETIME(6)       NULL,
  modificado_por         BIGINT UNSIGNED   NULL,
  PRIMARY KEY (id),
  CONSTRAINT ck_politica_password_longitud CHECK (longitud_minima BETWEEN 8 AND 128),
  CONSTRAINT ck_politica_password_intentos CHECK (max_intentos_fallidos >= 1),
  CONSTRAINT ck_politica_password_bloqueo CHECK (bloqueo_minutos >= 1),
  CONSTRAINT ck_politica_password_recuperacion CHECK (recuperacion_minutos >= 1)
) ENGINE=InnoDB COMMENT='Politica de complejidad, historial y bloqueo de contrasenas';

-- Sin datos laborales: en Fase 3 `empleado` referenciara a `usuario`, no al reves.
-- password_hash: formato versionado `pbkdf2-sha512$<iteraciones>$<sal b64>$<hash b64>`.
CREATE TABLE usuario (
  id                     BIGINT UNSIGNED   NOT NULL AUTO_INCREMENT,
  nombre_usuario         VARCHAR(50)       NOT NULL,
  correo                 VARCHAR(150)      NOT NULL,
  nombre_completo        VARCHAR(150)      NOT NULL,
  password_hash          VARCHAR(255)      NOT NULL,
  debe_cambiar_password  TINYINT(1)        NOT NULL DEFAULT 1,
  password_cambiado_en   DATETIME(6)       NULL,
  intentos_fallidos      SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  bloqueado_hasta        DATETIME(6)       NULL,
  ultimo_acceso_en       DATETIME(6)       NULL,
  activo                 TINYINT(1)        NOT NULL DEFAULT 1,
  version_fila           INT UNSIGNED      NOT NULL DEFAULT 1,
  creado_en              DATETIME(6)       NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por             BIGINT UNSIGNED   NULL,
  modificado_en          DATETIME(6)       NULL,
  modificado_por         BIGINT UNSIGNED   NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_usuario_nombre_usuario (nombre_usuario),
  UNIQUE KEY uq_usuario_correo (correo),
  KEY ix_usuario_nombre_completo (nombre_completo)
) ENGINE=InnoDB COMMENT='Usuarios del sistema';

-- Solo insercion. Impide reutilizar las ultimas N contrasenas.
CREATE TABLE password_historial (
  id             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  usuario_id     BIGINT UNSIGNED NOT NULL,
  password_hash  VARCHAR(255)    NOT NULL,
  creado_en      DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (id),
  KEY ix_password_historial_usuario (usuario_id, creado_en),
  CONSTRAINT fk_password_historial_usuario FOREIGN KEY (usuario_id) REFERENCES usuario (id)
) ENGINE=InnoDB COMMENT='Historial de contrasenas por usuario (solo insercion)';

-- Token de un solo uso para recuperar la contrasena. Se guarda el SHA-256
-- (hex) del token, nunca el token.
CREATE TABLE token_recuperacion (
  id          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  usuario_id  BIGINT UNSIGNED NOT NULL,
  token_hash  CHAR(64)        NOT NULL,
  expira_en   DATETIME(6)     NOT NULL,
  usado_en    DATETIME(6)     NULL,
  ip          VARCHAR(45)     NULL,
  creado_en   DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (id),
  UNIQUE KEY uq_token_recuperacion_hash (token_hash),
  KEY ix_token_recuperacion_usuario (usuario_id, creado_en),
  CONSTRAINT fk_token_recuperacion_usuario FOREIGN KEY (usuario_id) REFERENCES usuario (id)
) ENGINE=InnoDB COMMENT='Tokens de recuperacion de contrasena (hash, un solo uso)';

-- Refresh token con rotacion. Todos los tokens de una misma sesion comparten
-- familia_id; si se presenta un token ya rotado se revoca la familia completa.
-- empresa_id / sucursal_id = contexto de trabajo activo de la sesion, con el
-- que se emite el siguiente token de acceso.
CREATE TABLE refresh_token (
  id                  BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  usuario_id          BIGINT UNSIGNED NOT NULL,
  familia_id          CHAR(36)        NOT NULL,
  token_hash          CHAR(64)        NOT NULL,
  empresa_id          BIGINT UNSIGNED NULL,
  sucursal_id         BIGINT UNSIGNED NULL,
  expira_en           DATETIME(6)     NOT NULL,
  revocado_en         DATETIME(6)     NULL,
  motivo_revocacion   VARCHAR(30)     NULL,
  reemplazado_por_id  BIGINT UNSIGNED NULL,
  ip                  VARCHAR(45)     NULL,
  user_agent          VARCHAR(300)    NULL,
  creado_en           DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (id),
  UNIQUE KEY uq_refresh_token_hash (token_hash),
  -- Revocacion masiva por usuario (al desactivarlo o cambiar su contrasena).
  KEY ix_refresh_token_usuario (usuario_id, revocado_en),
  KEY ix_refresh_token_familia (familia_id),
  KEY ix_refresh_token_empresa (empresa_id),
  KEY ix_refresh_token_sucursal (sucursal_id),
  KEY ix_refresh_token_reemplazado_por (reemplazado_por_id),
  -- Limpieza periodica de tokens vencidos.
  KEY ix_refresh_token_expira (expira_en),
  CONSTRAINT fk_refresh_token_usuario FOREIGN KEY (usuario_id) REFERENCES usuario (id),
  CONSTRAINT fk_refresh_token_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id),
  CONSTRAINT fk_refresh_token_sucursal FOREIGN KEY (sucursal_id) REFERENCES sucursal (id),
  CONSTRAINT fk_refresh_token_reemplazado_por FOREIGN KEY (reemplazado_por_id) REFERENCES refresh_token (id),
  CONSTRAINT ck_refresh_token_motivo CHECK (
    motivo_revocacion IS NULL OR motivo_revocacion IN
      ('ROTACION', 'LOGOUT', 'REUTILIZACION', 'CAMBIO_PASSWORD', 'USUARIO_INACTIVO', 'ADMINISTRADOR'))
) ENGINE=InnoDB COMMENT='Refresh tokens (hash) con rotacion y deteccion de reutilizacion';

-- Solo insercion. Base del bloqueo por intentos y de la limitacion de tasa.
-- nombre_usuario es lo que se digito (puede no existir); usuario_id solo si existe.
CREATE TABLE intento_login (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  nombre_usuario  VARCHAR(150)    NOT NULL,
  usuario_id      BIGINT UNSIGNED NULL,
  exitoso         TINYINT(1)      NOT NULL,
  motivo          VARCHAR(30)     NULL,
  ip              VARCHAR(45)     NULL,
  user_agent      VARCHAR(300)    NULL,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (id),
  KEY ix_intento_login_nombre (nombre_usuario, creado_en),
  KEY ix_intento_login_ip (ip, creado_en),
  KEY ix_intento_login_usuario (usuario_id),
  CONSTRAINT fk_intento_login_usuario FOREIGN KEY (usuario_id) REFERENCES usuario (id)
) ENGINE=InnoDB COMMENT='Intentos de inicio de sesion (solo insercion)';

-- es_sistema = 1: rol sembrado que la aplicacion no permite eliminar.
CREATE TABLE rol (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  codigo          VARCHAR(50)     NOT NULL,
  nombre          VARCHAR(100)    NOT NULL,
  descripcion     VARCHAR(300)    NULL,
  es_sistema      TINYINT(1)      NOT NULL DEFAULT 0,
  activo          TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_rol_codigo (codigo)
) ENGINE=InnoDB COMMENT='Roles (perfiles)';

-- Modulos funcionales y su estado global en este ambiente.
-- es_base = 1: modulo que no puede bloquearse (Seguridad, Organizacion, Auditoria).
CREATE TABLE modulo (
  id              BIGINT UNSIGNED   NOT NULL AUTO_INCREMENT,
  codigo          VARCHAR(30)       NOT NULL,
  nombre          VARCHAR(100)      NOT NULL,
  fase            TINYINT UNSIGNED  NOT NULL,
  orden           SMALLINT UNSIGNED NOT NULL,
  estado_global   VARCHAR(20)       NOT NULL,
  es_base         TINYINT(1)        NOT NULL DEFAULT 0,
  activo          TINYINT(1)        NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED      NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)       NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED   NULL,
  modificado_en   DATETIME(6)       NULL,
  modificado_por  BIGINT UNSIGNED   NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_modulo_codigo (codigo),
  CONSTRAINT ck_modulo_fase CHECK (fase BETWEEN 1 AND 3),
  CONSTRAINT ck_modulo_estado_global CHECK (
    estado_global IN ('EN_DESARROLLO', 'EN_PRUEBAS', 'NO_LIBERADO', 'LIBERADO', 'SUSPENDIDO', 'RETIRADO'))
) ENGINE=InnoDB COMMENT='Modulos funcionales y su estado de liberacion global';

-- Catalogo de permisos. Se siembra por script (aqui y en updates/); la
-- aplicacion no crea ni edita permisos. codigo = {MODULO}_{RECURSO}_{ACCION}.
CREATE TABLE permiso (
  id           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  modulo_id    BIGINT UNSIGNED NOT NULL,
  codigo       VARCHAR(100)    NOT NULL,
  recurso      VARCHAR(50)     NOT NULL,
  accion       VARCHAR(30)     NOT NULL,
  nombre       VARCHAR(150)    NOT NULL,
  descripcion  VARCHAR(300)    NULL,
  activo       TINYINT(1)      NOT NULL DEFAULT 1,
  creado_en    DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por   BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_permiso_codigo (codigo),
  KEY ix_permiso_modulo (modulo_id, recurso),
  CONSTRAINT fk_permiso_modulo FOREIGN KEY (modulo_id) REFERENCES modulo (id)
) ENGINE=InnoDB COMMENT='Catalogo de permisos';

CREATE TABLE rol_permiso (
  id          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  rol_id      BIGINT UNSIGNED NOT NULL,
  permiso_id  BIGINT UNSIGNED NOT NULL,
  creado_en   DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_rol_permiso (rol_id, permiso_id),
  KEY ix_rol_permiso_permiso (permiso_id),
  CONSTRAINT fk_rol_permiso_rol FOREIGN KEY (rol_id) REFERENCES rol (id),
  CONSTRAINT fk_rol_permiso_permiso FOREIGN KEY (permiso_id) REFERENCES permiso (id)
) ENGINE=InnoDB COMMENT='Permisos concedidos a cada rol';

-- empresa_id NULL = el rol aplica en todas las empresas a las que el usuario
-- tiene acceso. Con valor, el rol aplica solo en esa empresa.
CREATE TABLE usuario_rol (
  id             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  usuario_id     BIGINT UNSIGNED NOT NULL,
  rol_id         BIGINT UNSIGNED NOT NULL,
  empresa_id     BIGINT UNSIGNED NULL,
  empresa_clave  BIGINT UNSIGNED GENERATED ALWAYS AS (IFNULL(empresa_id, 0)) STORED,
  creado_en      DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por     BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_usuario_rol (usuario_id, rol_id, empresa_clave),
  KEY ix_usuario_rol_rol (rol_id),
  KEY ix_usuario_rol_empresa (empresa_id),
  CONSTRAINT fk_usuario_rol_usuario FOREIGN KEY (usuario_id) REFERENCES usuario (id),
  CONSTRAINT fk_usuario_rol_rol FOREIGN KEY (rol_id) REFERENCES rol (id),
  CONSTRAINT fk_usuario_rol_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id)
) ENGINE=InnoDB COMMENT='Roles asignados a cada usuario, globales o por empresa';

-- Permiso directo al usuario. Precedencia (se resuelve en el backend):
-- DENEGAR de usuario > CONCEDER de usuario > union de los permisos de sus roles.
CREATE TABLE usuario_permiso (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  usuario_id      BIGINT UNSIGNED NOT NULL,
  permiso_id      BIGINT UNSIGNED NOT NULL,
  efecto          VARCHAR(10)     NOT NULL,
  vigente_desde   DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  vigente_hasta   DATETIME(6)     NULL,
  motivo          VARCHAR(300)    NULL,
  activo          TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_usuario_permiso (usuario_id, permiso_id),
  KEY ix_usuario_permiso_permiso (permiso_id),
  CONSTRAINT fk_usuario_permiso_usuario FOREIGN KEY (usuario_id) REFERENCES usuario (id),
  CONSTRAINT fk_usuario_permiso_permiso FOREIGN KEY (permiso_id) REFERENCES permiso (id),
  CONSTRAINT ck_usuario_permiso_efecto CHECK (efecto IN ('CONCEDER', 'DENEGAR')),
  CONSTRAINT ck_usuario_permiso_vigencia CHECK (vigente_hasta IS NULL OR vigente_hasta > vigente_desde)
) ENGINE=InnoDB COMMENT='Permisos concedidos o denegados directamente a un usuario';

CREATE TABLE usuario_empresa (
  id                 BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  usuario_id         BIGINT UNSIGNED NOT NULL,
  empresa_id         BIGINT UNSIGNED NOT NULL,
  es_predeterminada  TINYINT(1)      NOT NULL DEFAULT 0,
  creado_en          DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por         BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_usuario_empresa (usuario_id, empresa_id),
  KEY ix_usuario_empresa_empresa (empresa_id),
  CONSTRAINT fk_usuario_empresa_usuario FOREIGN KEY (usuario_id) REFERENCES usuario (id),
  CONSTRAINT fk_usuario_empresa_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id)
) ENGINE=InnoDB COMMENT='Empresas a las que el usuario tiene acceso';

-- El backend exige ademas que el usuario tenga acceso a la empresa de la sucursal.
CREATE TABLE usuario_sucursal (
  id                 BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  usuario_id         BIGINT UNSIGNED NOT NULL,
  sucursal_id        BIGINT UNSIGNED NOT NULL,
  es_predeterminada  TINYINT(1)      NOT NULL DEFAULT 0,
  creado_en          DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por         BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_usuario_sucursal (usuario_id, sucursal_id),
  KEY ix_usuario_sucursal_sucursal (sucursal_id),
  CONSTRAINT fk_usuario_sucursal_usuario FOREIGN KEY (usuario_id) REFERENCES usuario (id),
  CONSTRAINT fk_usuario_sucursal_sucursal FOREIGN KEY (sucursal_id) REFERENCES sucursal (id)
) ENGINE=InnoDB COMMENT='Sucursales a las que el usuario tiene acceso';

-- Estado del modulo para una empresa. Estado efectivo = el mas restrictivo
-- entre modulo.estado_global y este (regla en el backend). Si no hay fila para
-- una empresa, el backend trata el modulo como NO_LIBERADO para ella.
-- El historial de cambios queda en `auditoria`.
CREATE TABLE modulo_empresa (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  modulo_id       BIGINT UNSIGNED NOT NULL,
  empresa_id      BIGINT UNSIGNED NOT NULL,
  estado          VARCHAR(20)     NOT NULL,
  vigente_desde   DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  cambiado_por    BIGINT UNSIGNED NULL,
  motivo          VARCHAR(500)    NOT NULL,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_modulo_empresa (modulo_id, empresa_id),
  KEY ix_modulo_empresa_empresa (empresa_id),
  CONSTRAINT fk_modulo_empresa_modulo FOREIGN KEY (modulo_id) REFERENCES modulo (id),
  CONSTRAINT fk_modulo_empresa_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id),
  CONSTRAINT ck_modulo_empresa_estado CHECK (
    estado IN ('EN_DESARROLLO', 'EN_PRUEBAS', 'NO_LIBERADO', 'LIBERADO', 'SUSPENDIDO', 'RETIRADO'))
) ENGINE=InnoDB COMMENT='Estado de liberacion de cada modulo por empresa';

-- Arbol del menu. `activo` cumple el papel del "estado" del elemento.
-- El backend lo devuelve ya filtrado por estado efectivo del modulo y permiso.
CREATE TABLE menu (
  id                    BIGINT UNSIGNED   NOT NULL AUTO_INCREMENT,
  padre_id              BIGINT UNSIGNED   NULL,
  modulo_id             BIGINT UNSIGNED   NOT NULL,
  tipo                  VARCHAR(10)       NOT NULL,
  codigo                VARCHAR(60)       NOT NULL,
  nombre                VARCHAR(100)      NOT NULL,
  icono                 VARCHAR(50)       NULL,
  ruta                  VARCHAR(200)      NULL,
  orden                 SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  permiso_requerido_id  BIGINT UNSIGNED   NULL,
  activo                TINYINT(1)        NOT NULL DEFAULT 1,
  version_fila          INT UNSIGNED      NOT NULL DEFAULT 1,
  creado_en             DATETIME(6)       NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por            BIGINT UNSIGNED   NULL,
  modificado_en         DATETIME(6)       NULL,
  modificado_por        BIGINT UNSIGNED   NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_menu_codigo (codigo),
  KEY ix_menu_padre (padre_id, orden),
  KEY ix_menu_modulo (modulo_id),
  KEY ix_menu_permiso_requerido (permiso_requerido_id),
  CONSTRAINT fk_menu_padre FOREIGN KEY (padre_id) REFERENCES menu (id),
  CONSTRAINT fk_menu_modulo FOREIGN KEY (modulo_id) REFERENCES modulo (id),
  CONSTRAINT fk_menu_permiso_requerido FOREIGN KEY (permiso_requerido_id) REFERENCES permiso (id),
  CONSTRAINT ck_menu_tipo CHECK (tipo IN ('MODULO', 'GRUPO', 'PANTALLA')),
  -- Una pantalla siempre tiene ruta y permiso; asi ninguna queda visible sin control.
  CONSTRAINT ck_menu_pantalla CHECK (
    tipo <> 'PANTALLA' OR (ruta IS NOT NULL AND permiso_requerido_id IS NOT NULL))
) ENGINE=InnoDB COMMENT='Menu dinamico';


-- ===== CATALOGOS =====
-- (etapa 1A)

-- ===== CLIENTES =====
-- ===== PROVEEDORES =====
-- ===== PRODUCTOS =====
-- (etapa 1B)

-- ===== INVENTARIO =====
-- (etapa 1C)

-- ===== COMPRAS =====
-- ===== CARTERA =====
-- (etapa 1D)

-- ===== VENTAS =====
-- (etapa 1E)

-- ===== CAJA =====
-- (etapa 1F)


-- ===== AUDITORIA =====
-- Tablas INMUTABLES: solo insercion. El usuario MySQL de la aplicacion recibe
-- unicamente SELECT e INSERT sobre ellas (sin UPDATE ni DELETE).

-- entidad_id es texto para admitir cualquier tipo de identificador.
-- operacion_id = correlation id de la solicitud, el mismo que aparece en los logs.
-- Los valores sensibles (contrasenas, tokens) llegan ya enmascarados desde el backend.
CREATE TABLE auditoria (
  id                  BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  fecha_utc           DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  usuario_id          BIGINT UNSIGNED NULL,
  empresa_id          BIGINT UNSIGNED NULL,
  sucursal_id         BIGINT UNSIGNED NULL,
  modulo              VARCHAR(30)     NOT NULL,
  entidad             VARCHAR(60)     NOT NULL,
  entidad_id          VARCHAR(40)     NULL,
  accion              VARCHAR(30)     NOT NULL,
  resultado           VARCHAR(10)     NOT NULL,
  valores_anteriores  JSON            NULL,
  valores_nuevos      JSON            NULL,
  ip                  VARCHAR(45)     NULL,
  user_agent          VARCHAR(300)    NULL,
  operacion_id        VARCHAR(64)     NULL,
  mensaje             VARCHAR(1000)   NULL,
  PRIMARY KEY (id),
  -- Consulta de la bitacora por empresa y rango de fechas.
  KEY ix_auditoria_empresa_fecha (empresa_id, fecha_utc),
  -- Historial de cambios de un registro concreto.
  KEY ix_auditoria_entidad (entidad, entidad_id, fecha_utc),
  KEY ix_auditoria_usuario (usuario_id, fecha_utc),
  KEY ix_auditoria_sucursal (sucursal_id),
  -- Cruce con los logs por correlation id.
  KEY ix_auditoria_operacion (operacion_id),
  CONSTRAINT fk_auditoria_usuario FOREIGN KEY (usuario_id) REFERENCES usuario (id),
  CONSTRAINT fk_auditoria_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id),
  CONSTRAINT fk_auditoria_sucursal FOREIGN KEY (sucursal_id) REFERENCES sucursal (id),
  CONSTRAINT ck_auditoria_resultado CHECK (resultado IN ('EXITO', 'FALLO'))
) ENGINE=InnoDB COMMENT='Bitacora de operaciones (inmutable)';

-- Eventos de acceso: LOGIN_EXITOSO, LOGIN_FALLIDO, BLOQUEO, LOGOUT, REFRESH,
-- REFRESH_REUTILIZADO, CAMBIO_PASSWORD, RECUPERACION_SOLICITADA,
-- RECUPERACION_USADA, CAMBIO_CONTEXTO. La lista la define el backend.
CREATE TABLE auditoria_acceso (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  fecha_utc       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  usuario_id      BIGINT UNSIGNED NULL,
  nombre_usuario  VARCHAR(150)    NULL,
  evento          VARCHAR(40)     NOT NULL,
  resultado       VARCHAR(10)     NOT NULL,
  ip              VARCHAR(45)     NULL,
  user_agent      VARCHAR(300)    NULL,
  operacion_id    VARCHAR(64)     NULL,
  mensaje         VARCHAR(500)    NULL,
  PRIMARY KEY (id),
  KEY ix_auditoria_acceso_usuario (usuario_id, fecha_utc),
  KEY ix_auditoria_acceso_evento (evento, fecha_utc),
  KEY ix_auditoria_acceso_ip (ip, fecha_utc),
  CONSTRAINT fk_auditoria_acceso_usuario FOREIGN KEY (usuario_id) REFERENCES usuario (id),
  CONSTRAINT ck_auditoria_acceso_resultado CHECK (resultado IN ('EXITO', 'FALLO'))
) ENGINE=InnoDB COMMENT='Bitacora de accesos y eventos de sesion (inmutable)';


-- ===== VISTAS =====
-- Ninguna en Fase 0.

-- ===== PROCEDIMIENTOS =====
-- Ninguno en Fase 0. `sp_siguiente_correlativo` llega en 1A junto con las series.

-- ===== FUNCIONES =====
-- Ninguna en Fase 0.

-- ===== TRIGGERS =====
-- Ninguno. Las reglas de negocio no van en triggers.


-- ===== SEEDS =====
-- Solo lo indispensable para iniciar sesion y administrar el sistema.
-- Los datos de prueba van en demo-data.sql, nunca aqui.

START TRANSACTION;

INSERT INTO seguridad_version (id, version) VALUES (1, 1);

INSERT INTO politica_password
  (longitud_minima, requiere_mayuscula, requiere_minuscula, requiere_digito, requiere_simbolo,
   historial_cantidad, expira_dias, max_intentos_fallidos, bloqueo_minutos, recuperacion_minutos)
VALUES
  (10, 1, 1, 1, 1, 5, 0, 5, 15, 30);

-- Empresa y sucursal iniciales, con datos genericos para editar despues.
INSERT INTO empresa (codigo, razon_social, nombre_comercial, nit)
VALUES ('EMP01', 'Empresa Principal', 'Empresa Principal', 'CF');

INSERT INTO sucursal (empresa_id, codigo, nombre, es_principal)
SELECT id, 'SUC01', 'Sucursal Principal', 1 FROM empresa WHERE codigo = 'EMP01';

-- Modulos. Los tres de base nacen liberados; el resto de Fase 1 aun no existe
-- en el codigo (EN_DESARROLLO) y los de Fases 2 y 3 se publican sin liberar.
INSERT INTO modulo (codigo, nombre, fase, orden, estado_global, es_base) VALUES
  ('SEGURIDAD',    'Seguridad',               1,  10, 'LIBERADO',      1),
  ('ORGANIZACION', 'Organización',            1,  20, 'LIBERADO',      1),
  ('AUDITORIA',    'Auditoría',               1,  30, 'LIBERADO',      1),
  ('CATALOGOS',    'Catálogos',               1,  40, 'EN_DESARROLLO', 0),
  ('CLIENTES',     'Clientes',                1,  50, 'EN_DESARROLLO', 0),
  ('PROVEEDORES',  'Proveedores',             1,  60, 'EN_DESARROLLO', 0),
  ('PRODUCTOS',    'Productos',               1,  70, 'EN_DESARROLLO', 0),
  ('INVENTARIO',   'Inventario',              1,  80, 'EN_DESARROLLO', 0),
  ('COMPRAS',      'Compras',                 1,  90, 'EN_DESARROLLO', 0),
  ('CARTERA',      'Cartera',                 1, 100, 'EN_DESARROLLO', 0),
  ('VENTAS',       'Ventas',                  1, 110, 'EN_DESARROLLO', 0),
  ('CAJA',         'Caja y bancos',           1, 120, 'EN_DESARROLLO', 0),
  ('REPORTES',     'Reportes',                1, 130, 'EN_DESARROLLO', 0),
  ('CONTABILIDAD', 'Contabilidad',            2, 200, 'NO_LIBERADO',   0),
  ('CXC',          'Cuentas por cobrar',      2, 210, 'NO_LIBERADO',   0),
  ('CXP',          'Cuentas por pagar',       2, 220, 'NO_LIBERADO',   0),
  ('RRHH',         'Recursos humanos',        3, 300, 'NO_LIBERADO',   0),
  ('NOMINA',       'Nómina',                  3, 310, 'NO_LIBERADO',   0);

-- Estado por empresa: la empresa inicial arranca igual que el estado global.
INSERT INTO modulo_empresa (modulo_id, empresa_id, estado, motivo)
SELECT m.id, e.id, m.estado_global, 'Instalación inicial'
FROM modulo m
CROSS JOIN empresa e
WHERE e.codigo = 'EMP01';

-- Permisos de Fase 0.
INSERT INTO permiso (modulo_id, codigo, recurso, accion, nombre)
SELECT m.id, p.codigo, p.recurso, p.accion, p.nombre
FROM (
  SELECT 'SEGURIDAD' AS modulo, 'SEGURIDAD_USUARIOS_VER' AS codigo, 'USUARIOS' AS recurso, 'VER' AS accion, 'Ver usuarios' AS nombre
  UNION ALL SELECT 'SEGURIDAD', 'SEGURIDAD_USUARIOS_CREAR',    'USUARIOS', 'CREAR',    'Crear usuarios'
  UNION ALL SELECT 'SEGURIDAD', 'SEGURIDAD_USUARIOS_EDITAR',   'USUARIOS', 'EDITAR',   'Editar usuarios, sus roles y accesos'
  UNION ALL SELECT 'SEGURIDAD', 'SEGURIDAD_USUARIOS_ELIMINAR', 'USUARIOS', 'ELIMINAR', 'Desactivar usuarios'
  UNION ALL SELECT 'SEGURIDAD', 'SEGURIDAD_ROLES_VER',         'ROLES',    'VER',      'Ver roles'
  UNION ALL SELECT 'SEGURIDAD', 'SEGURIDAD_ROLES_CREAR',       'ROLES',    'CREAR',    'Crear roles'
  UNION ALL SELECT 'SEGURIDAD', 'SEGURIDAD_ROLES_EDITAR',      'ROLES',    'EDITAR',   'Editar roles y sus permisos'
  UNION ALL SELECT 'SEGURIDAD', 'SEGURIDAD_ROLES_ELIMINAR',    'ROLES',    'ELIMINAR', 'Desactivar roles'
  UNION ALL SELECT 'SEGURIDAD', 'SEGURIDAD_PERMISOS_VER',      'PERMISOS', 'VER',      'Ver el catálogo de permisos'
  UNION ALL SELECT 'SEGURIDAD', 'SEGURIDAD_PERMISOS_EDITAR',   'PERMISOS', 'EDITAR',   'Conceder o denegar permisos directos a un usuario'
  UNION ALL SELECT 'SEGURIDAD', 'SEGURIDAD_MODULOS_VER',       'MODULOS',  'VER',      'Ver módulos y su estado'
  UNION ALL SELECT 'SEGURIDAD', 'SEGURIDAD_MODULOS_EDITAR',    'MODULOS',  'EDITAR',   'Cambiar el estado de un módulo'
  UNION ALL SELECT 'ORGANIZACION', 'ORGANIZACION_EMPRESAS_VER',        'EMPRESAS',   'VER',      'Ver empresas'
  UNION ALL SELECT 'ORGANIZACION', 'ORGANIZACION_SUCURSALES_VER',      'SUCURSALES', 'VER',      'Ver sucursales'
  UNION ALL SELECT 'ORGANIZACION', 'ORGANIZACION_SUCURSALES_CREAR',    'SUCURSALES', 'CREAR',    'Crear sucursales'
  UNION ALL SELECT 'ORGANIZACION', 'ORGANIZACION_SUCURSALES_EDITAR',   'SUCURSALES', 'EDITAR',   'Editar sucursales'
  UNION ALL SELECT 'ORGANIZACION', 'ORGANIZACION_SUCURSALES_ELIMINAR', 'SUCURSALES', 'ELIMINAR', 'Desactivar sucursales'
  UNION ALL SELECT 'ORGANIZACION', 'ORGANIZACION_SUCURSALES_EXPORTAR', 'SUCURSALES', 'EXPORTAR', 'Exportar sucursales'
  UNION ALL SELECT 'AUDITORIA', 'AUDITORIA_BITACORA_VER', 'BITACORA', 'VER', 'Ver la bitácora de auditoría'
) p
JOIN modulo m ON m.codigo = p.modulo;

-- Rol administrador: recibe TODOS los permisos. No hay atajo "superadmin" en el
-- codigo, asi que cada script de updates/ que agregue permisos debe concederlos
-- tambien a este rol.
INSERT INTO rol (codigo, nombre, descripcion, es_sistema)
VALUES ('ADMINISTRADOR', 'Administrador', 'Acceso a todos los permisos del sistema', 1);

INSERT INTO rol_permiso (rol_id, permiso_id)
SELECT r.id, p.id
FROM rol r
CROSS JOIN permiso p
WHERE r.codigo = 'ADMINISTRADOR';

-- Usuario administrador inicial.
--   usuario: admin    contrasena temporal: Cambiar.2026!
-- debe_cambiar_password = 1 obliga a cambiarla en el primer ingreso.
-- Hash PBKDF2-HMAC-SHA512, 210000 iteraciones, sal de 16 bytes, clave de 32 bytes.
INSERT INTO usuario (nombre_usuario, correo, nombre_completo, password_hash, debe_cambiar_password)
VALUES ('admin', 'admin@erpgt.local', 'Administrador del sistema',
        'pbkdf2-sha512$210000$Mb7BW9Tadnpz/2dJ4fs4/A==$GJ0s7esg+gjdUTmBns68aV6NeQOFbI3qeQ20jhJdaKw=', 1);

-- Rol global (empresa_id NULL = todas las empresas).
INSERT INTO usuario_rol (usuario_id, rol_id, empresa_id)
SELECT u.id, r.id, NULL
FROM usuario u
CROSS JOIN rol r
WHERE u.nombre_usuario = 'admin' AND r.codigo = 'ADMINISTRADOR';

INSERT INTO usuario_empresa (usuario_id, empresa_id, es_predeterminada)
SELECT u.id, e.id, 1
FROM usuario u
CROSS JOIN empresa e
WHERE u.nombre_usuario = 'admin' AND e.codigo = 'EMP01';

INSERT INTO usuario_sucursal (usuario_id, sucursal_id, es_predeterminada)
SELECT u.id, s.id, 1
FROM usuario u
CROSS JOIN sucursal s
WHERE u.nombre_usuario = 'admin' AND s.codigo = 'SUC01';

-- Menu: primero los nodos de modulo, luego sus pantallas.
-- `icono` es el nombre del icono en la libreria del frontend (lucide).
INSERT INTO menu (padre_id, modulo_id, tipo, codigo, nombre, icono, ruta, orden, permiso_requerido_id)
SELECT NULL, m.id, 'MODULO', n.codigo, n.nombre, n.icono, NULL, n.orden, NULL
FROM (
  SELECT 'SEGURIDAD' AS modulo, 'MNU_SEGURIDAD' AS codigo, 'Seguridad' AS nombre, 'shield' AS icono, 10 AS orden
  UNION ALL SELECT 'ORGANIZACION', 'MNU_ORGANIZACION', 'Organización', 'building-2', 20
  UNION ALL SELECT 'AUDITORIA',    'MNU_AUDITORIA',    'Auditoría',    'history',    30
) n
JOIN modulo m ON m.codigo = n.modulo;

INSERT INTO menu (padre_id, modulo_id, tipo, codigo, nombre, icono, ruta, orden, permiso_requerido_id)
SELECT padre.id, padre.modulo_id, 'PANTALLA', n.codigo, n.nombre, n.icono, n.ruta, n.orden, p.id
FROM (
  SELECT 'MNU_SEGURIDAD' AS padre, 'MNU_SEGURIDAD_USUARIOS' AS codigo, 'Usuarios' AS nombre, 'users' AS icono,
         '/seguridad/usuarios' AS ruta, 10 AS orden, 'SEGURIDAD_USUARIOS_VER' AS permiso
  UNION ALL SELECT 'MNU_SEGURIDAD',    'MNU_SEGURIDAD_ROLES',         'Roles',      'key-round',   '/seguridad/roles',         20, 'SEGURIDAD_ROLES_VER'
  UNION ALL SELECT 'MNU_SEGURIDAD',    'MNU_SEGURIDAD_MODULOS',       'Módulos',    'blocks',      '/seguridad/modulos',       30, 'SEGURIDAD_MODULOS_VER'
  UNION ALL SELECT 'MNU_ORGANIZACION', 'MNU_ORGANIZACION_SUCURSALES', 'Sucursales', 'store',       '/organizacion/sucursales', 10, 'ORGANIZACION_SUCURSALES_VER'
  UNION ALL SELECT 'MNU_AUDITORIA',    'MNU_AUDITORIA_BITACORA',      'Bitácora',   'scroll-text', '/auditoria/bitacora',      10, 'AUDITORIA_BITACORA_VER'
) n
JOIN menu padre ON padre.codigo = n.padre
JOIN permiso p ON p.codigo = n.permiso;

INSERT INTO esquema_version (version, script, aplicado_por)
VALUES ('0.1.0', 'database.sql', CURRENT_USER());

COMMIT;


-- ===== RELACIONES DIFERIDAS =====
-- FK que apuntan a una tabla de una seccion posterior a la de su tabla.

-- CONFIGURACION -> ORGANIZACION
ALTER TABLE configuracion
  ADD CONSTRAINT fk_configuracion_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id);

-- =====================================================================
-- Fin de database.sql (version 0.1.0)
-- =====================================================================
