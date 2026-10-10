"use client";

import { PanelConfiguracion } from "@/components/catalogos/PanelConfiguracion";
import { EncabezadoPagina } from "@/components/comun/Formulario";
import { SinPermiso } from "@/components/comun/Listado";
import { usePermiso } from "@/contexts/sesion";

export default function PaginaConfiguracion() {
  const puedeVer = usePermiso("CATALOGOS_CONFIGURACION_VER");
  if (!puedeVer) return <SinPermiso />;

  return (
    <>
      <EncabezadoPagina
        titulo="Configuración general"
        descripcion="Parámetros de negocio de la empresa activa. Cada cambio queda con su fecha de inicio y en la bitácora."
      />
      <PanelConfiguracion />
    </>
  );
}
