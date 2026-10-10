"use client";

import { flexRender, getCoreRowModel, useReactTable, type ColumnDef } from "@tanstack/react-table";
import { ArrowDown, ArrowUp, ChevronsUpDown } from "lucide-react";
import { EstadoCarga, EstadoError, EstadoVacio } from "@/components/comun/Estados";
import { Button } from "@/components/ui/button";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import type { ResultadoPaginado } from "@/types";

interface Props<T> {
  columnas: ColumnDef<T>[];
  datos: ResultadoPaginado<T> | undefined;
  cargando: boolean;
  error: unknown;
  onReintentar: () => void;
  /** Orden actual, con prefijo "-" si es descendente. Lo aplica el servidor. */
  orden: string | undefined;
  onOrden: (orden: string | undefined) => void;
  onPagina: (pagina: number) => void;
  vacio?: string;
}

/**
 * Tabla cuyos datos, orden y paginación vienen del servidor. Una columna es ordenable si
 * declara `enableSorting: true`; su `id` debe ser un campo que la API acepte en `sort`.
 */
export function TablaServidor<T>({ columnas, datos, cargando, error, onReintentar, orden, onOrden, onPagina, vacio }: Props<T>) {
  // eslint-disable-next-line react-hooks/incompatible-library -- Las funciones de TanStack Table no se pasan a componentes memoizados.
  const tabla = useReactTable({
    data: datos?.items ?? [],
    columns: columnas,
    getCoreRowModel: getCoreRowModel(),
    manualPagination: true,
    manualSorting: true,
    defaultColumn: { enableSorting: false },
  });

  if (error) return <EstadoError error={error} onReintentar={onReintentar} />;
  if (cargando && !datos) return <EstadoCarga />;

  const campo = orden?.replace(/^-/, "");
  const descendente = orden?.startsWith("-") ?? false;
  const alternar = (id: string) => onOrden(campo !== id ? id : descendente ? undefined : `-${id}`);

  return (
    <div>
      <Table>
        <TableHeader>
          {tabla.getHeaderGroups().map((grupo) => (
            <TableRow key={grupo.id}>
              {grupo.headers.map((cabecera) => {
                const contenido = flexRender(cabecera.column.columnDef.header, cabecera.getContext());
                const activa = campo === cabecera.column.id;
                return (
                  <TableHead key={cabecera.id} aria-sort={activa ? (descendente ? "descending" : "ascending") : undefined}>
                    {cabecera.column.getCanSort() ? (
                      <Button variant="ghost" size="sm" className="-ml-2.5" onClick={() => alternar(cabecera.column.id)}>
                        {contenido}
                        {activa ? descendente ? <ArrowDown /> : <ArrowUp /> : <ChevronsUpDown className="text-muted-foreground" />}
                      </Button>
                    ) : (
                      contenido
                    )}
                  </TableHead>
                );
              })}
            </TableRow>
          ))}
        </TableHeader>
        <TableBody>
          {tabla.getRowModel().rows.map((fila) => (
            <TableRow key={fila.id}>
              {fila.getVisibleCells().map((celda) => (
                <TableCell key={celda.id}>{flexRender(celda.column.columnDef.cell, celda.getContext())}</TableCell>
              ))}
            </TableRow>
          ))}
        </TableBody>
      </Table>

      {datos && datos.items.length === 0 && <EstadoVacio titulo={vacio} />}

      {datos && datos.totalItems > 0 && (
        <div className="flex items-center justify-between gap-2 border-t px-4 py-3 text-sm text-muted-foreground">
          <span>
            {datos.totalItems} {datos.totalItems === 1 ? "registro" : "registros"} · página {datos.page} de {Math.max(datos.totalPages, 1)}
          </span>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={datos.page <= 1 || cargando} onClick={() => onPagina(datos.page - 1)}>
              Anterior
            </Button>
            <Button variant="outline" size="sm" disabled={datos.page >= datos.totalPages || cargando} onClick={() => onPagina(datos.page + 1)}>
              Siguiente
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
