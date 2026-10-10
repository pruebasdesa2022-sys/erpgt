# ERP — Frontend

Aplicación Next.js 16 (App Router). Las instrucciones de instalación y arranque están en el [README de la raíz](../README.md); el tema visual y cómo cambiar la marca, en [documentation/DISENO-UI.md](../documentation/DISENO-UI.md).

```
src/app/(publico)/     login, recuperar y restablecer contraseña
src/app/(app)/         pantallas con sesión (una carpeta por módulo)
src/app/api/           BFF: /api/auth (cookies de sesión) y /api/bff (reenvío a la API)
src/proxy.ts           redirige al login si no hay cookie de sesión (solo experiencia, no seguridad)
src/components/ui/     componentes base (shadcn/ui)
src/components/layout/ marco de la aplicación y marca
src/components/comun/  tabla de servidor, estados, formularios, guarda de permisos
src/services/          llamadas al BFF
src/types/             tipos que reflejan los DTO del backend
src/theme/marca.ts     nombre y logo
```

Comandos: `corepack yarn dev` · `build` · `start` · `lint` · `typecheck` · `test`.
