/**
 * Tipos que reflejan los DTO del backend (ERP.Application). Si cambia un DTO, se cambia aquí
 * en la misma tarea. Las fechas llegan como texto ISO en UTC.
 */

export interface ResultadoPaginado<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface ConsultaPaginada {
  page?: number;
  pageSize?: number;
  /** Campo de orden; con prefijo "-" es descendente. */
  sort?: string;
  q?: string;
}

/** Cuerpo de error de la API (RFC 7807 + extensiones). */
export interface ProblemaApi {
  status?: number;
  title?: string;
  detail?: string;
  errorCode?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

// ----- Seguridad: sesión -----

export interface EmpresaResumen {
  id: number;
  codigo: string;
  nombreComercial: string;
}

export interface SucursalResumen {
  id: number;
  empresaId: number;
  codigo: string;
  nombre: string;
}

export interface Sesion {
  usuarioId: number;
  nombreUsuario: string;
  nombreCompleto: string;
  correo: string;
  debeCambiarPassword: boolean;
  empresaId: number;
  sucursalId: number;
  empresas: EmpresaResumen[];
  sucursales: SucursalResumen[];
}

export interface MenuItem {
  id: number;
  tipo: "MODULO" | "GRUPO" | "PANTALLA";
  codigo: string;
  nombre: string;
  icono: string | null;
  ruta: string | null;
  hijos: MenuItem[];
}

// ----- Seguridad: usuarios -----

export interface UsuarioLista {
  id: number;
  nombreUsuario: string;
  correo: string;
  nombreCompleto: string;
  activo: boolean;
  debeCambiarPassword: boolean;
  bloqueadoHasta: string | null;
  ultimoAccesoEn: string | null;
  roles: string | null;
  versionFila: number;
}

export interface UsuarioDetalle {
  id: number;
  nombreUsuario: string;
  correo: string;
  nombreCompleto: string;
  activo: boolean;
  debeCambiarPassword: boolean;
  bloqueadoHasta: string | null;
  ultimoAccesoEn: string | null;
  versionFila: number;
  rolIds: number[];
  empresaIds: number[];
  sucursalIds: number[];
}

export interface CrearUsuario {
  nombreUsuario: string;
  correo: string;
  nombreCompleto: string;
  passwordTemporal: string;
  rolIds: number[];
  empresaIds: number[];
  sucursalIds: number[];
}

export interface EditarUsuario {
  correo: string;
  nombreCompleto: string;
  activo: boolean;
  rolIds: number[];
  empresaIds: number[];
  sucursalIds: number[];
  versionFila: number;
}

export interface OpcionesUsuario {
  roles: { id: number; nombre: string }[];
  empresas: EmpresaResumen[];
  sucursales: SucursalResumen[];
}

export type EfectoPermiso = "CONCEDER" | "DENEGAR";

export interface PermisoDirecto {
  permisoId: number;
  codigo: string;
  efecto: EfectoPermiso;
  vigenteHasta: string | null;
  motivo: string | null;
}

export interface PermisoDirectoEntrada {
  permisoId: number;
  efecto: EfectoPermiso;
  vigenteHasta: string | null;
  motivo: string | null;
}

// ----- Seguridad: roles y permisos -----

export interface RolLista {
  id: number;
  codigo: string;
  nombre: string;
  descripcion: string | null;
  esSistema: boolean;
  activo: boolean;
  cantidadPermisos: number;
  cantidadUsuarios: number;
  versionFila: number;
}

export interface RolDetalle {
  id: number;
  codigo: string;
  nombre: string;
  descripcion: string | null;
  esSistema: boolean;
  activo: boolean;
  versionFila: number;
  permisoIds: number[];
}

export interface CrearRol {
  codigo: string;
  nombre: string;
  descripcion: string | null;
  permisoIds: number[];
}

export interface EditarRol {
  nombre: string;
  descripcion: string | null;
  activo: boolean;
  permisoIds: number[];
  versionFila: number;
}

export interface Permiso {
  id: number;
  codigo: string;
  modulo: string;
  moduloNombre: string;
  recurso: string;
  accion: string;
  nombre: string;
}

// ----- Seguridad: módulos -----

export type EstadoModulo = "EN_DESARROLLO" | "EN_PRUEBAS" | "NO_LIBERADO" | "LIBERADO" | "SUSPENDIDO" | "RETIRADO";

export interface Modulo {
  id: number;
  codigo: string;
  nombre: string;
  fase: number;
  orden: number;
  estadoGlobal: EstadoModulo;
  estadoEmpresa: EstadoModulo | null;
  estadoEfectivo: EstadoModulo;
  esBase: boolean;
  versionFila: number;
  versionFilaEmpresa: number | null;
}

export interface CambiarEstadoModulo {
  ambito: "GLOBAL" | "EMPRESA";
  estado: EstadoModulo;
  motivo: string;
  /** Versión de la fila que se modifica; 0 si la empresa aún no tiene estado propio. */
  versionFila: number;
}

// ----- Organización -----

export interface Sucursal {
  id: number;
  empresaId: number;
  codigo: string;
  nombre: string;
  direccion: string | null;
  telefono: string | null;
  correo: string | null;
  esPrincipal: boolean;
  activo: boolean;
  versionFila: number;
  creadoEn: string;
  modificadoEn: string | null;
}

export interface CrearSucursal {
  codigo: string;
  nombre: string;
  direccion: string | null;
  telefono: string | null;
  correo: string | null;
}

export interface EditarSucursal {
  nombre: string;
  direccion: string | null;
  telefono: string | null;
  correo: string | null;
  activo: boolean;
  versionFila: number;
}

// ----- Auditoría -----

export interface Bitacora {
  id: number;
  fechaUtc: string;
  usuarioId: number | null;
  nombreUsuario: string | null;
  modulo: string;
  entidad: string;
  entidadId: string | null;
  accion: string;
  resultado: "EXITO" | "FALLO";
  valoresAnteriores: string | null;
  valoresNuevos: string | null;
  ip: string | null;
  operacionId: string | null;
  mensaje: string | null;
}

export interface ConsultaBitacora extends ConsultaPaginada {
  desde?: string;
  hasta?: string;
  entidad?: string;
  accion?: string;
}
