import type { CampoCatalogo, ColumnaCatalogo, ConfigCatalogo } from "@/components/catalogos/config";

// Configuración de cada pantalla de catálogo. Los permisos y rutas deben coincidir con los del backend
// (ERP.Api/Controllers/Catalogos y Organizacion). Cambiar un DTO obliga a revisar su entrada aquí.

const codigo = (max = 20): CampoCatalogo => ({
  nombre: "codigo",
  etiqueta: "Código",
  tipo: "texto",
  max,
  patron: /^[A-Za-z0-9_-]+$/,
  mensajePatron: "Solo letras, números, guion y guion bajo.",
  mayusculas: true,
  soloCreacion: true,
  ayuda: "Identifica al registro y no se podrá cambiar después.",
});

const nombre = (max = 150): CampoCatalogo => ({ nombre: "nombre", etiqueta: "Nombre", tipo: "texto", max });

const colCodigo: ColumnaCatalogo = { id: "codigo", encabezado: "Código", ordenable: true };
const colNombre: ColumnaCatalogo = { id: "nombre", encabezado: "Nombre", ordenable: true };

const SOLO_CODIGO_Y_NOMBRE = { columnas: [colCodigo, colNombre], campos: [codigo(), nombre()] };

export const bodegas: ConfigCatalogo = {
  clave: "bodegas",
  titulo: "Bodegas",
  descripcion: "Bodegas de inventario de cada sucursal.",
  singular: "bodega",
  femenino: true,
  recurso: "organizacion/bodegas",
  permiso: "ORGANIZACION_BODEGAS",
  ordenInicial: "nombre",
  columnas: [colCodigo, colNombre, { id: "sucursalNombre", encabezado: "Sucursal", ordenable: true }, { id: "descripcion", encabezado: "Descripción" }],
  campos: [
    codigo(),
    nombre(),
    { nombre: "sucursalId", etiqueta: "Sucursal", tipo: "lista", fuente: "sucursales", numerico: true, soloCreacion: true },
    { nombre: "descripcion", etiqueta: "Descripción", tipo: "texto", max: 300, opcional: true },
  ],
};

export const cajas: ConfigCatalogo = {
  clave: "cajas",
  titulo: "Cajas",
  descripcion: "Puntos de cobro de cada sucursal.",
  singular: "caja",
  femenino: true,
  recurso: "organizacion/cajas",
  permiso: "ORGANIZACION_CAJAS",
  ordenInicial: "nombre",
  columnas: [
    colCodigo,
    colNombre,
    { id: "sucursalNombre", encabezado: "Sucursal", ordenable: true },
    { id: "bodegaNombre", encabezado: "Bodega", ordenable: true },
  ],
  campos: [
    codigo(),
    nombre(),
    { nombre: "sucursalId", etiqueta: "Sucursal", tipo: "lista", fuente: "sucursales", numerico: true, soloCreacion: true },
    {
      nombre: "bodegaId",
      etiqueta: "Bodega",
      tipo: "lista",
      fuente: "bodegas",
      numerico: true,
      opcional: true,
      ayuda: "Debe ser de la misma sucursal que la caja.",
    },
  ],
};

export const departamentos: ConfigCatalogo = {
  clave: "departamentos",
  titulo: "Departamentos",
  descripcion: "Departamentos de la empresa.",
  singular: "departamento",
  femenino: false,
  recurso: "organizacion/departamentos",
  permiso: "ORGANIZACION_DEPARTAMENTOS",
  ordenInicial: "nombre",
  ...SOLO_CODIGO_Y_NOMBRE,
};

export const centrosCosto: ConfigCatalogo = {
  clave: "centros-costo",
  titulo: "Centros de costo",
  descripcion: "Estructura de centros de costo; la contabilidad que los usa llega en la fase 2.",
  singular: "centro de costo",
  femenino: false,
  recurso: "organizacion/centros-costo",
  permiso: "ORGANIZACION_CENTROSCOSTO",
  ordenInicial: "nombre",
  columnas: [colCodigo, colNombre, { id: "departamentoNombre", encabezado: "Departamento", ordenable: true }],
  campos: [
    codigo(),
    nombre(),
    { nombre: "departamentoId", etiqueta: "Departamento", tipo: "lista", fuente: "departamentos", numerico: true, opcional: true },
  ],
};

export const tiposProducto: ConfigCatalogo = {
  clave: "tipos-producto",
  titulo: "Tipos de producto",
  descripcion: "Clasifican los productos; los servicios no manejan inventario.",
  singular: "tipo de producto",
  femenino: false,
  recurso: "catalogos/tipos-producto",
  permiso: "CATALOGOS_TIPOSPRODUCTO",
  ordenInicial: "nombre",
  columnas: [colCodigo, colNombre, { id: "manejaInventario", encabezado: "Maneja inventario", formato: "sino" }],
  campos: [codigo(), nombre(), { nombre: "manejaInventario", etiqueta: "Maneja inventario", tipo: "booleano", porDefecto: true }],
};

export const categorias: ConfigCatalogo = {
  clave: "categorias",
  titulo: "Categorías",
  descripcion: "Categorías de producto.",
  singular: "categoría",
  femenino: true,
  recurso: "catalogos/categorias",
  permiso: "CATALOGOS_CATEGORIAS",
  ordenInicial: "nombre",
  ...SOLO_CODIGO_Y_NOMBRE,
};

export const marcas: ConfigCatalogo = {
  clave: "marcas",
  titulo: "Marcas",
  descripcion: "Marcas de producto.",
  singular: "marca",
  femenino: true,
  recurso: "catalogos/marcas",
  permiso: "CATALOGOS_MARCAS",
  ordenInicial: "nombre",
  ...SOLO_CODIGO_Y_NOMBRE,
};

export const unidadesMedida: ConfigCatalogo = {
  clave: "unidades-medida",
  titulo: "Unidades de medida",
  descripcion: "Las unidades estándar del sistema son de solo lectura; cada empresa puede agregar las suyas.",
  singular: "unidad de medida",
  femenino: true,
  recurso: "catalogos/unidades-medida",
  permiso: "CATALOGOS_UNIDADES",
  ordenInicial: "magnitud",
  columnas: [colCodigo, colNombre, { id: "magnitud", encabezado: "Magnitud", ordenable: true }, { id: "decimales", encabezado: "Decimales" }],
  campos: [
    codigo(10),
    nombre(60),
    {
      nombre: "magnitud",
      etiqueta: "Magnitud",
      tipo: "lista",
      opciones: [
        { valor: "CANTIDAD", etiqueta: "Cantidad" },
        { valor: "PESO", etiqueta: "Peso" },
        { valor: "VOLUMEN", etiqueta: "Volumen" },
        { valor: "LONGITUD", etiqueta: "Longitud" },
      ],
      soloCreacion: true,
      porDefecto: "CANTIDAD",
      ayuda: "Solo se pueden convertir unidades de la misma magnitud.",
    },
    { nombre: "decimales", etiqueta: "Decimales permitidos", tipo: "entero", minimo: 0, maximo: 6, porDefecto: "0" },
  ],
  soloLectura: (fila) => fila.empresaId === null,
};

export const conversionesUnidad: ConfigCatalogo = {
  clave: "conversiones-unidad",
  titulo: "Conversiones de unidades",
  descripcion: "1 unidad de origen equivale a «factor» unidades de destino. La conversión inversa se deduce sola.",
  singular: "conversión",
  femenino: true,
  recurso: "catalogos/conversiones-unidad",
  permiso: "CATALOGOS_CONVERSIONES",
  ordenInicial: "magnitud",
  marcadorBusqueda: "Buscar por unidad",
  sinExportar: true,
  columnas: [
    { id: "unidadOrigenCodigo", encabezado: "Origen", ordenable: true },
    { id: "unidadDestinoCodigo", encabezado: "Destino", ordenable: true },
    { id: "magnitud", encabezado: "Magnitud", ordenable: true },
    { id: "factor", encabezado: "Factor", ordenable: true, formato: "tasa" },
  ],
  campos: [
    { nombre: "unidadOrigenId", etiqueta: "Unidad de origen", tipo: "lista", fuente: "unidades", numerico: true, soloCreacion: true },
    { nombre: "unidadDestinoId", etiqueta: "Unidad de destino", tipo: "lista", fuente: "unidades", numerico: true, soloCreacion: true },
    { nombre: "factor", etiqueta: "Factor", tipo: "decimal", decimales: 6, minimo: 0.000001, ayuda: "Hasta 6 decimales." },
  ],
  soloLectura: (fila) => fila.empresaId === null,
  campoNombre: (fila) => `${String(fila.unidadOrigenCodigo)} → ${String(fila.unidadDestinoCodigo)}`,
};

export const tiposProveedor: ConfigCatalogo = {
  clave: "tipos-proveedor",
  titulo: "Tipos de proveedor",
  descripcion: "Clasificación de proveedores.",
  singular: "tipo de proveedor",
  femenino: false,
  recurso: "catalogos/tipos-proveedor",
  permiso: "CATALOGOS_TIPOSPROVEEDOR",
  ordenInicial: "nombre",
  ...SOLO_CODIGO_Y_NOMBRE,
};

export const formasPago: ConfigCatalogo = {
  clave: "formas-pago",
  titulo: "Formas de pago",
  descripcion: "Cómo se cobra o se paga un documento.",
  singular: "forma de pago",
  femenino: true,
  recurso: "catalogos/formas-pago",
  permiso: "CATALOGOS_FORMASPAGO",
  ordenInicial: "nombre",
  columnas: [colCodigo, colNombre, { id: "tipo", encabezado: "Tipo" }, { id: "requiereReferencia", encabezado: "Pide referencia", formato: "sino" }],
  campos: [
    codigo(),
    nombre(),
    {
      nombre: "tipo",
      etiqueta: "Tipo",
      tipo: "lista",
      opciones: [
        { valor: "EFECTIVO", etiqueta: "Efectivo" },
        { valor: "BANCARIO", etiqueta: "Bancario (cheque, transferencia)" },
        { valor: "TARJETA", etiqueta: "Tarjeta" },
        { valor: "CREDITO", etiqueta: "Crédito" },
      ],
      porDefecto: "EFECTIVO",
    },
    { nombre: "requiereReferencia", etiqueta: "Pide número de referencia", tipo: "booleano" },
  ],
};

export const condicionesPago: ConfigCatalogo = {
  clave: "condiciones-pago",
  titulo: "Condiciones de pago",
  descripcion: "Plazos de crédito que se ofrecen o se reciben.",
  singular: "condición de pago",
  femenino: true,
  recurso: "catalogos/condiciones-pago",
  permiso: "CATALOGOS_CONDICIONESPAGO",
  ordenInicial: "diasCredito",
  columnas: [colCodigo, colNombre, { id: "diasCredito", encabezado: "Días de crédito", ordenable: true }],
  campos: [codigo(), nombre(), { nombre: "diasCredito", etiqueta: "Días de crédito", tipo: "entero", minimo: 0, maximo: 365, porDefecto: "0" }],
};

export const motivosAnulacion: ConfigCatalogo = {
  clave: "motivos-anulacion",
  titulo: "Motivos de anulación",
  descripcion: "Motivos que se piden al anular un documento.",
  singular: "motivo de anulación",
  femenino: false,
  recurso: "catalogos/motivos-anulacion",
  permiso: "CATALOGOS_MOTIVOSANULACION",
  ordenInicial: "nombre",
  ...SOLO_CODIGO_Y_NOMBRE,
};

export const monedas: ConfigCatalogo = {
  clave: "monedas",
  titulo: "Monedas",
  descripcion: "Catálogo compartido por todas las empresas. La moneda local de la empresa se define en Organización › Empresa.",
  singular: "moneda",
  femenino: true,
  recurso: "catalogos/monedas",
  permiso: "CATALOGOS_MONEDAS",
  ordenInicial: "codigo",
  avisoBaja: "quedará inactiva y no se podrá seleccionar en documentos nuevos.",
  columnas: [colCodigo, colNombre, { id: "simbolo", encabezado: "Símbolo" }, { id: "decimales", encabezado: "Decimales" }],
  campos: [
    { ...codigo(3), max: 3, patron: /^[A-Za-z]{3}$/, mensajePatron: "El código es de 3 letras (ISO 4217).", ayuda: "Código ISO de 3 letras; no se podrá cambiar." },
    nombre(60),
    { nombre: "simbolo", etiqueta: "Símbolo", tipo: "texto", max: 5 },
    { nombre: "decimales", etiqueta: "Decimales", tipo: "entero", minimo: 0, maximo: 6, porDefecto: "2" },
  ],
};

export const tiposCambio: ConfigCatalogo = {
  clave: "tipos-cambio",
  titulo: "Tipos de cambio",
  descripcion: "Una tasa por moneda y fecha (unidades de moneda local por unidad de moneda extranjera). Para una fecha sin tasa rige la última anterior.",
  singular: "tipo de cambio",
  femenino: false,
  recurso: "catalogos/tipos-cambio",
  permiso: "CATALOGOS_TIPOSCAMBIO",
  ordenInicial: "-fecha",
  buscar: false,
  conActivo: false,
  sinBaja: true,
  filtros: [{ parametro: "monedaId", etiqueta: "Moneda", fuente: "monedas" }],
  columnas: [
    { id: "fecha", encabezado: "Fecha", ordenable: true, formato: "fecha" },
    { id: "monedaCodigo", encabezado: "Moneda", ordenable: true },
    { id: "tasa", encabezado: "Tasa", ordenable: true, formato: "tasa" },
  ],
  campos: [
    { nombre: "monedaId", etiqueta: "Moneda", tipo: "lista", fuente: "monedas", numerico: true, soloCreacion: true },
    { nombre: "fecha", etiqueta: "Fecha", tipo: "fecha", soloCreacion: true },
    { nombre: "tasa", etiqueta: "Tasa", tipo: "decimal", decimales: 8, minimo: 0.00000001, ayuda: "Hasta 8 decimales." },
  ],
  campoNombre: (fila) => `${String(fila.monedaCodigo)} del ${String(fila.fecha)}`,
};

export const impuestos: ConfigCatalogo = {
  clave: "impuestos",
  titulo: "Impuestos",
  descripcion: "Un cambio de tasa es una vigencia nueva: cierre la vigente (fecha «hasta») y cree la siguiente.",
  singular: "impuesto",
  femenino: false,
  recurso: "catalogos/impuestos",
  permiso: "CATALOGOS_IMPUESTOS",
  ordenInicial: "codigo",
  columnas: [
    colCodigo,
    colNombre,
    { id: "tipo", encabezado: "Tipo" },
    { id: "porcentaje", encabezado: "Porcentaje", ordenable: true, formato: "tasa" },
    { id: "vigenteDesde", encabezado: "Desde", ordenable: true, formato: "fecha" },
    { id: "vigenteHasta", encabezado: "Hasta", formato: "fecha" },
  ],
  campos: [
    codigo(),
    nombre(),
    {
      nombre: "tipo",
      etiqueta: "Tipo",
      tipo: "lista",
      opciones: [
        { valor: "IVA", etiqueta: "IVA" },
        { valor: "OTRO", etiqueta: "Otro impuesto" },
      ],
      soloCreacion: true,
      porDefecto: "IVA",
    },
    { nombre: "porcentaje", etiqueta: "Porcentaje (%)", tipo: "decimal", decimales: 6, minimo: 0, maximo: 100, soloCreacion: true },
    { nombre: "vigenteDesde", etiqueta: "Vigente desde", tipo: "fecha", soloCreacion: true },
    { nombre: "vigenteHasta", etiqueta: "Vigente hasta", tipo: "fecha", opcional: true, ayuda: "Vacío = sin fecha de fin." },
  ],
  campoNombre: (fila) => `${String(fila.codigo)} desde ${String(fila.vigenteDesde)}`,
};

export const bancos: ConfigCatalogo = {
  clave: "bancos",
  titulo: "Bancos",
  descripcion: "Bancos con los que opera la empresa.",
  singular: "banco",
  femenino: false,
  recurso: "catalogos/bancos",
  permiso: "CATALOGOS_BANCOS",
  ordenInicial: "nombre",
  ...SOLO_CODIGO_Y_NOMBRE,
};

export const cuentasBancarias: ConfigCatalogo = {
  clave: "cuentas-bancarias",
  titulo: "Cuentas bancarias",
  descripcion: "Maestro de cuentas. Los saldos y movimientos se manejan en Caja y bancos.",
  singular: "cuenta bancaria",
  femenino: true,
  recurso: "catalogos/cuentas-bancarias",
  permiso: "CATALOGOS_CUENTASBANCARIAS",
  ordenInicial: "nombre",
  marcadorBusqueda: "Buscar por código o nombre",
  columnas: [
    colCodigo,
    colNombre,
    { id: "bancoNombre", encabezado: "Banco", ordenable: true },
    { id: "monedaCodigo", encabezado: "Moneda", ordenable: true },
    { id: "numeroCuenta", encabezado: "Número", ordenable: true },
    { id: "tipoCuenta", encabezado: "Tipo" },
  ],
  campos: [
    codigo(),
    nombre(),
    { nombre: "bancoId", etiqueta: "Banco", tipo: "lista", fuente: "bancos", numerico: true, soloCreacion: true },
    { nombre: "monedaId", etiqueta: "Moneda", tipo: "lista", fuente: "monedas", numerico: true, soloCreacion: true },
    { nombre: "numeroCuenta", etiqueta: "Número de cuenta", tipo: "texto", max: 40, soloCreacion: true },
    {
      nombre: "tipoCuenta",
      etiqueta: "Tipo de cuenta",
      tipo: "lista",
      opciones: [
        { valor: "MONETARIA", etiqueta: "Monetaria" },
        { valor: "AHORRO", etiqueta: "Ahorro" },
        { valor: "OTRA", etiqueta: "Otra" },
      ],
      porDefecto: "MONETARIA",
    },
    { nombre: "titular", etiqueta: "Titular", tipo: "texto", max: 150, opcional: true },
  ],
};

export const tiposDocumento: ConfigCatalogo = {
  clave: "tipos-documento",
  titulo: "Tipos de documento",
  descripcion: "Catálogo cerrado: cada tipo implica un comportamiento del sistema. Aquí solo se cambia el nombre o se desactiva.",
  singular: "tipo de documento",
  femenino: false,
  recurso: "catalogos/tipos-documento",
  permiso: "CATALOGOS_TIPOSDOCUMENTO",
  ordenInicial: "modulo",
  sinCrear: true,
  sinBaja: true,
  sinExportar: true,
  columnas: [colCodigo, colNombre, { id: "modulo", encabezado: "Módulo", ordenable: true }],
  campos: [nombre(100)],
};

export const seriesDocumento: ConfigCatalogo = {
  clave: "series-documento",
  titulo: "Series y correlativos",
  descripcion: "El correlativo avanza solo al emitir documentos. De una serie existente se puede ampliar el rango y su vigencia.",
  singular: "serie",
  femenino: true,
  recurso: "catalogos/series-documento",
  permiso: "CATALOGOS_SERIES",
  ordenInicial: "tipoDocumentoNombre",
  marcadorBusqueda: "Buscar por serie, prefijo o tipo",
  sinExportar: true,
  filtros: [{ parametro: "tipoDocumentoId", etiqueta: "Tipo de documento", fuente: "tiposDocumento" }],
  columnas: [
    { id: "tipoDocumentoNombre", encabezado: "Documento", ordenable: true },
    { id: "serie", encabezado: "Serie", ordenable: true },
    { id: "prefijo", encabezado: "Prefijo" },
    { id: "siguienteCorrelativo", encabezado: "Siguiente número", ordenable: true },
    { id: "numeroMaximo", encabezado: "Máximo" },
    { id: "vigenteDesde", encabezado: "Desde", ordenable: true, formato: "fecha" },
    { id: "vigenteHasta", encabezado: "Hasta", formato: "fecha" },
    { id: "sucursalNombre", encabezado: "Sucursal" },
  ],
  campos: [
    { nombre: "tipoDocumentoId", etiqueta: "Tipo de documento", tipo: "lista", fuente: "tiposDocumento", numerico: true, soloCreacion: true },
    {
      nombre: "serie",
      etiqueta: "Serie",
      tipo: "texto",
      max: 10,
      patron: /^[A-Za-z0-9]+$/,
      mensajePatron: "Solo letras y números.",
      mayusculas: true,
      soloCreacion: true,
    },
    {
      nombre: "prefijo",
      etiqueta: "Prefijo",
      tipo: "texto",
      max: 10,
      opcional: true,
      patron: /^[A-Za-z0-9]+$/,
      mensajePatron: "Solo letras y números.",
      mayusculas: true,
      soloCreacion: true,
    },
    { nombre: "incluirAnio", etiqueta: "Incluir el año en el número", tipo: "booleano", soloCreacion: true },
    { nombre: "longitud", etiqueta: "Dígitos del número", tipo: "entero", minimo: 1, maximo: 12, porDefecto: "6", soloCreacion: true },
    { nombre: "siguienteCorrelativo", etiqueta: "Número inicial", tipo: "entero", minimo: 1, porDefecto: "1", soloCreacion: true },
    { nombre: "numeroMaximo", etiqueta: "Número máximo", tipo: "entero", minimo: 1, opcional: true, ayuda: "Tope autorizado; vacío = sin tope." },
    { nombre: "vigenteDesde", etiqueta: "Vigente desde", tipo: "fecha", soloCreacion: true },
    { nombre: "vigenteHasta", etiqueta: "Vigente hasta", tipo: "fecha", opcional: true },
    { nombre: "sucursalId", etiqueta: "Sucursal", tipo: "lista", fuente: "sucursales", numerico: true, opcional: true, soloCreacion: true, ayuda: "Vacío = toda la empresa." },
    { nombre: "cajaId", etiqueta: "Caja", tipo: "lista", fuente: "cajas", numerico: true, opcional: true, soloCreacion: true, ayuda: "Requiere sucursal; debe ser de esa sucursal." },
  ],
  campoNombre: (fila) => `${String(fila.tipoDocumentoNombre)} ${String(fila.serie)}`,
};
