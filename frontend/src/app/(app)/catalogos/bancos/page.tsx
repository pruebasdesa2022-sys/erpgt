"use client";

import { CatalogoPagina } from "@/components/catalogos/CatalogoPagina";
import { bancos } from "@/components/catalogos/configuraciones";

export default function Pagina() {
  return <CatalogoPagina config={bancos} />;
}
