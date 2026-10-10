"use client";

import { CatalogoPagina } from "@/components/catalogos/CatalogoPagina";
import { motivosAnulacion } from "@/components/catalogos/configuraciones";

export default function Pagina() {
  return <CatalogoPagina config={motivosAnulacion} />;
}
