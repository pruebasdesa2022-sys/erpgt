/** Formato de presentación para Guatemala: es-GT, quetzales y hora de America/Guatemala. */

const ZONA = "America/Guatemala";
const LOCAL = "es-GT";

const fechaHora = new Intl.DateTimeFormat(LOCAL, { dateStyle: "short", timeStyle: "short", timeZone: ZONA });
const soloFecha = new Intl.DateTimeFormat(LOCAL, { dateStyle: "medium", timeZone: ZONA });
const quetzales = new Intl.NumberFormat(LOCAL, { style: "currency", currency: "GTQ" });

/** La API envía fechas en UTC; aquí se muestran en la hora de Guatemala. */
export function formatoFechaHora(iso: string | null | undefined): string {
  return iso ? fechaHora.format(new Date(iso)) : "—";
}

export function formatoFecha(iso: string | null | undefined): string {
  return iso ? soloFecha.format(new Date(iso)) : "—";
}

export function formatoMoneda(monto: number): string {
  return quetzales.format(monto);
}

/** Inicio del día (hora de Guatemala, UTC−6 sin horario de verano) como instante UTC, para filtrar por fecha. */
export function inicioDelDiaUtc(fecha: string): string | undefined {
  return fecha ? new Date(`${fecha}T00:00:00-06:00`).toISOString() : undefined;
}

export function finDelDiaUtc(fecha: string): string | undefined {
  if (!fecha) return undefined;
  const fin = new Date(`${fecha}T00:00:00-06:00`);
  fin.setUTCDate(fin.getUTCDate() + 1);
  return fin.toISOString();
}

const fechaNegocio = new Intl.DateTimeFormat(LOCAL, { dateStyle: "medium", timeZone: "UTC" });
const numeroTasa = new Intl.NumberFormat(LOCAL, { minimumFractionDigits: 2, maximumFractionDigits: 8 });

/**
 * Fecha de negocio (DATE, sin hora) como "2026-10-10". No se convierte de zona horaria:
 * es el día calendario tal cual, a diferencia de `formatoFecha` que recibe instantes UTC.
 */
export function formatoFechaNegocio(fecha: string | null | undefined): string {
  if (!fecha) return "—";
  const [anio, mes, dia] = fecha.slice(0, 10).split("-").map(Number);
  return fechaNegocio.format(new Date(Date.UTC(anio, mes - 1, dia)));
}

/** Tasas y factores: al menos 2 decimales y hasta 8, sin redondear lo que la base guarda. */
export function formatoTasa(valor: number): string {
  return numeroTasa.format(valor);
}
