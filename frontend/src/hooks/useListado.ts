"use client";

import { useEffect, useState } from "react";

/**
 * Estado de un listado con paginación, orden y búsqueda en el servidor.
 * La búsqueda se envía con un pequeño retardo para no consultar en cada tecla.
 */
export function useListado(ordenInicial?: string, tamano = 20) {
  const [texto, setTexto] = useState("");
  const [q, setQ] = useState("");
  const [page, setPage] = useState(1);
  const [sort, setSort] = useState<string | undefined>(ordenInicial);

  useEffect(() => {
    const temporizador = setTimeout(() => {
      setQ(texto.trim());
      setPage(1);
    }, 300);
    return () => clearTimeout(temporizador);
  }, [texto]);

  return {
    texto,
    setTexto,
    page,
    setPage,
    sort,
    setSort: (nuevo: string | undefined) => {
      setSort(nuevo);
      setPage(1);
    },
    consulta: { q: q || undefined, page, pageSize: tamano, sort },
  };
}
