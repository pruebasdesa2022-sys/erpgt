"use client";

import { CatalogoPagina } from "@/components/catalogos/CatalogoPagina";
import { condicionesPago } from "@/components/catalogos/configuraciones";

export default function Pagina() {
  return <CatalogoPagina config={condicionesPago} />;
}
