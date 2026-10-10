/**
 * NIT de Guatemala (cuerpo numérico + dígito verificador 0-9 o K, módulo 11). Solo mejora la
 * experiencia de captura: el servidor valida el mismo algoritmo y es quien decide.
 */
export function normalizarNit(valor: string): string {
  return valor.replace(/[\s-]/g, "").toUpperCase();
}

export function nitValido(valor: string): boolean {
  const nit = normalizarNit(valor);
  if (nit.length < 2 || nit.length > 13) return false;

  const cuerpo = nit.slice(0, -1);
  const verificador = nit.slice(-1);
  if (!/^\d+$/.test(cuerpo) || !/^[\dK]$/.test(verificador)) return false;

  let factor = cuerpo.length + 1;
  let suma = 0;
  for (const digito of cuerpo) suma += Number(digito) * factor--;

  const resto = (11 - (suma % 11)) % 11;
  return verificador === (resto === 10 ? "K" : String(resto));
}
