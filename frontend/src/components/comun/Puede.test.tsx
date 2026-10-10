import { render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { describe, expect, it } from "vitest";
import { Puede } from "@/components/comun/Puede";
import { ProveedorSesion } from "@/contexts/sesion";
import type { Sesion } from "@/types";

const sesion: Sesion = {
  usuarioId: 1,
  nombreUsuario: "ana",
  nombreCompleto: "Ana",
  correo: "ana@pruebas.local",
  debeCambiarPassword: false,
  empresaId: 1,
  sucursalId: 1,
  empresas: [],
  sucursales: [],
};

function conPermisos(permisos: string[], contenido: ReactNode) {
  return render(<ProveedorSesion datos={{ sesion, menu: [], permisos }}>{contenido}</ProveedorSesion>);
}

describe("Puede", () => {
  it("muestra el contenido cuando el usuario tiene el permiso", () => {
    conPermisos(["ORGANIZACION_SUCURSALES_CREAR"], <Puede permiso="ORGANIZACION_SUCURSALES_CREAR">Nueva sucursal</Puede>);
    expect(screen.getByText("Nueva sucursal")).toBeInTheDocument();
  });

  it("oculta el contenido cuando no lo tiene", () => {
    conPermisos(["ORGANIZACION_SUCURSALES_VER"], <Puede permiso="ORGANIZACION_SUCURSALES_CREAR">Nueva sucursal</Puede>);
    expect(screen.queryByText("Nueva sucursal")).not.toBeInTheDocument();
  });

  it("muestra la alternativa cuando no lo tiene", () => {
    conPermisos(
      [],
      <Puede permiso="X_Y_VER" alternativa="Sin acceso">
        Contenido
      </Puede>,
    );
    expect(screen.getByText("Sin acceso")).toBeInTheDocument();
    expect(screen.queryByText("Contenido")).not.toBeInTheDocument();
  });

  it("no confunde un permiso con otro de nombre parecido", () => {
    conPermisos(["ORGANIZACION_SUCURSALES_VER"], <Puede permiso="ORGANIZACION_SUCURSALES">Contenido</Puede>);
    expect(screen.queryByText("Contenido")).not.toBeInTheDocument();
  });
});
