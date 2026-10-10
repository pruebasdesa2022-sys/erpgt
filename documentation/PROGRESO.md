# ERP Modular — Progreso

> Se lee al iniciar cada sesión (`CLAUDE.md §2.1`). Mantener corto: estado, decisiones, pendientes.

## Estado actual

| | |
|---|---|
| **Etapa en curso** | **Fase 0 — Fundamentos**: implementada (base de datos, backend y frontend). Pendiente de revisión y aprobación del dueño |
| **Siguiente etapa** | 1A — Maestros y organización. **No iniciar sin que el dueño lo pida** |
| **Rama** | `fase-0` (sin fusionar a `main`) |
| **Última actualización** | 2026-10-10 |

## Etapas

| Etapa | Estado |
|---|---|
| Diseño (C0) | Aprobado |
| Fase 0 | Implementada — espera aprobación |
| 1A · 1B · 1C · 1D · 1E · 1F · 1G | No iniciadas |
| 1H (FEL, flotante) | Pendiente de certificador |
| Fase 2 · Fase 3 | No iniciadas |

## Qué incluye Fase 0

- **Base de datos** `erpgt` (v0.1.0): 23 tablas y seeds indispensables. Plantilla del usuario MySQL de la aplicación en `database/usuarios-ejemplo.sql`.
- **Backend** (.NET 10, capas Domain / Application / Infrastructure / Api):
  - Autenticación: login, refresh rotativo con detección de reutilización, logout, bloqueo por intentos, cambio obligatorio de contraseña, recuperación por correo, cambio de empresa/sucursal.
  - Autorización en servidor: contexto → módulo → permiso, con denegar > conceder > roles y caché invalidable.
  - Administración: usuarios, roles, permisos directos, estado de módulos, menú dinámico, bitácora.
  - Catálogo de referencia **Sucursales** (patrón CRUD para los demás): listado en servidor, concurrencia optimista, baja lógica, exportación CSV, auditoría en la misma transacción.
  - Transversal: ProblemDetails, correlation id, Serilog, limitación de tasa, `/health`, `/health/version`, OpenAPI en desarrollo.
- **Frontend** (Next.js 16): BFF con cookies httpOnly, `proxy.ts`, login, recuperación, cambio de contraseña, marco con menú dinámico y selector de sucursal, pantallas de usuarios, roles, módulos, sucursales y bitácora. Tema por tokens (`documentation/DISENO-UI.md`).

## Verificación realizada (2026-10-10)

| Comprobación | Resultado |
|---|---|
| `dotnet build ERP.sln` | Correcto, 0 advertencias (las advertencias son errores) |
| `dotnet test` | 157 unitarias y de arquitectura + 34 de integración, todas pasan |
| `corepack yarn typecheck` / `lint` / `test` / `build` | Correctos; 39 pruebas de frontend |
| Flujo real por el BFF contra una base temporal | Redirección al login, CSRF, login, cambio obligatorio de contraseña, menú, CRUD de sucursales, 409 por duplicado y por versión vieja, exportación, bitácora, logout |
| Recorrido de pantallas con navegador sin ventana | 14 pasos (login, sucursales, usuarios, roles, módulos, bitácora, menú de usuario) sin errores de consola ni de red |

Criterios de aceptación de `ESPECIFICACION.md §C1`:

1. `database.sql` se instala en MySQL 8.4 vacío — **cumplido** (cada corrida de pruebas lo instala en 8.4.11).
2. Compila, pasa pruebas, `yarn dev` y la API arrancan — **cumplido**. Abrir en Visual Studio y depurar desde VS Code (`.vscode/launch.json`) — **sin verificar**.
3. Login, refresh, logout, bloqueo y cambio obligatorio de contraseña — **cumplido** (pruebas de integración).
4. 401 sin token, 403 sin permiso, 403 con módulo no liberado y prueba "todo endpoint tiene permiso" — **cumplido**.
5. El menú cambia según rol y estado del módulo; llamar a la API directamente no evita los controles — **cumplido**.
6. Cada operación del catálogo de referencia deja auditoría — **cumplido**.
7. La aplicación corre con un usuario MySQL sin DDL — **sin verificar**: se probó con `root`. La plantilla de otorgamientos existe pero no se ha ejecutado.

## Decisiones

- **2026-10-10 — MySQL 8.4.11** instalado por el dueño; el 8.0 se retiró. ADR-021 queda sin efecto.
- **2026-10-10 — Decisiones de implementación** en ADR-023: marca `[SoloAutenticado]`, rol de sistema no editable, módulos base siempre liberados, cambiar contraseña cierra todas las sesiones, cookies `SameSite=Strict` con verificación de origen, TanStack Table fijado en la versión 8.
- **2026-10-10 — Único cambio a `database.sql`** en esta sesión: el comentario de cabecera con la versión del motor (anunciado en el plan).
- **2026-10-10 — Fuera de Fase 0** (no estaban en `§C1`): pantalla para editar la política de contraseñas y los datos de la empresa (1A), listado y cierre de sesiones activas, idempotencia (`Idempotency-Key`, 1C).
- Decisiones anteriores (diseño, esquema de Fase 0): ver `DECISIONES.md`.

## Pendientes y problemas conocidos

1. **Usuario MySQL de la aplicación.** La API de desarrollo usa `root` (User Secrets). Falta crear el usuario con `database/usuarios-ejemplo.sql` y comprobar que la aplicación funciona con él (criterio 7).
2. **Certificado HTTPS de desarrollo.** Instalado y confiable en este equipo (`dotnet dev-certs https --trust`, vence el 2027-10-09). El perfil `https` de la API (`https://localhost:7080`) no se ha probado; el desarrollo normal usa `http://localhost:5080`.
3. **`erpgt` conserva la contraseña inicial** del administrador: el flujo se probó en bases temporales para no alterarla.
4. **Correo.** En desarrollo los mensajes se guardan en `backend/src/ERP.Api/correo-saliente/`. Para producción falta configurar SMTP (`Correo:Modo = Smtp`).
5. **Seguridad pendiente para 1G:** cabecera `Content-Security-Policy` en el frontend; limpieza periódica de `refresh_token`, `intento_login` y `token_recuperacion`; límite de solicitudes general (hoy solo en autenticación).
6. **Mensajes de validación del servidor** nombran el campo en formato técnico (por ejemplo `'Nombre Usuario' no debería estar vacío`). El formulario valida antes con mensajes propios; mejorar los del servidor queda pendiente.
7. **Modo oscuro**: los tokens existen, no hay selector.
8. `ESPECIFICACION.md §B2` conserva el orden antiguo de secciones del script; prevalece `CLAUDE.md §7`.
9. `PROMPTS.md`, citado en la especificación, no existe en el proyecto.
10. Los diagramas Mermaid de `DISENO.md` no se han comprobado en un visor.

## Comandos de verificación

```
backend/    dotnet build ERP.sln · dotnet test · dotnet run --project src/ERP.Api --launch-profile http
frontend/   corepack yarn install · dev · build · lint · typecheck · test
base        mysql -u root -p --default-character-set=utf8mb4 -e "source database/database.sql"   (la ejecuta el dueño)
```

Git no está en el `PATH` de las sesiones de Claude Code en este equipo; se invoca como `C:\Program Files\Git\cmd\git.exe`.
