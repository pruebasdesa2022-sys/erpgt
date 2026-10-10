"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { EstadoCarga, EstadoError } from "@/components/comun/Estados";
import { Puede } from "@/components/comun/Puede";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { SelectNativo } from "@/components/ui/select-nativo";
import { usePermiso } from "@/contexts/sesion";
import { mostrarError } from "@/lib/errores";
import { formatoFechaNegocio } from "@/lib/formato";
import { servicioConfiguracion } from "@/services";
import type { ConfiguracionItem } from "@/types/catalogos";

const CLAVE = ["configuracion"] as const;
const FECHA_BLOQUEO = "FECHA_BLOQUEO";

function Parametro({ item }: { item: ConfiguracionItem }) {
  const esBloqueo = item.clave === FECHA_BLOQUEO;
  const puedeEditar = usePermiso(esBloqueo ? "CATALOGOS_FECHABLOQUEO_EDITAR" : "CATALOGOS_CONFIGURACION_EDITAR");
  const clienteConsultas = useQueryClient();
  const [valor, setValor] = useState(item.valor ?? "");
  const cambio = valor !== (item.valor ?? "");

  const guardar = useMutation({
    mutationFn: (nuevo: string | null) =>
      esBloqueo ? servicioConfiguracion.establecerFechaBloqueo(nuevo) : servicioConfiguracion.establecer(item.clave, nuevo),
    onSuccess: (guardado) => {
      toast.success("Parámetro guardado.");
      setValor(guardado.valor ?? "");
      void clienteConsultas.invalidateQueries({ queryKey: CLAVE });
    },
    onError: (error) => mostrarError(error),
  });

  const id = `parametro-${item.clave}`;
  const deshabilitado = !puedeEditar || guardar.isPending;

  return (
    <li className="grid gap-2 border-b p-4 last:border-b-0 sm:grid-cols-[1fr_auto] sm:items-center">
      <div>
        <label htmlFor={id} className="text-sm font-medium">
          {item.clave}
        </label>
        <p className="text-sm text-muted-foreground">{item.descripcion}</p>
        <p className="mt-1 flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
          {item.esPredeterminado ? <Badge variant="outline">Valor predeterminado</Badge> : <Badge variant="secondary">Definido desde {formatoFechaNegocio(item.vigenteDesde)}</Badge>}
          {item.valorPorDefecto !== null && <span>Predeterminado: {item.valorPorDefecto}</span>}
        </p>
      </div>
      <div className="flex flex-wrap items-center gap-2">
        {item.tipoDato === "BOOLEANO" ? (
          <SelectNativo id={id} className="w-40" value={valor} disabled={deshabilitado} onChange={(e) => setValor(e.target.value)}>
            <option value="false">No</option>
            <option value="true">Sí</option>
          </SelectNativo>
        ) : item.opciones ? (
          <SelectNativo id={id} className="w-56" value={valor} disabled={deshabilitado} onChange={(e) => setValor(e.target.value)}>
            {item.opciones.map((o) => (
              <option key={o} value={o}>
                {o}
              </option>
            ))}
          </SelectNativo>
        ) : (
          <Input
            id={id}
            className="w-44"
            type={item.tipoDato === "FECHA" ? "date" : "text"}
            inputMode={item.tipoDato === "DECIMAL" ? "decimal" : undefined}
            value={valor}
            disabled={deshabilitado}
            onChange={(e) => setValor(e.target.value)}
          />
        )}
        <Puede permiso={esBloqueo ? "CATALOGOS_FECHABLOQUEO_EDITAR" : "CATALOGOS_CONFIGURACION_EDITAR"}>
          <Button size="sm" disabled={deshabilitado || !cambio} onClick={() => guardar.mutate(valor.trim() === "" ? null : valor.trim())}>
            Guardar
          </Button>
          {!item.esPredeterminado && (
            <Button size="sm" variant="outline" disabled={deshabilitado} onClick={() => guardar.mutate(null)}>
              {esBloqueo ? "Quitar bloqueo" : "Restablecer"}
            </Button>
          )}
        </Puede>
      </div>
    </li>
  );
}

/** Parámetros generales de negocio de la empresa activa, con su valor vigente y su valor predeterminado. */
export function PanelConfiguracion() {
  const parametros = useQuery({ queryKey: CLAVE, queryFn: servicioConfiguracion.listar });

  if (parametros.error) return <EstadoError error={parametros.error} onReintentar={() => void parametros.refetch()} />;
  if (!parametros.data) return <EstadoCarga />;

  return (
    <ul className="rounded-xl border bg-card">
      {parametros.data.map((item) => (
        // La clave incluye el valor vigente: tras guardar, el campo se reinicia con lo que quedó en el servidor.
        <Parametro key={`${item.clave}:${item.valor ?? ""}:${item.esPredeterminado}`} item={item} />
      ))}
    </ul>
  );
}
