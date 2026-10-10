/**
 * Identidad de la marca. Junto con los tokens de `src/app/globals.css` es lo único que se
 * toca para un cambio de marca: ningún componente lleva nombre, logo ni colores fijos.
 */
export const marca = {
  nombre: "ERP",
  descripcion: "Sistema de gestión empresarial",
  /** Ruta de un logo dentro de /public, o null para mostrar las iniciales del nombre. */
  logo: null as string | null,
} as const;
