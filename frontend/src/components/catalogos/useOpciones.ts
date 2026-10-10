import { useQuery } from "@tanstack/react-query";
import type { FuenteOpciones, OpcionLista } from "@/components/catalogos/config";
import { api } from "@/services/http";
import type { ResultadoPaginado } from "@/types";
import type { FilaCatalogo } from "@/types/catalogos";

const RUTAS: Record<FuenteOpciones, string> = {
  sucursales: "organizacion/sucursales",
  bodegas: "organizacion/bodegas",
  cajas: "organizacion/cajas",
  departamentos: "organizacion/departamentos",
  bancos: "catalogos/bancos",
  monedas: "catalogos/monedas",
  unidades: "catalogos/unidades-medida",
  tiposDocumento: "catalogos/tipos-documento",
};

/** Filas activas de un catálogo como opciones de una lista desplegable (hasta 100). */
export function useOpciones(fuente: FuenteOpciones | undefined) {
  return useQuery({
    queryKey: ["opciones", fuente],
    enabled: fuente !== undefined,
    staleTime: 60_000,
    queryFn: async (): Promise<OpcionLista[]> => {
      const pagina = await api<ResultadoPaginado<FilaCatalogo>>(RUTAS[fuente!], { consulta: { pageSize: 100, activo: true, sort: "nombre" } });
      return pagina.items.map((f) => ({ valor: String(f.id), etiqueta: `${String(f.codigo)} · ${String(f.nombre)}` }));
    },
  });
}
