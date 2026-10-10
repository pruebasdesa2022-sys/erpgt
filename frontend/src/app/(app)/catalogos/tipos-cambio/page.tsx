"use client";

import { CatalogoPagina } from "@/components/catalogos/CatalogoPagina";
import { tiposCambio } from "@/components/catalogos/configuraciones";

export default function Pagina() {
  return <CatalogoPagina config={tiposCambio} />;
}
