"use client";

import { CatalogoPagina } from "@/components/catalogos/CatalogoPagina";
import { tiposProveedor } from "@/components/catalogos/configuraciones";

export default function Pagina() {
  return <CatalogoPagina config={tiposProveedor} />;
}
