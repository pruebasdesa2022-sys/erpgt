"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { EstadoCarga, EstadoError } from "@/components/comun/Estados";
import { Campo, EncabezadoPagina } from "@/components/comun/Formulario";
import { SinPermiso } from "@/components/comun/Listado";
import { Puede } from "@/components/comun/Puede";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { SelectNativo } from "@/components/ui/select-nativo";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Textarea } from "@/components/ui/textarea";
import { CLAVE_SESION, usePermiso } from "@/contexts/sesion";
import { mostrarError } from "@/lib/errores";
import { servicioModulos } from "@/services";
import type { CambiarEstadoModulo, EstadoModulo, Modulo } from "@/types";

const ETIQUETAS: Record<EstadoModulo, string> = {
  EN_DESARROLLO: "En desarrollo",
  EN_PRUEBAS: "En pruebas",
  NO_LIBERADO: "No liberado",
  LIBERADO: "Liberado",
  SUSPENDIDO: "Suspendido",
  RETIRADO: "Retirado",
};

function InsigniaEstado({ estado }: { estado: EstadoModulo | null }) {
  if (!estado) return <span className="text-muted-foreground">Sin definir</span>;
  return <Badge variant={estado === "LIBERADO" ? "secondary" : estado === "EN_PRUEBAS" ? "default" : "outline"}>{ETIQUETAS[estado]}</Badge>;
}

/**
 * Liberación progresiva: un módulo está disponible solo si su estado efectivo (el más
 * restrictivo entre el global y el de la empresa) es "Liberado". Cada cambio exige motivo y queda auditado.
 */
export default function PaginaModulos() {
  const puedeVer = usePermiso("SEGURIDAD_MODULOS_VER");
  const [enCambio, setEnCambio] = useState<Modulo | null>(null);
  const clienteConsultas = useQueryClient();
  const modulos = useQuery({ queryKey: ["modulos"], queryFn: servicioModulos.listar, enabled: puedeVer });

  if (!puedeVer) return <SinPermiso />;

  return (
    <>
      <EncabezadoPagina titulo="Módulos" descripcion="Estado de liberación de cada módulo en este ambiente y en la empresa activa." />

      <div className="rounded-xl border bg-card">
        {modulos.error ? (
          <EstadoError error={modulos.error} onReintentar={() => void modulos.refetch()} />
        ) : !modulos.data ? (
          <EstadoCarga filas={8} />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Módulo</TableHead>
                <TableHead>Fase</TableHead>
                <TableHead>Estado global</TableHead>
                <TableHead>Estado en la empresa</TableHead>
                <TableHead>Estado efectivo</TableHead>
                <TableHead>
                  <span className="sr-only">Acciones</span>
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {modulos.data.map((m) => (
                <TableRow key={m.id}>
                  <TableCell>
                    <span className="flex items-center gap-2">
                      {m.nombre}
                      {m.esBase && <Badge variant="outline">Base</Badge>}
                    </span>
                  </TableCell>
                  <TableCell>{m.fase}</TableCell>
                  <TableCell>
                    <InsigniaEstado estado={m.estadoGlobal} />
                  </TableCell>
                  <TableCell>
                    <InsigniaEstado estado={m.estadoEmpresa} />
                  </TableCell>
                  <TableCell>
                    <InsigniaEstado estado={m.estadoEfectivo} />
                  </TableCell>
                  <TableCell className="text-right">
                    {/* Los módulos base (Seguridad, Organización, Auditoría) deben permanecer liberados. */}
                    {!m.esBase && (
                      <Puede permiso="SEGURIDAD_MODULOS_EDITAR">
                        <Button variant="outline" size="sm" onClick={() => setEnCambio(m)}>
                          Cambiar estado
                        </Button>
                      </Puede>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>

      {enCambio && (
        <DialogoEstado
          modulo={enCambio}
          onCerrar={() => setEnCambio(null)}
          onGuardado={() => {
            setEnCambio(null);
            // El menú y los permisos dependen del estado de los módulos.
            void clienteConsultas.invalidateQueries({ queryKey: ["modulos"] });
            void clienteConsultas.invalidateQueries({ queryKey: CLAVE_SESION });
          }}
        />
      )}
    </>
  );
}

type DatosEstado = Pick<CambiarEstadoModulo, "ambito" | "estado" | "motivo">;

function DialogoEstado({ modulo, onCerrar, onGuardado }: { modulo: Modulo; onCerrar: () => void; onGuardado: () => void }) {
  const { register, handleSubmit, formState, setError } = useForm<DatosEstado>({
    defaultValues: { ambito: "EMPRESA", estado: modulo.estadoEmpresa ?? modulo.estadoGlobal, motivo: "" },
  });

  const cambio = useMutation({
    mutationFn: (datos: DatosEstado) =>
      servicioModulos.cambiarEstado(modulo.id, {
        ...datos,
        motivo: datos.motivo.trim(),
        versionFila: datos.ambito === "GLOBAL" ? modulo.versionFila : (modulo.versionFilaEmpresa ?? 0),
      }),
    onSuccess: (actualizado) => {
      toast.success(`${actualizado.nombre}: estado efectivo ${ETIQUETAS[actualizado.estadoEfectivo].toLowerCase()}.`);
      onGuardado();
    },
    onError: (e) => mostrarError(e, { setError, campos: ["ambito", "estado", "motivo"] }),
  });

  return (
    <Dialog open onOpenChange={(abierto) => !abierto && !cambio.isPending && onCerrar()}>
      <DialogContent className="sm:max-w-md">
        <form
          className="grid gap-4"
          noValidate
          onSubmit={handleSubmit((datos) => (datos.motivo.trim() ? cambio.mutate(datos) : setError("motivo", { message: "Indique el motivo del cambio." })))}
        >
          <DialogHeader>
            <DialogTitle>Cambiar estado de {modulo.nombre}</DialogTitle>
            <DialogDescription>Rige el estado más restrictivo entre el global y el de la empresa. El cambio queda en la bitácora.</DialogDescription>
          </DialogHeader>
          <Campo etiqueta="Ámbito" id="ambito">
            <SelectNativo id="ambito" {...register("ambito")}>
              <option value="EMPRESA">Solo la empresa activa</option>
              <option value="GLOBAL">Global (todo el ambiente)</option>
            </SelectNativo>
          </Campo>
          <Campo etiqueta="Estado" id="estado" error={formState.errors.estado?.message}>
            <SelectNativo id="estado" {...register("estado")}>
              {(Object.keys(ETIQUETAS) as EstadoModulo[]).map((estado) => (
                <option key={estado} value={estado}>
                  {ETIQUETAS[estado]}
                </option>
              ))}
            </SelectNativo>
          </Campo>
          <Campo etiqueta="Motivo" id="motivo" error={formState.errors.motivo?.message}>
            <Textarea id="motivo" rows={3} maxLength={500} aria-invalid={!!formState.errors.motivo} {...register("motivo")} />
          </Campo>
          <DialogFooter>
            <Button type="button" variant="outline" disabled={cambio.isPending} onClick={onCerrar}>
              Cancelar
            </Button>
            <Button type="submit" disabled={cambio.isPending}>
              {cambio.isPending ? "Guardando…" : "Guardar"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
