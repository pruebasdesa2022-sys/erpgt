import { api, urlBff } from "@/services/http";
import type { AccesosOperativos, ConfiguracionItem, Empresa, EditarEmpresa, FilaCatalogo, MisAccesosOperativos, TasaVigente } from "@/types/catalogos";
import type {
  Bitacora,
  CambiarEstadoModulo,
  ConsultaBitacora,
  ConsultaPaginada,
  CrearRol,
  CrearSucursal,
  CrearUsuario,
  EditarRol,
  EditarSucursal,
  EditarUsuario,
  MenuItem,
  Modulo,
  OpcionesUsuario,
  Permiso,
  PermisoDirecto,
  PermisoDirectoEntrada,
  ResultadoPaginado,
  RolDetalle,
  RolLista,
  Sesion,
  Sucursal,
  UsuarioDetalle,
  UsuarioLista,
} from "@/types";

type ConFiltroActivo = ConsultaPaginada & { activo?: boolean };

/** Sesión propia: rutas de /api/auth (manejan cookies) y datos del usuario autenticado. */
export const servicioSesion = {
  login: (nombreUsuario: string, password: string) =>
    api<{ debeCambiarPassword: boolean }>("/api/auth/login", { metodo: "POST", cuerpo: { nombreUsuario, password }, absoluta: true }),
  logout: () => api<void>("/api/auth/logout", { metodo: "POST", absoluta: true }),
  cambiarContexto: (empresaId: number, sucursalId: number) =>
    api<void>("/api/auth/contexto", { metodo: "POST", cuerpo: { empresaId, sucursalId }, absoluta: true }),
  solicitarRecuperacion: (usuario: string) =>
    api<void>("/api/auth/recuperacion", { metodo: "POST", cuerpo: { usuario }, absoluta: true }),
  restablecer: (token: string, passwordNueva: string) =>
    api<void>("/api/auth/restablecer", { metodo: "POST", cuerpo: { token, passwordNueva }, absoluta: true }),
  cambiarPassword: (passwordActual: string, passwordNueva: string) =>
    api<void>("seguridad/autenticacion/cambiar-password", { metodo: "POST", cuerpo: { passwordActual, passwordNueva } }),
  obtener: () => api<Sesion>("seguridad/sesion"),
  menu: () => api<MenuItem[]>("seguridad/menu"),
  misPermisos: () => api<string[]>("seguridad/mis-permisos"),
};

export const servicioUsuarios = {
  listar: (consulta: ConFiltroActivo) => api<ResultadoPaginado<UsuarioLista>>("seguridad/usuarios", { consulta: { ...consulta } }),
  opciones: () => api<OpcionesUsuario>("seguridad/usuarios/opciones"),
  obtener: (id: number) => api<UsuarioDetalle>(`seguridad/usuarios/${id}`),
  crear: (datos: CrearUsuario) => api<UsuarioDetalle>("seguridad/usuarios", { metodo: "POST", cuerpo: datos }),
  editar: (id: number, datos: EditarUsuario) => api<UsuarioDetalle>(`seguridad/usuarios/${id}`, { metodo: "PUT", cuerpo: datos }),
  desactivar: (id: number, versionFila: number) =>
    api<void>(`seguridad/usuarios/${id}`, { metodo: "DELETE", consulta: { versionFila } }),
  restablecerPassword: (id: number, passwordTemporal: string) =>
    api<void>(`seguridad/usuarios/${id}/restablecer-password`, { metodo: "POST", cuerpo: { passwordTemporal } }),
  permisosDirectos: (id: number) => api<PermisoDirecto[]>(`seguridad/usuarios/${id}/permisos`),
  guardarPermisosDirectos: (id: number, permisos: PermisoDirectoEntrada[]) =>
    api<PermisoDirecto[]>(`seguridad/usuarios/${id}/permisos`, { metodo: "PUT", cuerpo: { permisos } }),
};

export const servicioRoles = {
  listar: (consulta: ConFiltroActivo) => api<ResultadoPaginado<RolLista>>("seguridad/roles", { consulta: { ...consulta } }),
  obtener: (id: number) => api<RolDetalle>(`seguridad/roles/${id}`),
  crear: (datos: CrearRol) => api<RolDetalle>("seguridad/roles", { metodo: "POST", cuerpo: datos }),
  editar: (id: number, datos: EditarRol) => api<RolDetalle>(`seguridad/roles/${id}`, { metodo: "PUT", cuerpo: datos }),
  desactivar: (id: number, versionFila: number) => api<void>(`seguridad/roles/${id}`, { metodo: "DELETE", consulta: { versionFila } }),
  permisos: () => api<Permiso[]>("seguridad/permisos"),
};

export const servicioModulos = {
  listar: () => api<Modulo[]>("seguridad/modulos"),
  cambiarEstado: (id: number, datos: CambiarEstadoModulo) =>
    api<Modulo>(`seguridad/modulos/${id}/estado`, { metodo: "PUT", cuerpo: datos }),
};

export const servicioSucursales = {
  listar: (consulta: ConFiltroActivo) => api<ResultadoPaginado<Sucursal>>("organizacion/sucursales", { consulta: { ...consulta } }),
  crear: (datos: CrearSucursal) => api<Sucursal>("organizacion/sucursales", { metodo: "POST", cuerpo: datos }),
  editar: (id: number, datos: EditarSucursal) => api<Sucursal>(`organizacion/sucursales/${id}`, { metodo: "PUT", cuerpo: datos }),
  desactivar: (id: number, versionFila: number) =>
    api<void>(`organizacion/sucursales/${id}`, { metodo: "DELETE", consulta: { versionFila } }),
  /** La exportación es una descarga: el navegador abre la URL del BFF directamente. */
  urlExportar: (consulta: ConFiltroActivo) => urlBff("organizacion/sucursales/exportar", { q: consulta.q, sort: consulta.sort, activo: consulta.activo }),
};

export const servicioBitacora = {
  listar: (consulta: ConsultaBitacora) => api<ResultadoPaginado<Bitacora>>("auditoria/bitacora", { consulta: { ...consulta } }),
};

/** Consulta de un catálogo del motor genérico; admite filtros adicionales propios de cada uno. */
export type ConsultaCatalogo = ConsultaPaginada & { activo?: boolean; [filtro: string]: string | number | boolean | undefined };

/** Operaciones estándar de un catálogo (ver CatalogoService en el backend). */
export function servicioCatalogo<T extends FilaCatalogo = FilaCatalogo>(recurso: string) {
  return {
    listar: (consulta: ConsultaCatalogo) => api<ResultadoPaginado<T>>(recurso, { consulta: { ...consulta } }),
    crear: (datos: Record<string, unknown>) => api<T>(recurso, { metodo: "POST", cuerpo: datos }),
    editar: (id: number, datos: Record<string, unknown>) => api<T>(`${recurso}/${id}`, { metodo: "PUT", cuerpo: datos }),
    desactivar: (id: number, versionFila: number) => api<void>(`${recurso}/${id}`, { metodo: "DELETE", consulta: { versionFila } }),
    /** La exportación es una descarga: el navegador abre la URL del BFF directamente. */
    urlExportar: (consulta: ConsultaCatalogo) => urlBff(`${recurso}/exportar`, { ...consulta, page: undefined, pageSize: undefined }),
  };
}

export const servicioEmpresas = {
  listar: () => api<Empresa[]>("organizacion/empresas"),
  editar: (id: number, datos: EditarEmpresa) => api<Empresa>(`organizacion/empresas/${id}`, { metodo: "PUT", cuerpo: datos }),
};

export const servicioConfiguracion = {
  listar: () => api<ConfiguracionItem[]>("catalogos/configuracion"),
  establecer: (clave: string, valor: string | null) =>
    api<ConfiguracionItem>(`catalogos/configuracion/${clave}`, { metodo: "PUT", cuerpo: { valor } }),
  establecerFechaBloqueo: (fecha: string | null) =>
    api<ConfiguracionItem>("catalogos/configuracion/fecha-bloqueo", { metodo: "PUT", cuerpo: { fecha } }),
};

export const servicioTiposCambio = {
  vigente: (monedaId: number, fecha: string) => api<TasaVigente>("catalogos/tipos-cambio/vigente", { consulta: { monedaId, fecha } }),
};

export const servicioAccesosOperativos = {
  obtener: (usuarioId: number) => api<AccesosOperativos>(`seguridad/usuarios/${usuarioId}/bodegas-cajas`),
  guardar: (usuarioId: number, datos: AccesosOperativos) =>
    api<AccesosOperativos>(`seguridad/usuarios/${usuarioId}/bodegas-cajas`, { metodo: "PUT", cuerpo: datos }),
  mias: () => api<MisAccesosOperativos>("seguridad/mis-bodegas-cajas"),
};
