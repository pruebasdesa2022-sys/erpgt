/**
 * DTO de organización y catálogos (etapa 1A). Reflejan ERP.Application.Catalogos y
 * ERP.Application.Organizacion; si cambia un DTO, se cambia aquí en la misma tarea.
 * Las fechas de negocio (DATE) llegan como "AAAA-MM-DD".
 */

/** Campos comunes del motor genérico de catálogos (CatalogoDto). */
export interface FilaCatalogo {
  id: number;
  /** Nulo en los catálogos globales y en las unidades y conversiones estándar. */
  empresaId?: number | null;
  activo?: boolean;
  versionFila: number;
  [propiedad: string]: unknown;
}

export interface Empresa {
  id: number;
  codigo: string;
  razonSocial: string;
  nombreComercial: string;
  nit: string;
  direccion: string | null;
  telefono: string | null;
  correo: string | null;
  regimenTributario: "GENERAL" | "PEQUENO_CONTRIBUYENTE";
  monedaLocalId: number;
  activo: boolean;
  versionFila: number;
}

export interface EditarEmpresa {
  razonSocial: string;
  nombreComercial: string;
  nit: string;
  direccion: string | null;
  telefono: string | null;
  correo: string | null;
  regimenTributario: string;
  monedaLocalId: number;
  versionFila: number;
}

export type TipoDatoConfiguracion = "TEXTO" | "ENTERO" | "DECIMAL" | "BOOLEANO" | "FECHA" | "JSON";

export interface ConfiguracionItem {
  clave: string;
  descripcion: string;
  tipoDato: TipoDatoConfiguracion;
  opciones: string[] | null;
  valor: string | null;
  valorPorDefecto: string | null;
  esPredeterminado: boolean;
  vigenteDesde: string | null;
}

export interface TasaVigente {
  monedaId: number;
  fechaTasa: string;
  tasa: number;
}

export interface AccesosOperativos {
  bodegaIds: number[];
  cajaIds: number[];
}

export interface MisAccesosOperativos {
  bodegas: { id: number; sucursalId: number; codigo: string; nombre: string }[];
  cajas: { id: number; sucursalId: number; bodegaId: number | null; codigo: string; nombre: string }[];
}
