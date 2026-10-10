"use client";

import { CatalogoPagina } from "@/components/catalogos/CatalogoPagina";
import { unidadesMedida } from "@/components/catalogos/configuraciones";

export default function Pagina() {
  return <CatalogoPagina config={unidadesMedida} />;
}
