import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Solo desarrollo: IP o nombre con que se abre la app desde otro equipo o el celular (sin puerto), separados por
  // comas. Sin esto `next dev` bloquea los scripts de la página cuando no se entra por localhost.
  allowedDevOrigins: (process.env.DEV_ORIGINS ?? "").split(",").map((o) => o.trim()).filter(Boolean),
  // Cabeceras de seguridad para todas las respuestas del frontend.
  async headers() {
    return [
      {
        source: "/:path*",
        headers: [
          { key: "X-Content-Type-Options", value: "nosniff" },
          { key: "X-Frame-Options", value: "DENY" },
          { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
          { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=()" },
        ],
      },
    ];
  },
  cacheComponents: true,
  partialPrefetching: true,
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
};

export default nextConfig;
