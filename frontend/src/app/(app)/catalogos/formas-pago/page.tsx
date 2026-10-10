"use client";

import { CatalogoPagina } from "@/components/catalogos/CatalogoPagina";
import { formasPago } from "@/components/catalogos/configuraciones";

export default function Pagina() {
  return <CatalogoPagina config={formasPago} />;
}
