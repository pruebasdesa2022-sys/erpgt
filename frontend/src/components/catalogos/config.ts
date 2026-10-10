import type { ReactNode } from "react";
import type { FilaCatalogo } from "@/types/catalogos";

/** Catálogos cuyas filas sirven de opciones en listas desplegables de otros formularios. */
export type FuenteOpciones = "sucursales" | "bodegas" | "departamentos" | "bancos" | "monedas" | "unidades" | "tiposDocumento" | "cajas";

export type TipoCampo = "texto" | "entero" | "decimal" | "fecha" | "booleano" | "lista";

export interface OpcionLista {
  valor: string;
  etiqueta: string;
}

/**
 * Un campo del formulario de un catálogo. `nombre` es la propiedad del DTO (camelCase).
 * Las reglas de aquí son solo experiencia de usuario: el servidor repite todas y es quien decide.
 */
export interface CampoCatalogo {
  nombre: string;
  etiqueta: string;
  tipo: TipoCampo;
  /** Vacío permitido (se envía null). Por defecto los campos son obligatorios. */
  opcional?: boolean;
  /** Longitud máxima de un texto. */
  max?: number;
  /** Patrón de un texto (por ejemplo, códigos). */
  patron?: RegExp;
  mensajePatron?: string;
  /** Texto en mayúsculas al enviar (códigos). */
  mayusculas?: boolean;
  /** Límites de un número. */
  minimo?: number;
  maximo?: number;
  /** Decimales permitidos en un campo `decimal`. */
  decimales?: number;
  opciones?: OpcionLista[];
  fuente?: FuenteOpciones;
  /** El valor de la lista se envía como número (identificadores). */
  numerico?: boolean;
  /** Solo se pide al crear; después no cambia (se muestra deshabilitado al editar). */
  soloCreacion?: boolean;
  /** No se pide al crear (campos que solo se editan). */
  soloEdicion?: boolean;
  porDefecto?: string | boolean;
  ayuda?: string;
}

export interface ColumnaCatalogo {
  id: string;
  encabezado: string;
  /** Ordenable en el servidor; `id` debe ser un campo que la API acepte en `sort`. */
  ordenable?: boolean;
  formato?: "fecha" | "sino" | "tasa" | "estado";
  render?: (fila: FilaCatalogo) => ReactNode;
}

export interface FiltroCatalogo {
  /** Nombre del parámetro de consulta. */
  parametro: string;
  etiqueta: string;
  fuente: FuenteOpciones;
}

export interface ConfigCatalogo {
  /** Clave de caché de las consultas. */
  clave: string;
  titulo: string;
  descripcion: string;
  /** "bodega", "tipo de cambio": para los mensajes. */
  singular: string;
  femenino: boolean;
  /** Ruta del recurso bajo /api/v1/, sin barra inicial. */
  recurso: string;
  /** Prefijo de permisos: {permiso}_VER, _CREAR, _EDITAR, _ELIMINAR, _EXPORTAR. */
  permiso: string;
  columnas: ColumnaCatalogo[];
  campos: CampoCatalogo[];
  ordenInicial: string;
  marcadorBusqueda?: string;
  /** El listado admite `q` (por defecto sí). */
  buscar?: boolean;
  filtros?: FiltroCatalogo[];
  /** El catálogo tiene estado activo/inactivo y baja lógica (por defecto sí). */
  conActivo?: boolean;
  /** No existe alta desde la aplicación (catálogos cerrados). */
  sinCrear?: boolean;
  sinBaja?: boolean;
  sinExportar?: boolean;
  /** Filas que se muestran pero no se editan (estándar del sistema). */
  soloLectura?: (fila: FilaCatalogo) => boolean;
  /** Texto de la baja; por defecto "quedará inactivo y no se podrá seleccionar". */
  avisoBaja?: string;
  /** Nombre de la propiedad que identifica a la fila en los mensajes. */
  campoNombre?: (fila: FilaCatalogo) => string;
}
