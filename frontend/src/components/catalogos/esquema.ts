import { z } from "zod";
import type { CampoCatalogo } from "@/components/catalogos/config";
import type { FilaCatalogo } from "@/types/catalogos";

/** Valores del formulario: todo texto salvo las casillas. Se convierten al enviar (ver `aPayload`). */
export type DatosFormulario = Record<string, string | boolean>;

export type ModoFormulario = "crear" | "editar";

/** Campos que se piden en cada modo: los de solo creación no se editan y los de solo edición no se piden al crear. */
export function camposDe(campos: CampoCatalogo[], modo: ModoFormulario): CampoCatalogo[] {
  return campos.filter((c) => (modo === "crear" ? !c.soloEdicion : true));
}

const FECHA = /^\d{4}-\d{2}-\d{2}$/;

function esquemaCampo(c: CampoCatalogo): z.ZodTypeAny {
  const vacioOk = (v: string) => c.opcional === true && v === "";
  const obligatorio = "Complete este campo.";

  switch (c.tipo) {
    case "booleano":
      return z.boolean();

    case "texto": {
      let base = z.string().trim();
      if (!c.opcional) base = base.min(1, obligatorio);
      if (c.max) base = base.max(c.max, `Máximo ${c.max} caracteres.`);
      const patron = c.patron;
      return patron ? base.refine((v) => v === "" || patron.test(v), c.mensajePatron ?? "El valor no es válido.") : base;
    }

    case "entero":
      return z
        .string()
        .trim()
        .refine((v) => vacioOk(v) || /^-?\d+$/.test(v), c.opcional ? "Indique un número entero." : obligatorio)
        .refine((v) => v === "" || c.minimo === undefined || Number(v) >= c.minimo, `Debe ser mayor o igual que ${c.minimo}.`)
        .refine((v) => v === "" || c.maximo === undefined || Number(v) <= c.maximo, `Debe ser menor o igual que ${c.maximo}.`);

    case "decimal": {
      const decimales = c.decimales ?? 2;
      const patron = new RegExp(`^\\d+([.,]\\d{1,${decimales}})?$`);
      return z
        .string()
        .trim()
        .refine((v) => vacioOk(v) || patron.test(v), c.opcional ? `Indique un número con hasta ${decimales} decimales.` : obligatorio)
        .refine((v) => v === "" || c.minimo === undefined || Number(v.replace(",", ".")) >= c.minimo, `Debe ser mayor o igual que ${c.minimo}.`)
        .refine((v) => v === "" || c.maximo === undefined || Number(v.replace(",", ".")) <= c.maximo, `Debe ser menor o igual que ${c.maximo}.`);
    }

    case "fecha":
      return z
        .string()
        .trim()
        .refine((v) => vacioOk(v) || FECHA.test(v), c.opcional ? "Indique una fecha válida." : obligatorio);

    case "lista":
      return z.string().refine((v) => vacioOk(v) || v !== "", "Seleccione una opción.");
  }
}

export function esquemaCatalogo(campos: CampoCatalogo[]): z.ZodType<DatosFormulario, DatosFormulario> {
  const forma: Record<string, z.ZodTypeAny> = { activo: z.boolean() };
  for (const c of campos) forma[c.nombre] = esquemaCampo(c);
  return z.object(forma) as unknown as z.ZodType<DatosFormulario, DatosFormulario>;
}

/** Valor inicial de cada campo: el de la fila al editar o el predeterminado al crear. */
export function valoresIniciales(campos: CampoCatalogo[], fila: FilaCatalogo | null): DatosFormulario {
  const valores: DatosFormulario = { activo: fila ? fila.activo !== false : true };
  for (const c of campos) {
    const actual = fila?.[c.nombre];
    if (c.tipo === "booleano") valores[c.nombre] = fila ? actual === true : c.porDefecto === true;
    else valores[c.nombre] = actual === null || actual === undefined ? String(c.porDefecto ?? "") : String(actual);
  }
  return valores;
}

/** Convierte los valores del formulario a lo que espera la API (números, nulos y mayúsculas). */
export function aPayload(campos: CampoCatalogo[], datos: DatosFormulario): Record<string, unknown> {
  const cuerpo: Record<string, unknown> = {};
  for (const c of campos) {
    const valor = datos[c.nombre];
    if (c.tipo === "booleano") {
      cuerpo[c.nombre] = valor === true;
      continue;
    }

    const texto = String(valor ?? "").trim();
    if (texto === "") {
      cuerpo[c.nombre] = null;
      continue;
    }

    if (c.tipo === "entero") cuerpo[c.nombre] = Number(texto);
    else if (c.tipo === "decimal") cuerpo[c.nombre] = Number(texto.replace(",", "."));
    else if (c.tipo === "lista" && c.numerico) cuerpo[c.nombre] = Number(texto);
    else cuerpo[c.nombre] = c.mayusculas ? texto.toUpperCase() : texto;
  }

  return cuerpo;
}
