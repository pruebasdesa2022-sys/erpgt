import { NextResponse, type NextRequest } from "next/server";

const RUTAS_PUBLICAS = ["/login", "/recuperar", "/restablecer"];

/**
 * Solo mejora la experiencia: manda al login a quien no tiene cookie de sesión.
 * NO es seguridad; la API valida cada solicitud por su cuenta.
 */
export function proxy(solicitud: NextRequest) {
  const { pathname, search } = solicitud.nextUrl;
  const esPublica = RUTAS_PUBLICAS.some((ruta) => pathname === ruta || pathname.startsWith(`${ruta}/`));

  if (!esPublica && !solicitud.cookies.has("erp_refresh")) {
    const destino = new URL("/login", solicitud.url);
    if (pathname !== "/") destino.searchParams.set("siguiente", pathname + search);
    return NextResponse.redirect(destino);
  }

  return NextResponse.next();
}

export const config = {
  // Todo menos la API del BFF, los recursos internos de Next y los archivos estáticos.
  matcher: ["/((?!api|_next/static|_next/image|favicon.ico|.*\.(?:png|svg|ico|webp)$).*)"],
};
