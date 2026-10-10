import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { destinoSeguro, FormularioLogin } from "@/components/seguridad/FormulariosAcceso";
import { ErrorApi } from "@/services/http";

const navegacion = vi.hoisted(() => ({ replace: vi.fn(), parametros: new URLSearchParams() }));
const servicio = vi.hoisted(() => ({ login: vi.fn() }));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: navegacion.replace }),
  useSearchParams: () => navegacion.parametros,
}));
vi.mock("@/services", () => ({ servicioSesion: servicio }));

function montar() {
  const cliente = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  render(
    <QueryClientProvider client={cliente}>
      <FormularioLogin />
    </QueryClientProvider>,
  );
  return userEvent.setup();
}

async function ingresar(usuario: ReturnType<typeof userEvent.setup>, nombre: string, password: string) {
  if (nombre) await usuario.type(screen.getByLabelText("Usuario"), nombre);
  if (password) await usuario.type(screen.getByLabelText("Contraseña"), password);
  await usuario.click(screen.getByRole("button", { name: "Ingresar" }));
}

describe("FormularioLogin", () => {
  beforeEach(() => {
    navegacion.replace.mockReset();
    servicio.login.mockReset();
    navegacion.parametros = new URLSearchParams();
  });

  it("no llama al servidor si faltan datos y dice qué falta", async () => {
    const usuario = montar();
    await ingresar(usuario, "", "");

    expect(await screen.findByText("Ingrese su usuario.")).toBeInTheDocument();
    expect(screen.getByText("Ingrese su contraseña.")).toBeInTheDocument();
    expect(servicio.login).not.toHaveBeenCalled();
  });

  it("entra al inicio cuando las credenciales son correctas", async () => {
    servicio.login.mockResolvedValue({ debeCambiarPassword: false });
    const usuario = montar();
    await ingresar(usuario, " admin ", "Secreta.2026!");

    await waitFor(() => expect(navegacion.replace).toHaveBeenCalledWith("/"));
    expect(servicio.login).toHaveBeenCalledWith("admin", "Secreta.2026!");
  });

  it("vuelve a la pantalla que se había pedido", async () => {
    navegacion.parametros = new URLSearchParams({ siguiente: "/organizacion/sucursales" });
    servicio.login.mockResolvedValue({ debeCambiarPassword: false });
    const usuario = montar();
    await ingresar(usuario, "admin", "Secreta.2026!");

    await waitFor(() => expect(navegacion.replace).toHaveBeenCalledWith("/organizacion/sucursales"));
  });

  it("lleva a cambiar la contraseña cuando el servidor lo exige", async () => {
    navegacion.parametros = new URLSearchParams({ siguiente: "/organizacion/sucursales" });
    servicio.login.mockResolvedValue({ debeCambiarPassword: true });
    const usuario = montar();
    await ingresar(usuario, "admin", "Cambiar.2026!");

    await waitFor(() => expect(navegacion.replace).toHaveBeenCalledWith("/cambiar-password"));
  });

  it("muestra el mensaje del servidor cuando rechaza las credenciales", async () => {
    servicio.login.mockRejectedValue(new ErrorApi(401, { errorCode: "CREDENCIALES_INVALIDAS", detail: "Usuario o contraseña incorrectos." }));
    const usuario = montar();
    await ingresar(usuario, "admin", "incorrecta");

    expect(await screen.findByRole("alert")).toHaveTextContent("Usuario o contraseña incorrectos.");
    expect(navegacion.replace).not.toHaveBeenCalled();
  });

  it("avisa cuando la sesión anterior expiró", () => {
    navegacion.parametros = new URLSearchParams({ expirada: "1" });
    montar();
    expect(screen.getByRole("status")).toHaveTextContent("Su sesión expiró");
  });
});

describe("destinoSeguro", () => {
  const barraInvertida = String.fromCharCode(92);

  it.each([
    ["/organizacion/sucursales", "/organizacion/sucursales"],
    ["/seguridad/usuarios?q=ana", "/seguridad/usuarios?q=ana"],
    [null, "/"],
    ["", "/"],
    ["https://malicioso.example", "/"],
    ["//malicioso.example", "/"],
    [`/${barraInvertida}malicioso.example`, "/"],
    ["javascript:alert(1)", "/"],
  ])("%s → %s", (entrada, esperado) => {
    expect(destinoSeguro(entrada)).toBe(esperado);
  });
});
