"use client";

import { CatalogoPagina } from "@/components/catalogos/CatalogoPagina";
import { cajas } from "@/components/catalogos/configuraciones";

export default function Pagina() {
  return <CatalogoPagina config={cajas} />;
}
