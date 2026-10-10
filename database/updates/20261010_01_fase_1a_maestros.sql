-- =====================================================================
-- ERP Modular - Actualizacion 0.1.0 -> 0.2.0 : Fase 1A (Maestros y organizacion)
-- =====================================================================
-- Para bases YA instaladas con la version 0.1.0. Equivale a lo que agrega
-- database.sql 0.2.0 sobre la 0.1.0; en una instalacion limpia NO se ejecuta.
--
-- Se ejecuta UNA sola vez, con un usuario con privilegios de DDL (no el de la app):
--   mysql -u <usuario> -p --default-character-set=utf8mb4 -e "source database/updates/20261010_01_fase_1a_maestros.sql"
--
-- Que hace:
--   * empresa: regimen_tributario y moneda_local_id.
--   * Tablas nuevas: bodega, caja, departamento, centro_costo, usuario_bodega,
--     usuario_caja, moneda, tipo_cambio, impuesto, unidad_medida, unidad_conversion,
--     tipo_producto, categoria, marca, tipo_proveedor, motivo_anulacion, banco,
--     forma_pago, condicion_pago, cuenta_bancaria, tipo_documento, serie_documento.
--   * Procedimiento sp_siguiente_correlativo.
--   * Seeds, permisos (concedidos al rol ADMINISTRADOR) y menu.
--   * Modulo CATALOGOS: LIBERADO y base.
--
-- La primera instruccion registra el script en esquema_version: si ya se aplico,
-- falla por clave duplicada y no se ejecuta nada mas. Si falla a mitad de camino
-- (DDL no es transaccional en MySQL), restaure el respaldo previo; no lo reintente.
-- Despues de ejecutarlo, otorgue al usuario de la aplicacion los privilegios de las
-- tablas nuevas (ver database/usuarios-ejemplo.sql).
-- =====================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_0900_ai_ci;

USE erpgt;

INSERT INTO esquema_version (version, script, aplicado_por)
VALUES ('0.2.0', '20261010_01_fase_1a_maestros.sql', CURRENT_USER());

-- ===== ORGANIZACION =====

ALTER TABLE empresa
  ADD COLUMN regimen_tributario VARCHAR(30) NOT NULL DEFAULT 'GENERAL' AFTER correo,
  ADD COLUMN moneda_local_id BIGINT UNSIGNED NULL AFTER regimen_tributario;

-- Bodegas, cajas, departamentos y centros de costo (etapa 1A).
-- La coherencia empresa/sucursal de las FK la valida el backend: sucursal.empresa_id
-- debe ser igual a empresa_id de la fila.

CREATE TABLE bodega (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  empresa_id      BIGINT UNSIGNED NOT NULL,
  sucursal_id     BIGINT UNSIGNED NOT NULL,
  codigo          VARCHAR(20)     NOT NULL,
  nombre          VARCHAR(150)    NOT NULL,
  descripcion     VARCHAR(300)    NULL,
  activo          TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_bodega_codigo (empresa_id, codigo),
  KEY ix_bodega_empresa_nombre (empresa_id, nombre),
  KEY ix_bodega_sucursal (sucursal_id),
  CONSTRAINT fk_bodega_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id),
  CONSTRAINT fk_bodega_sucursal FOREIGN KEY (sucursal_id) REFERENCES sucursal (id)
) ENGINE=InnoDB COMMENT='Bodegas de inventario, por sucursal';

-- bodega_id: bodega que descuenta la caja por defecto en ventas de mostrador (opcional).
CREATE TABLE caja (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  empresa_id      BIGINT UNSIGNED NOT NULL,
  sucursal_id     BIGINT UNSIGNED NOT NULL,
  bodega_id       BIGINT UNSIGNED NULL,
  codigo          VARCHAR(20)     NOT NULL,
  nombre          VARCHAR(150)    NOT NULL,
  activo          TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_caja_codigo (empresa_id, codigo),
  KEY ix_caja_empresa_nombre (empresa_id, nombre),
  KEY ix_caja_sucursal (sucursal_id),
  KEY ix_caja_bodega (bodega_id),
  CONSTRAINT fk_caja_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id),
  CONSTRAINT fk_caja_sucursal FOREIGN KEY (sucursal_id) REFERENCES sucursal (id),
  CONSTRAINT fk_caja_bodega FOREIGN KEY (bodega_id) REFERENCES bodega (id)
) ENGINE=InnoDB COMMENT='Cajas (puntos de cobro), por sucursal';

CREATE TABLE departamento (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  empresa_id      BIGINT UNSIGNED NOT NULL,
  codigo          VARCHAR(20)     NOT NULL,
  nombre          VARCHAR(150)    NOT NULL,
  activo          TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_departamento_codigo (empresa_id, codigo),
  KEY ix_departamento_empresa_nombre (empresa_id, nombre),
  CONSTRAINT fk_departamento_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id)
) ENGINE=InnoDB COMMENT='Departamentos organizacionales';

CREATE TABLE centro_costo (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  empresa_id      BIGINT UNSIGNED NOT NULL,
  departamento_id BIGINT UNSIGNED NULL,
  codigo          VARCHAR(20)     NOT NULL,
  nombre          VARCHAR(150)    NOT NULL,
  activo          TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_centro_costo_codigo (empresa_id, codigo),
  KEY ix_centro_costo_empresa_nombre (empresa_id, nombre),
  KEY ix_centro_costo_departamento (departamento_id),
  CONSTRAINT fk_centro_costo_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id),
  CONSTRAINT fk_centro_costo_departamento FOREIGN KEY (departamento_id) REFERENCES departamento (id)
) ENGINE=InnoDB COMMENT='Centros de costo (estructura; la contabilidad llega en la fase 2)';

-- ===== SEGURIDAD =====

-- Bodegas y cajas a las que el usuario tiene acceso (etapa 1A). El backend exige
-- ademas que la bodega/caja pertenezca a una sucursal accesible al usuario.
CREATE TABLE usuario_bodega (
  id                 BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  usuario_id         BIGINT UNSIGNED NOT NULL,
  bodega_id          BIGINT UNSIGNED NOT NULL,
  es_predeterminada  TINYINT(1)      NOT NULL DEFAULT 0,
  creado_en          DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por         BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_usuario_bodega (usuario_id, bodega_id),
  KEY ix_usuario_bodega_bodega (bodega_id),
  CONSTRAINT fk_usuario_bodega_usuario FOREIGN KEY (usuario_id) REFERENCES usuario (id),
  CONSTRAINT fk_usuario_bodega_bodega FOREIGN KEY (bodega_id) REFERENCES bodega (id)
) ENGINE=InnoDB COMMENT='Bodegas a las que el usuario tiene acceso';

CREATE TABLE usuario_caja (
  id                 BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  usuario_id         BIGINT UNSIGNED NOT NULL,
  caja_id            BIGINT UNSIGNED NOT NULL,
  es_predeterminada  TINYINT(1)      NOT NULL DEFAULT 0,
  creado_en          DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por         BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_usuario_caja (usuario_id, caja_id),
  KEY ix_usuario_caja_caja (caja_id),
  CONSTRAINT fk_usuario_caja_usuario FOREIGN KEY (usuario_id) REFERENCES usuario (id),
  CONSTRAINT fk_usuario_caja_caja FOREIGN KEY (caja_id) REFERENCES caja (id)
) ENGINE=InnoDB COMMENT='Cajas a las que el usuario tiene acceso';

-- ===== CATALOGOS =====

-- Catalogos de la etapa 1A. Salvo `moneda`, `tipo_documento` y las unidades
-- estandar (empresa_id NULL), todo pertenece a una empresa.

-- Monedas: catalogo global. La moneda local de cada empresa esta en empresa.moneda_local_id.
CREATE TABLE moneda (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  codigo          VARCHAR(3)      NOT NULL,
  nombre          VARCHAR(60)     NOT NULL,
  simbolo         VARCHAR(5)      NOT NULL,
  decimales       TINYINT UNSIGNED NOT NULL DEFAULT 2,
  activo          TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_moneda_codigo (codigo),
  CONSTRAINT ck_moneda_decimales CHECK (decimales <= 6)
) ENGINE=InnoDB COMMENT='Monedas (catalogo global)';

-- Una tasa por moneda y fecha. Para una fecha sin tasa propia se usa la ultima anterior.
-- La moneda local no lleva tipo de cambio (regla en el backend). Las correcciones
-- se hacen editando la fila: la bitacora conserva el valor anterior.
CREATE TABLE tipo_cambio (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  empresa_id      BIGINT UNSIGNED NOT NULL,
  moneda_id       BIGINT UNSIGNED NOT NULL,
  fecha           DATE            NOT NULL,
  tasa            DECIMAL(18,8)   NOT NULL,
  activo          TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  -- Busqueda de la tasa vigente a una fecha.
  UNIQUE KEY uq_tipo_cambio (empresa_id, moneda_id, fecha),
  KEY ix_tipo_cambio_moneda (moneda_id),
  CONSTRAINT fk_tipo_cambio_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id),
  CONSTRAINT fk_tipo_cambio_moneda FOREIGN KEY (moneda_id) REFERENCES moneda (id),
  CONSTRAINT ck_tipo_cambio_tasa CHECK (tasa > 0)
) ENGINE=InnoDB COMMENT='Tipo de cambio por moneda y fecha';

-- Una fila por codigo y vigencia: cambiar la tasa es agregar una vigencia nueva,
-- nunca sobrescribir la anterior. Que no se traslapen lo valida el backend.
CREATE TABLE impuesto (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  empresa_id      BIGINT UNSIGNED NOT NULL,
  codigo          VARCHAR(20)     NOT NULL,
  nombre          VARCHAR(150)    NOT NULL,
  tipo            VARCHAR(10)     NOT NULL,
  porcentaje      DECIMAL(9,6)    NOT NULL,
  vigente_desde   DATE            NOT NULL,
  vigente_hasta   DATE            NULL,
  activo          TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_impuesto_vigencia (empresa_id, codigo, vigente_desde),
  KEY ix_impuesto_empresa_nombre (empresa_id, nombre),
  CONSTRAINT fk_impuesto_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id),
  CONSTRAINT ck_impuesto_tipo CHECK (tipo IN ('IVA', 'OTRO')),
  CONSTRAINT ck_impuesto_porcentaje CHECK (porcentaje >= 0 AND porcentaje <= 100),
  CONSTRAINT ck_impuesto_vigencia CHECK (vigente_hasta IS NULL OR vigente_hasta >= vigente_desde)
) ENGINE=InnoDB COMMENT='Impuestos con vigencia';

-- empresa_id NULL = unidad estandar del sistema (solo lectura para las empresas).
CREATE TABLE unidad_medida (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  empresa_id      BIGINT UNSIGNED NULL,
  -- UNIQUE no detecta duplicados con NULL; esta columna lo resuelve (como en configuracion).
  empresa_clave   BIGINT UNSIGNED GENERATED ALWAYS AS (IFNULL(empresa_id, 0)) STORED,
  codigo          VARCHAR(10)     NOT NULL,
  nombre          VARCHAR(60)     NOT NULL,
  magnitud        VARCHAR(10)     NOT NULL,
  decimales       TINYINT UNSIGNED NOT NULL DEFAULT 0,
  activo          TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_unidad_medida_codigo (empresa_clave, codigo),
  KEY ix_unidad_medida_empresa (empresa_id),
  CONSTRAINT fk_unidad_medida_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id),
  CONSTRAINT ck_unidad_medida_magnitud CHECK (magnitud IN ('CANTIDAD', 'PESO', 'VOLUMEN', 'LONGITUD')),
  CONSTRAINT ck_unidad_medida_decimales CHECK (decimales <= 6)
) ENGINE=InnoDB COMMENT='Unidades de medida (estandar y por empresa)';

-- 1 unidad de origen = factor unidades de destino. Las dos unidades son de la misma
-- magnitud y el conjunto de conversiones visible para una empresa no forma ciclos
-- ni duplica caminos (reglas en el backend).
CREATE TABLE unidad_conversion (
  id                 BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  empresa_id         BIGINT UNSIGNED NULL,
  empresa_clave      BIGINT UNSIGNED GENERATED ALWAYS AS (IFNULL(empresa_id, 0)) STORED,
  unidad_origen_id   BIGINT UNSIGNED NOT NULL,
  unidad_destino_id  BIGINT UNSIGNED NOT NULL,
  factor             DECIMAL(18,6)   NOT NULL,
  activo          TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_unidad_conversion (empresa_clave, unidad_origen_id, unidad_destino_id),
  KEY ix_unidad_conversion_empresa (empresa_id),
  KEY ix_unidad_conversion_destino (unidad_destino_id),
  CONSTRAINT fk_unidad_conversion_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id),
  CONSTRAINT fk_unidad_conversion_origen FOREIGN KEY (unidad_origen_id) REFERENCES unidad_medida (id),
  CONSTRAINT fk_unidad_conversion_destino FOREIGN KEY (unidad_destino_id) REFERENCES unidad_medida (id),
  CONSTRAINT ck_unidad_conversion_distintas CHECK (unidad_origen_id <> unidad_destino_id),
  CONSTRAINT ck_unidad_conversion_factor CHECK (factor > 0)
) ENGINE=InnoDB COMMENT='Conversiones entre unidades de la misma magnitud';

CREATE TABLE tipo_producto (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  empresa_id      BIGINT UNSIGNED NOT NULL,
  codigo          VARCHAR(20)     NOT NULL,
  nombre          VARCHAR(150)    NOT NULL,
  maneja_inventario TINYINT(1)    NOT NULL DEFAULT 1,
  activo          TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_tipo_producto_codigo (empresa_id, codigo),
  KEY ix_tipo_producto_empresa_nombre (empresa_id, nombre),
  CONSTRAINT fk_tipo_producto_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id)
) ENGINE=InnoDB COMMENT='Tipos de producto';

CREATE TABLE categoria (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  empresa_id      BIGINT UNSIGNED NOT NULL,
  codigo          VARCHAR(20)     NOT NULL,
  nombre          VARCHAR(150)    NOT NULL,
  activo          TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_categoria_codigo (empresa_id, codigo),
  KEY ix_categoria_empresa_nombre (empresa_id, nombre),
  CONSTRAINT fk_categoria_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id)
) ENGINE=InnoDB COMMENT='Categorias de producto (planas)';

CREATE TABLE marca (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  empresa_id      BIGINT UNSIGNED NOT NULL,
  codigo          VARCHAR(20)     NOT NULL,
  nombre          VARCHAR(150)    NOT NULL,
  activo          TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_marca_codigo (empresa_id, codigo),
  KEY ix_marca_empresa_nombre (empresa_id, nombre),
  CONSTRAINT fk_marca_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id)
) ENGINE=InnoDB COMMENT='Marcas de producto';

CREATE TABLE tipo_proveedor (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  empresa_id      BIGINT UNSIGNED NOT NULL,
  codigo          VARCHAR(20)     NOT NULL,
  nombre          VARCHAR(150)    NOT NULL,
  activo          TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_tipo_proveedor_codigo (empresa_id, codigo),
  KEY ix_tipo_proveedor_empresa_nombre (empresa_id, nombre),
  CONSTRAINT fk_tipo_proveedor_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id)
) ENGINE=InnoDB COMMENT='Tipos de proveedor';

CREATE TABLE motivo_anulacion (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  empresa_id      BIGINT UNSIGNED NOT NULL,
  codigo          VARCHAR(20)     NOT NULL,
  nombre          VARCHAR(150)    NOT NULL,
  activo          TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_motivo_anulacion_codigo (empresa_id, codigo),
  KEY ix_motivo_anulacion_empresa_nombre (empresa_id, nombre),
  CONSTRAINT fk_motivo_anulacion_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id)
) ENGINE=InnoDB COMMENT='Motivos para anular documentos';

CREATE TABLE banco (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  empresa_id      BIGINT UNSIGNED NOT NULL,
  codigo          VARCHAR(20)     NOT NULL,
  nombre          VARCHAR(150)    NOT NULL,
  activo          TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_banco_codigo (empresa_id, codigo),
  KEY ix_banco_empresa_nombre (empresa_id, nombre),
  CONSTRAINT fk_banco_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id)
) ENGINE=InnoDB COMMENT='Bancos';

CREATE TABLE forma_pago (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  empresa_id      BIGINT UNSIGNED NOT NULL,
  codigo          VARCHAR(20)     NOT NULL,
  nombre          VARCHAR(150)    NOT NULL,
  tipo            VARCHAR(10)     NOT NULL,
  requiere_referencia TINYINT(1)  NOT NULL DEFAULT 0,
  activo          TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_forma_pago_codigo (empresa_id, codigo),
  KEY ix_forma_pago_empresa_nombre (empresa_id, nombre),
  CONSTRAINT fk_forma_pago_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id),
  CONSTRAINT ck_forma_pago_tipo CHECK (tipo IN ('EFECTIVO', 'BANCARIO', 'TARJETA', 'CREDITO'))
) ENGINE=InnoDB COMMENT='Formas de pago';

CREATE TABLE condicion_pago (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  empresa_id      BIGINT UNSIGNED NOT NULL,
  codigo          VARCHAR(20)     NOT NULL,
  nombre          VARCHAR(150)    NOT NULL,
  dias_credito    SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  activo          TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_condicion_pago_codigo (empresa_id, codigo),
  KEY ix_condicion_pago_empresa_nombre (empresa_id, nombre),
  CONSTRAINT fk_condicion_pago_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id),
  CONSTRAINT ck_condicion_pago_dias CHECK (dias_credito <= 365)
) ENGINE=InnoDB COMMENT='Condiciones de pago (dias de credito)';

-- Maestro de cuentas bancarias. No guarda saldos: los movimientos son de Caja y bancos (1F).
CREATE TABLE cuenta_bancaria (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  empresa_id      BIGINT UNSIGNED NOT NULL,
  codigo          VARCHAR(20)     NOT NULL,
  nombre          VARCHAR(150)    NOT NULL,
  banco_id        BIGINT UNSIGNED NOT NULL,
  moneda_id       BIGINT UNSIGNED NOT NULL,
  numero_cuenta   VARCHAR(40)     NOT NULL,
  tipo_cuenta     VARCHAR(10)     NOT NULL,
  titular         VARCHAR(150)    NULL,
  activo          TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_cuenta_bancaria_codigo (empresa_id, codigo),
  KEY ix_cuenta_bancaria_empresa_nombre (empresa_id, nombre),
  UNIQUE KEY uq_cuenta_bancaria_numero (empresa_id, banco_id, numero_cuenta),
  KEY ix_cuenta_bancaria_banco (banco_id),
  KEY ix_cuenta_bancaria_moneda (moneda_id),
  CONSTRAINT fk_cuenta_bancaria_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id),
  CONSTRAINT fk_cuenta_bancaria_banco FOREIGN KEY (banco_id) REFERENCES banco (id),
  CONSTRAINT fk_cuenta_bancaria_moneda FOREIGN KEY (moneda_id) REFERENCES moneda (id),
  CONSTRAINT ck_cuenta_bancaria_tipo CHECK (tipo_cuenta IN ('MONETARIA', 'AHORRO', 'OTRA'))
) ENGINE=InnoDB COMMENT='Cuentas bancarias de la empresa';

-- Tipos de documento: catalogo global y cerrado (cada codigo implica un comportamiento en el
-- codigo); la aplicacion solo permite cambiar nombre y estado.
CREATE TABLE tipo_documento (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  codigo          VARCHAR(30)     NOT NULL,
  nombre          VARCHAR(100)    NOT NULL,
  modulo          VARCHAR(30)     NOT NULL,
  activo          TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_tipo_documento_codigo (codigo)
) ENGINE=InnoDB COMMENT='Tipos de documento numerables (catalogo global)';

-- Series y correlativos. siguiente_correlativo solo lo mueve sp_siguiente_correlativo,
-- dentro de la transaccion del documento que consume el numero.
-- numero_maximo NULL = sin tope. La serie agotada (siguiente > numero_maximo) no emite mas.
-- sucursal_id / caja_id NULL = la serie sirve a toda la empresa / sucursal.
CREATE TABLE serie_documento (
  id                      BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  empresa_id              BIGINT UNSIGNED NOT NULL,
  sucursal_id             BIGINT UNSIGNED NULL,
  caja_id                 BIGINT UNSIGNED NULL,
  tipo_documento_id       BIGINT UNSIGNED NOT NULL,
  serie                   VARCHAR(10)     NOT NULL,
  prefijo                 VARCHAR(10)     NULL,
  incluir_anio            TINYINT(1)      NOT NULL DEFAULT 0,
  longitud                TINYINT UNSIGNED NOT NULL DEFAULT 6,
  siguiente_correlativo   BIGINT UNSIGNED NOT NULL DEFAULT 1,
  numero_maximo           BIGINT UNSIGNED NULL,
  vigente_desde           DATE            NOT NULL,
  vigente_hasta           DATE            NULL,
  activo          TINYINT(1)      NOT NULL DEFAULT 1,
  version_fila    INT UNSIGNED    NOT NULL DEFAULT 1,
  creado_en       DATETIME(6)     NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  creado_por      BIGINT UNSIGNED NULL,
  modificado_en   DATETIME(6)     NULL,
  modificado_por  BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_serie_documento (empresa_id, tipo_documento_id, serie),
  KEY ix_serie_documento_tipo (tipo_documento_id),
  KEY ix_serie_documento_sucursal (sucursal_id),
  KEY ix_serie_documento_caja (caja_id),
  CONSTRAINT fk_serie_documento_empresa FOREIGN KEY (empresa_id) REFERENCES empresa (id),
  CONSTRAINT fk_serie_documento_sucursal FOREIGN KEY (sucursal_id) REFERENCES sucursal (id),
  CONSTRAINT fk_serie_documento_caja FOREIGN KEY (caja_id) REFERENCES caja (id),
  CONSTRAINT fk_serie_documento_tipo FOREIGN KEY (tipo_documento_id) REFERENCES tipo_documento (id),
  CONSTRAINT ck_serie_documento_longitud CHECK (longitud BETWEEN 1 AND 12),
  CONSTRAINT ck_serie_documento_siguiente CHECK (siguiente_correlativo >= 1),
  CONSTRAINT ck_serie_documento_maximo CHECK (numero_maximo IS NULL OR numero_maximo >= 1),
  CONSTRAINT ck_serie_documento_vigencia CHECK (vigente_hasta IS NULL OR vigente_hasta >= vigente_desde)
) ENGINE=InnoDB COMMENT='Series y correlativos de documentos';

-- ===== PROCEDIMIENTOS =====

-- Siguiente numero de una serie. Es una operacion atomica: bloquea la fila de la serie
-- (FOR UPDATE), la avanza y devuelve el numero asignado junto con los datos que el
-- backend necesita para validarlo (activa, vigencia, tope) y formatearlo. NO decide
-- nada: si el backend rechaza la serie, revierte la transaccion del documento y con
-- ella el avance. Debe llamarse dentro de la transaccion del documento.
-- Sin filas en el resultado = la serie no existe para esa empresa.
DELIMITER $$

CREATE PROCEDURE sp_siguiente_correlativo(
  IN p_empresa_id BIGINT UNSIGNED,
  IN p_serie_documento_id BIGINT UNSIGNED
)
BEGIN
  DECLARE v_numero BIGINT UNSIGNED DEFAULT NULL;

  SELECT siguiente_correlativo INTO v_numero
  FROM serie_documento
  WHERE id = p_serie_documento_id AND empresa_id = p_empresa_id
  FOR UPDATE;

  IF v_numero IS NOT NULL THEN
    UPDATE serie_documento
    SET siguiente_correlativo = v_numero + 1
    WHERE id = p_serie_documento_id;
  END IF;

  SELECT s.id AS serie_documento_id, s.serie, s.prefijo, s.incluir_anio, s.longitud,
         s.numero_maximo, s.vigente_desde, s.vigente_hasta, s.activo, v_numero AS numero
  FROM serie_documento s
  WHERE s.id = p_serie_documento_id AND s.empresa_id = p_empresa_id AND v_numero IS NOT NULL;
END$$

DELIMITER ;

-- ===== SEEDS =====

START TRANSACTION;

-- Catalogos globales de la etapa 1A: monedas, unidades estandar y tipos de documento.
INSERT INTO moneda (codigo, nombre, simbolo, decimales) VALUES
  ('GTQ', 'Quetzal',               'Q', 2),
  ('USD', 'Dólar estadounidense',  '$', 2);

INSERT INTO unidad_medida (empresa_id, codigo, nombre, magnitud, decimales) VALUES
  (NULL, 'UNI', 'Unidad',      'CANTIDAD', 0),
  (NULL, 'PAR', 'Par',         'CANTIDAD', 0),
  (NULL, 'DOC', 'Docena',      'CANTIDAD', 0),
  (NULL, 'KG',  'Kilogramo',   'PESO',     3),
  (NULL, 'G',   'Gramo',       'PESO',     3),
  (NULL, 'LB',  'Libra',       'PESO',     3),
  (NULL, 'OZ',  'Onza',        'PESO',     3),
  (NULL, 'QQ',  'Quintal',     'PESO',     3),
  (NULL, 'L',   'Litro',       'VOLUMEN',  3),
  (NULL, 'ML',  'Mililitro',   'VOLUMEN',  3),
  (NULL, 'GAL', 'Galón',       'VOLUMEN',  3),
  (NULL, 'M',   'Metro',       'LONGITUD', 2),
  (NULL, 'CM',  'Centímetro',  'LONGITUD', 2),
  (NULL, 'FT',  'Pie',         'LONGITUD', 2),
  (NULL, 'IN',  'Pulgada',     'LONGITUD', 2);

-- Conversiones estandar: 1 origen = factor destino. Forman un arbol por magnitud
-- (sin ciclos). KG-LB es aproximada a 6 decimales (1 kg = 2.2046226 lb).
INSERT INTO unidad_conversion (empresa_id, unidad_origen_id, unidad_destino_id, factor)
SELECT NULL, o.id, d.id, c.factor
FROM (
  SELECT 'DOC' AS origen, 'UNI' AS destino, 12 AS factor
  UNION ALL SELECT 'PAR', 'UNI', 2
  UNION ALL SELECT 'KG',  'G',   1000
  UNION ALL SELECT 'LB',  'OZ',  16
  UNION ALL SELECT 'QQ',  'LB',  100
  UNION ALL SELECT 'KG',  'LB',  2.204623
  UNION ALL SELECT 'L',   'ML',  1000
  UNION ALL SELECT 'GAL', 'L',   3.785412
  UNION ALL SELECT 'M',   'CM',  100
  UNION ALL SELECT 'FT',  'M',   0.3048
  UNION ALL SELECT 'IN',  'CM',  2.54
) c
JOIN unidad_medida o ON o.codigo = c.origen AND o.empresa_id IS NULL
JOIN unidad_medida d ON d.codigo = c.destino AND d.empresa_id IS NULL;

INSERT INTO tipo_documento (codigo, nombre, modulo) VALUES
  ('COTIZACION',          'Cotización',                 'VENTAS'),
  ('PEDIDO_VENTA',        'Pedido de venta',            'VENTAS'),
  ('FACTURA_VENTA',       'Factura de venta',           'VENTAS'),
  ('NOTA_CREDITO_VENTA',  'Nota de crédito de venta',   'VENTAS'),
  ('NOTA_DEBITO_VENTA',   'Nota de débito de venta',    'VENTAS'),
  ('ORDEN_COMPRA',        'Orden de compra',            'COMPRAS'),
  ('RECEPCION_COMPRA',    'Recepción de compra',        'COMPRAS'),
  ('FACTURA_COMPRA',      'Factura de compra',          'COMPRAS'),
  ('NOTA_CREDITO_COMPRA', 'Nota de crédito de compra',  'COMPRAS'),
  ('AJUSTE_INVENTARIO',   'Ajuste de inventario',       'INVENTARIO'),
  ('TRASLADO_INVENTARIO', 'Traslado de inventario',     'INVENTARIO'),
  ('RECIBO_INGRESO',      'Recibo de ingreso',          'CAJA'),
  ('COMPROBANTE_EGRESO',  'Comprobante de egreso',      'CAJA');

-- Empresas existentes: moneda local GTQ y modulo Catalogos liberado.
UPDATE empresa SET moneda_local_id = (SELECT id FROM moneda WHERE codigo = 'GTQ');

UPDATE modulo SET estado_global = 'LIBERADO', es_base = 1, version_fila = version_fila + 1,
  modificado_en = UTC_TIMESTAMP(6)
WHERE codigo = 'CATALOGOS';

UPDATE modulo_empresa me
JOIN modulo m ON m.id = me.modulo_id
SET me.estado = 'LIBERADO', me.motivo = 'Etapa 1A: catálogos base', me.version_fila = me.version_fila + 1,
    me.modificado_en = UTC_TIMESTAMP(6)
WHERE m.codigo = 'CATALOGOS';

-- ----- Etapa 1A: catalogos por empresa (cada empresa existente recibe los suyos) -----
INSERT INTO tipo_producto (empresa_id, codigo, nombre, maneja_inventario)
SELECT e.id, v.codigo, v.nombre, v.maneja_inventario
FROM empresa e
CROSS JOIN (
  SELECT 'BIEN' AS codigo, 'Bien' AS nombre, 1 AS maneja_inventario
  UNION ALL SELECT 'SERVICIO', 'Servicio', 0
) v;

INSERT INTO tipo_proveedor (empresa_id, codigo, nombre)
SELECT e.id, v.codigo, v.nombre
FROM empresa e
CROSS JOIN (
  SELECT 'BIENES' AS codigo, 'Proveedor de bienes' AS nombre
  UNION ALL SELECT 'SERVICIOS', 'Proveedor de servicios'
) v;

INSERT INTO forma_pago (empresa_id, codigo, nombre, tipo, requiere_referencia)
SELECT e.id, v.codigo, v.nombre, v.tipo, v.requiere_referencia
FROM empresa e
CROSS JOIN (
  SELECT 'EFECTIVO' AS codigo, 'Efectivo' AS nombre, 'EFECTIVO' AS tipo, 0 AS requiere_referencia
  UNION ALL SELECT 'CHEQUE',        'Cheque',                  'BANCARIO', 1
  UNION ALL SELECT 'TRANSFERENCIA', 'Transferencia bancaria', 'BANCARIO', 1
  UNION ALL SELECT 'TARJETA',       'Tarjeta de crédito o débito', 'TARJETA', 1
  UNION ALL SELECT 'CREDITO',       'Crédito',                 'CREDITO',  0
) v;

INSERT INTO condicion_pago (empresa_id, codigo, nombre, dias_credito)
SELECT e.id, v.codigo, v.nombre, v.dias_credito
FROM empresa e
CROSS JOIN (
  SELECT 'CONTADO' AS codigo, 'Contado' AS nombre, 0 AS dias_credito
  UNION ALL SELECT 'CREDITO15', 'Crédito a 15 días', 15
  UNION ALL SELECT 'CREDITO30', 'Crédito a 30 días', 30
  UNION ALL SELECT 'CREDITO60', 'Crédito a 60 días', 60
) v;

INSERT INTO motivo_anulacion (empresa_id, codigo, nombre)
SELECT e.id, v.codigo, v.nombre
FROM empresa e
CROSS JOIN (
  SELECT 'ERROR' AS codigo, 'Error de captura' AS nombre
  UNION ALL SELECT 'CLIENTE',   'Solicitud del cliente'
  UNION ALL SELECT 'DUPLICADO', 'Documento duplicado'
  UNION ALL SELECT 'OTRO',      'Otro motivo'
) v;

-- IVA vigente desde antes de cualquier documento; un cambio de tasa es una vigencia nueva.
INSERT INTO impuesto (empresa_id, codigo, nombre, tipo, porcentaje, vigente_desde)
SELECT e.id, v.codigo, v.nombre, 'IVA', v.porcentaje, '2000-01-01'
FROM empresa e
CROSS JOIN (
  SELECT 'IVA' AS codigo, 'IVA 12 %' AS nombre, 12 AS porcentaje
  UNION ALL SELECT 'EXENTO', 'Exento de IVA', 0
) v;

-- Permisos de la etapa 1A: recursos con las cinco acciones...
INSERT INTO permiso (modulo_id, codigo, recurso, accion, nombre)
SELECT m.id, CONCAT(r.modulo, '_', r.recurso, '_', a.accion), r.recurso, a.accion, CONCAT(a.verbo, ' ', r.etiqueta)
FROM (
  SELECT 'ORGANIZACION' AS modulo, 'BODEGAS' AS recurso, 'bodegas' AS etiqueta
  UNION ALL SELECT 'ORGANIZACION', 'CAJAS',            'cajas'
  UNION ALL SELECT 'ORGANIZACION', 'DEPARTAMENTOS',    'departamentos'
  UNION ALL SELECT 'ORGANIZACION', 'CENTROSCOSTO',     'centros de costo'
  UNION ALL SELECT 'CATALOGOS',    'TIPOSPRODUCTO',    'tipos de producto'
  UNION ALL SELECT 'CATALOGOS',    'CATEGORIAS',       'categorías'
  UNION ALL SELECT 'CATALOGOS',    'MARCAS',           'marcas'
  UNION ALL SELECT 'CATALOGOS',    'TIPOSPROVEEDOR',   'tipos de proveedor'
  UNION ALL SELECT 'CATALOGOS',    'FORMASPAGO',       'formas de pago'
  UNION ALL SELECT 'CATALOGOS',    'CONDICIONESPAGO',  'condiciones de pago'
  UNION ALL SELECT 'CATALOGOS',    'MOTIVOSANULACION', 'motivos de anulación'
  UNION ALL SELECT 'CATALOGOS',    'BANCOS',           'bancos'
  UNION ALL SELECT 'CATALOGOS',    'CUENTASBANCARIAS', 'cuentas bancarias'
  UNION ALL SELECT 'CATALOGOS',    'MONEDAS',          'monedas'
  UNION ALL SELECT 'CATALOGOS',    'IMPUESTOS',        'impuestos'
  UNION ALL SELECT 'CATALOGOS',    'UNIDADES',         'unidades de medida'
) r
CROSS JOIN (
  SELECT 'VER' AS accion, 'Ver' AS verbo
  UNION ALL SELECT 'CREAR',    'Crear'
  UNION ALL SELECT 'EDITAR',   'Editar'
  UNION ALL SELECT 'ELIMINAR', 'Desactivar'
  UNION ALL SELECT 'EXPORTAR', 'Exportar'
) a
JOIN modulo m ON m.codigo = r.modulo;

-- ...y los que no admiten todas las acciones.
INSERT INTO permiso (modulo_id, codigo, recurso, accion, nombre)
SELECT m.id, p.codigo, p.recurso, p.accion, p.nombre
FROM (
  SELECT 'ORGANIZACION' AS modulo, 'ORGANIZACION_EMPRESAS_EDITAR' AS codigo, 'EMPRESAS' AS recurso, 'EDITAR' AS accion, 'Editar los datos de la empresa' AS nombre
  UNION ALL SELECT 'CATALOGOS', 'CATALOGOS_CONVERSIONES_VER',      'CONVERSIONES',  'VER',      'Ver conversiones de unidades'
  UNION ALL SELECT 'CATALOGOS', 'CATALOGOS_CONVERSIONES_CREAR',    'CONVERSIONES',  'CREAR',    'Crear conversiones de unidades'
  UNION ALL SELECT 'CATALOGOS', 'CATALOGOS_CONVERSIONES_EDITAR',   'CONVERSIONES',  'EDITAR',   'Editar conversiones de unidades'
  UNION ALL SELECT 'CATALOGOS', 'CATALOGOS_CONVERSIONES_ELIMINAR', 'CONVERSIONES',  'ELIMINAR', 'Desactivar conversiones de unidades'
  UNION ALL SELECT 'CATALOGOS', 'CATALOGOS_TIPOSCAMBIO_VER',       'TIPOSCAMBIO',   'VER',      'Ver tipos de cambio'
  UNION ALL SELECT 'CATALOGOS', 'CATALOGOS_TIPOSCAMBIO_CREAR',     'TIPOSCAMBIO',   'CREAR',    'Registrar tipos de cambio'
  UNION ALL SELECT 'CATALOGOS', 'CATALOGOS_TIPOSCAMBIO_EDITAR',    'TIPOSCAMBIO',   'EDITAR',   'Corregir tipos de cambio'
  UNION ALL SELECT 'CATALOGOS', 'CATALOGOS_TIPOSCAMBIO_EXPORTAR',  'TIPOSCAMBIO',   'EXPORTAR', 'Exportar tipos de cambio'
  UNION ALL SELECT 'CATALOGOS', 'CATALOGOS_TIPOSDOCUMENTO_VER',    'TIPOSDOCUMENTO','VER',      'Ver tipos de documento'
  UNION ALL SELECT 'CATALOGOS', 'CATALOGOS_TIPOSDOCUMENTO_EDITAR', 'TIPOSDOCUMENTO','EDITAR',   'Editar tipos de documento'
  UNION ALL SELECT 'CATALOGOS', 'CATALOGOS_SERIES_VER',            'SERIES',        'VER',      'Ver series y correlativos'
  UNION ALL SELECT 'CATALOGOS', 'CATALOGOS_SERIES_CREAR',          'SERIES',        'CREAR',    'Crear series y correlativos'
  UNION ALL SELECT 'CATALOGOS', 'CATALOGOS_SERIES_EDITAR',         'SERIES',        'EDITAR',   'Editar series (no el correlativo en curso)'
  UNION ALL SELECT 'CATALOGOS', 'CATALOGOS_SERIES_ELIMINAR',       'SERIES',        'ELIMINAR', 'Desactivar series'
  UNION ALL SELECT 'CATALOGOS', 'CATALOGOS_CONFIGURACION_VER',     'CONFIGURACION', 'VER',      'Ver la configuración general de negocio'
  UNION ALL SELECT 'CATALOGOS', 'CATALOGOS_CONFIGURACION_EDITAR',  'CONFIGURACION', 'EDITAR',   'Editar la configuración general de negocio'
  UNION ALL SELECT 'CATALOGOS', 'CATALOGOS_FECHABLOQUEO_EDITAR',   'FECHABLOQUEO',  'EDITAR',   'Cambiar la fecha de bloqueo de documentos'
) p
JOIN modulo m ON m.codigo = p.modulo;

-- Menu: pantallas nuevas de Organizacion, nodo Catalogos y sus grupos.
INSERT INTO menu (padre_id, modulo_id, tipo, codigo, nombre, icono, ruta, orden, permiso_requerido_id)
SELECT NULL, m.id, 'MODULO', 'MNU_CATALOGOS', 'Catálogos', 'library', NULL, 40, NULL
FROM modulo m WHERE m.codigo = 'CATALOGOS';

INSERT INTO menu (padre_id, modulo_id, tipo, codigo, nombre, icono, ruta, orden, permiso_requerido_id)
SELECT padre.id, padre.modulo_id, 'GRUPO', g.codigo, g.nombre, g.icono, NULL, g.orden, NULL
FROM (
  SELECT 'GRP_CAT_PRODUCTOS' AS codigo, 'Productos' AS nombre, 'package' AS icono, 10 AS orden
  UNION ALL SELECT 'GRP_CAT_COMERCIAL',  'Comercial',  'handshake',   20
  UNION ALL SELECT 'GRP_CAT_FINANZAS',   'Finanzas',   'landmark',    30
  UNION ALL SELECT 'GRP_CAT_DOCUMENTOS', 'Documentos', 'file-text',   40
) g
JOIN menu padre ON padre.codigo = 'MNU_CATALOGOS';

INSERT INTO menu (padre_id, modulo_id, tipo, codigo, nombre, icono, ruta, orden, permiso_requerido_id)
SELECT padre.id, padre.modulo_id, 'PANTALLA', n.codigo, n.nombre, n.icono, n.ruta, n.orden, p.id
FROM (
  SELECT 'MNU_ORGANIZACION' AS padre, 'MNU_ORG_EMPRESA' AS codigo, 'Empresa' AS nombre, 'building' AS icono,
         '/organizacion/empresa' AS ruta, 5 AS orden, 'ORGANIZACION_EMPRESAS_VER' AS permiso
  UNION ALL SELECT 'MNU_ORGANIZACION', 'MNU_ORG_BODEGAS',       'Bodegas',          'warehouse',   '/organizacion/bodegas',        20, 'ORGANIZACION_BODEGAS_VER'
  UNION ALL SELECT 'MNU_ORGANIZACION', 'MNU_ORG_CAJAS',         'Cajas',            'wallet',      '/organizacion/cajas',          30, 'ORGANIZACION_CAJAS_VER'
  UNION ALL SELECT 'MNU_ORGANIZACION', 'MNU_ORG_DEPARTAMENTOS', 'Departamentos',    'network',     '/organizacion/departamentos',  40, 'ORGANIZACION_DEPARTAMENTOS_VER'
  UNION ALL SELECT 'MNU_ORGANIZACION', 'MNU_ORG_CENTROS',       'Centros de costo', 'target',      '/organizacion/centros-costo',  50, 'ORGANIZACION_CENTROSCOSTO_VER'
  UNION ALL SELECT 'GRP_CAT_PRODUCTOS', 'MNU_CAT_TIPOSPRODUCTO', 'Tipos de producto', 'tag',        '/catalogos/tipos-producto',   10, 'CATALOGOS_TIPOSPRODUCTO_VER'
  UNION ALL SELECT 'GRP_CAT_PRODUCTOS', 'MNU_CAT_CATEGORIAS',    'Categorías',        'folder-tree','/catalogos/categorias',       20, 'CATALOGOS_CATEGORIAS_VER'
  UNION ALL SELECT 'GRP_CAT_PRODUCTOS', 'MNU_CAT_MARCAS',        'Marcas',            'badge',      '/catalogos/marcas',           30, 'CATALOGOS_MARCAS_VER'
  UNION ALL SELECT 'GRP_CAT_PRODUCTOS', 'MNU_CAT_UNIDADES',      'Unidades de medida','ruler',      '/catalogos/unidades',         40, 'CATALOGOS_UNIDADES_VER'
  UNION ALL SELECT 'GRP_CAT_PRODUCTOS', 'MNU_CAT_CONVERSIONES',  'Conversiones',      'arrow-left-right', '/catalogos/conversiones', 50, 'CATALOGOS_CONVERSIONES_VER'
  UNION ALL SELECT 'GRP_CAT_COMERCIAL', 'MNU_CAT_TIPOSPROVEEDOR','Tipos de proveedor','truck',      '/catalogos/tipos-proveedor',  10, 'CATALOGOS_TIPOSPROVEEDOR_VER'
  UNION ALL SELECT 'GRP_CAT_COMERCIAL', 'MNU_CAT_FORMASPAGO',    'Formas de pago',    'credit-card','/catalogos/formas-pago',      20, 'CATALOGOS_FORMASPAGO_VER'
  UNION ALL SELECT 'GRP_CAT_COMERCIAL', 'MNU_CAT_CONDICIONES',   'Condiciones de pago','calendar-clock','/catalogos/condiciones-pago', 30, 'CATALOGOS_CONDICIONESPAGO_VER'
  UNION ALL SELECT 'GRP_CAT_COMERCIAL', 'MNU_CAT_MOTIVOS',       'Motivos de anulación','ban',      '/catalogos/motivos-anulacion',40, 'CATALOGOS_MOTIVOSANULACION_VER'
  UNION ALL SELECT 'GRP_CAT_FINANZAS',  'MNU_CAT_MONEDAS',       'Monedas',           'coins',      '/catalogos/monedas',          10, 'CATALOGOS_MONEDAS_VER'
  UNION ALL SELECT 'GRP_CAT_FINANZAS',  'MNU_CAT_TIPOSCAMBIO',   'Tipos de cambio',   'trending-up','/catalogos/tipos-cambio',     20, 'CATALOGOS_TIPOSCAMBIO_VER'
  UNION ALL SELECT 'GRP_CAT_FINANZAS',  'MNU_CAT_IMPUESTOS',     'Impuestos',         'percent',    '/catalogos/impuestos',        30, 'CATALOGOS_IMPUESTOS_VER'
  UNION ALL SELECT 'GRP_CAT_FINANZAS',  'MNU_CAT_BANCOS',        'Bancos',            'landmark',   '/catalogos/bancos',           40, 'CATALOGOS_BANCOS_VER'
  UNION ALL SELECT 'GRP_CAT_FINANZAS',  'MNU_CAT_CUENTAS',       'Cuentas bancarias', 'piggy-bank', '/catalogos/cuentas-bancarias',50, 'CATALOGOS_CUENTASBANCARIAS_VER'
  UNION ALL SELECT 'GRP_CAT_DOCUMENTOS','MNU_CAT_TIPOSDOC',      'Tipos de documento','files',      '/catalogos/tipos-documento',  10, 'CATALOGOS_TIPOSDOCUMENTO_VER'
  UNION ALL SELECT 'GRP_CAT_DOCUMENTOS','MNU_CAT_SERIES',        'Series y correlativos','list-ordered','/catalogos/series',       20, 'CATALOGOS_SERIES_VER'
  UNION ALL SELECT 'MNU_CATALOGOS',     'MNU_CAT_CONFIGURACION', 'Configuración',     'settings',   '/catalogos/configuracion',    50, 'CATALOGOS_CONFIGURACION_VER'
) n
JOIN menu padre ON padre.codigo = n.padre
JOIN permiso p ON p.codigo = n.permiso;

-- El administrador recibe los permisos nuevos (no hay atajo "superadmin" en el codigo).
INSERT INTO rol_permiso (rol_id, permiso_id)
SELECT r.id, p.id
FROM rol r
CROSS JOIN permiso p
WHERE r.codigo = 'ADMINISTRADOR'
  AND NOT EXISTS (SELECT 1 FROM rol_permiso rp WHERE rp.rol_id = r.id AND rp.permiso_id = p.id);

-- Invalida la cache de autorizacion de las instancias en marcha.
UPDATE seguridad_version SET version = version + 1, modificado_en = UTC_TIMESTAMP(6) WHERE id = 1;

COMMIT;

-- ===== RELACIONES DIFERIDAS =====

ALTER TABLE empresa
  MODIFY moneda_local_id BIGINT UNSIGNED NOT NULL,
  ADD KEY ix_empresa_moneda_local (moneda_local_id),
  ADD CONSTRAINT ck_empresa_regimen CHECK (regimen_tributario IN ('GENERAL', 'PEQUENO_CONTRIBUYENTE')),
  ADD CONSTRAINT fk_empresa_moneda_local FOREIGN KEY (moneda_local_id) REFERENCES moneda (id);

-- =====================================================================
-- Fin de la actualizacion 0.2.0
-- =====================================================================
