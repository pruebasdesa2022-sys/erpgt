# ERP Modular — Progreso

> Se lee al iniciar cada sesión (`CLAUDE.md §2.1`). Mantener corto: estado, decisiones, pendientes.

## Estado actual

| | |
|---|---|
| **Etapa en curso** | **1A — Maestros y organización**: implementada (base de datos, backend y frontend). Pendiente de revisión y aprobación del dueño |
| **Siguiente etapa** | 1B — Clientes, proveedores y productos. **No iniciar sin que el dueño lo pida** |
| **Rama** | `fase-1a` (creada desde `fase-0`; ninguna está fusionada a `main`) |
| **Esquema** | 0.2.1 — una base 0.1.0 necesita, en orden, `20261010_01_fase_1a_maestros.sql` y `20261010_02_permiso_catalogos_globales.sql`; una base 0.2.0 solo la segunda (**las ejecuta el dueño**) |
| **Última actualización** | 2026-10-10 |

## Etapas

| Etapa | Estado |
|---|---|
| Diseño (C0) | Aprobado |
| Fase 0 | Implementada — espera aprobación |
| 1A | Implementada — espera aprobación |
| 1B · 1C · 1D · 1E · 1F · 1G | No iniciadas |
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

## Qué incluye 1A (maestros y organización)

- **Base de datos** (v0.2.0, 45 tablas): `bodega`, `caja`, `departamento`, `centro_costo`, `usuario_bodega`, `usuario_caja`; `moneda`, `tipo_cambio`, `impuesto`, `unidad_medida`, `unidad_conversion`; `tipo_producto`, `categoria`, `marca`, `tipo_proveedor`, `motivo_anulacion`, `banco`, `cuenta_bancaria`, `forma_pago`, `condicion_pago`; `tipo_documento`, `serie_documento` y el procedimiento `sp_siguiente_correlativo`. `empresa` gana `regimen_tributario` y `moneda_local_id`. El módulo `CATALOGOS` queda liberado y base.
- **Backend:** motor genérico de catálogos (ADR-024) con 15 catálogos; servicios dedicados de tipo de cambio, impuestos, conversiones, series, numeración (`INumeracion`), configuración y fecha de bloqueo (`IPeriodoBloqueo`); edición de empresa con NIT validado; asignación de bodegas y cajas por usuario. Todos los endpoints con `[RequierePermiso]`, auditoría en la misma transacción, concurrencia optimista y baja lógica.
- **Frontend:** 20 pantallas nuevas (empresa, estructura, catálogos, series, configuración), todas a partir de `CatalogoPagina` salvo empresa y configuración; menú con grupos; diálogo de bodegas y cajas en Usuarios.
- **«Preparado», sin consumidor todavía:** `INumeracion`, `IPeriodoBloqueo`, `ITiposCambioConsulta`, `IConfiguracionNegocio`, `GrafoConversiones` (factores compuestos) y las columnas `tipo_producto.maneja_inventario` y `forma_pago.requiere_referencia`; los usan 1B–1F.

## Verificación de la etapa 1A (2026-10-10)

| Comprobación | Resultado |
|---|---|
| `dotnet build ERP.sln` | Correcto, 0 advertencias |
| `dotnet test` | 562 unitarias y de arquitectura + 86 de integración, todas pasan (incluye 50 asignaciones de correlativo concurrentes sin saltos ni repetidos, reversión del número, 8 altas simultáneas del mismo código, 8 cambios simultáneos de un parámetro y aislamiento entre empresas) |
| `corepack yarn typecheck` / `lint` / `test` / `build` | Correctos; 133 pruebas de frontend |
| `database.sql` 0.2.0 vs. 0.1.0 + script incremental | Esquemas idénticos (columnas, índices, FK, CHECK, rutinas) y mismos seeds; comparados en bases temporales |
| Flujo real por el BFF contra una base temporal | 26 verificaciones: login y cambio de contraseña, menú, empresa (NIT), bodegas y cajas, marcas, exportación CSV, tipo de cambio, impuestos, unidades, series, fecha de bloqueo, configuración, bitácora y 401 sin cookie |
| Recorrido de pantallas con Edge sin ventana | 23 pantallas y 4 interacciones sin errores de consola ni de red |

### Correcciones de la revisión adversarial (2026-10-10)

Aplicadas tras la revisión externa de 1A, sobre la misma rama. Cada una tiene prueba automática en `CatalogosCorreccionesTests.cs`:

| Hallazgo | Corrección |
|---|---|
| A1 — editar otra empresa con el permiso de la activa | `PUT /organizacion/empresas/{id}` solo vale para la empresa activa (403 `EMPRESA_NO_ACTIVA`) |
| A2/A3 — accesos de bodega y caja entre empresas | Se ven, validan y reemplazan solo los de la empresa activa; el usuario debe pertenecer a ella (404 si no) |
| M1 — catálogos globales | Monedas y tipos de documento solo con `CATALOGOS_GLOBALES_EDITAR`, que concede únicamente el rol ADMINISTRADOR (ADR-025) |
| M2 — baja con dependientes | No se desactiva una unidad con conversiones, un tipo de documento con series, ni una caja o sucursal con series activas |
| M4 — alcance de la serie | `INumeracion` recibe tipo, sucursal y caja del documento y rechaza series de otro alcance (422) |
| M5 — período bloqueado | Tipos de cambio e impuestos respetan `fecha_bloqueo` (422 `PERIODO_BLOQUEADO`) |
| M6 — escrituras simultáneas de configuración | Se serializan por empresa; antes la segunda podía responder 500 |

El script incremental `20261010_02` se comparó contra una instalación limpia 0.2.1 en bases temporales (mismos permisos, concesiones del administrador y menú) y se comprobó que no se puede ejecutar dos veces.

**Sin verificar en 1A:** que la aplicación corra con el usuario MySQL de mínimo privilegio y que `EXECUTE` sobre `sp_siguiente_correlativo` funcione con él (la plantilla ya lo incluye; ver pendiente 1). El script incremental se verificó en bases temporales, no en `erpgt`.

## Verificación de Fase 0 (2026-10-10)

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
- **2026-10-10 — Decisiones de 1A** en ADR-024: motor genérico de catálogos, alcance global/empresa/estándar, `fecha_bloqueo` como «último día cerrado», categorías planas, tipos de documento cerrados, permisos de recurso de una palabra, accesos por bodega/caja sin contexto de sesión todavía.
- **2026-10-10 — Archivos de Fase 0 tocados en 1A** (anunciados en el plan o necesarios): `SucursalesService` (no desactivar con dependientes), `Organizacion.cs` (`EmpresaDto` y edición de empresa), `UnitOfWork` (registro del manejador de `DateOnly`), `Barra` del frontend (parámetro opcional para ocultar la búsqueda), página de Usuarios (botón de bodegas y cajas) y, en pruebas, el instalador (`DELIMITER`) y dos pruebas que dependían del estado de `CATALOGOS` y del número de tablas.
- **2026-10-10 — Decisiones tras la revisión adversarial** en ADR-025: catálogos globales solo para el administrador; el bloqueo de período cubre tipos de cambio e impuestos; la serie se valida contra el alcance del documento. Se **descartó**, con aviso al dueño, impedir series activas traslapadas del mismo tipo, sucursal y caja (ver pendiente 18).
- Decisiones anteriores (diseño, esquema de Fase 0): ver `DECISIONES.md`.

## Pendientes y problemas conocidos

1. **Usuario MySQL de la aplicación.** La API de desarrollo usa `root` (User Secrets). Falta crear el usuario con `database/usuarios-ejemplo.sql` (ya incluye las tablas de 1A y `EXECUTE` del procedimiento) y comprobar que la aplicación funciona con él (criterio 7).
2. **Certificado HTTPS de desarrollo.** Instalado y confiable en este equipo (`dotnet dev-certs https --trust`, vence el 2027-10-09). El perfil `https` de la API (`https://localhost:7080`) no se ha probado; el desarrollo normal usa `http://localhost:5080`.
3. **`erpgt` conserva la contraseña inicial** del administrador: el flujo se probó en bases temporales para no alterarla.
4. **Correo.** En desarrollo los mensajes se guardan en `backend/src/ERP.Api/correo-saliente/`. Para producción falta configurar SMTP (`Correo:Modo = Smtp`).
5. **Seguridad pendiente para 1G:** cabecera `Content-Security-Policy` en el frontend; limpieza periódica de `refresh_token`, `intento_login` y `token_recuperacion`; límite de solicitudes general (hoy solo en autenticación).
6. **Mensajes de validación del servidor** nombran el campo en formato técnico (por ejemplo `'Nombre Usuario' no debería estar vacío`). El formulario valida antes con mensajes propios; mejorar los del servidor queda pendiente.
7. **Modo oscuro**: los tokens existen, no hay selector.
8. `ESPECIFICACION.md §B2` conserva el orden antiguo de secciones del script; prevalece `CLAUDE.md §7`.
9. `PROMPTS.md`, citado en la especificación, no existe en el proyecto.
10. Los diagramas Mermaid de `DISENO.md` no se han comprobado en un visor.
11. **Script incremental `20261010_02_permiso_catalogos_globales.sql` en `erpgt`** (lo hace el dueño; el `_01` ya se ejecutó). Solo cambia datos (permisos), no crea tablas: no requiere privilegios nuevos para el usuario de la aplicación. Después de aplicarlo, los usuarios que tenían los permisos retirados (`CATALOGOS_MONEDAS_CREAR/EDITAR/ELIMINAR`, `CATALOGOS_TIPOSDOCUMENTO_EDITAR`) los pierden; solo el rol ADMINISTRADOR queda con `CATALOGOS_GLOBALES_EDITAR`.
12. **NIT de la empresa semilla:** es `CF` y el validador no lo acepta; al editar la empresa hay que capturar el NIT real.
13. **Confirmar con el contador:** monto máximo de consumidor final (Q2,500 sembrado) e IVA 12 % sembrado con vigencia desde 2000-01-01.
14. **Conversión estándar KG–LB** aproximada a 6 decimales (2.204623).
15. **Bodega y caja activas en el contexto de sesión** y su validación por solicitud: se agregan con 1C y 1F.
16. **Moneda local:** cambiarla no recalcula nada; solo es seguro antes de registrar documentos. **Antes de cerrar 1C** debe bloquearse su cambio cuando existan movimientos o documentos (hoy no hay ninguno que lo impida).
17. **Valores históricos en los documentos (1C en adelante):** cada documento debe guardar su propia tasa de cambio y su porcentaje de impuesto; editar o cerrar una vigencia no debe reinterpretar lo ya emitido. El bloqueo de período lo protege solo en períodos cerrados.
18. **Series activas traslapadas.** No se impide tener dos series activas del mismo tipo, sucursal y caja con vigencias que se cruzan: `INumeracion` recibe el id de la serie, así que no hay ambigüedad al numerar, y prohibirlo impediría usar varias series a la vez (por ejemplo A y B). Falta definir en 1E cómo se elige la serie por defecto de una caja.
19. **Bajos de la revisión sin aplicar** (no estaban en las correcciones aceptadas): aviso cuando la exportación llega a 10 000 filas; el CSV antepone `'` a textos que empiezan por `+` (teléfonos); enumeraciones duplicadas en backend, frontend y CHECK; tope de longitud de `bodegaIds` y `cajaIds`; formato del número cuando excede la longitud; el correlativo no se reinicia por año aunque la serie incluya el año; los selectores de un formulario consumen el listado de otro catálogo y exigen su permiso `VER`.
20. **Rotar el `Jwt:Clave` de desarrollo** (`dotnet user-secrets set "Jwt:Clave" "<nueva>" --project src/ERP.Api`): se mostró en una salida de consola durante la etapa. No está en el repositorio.

## Comandos de verificación

```
backend/    dotnet build ERP.sln · dotnet test · dotnet run --project src/ERP.Api --launch-profile http
frontend/   corepack yarn install · dev · build · lint · typecheck · test
base        mysql -u root -p --default-character-set=utf8mb4 -e "source database/database.sql"   (la ejecuta el dueño)
```

Git no está en el `PATH` de las sesiones de Claude Code en este equipo; se invoca como `C:\Program Files\Git\cmd\git.exe`.
