# ERP Modular — Diseño de interfaz

> Brief de diseño vigente y reglas para que un cambio de marca no toque lógica, servicios, permisos ni rutas (`ESPECIFICACION.md §B19`, `CLAUDE.md §11`).

## Tema actual

Tema **neutro**: escala de grises, sin color de marca, tipografía Geist, esquinas redondeadas medias. Es deliberadamente genérico: sirve de base hasta que exista una identidad definida.

- Nombre mostrado: **ERP**. Sin logo: se muestran las iniciales del nombre.
- Idioma `es-GT`. Fechas en hora de `America/Guatemala`. Moneda en quetzales (`Q`).
- Componentes de [shadcn/ui](https://ui.shadcn.com) (estilo `base-nova`, sobre Base UI) e iconos de Lucide.

## Dónde vive cada cosa

| Qué | Dónde | Se toca en un cambio de marca |
|---|---|---|
| Colores, radios, tipografías (tokens) | `frontend/src/app/globals.css` (`:root`, `.dark`, `@theme`) | Sí |
| Nombre, descripción y logo | `frontend/src/theme/marca.ts` | Sí |
| Fuentes cargadas | `frontend/src/app/layout.tsx` | Sí |
| Componentes base (botón, campo, diálogo, tabla…) | `frontend/src/components/ui/` | Sí, si cambia el estilo de los controles |
| Marco de la aplicación (login, menú lateral, cabecera) | `frontend/src/components/layout/` | Sí, si cambia la disposición |
| Pantallas, formularios, servicios, permisos, rutas | todo lo demás | **No** |

## Reglas

1. **Ningún color fijo fuera de `ui` y `layout`.** Las pantallas usan solo clases basadas en tokens: `bg-primary`, `text-muted-foreground`, `border`, `bg-destructive/10`, etc. Una regla de ESLint (`eslint.config.mjs`) rechaza colores hexadecimales, `rgb()/hsl()/oklch()` y la paleta de Tailwind (`bg-red-500`, `text-blue-600`…) en el resto del código.
2. **El nombre de la marca nunca se escribe en una pantalla**: se toma de `marca.ts`.
3. **`components/ui` son envoltorios delgados**: sin llamadas a la API ni reglas de negocio.
4. **Los iconos del menú** se guardan por nombre en la base (`menu.icono`) y se traducen en `components/layout/Marca.tsx`. Un nombre desconocido muestra un icono genérico.

## Cómo cambiar la marca

1. Edite los tokens de `globals.css` (como mínimo `--primary`, `--primary-foreground`, `--radius` y los `--sidebar-*`).
2. Ponga el logo en `frontend/public/` y su ruta, nombre y descripción en `marca.ts`.
3. Si cambia la tipografía, reemplace las fuentes en `app/layout.tsx`.
4. Ejecute `corepack yarn lint` y `corepack yarn build`.

## Comportamiento común de las pantallas

- **Listados**: búsqueda, orden y paginación en el servidor (`components/comun/TablaServidor.tsx` y `hooks/useListado.ts`).
- **Estados**: toda pantalla muestra carga, vacío y error con reintento (`components/comun/Estados.tsx`).
- **Formularios**: validación inmediata en el navegador (Zod) y errores del servidor bajo su campo; el servidor siempre valida de nuevo.
- **Acciones destructivas**: piden confirmación (`DialogoConfirmar`).
- **Errores de la API**: 400 bajo el campo; 403, 409 y 422 como notificación con el mensaje del servidor; 401 vuelve al inicio de sesión; 500 muestra el identificador de la operación para soporte (`lib/errores.ts`).
- **Permisos**: los botones se muestran con `<Puede permiso="…">`. Es solo presentación.

## Pendiente

- Modo oscuro: los tokens `.dark` existen, pero no hay selector de tema.
- Formatos de impresión (carta y ticket de 80 mm): se definen en la etapa 1E.
