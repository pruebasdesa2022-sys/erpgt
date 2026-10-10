"use client";

import { useQuery } from "@tanstack/react-query";
import { EstadoCarga, EstadoError } from "@/components/comun/Estados";
import { EncabezadoPagina } from "@/components/comun/Formulario";
import { SinPermiso } from "@/components/comun/Listado";
import { FormularioEmpresa } from "@/components/organizacion/FormularioEmpresa";
import { usePermiso, useSesion } from "@/contexts/sesion";
import { servicioEmpresas } from "@/services";

export default function PaginaEmpresa() {
  const puedeVer = usePermiso("ORGANIZACION_EMPRESAS_VER");
  const puedeEditar = usePermiso("ORGANIZACION_EMPRESAS_EDITAR");
  const { sesion } = useSesion();
  const empresas = useQuery({ queryKey: ["empresas"], queryFn: servicioEmpresas.listar, enabled: puedeVer });

  if (!puedeVer) return <SinPermiso />;
  if (empresas.error) return <EstadoError error={empresas.error} onReintentar={() => void empresas.refetch()} />;
  if (!empresas.data) return <EstadoCarga />;

  const empresa = empresas.data.find((e) => e.id === sesion.empresaId);
  if (!empresa) return <EstadoError error={new Error("No se encontró la empresa activa.")} />;

  return (
    <>
      <EncabezadoPagina titulo="Empresa" descripcion={`Datos fiscales y generales de ${empresa.nombreComercial} (${empresa.codigo}).`} />
      {/* La clave fuerza el reinicio del formulario cuando cambia la versión guardada. */}
      <FormularioEmpresa key={empresa.versionFila} empresa={empresa} puedeEditar={puedeEditar} />
    </>
  );
}
