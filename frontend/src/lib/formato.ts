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
