# ERP Modular — Reglas permanentes del proyecto

> Claude Code lee este archivo en CADA sesión. Mantenlo corto y vigente.
> Detalle funcional: `documentation/ESPECIFICACION.md` · Estado actual: `documentation/PROGRESO.md`

## 1. Qué construimos

ERP modular para empresas en Guatemala. Es **un solo sistema** (no tres) que crece por fases sobre la misma base de seguridad, usuarios, empresas, auditoría y catálogos:

- **Fase 1** — Operación comercial (maestros, clientes, proveedores, productos, inventario, compras, ventas, caja).
- **Fase 2** — Contabilidad, cuentas por cobrar y por pagar.
- **Fase 3** — Recursos humanos y nómina.

Debe ser una base de producción mantenible por otro desarrollador, no una demo.

## 2. Forma de trabajo (OBLIGATORIA)

1. Al iniciar una sesión lee este archivo y `documentation/PROGRESO.md`. Luego lee SOLO la sección de la especificación que corresponde a la etapa en curso; no releas todo el documento.
2. Una etapa a la vez, en este orden: **Diseño → Fase 0 → 1A → 1B → 1C → 1D → 1E → 1F → 1G → Fase 2 → Fase 3**.
3. Ciclo de cada etapa: plan breve → **espera aprobación del usuario** → implementa → verifica (compila, pruebas, ejecuta) → actualiza `PROGRESO.md` y documentación → commit → **detente y reporta**. No inicies la siguiente etapa si el usuario no lo pide.
4. Ante una ambigüedad o contradicción en la especificación, pregunta (todas las dudas en una sola tanda) antes de construir sobre ella. Si no hay quien responda, elige la opción más conservadora, regístrala en `PROGRESO.md > Decisiones` y sigue.
5. Sin stubs, TODOs ni placeholders en lo que la etapa declara implementado. Lo que solo queda "preparado" se marca así en la documentación.
6. Antes de modificar código existente revisa referencias y convenciones. Reutiliza antes de crear. No generes archivos huérfanos que no estén integrados al proyecto.
7. Mantén coherencia entre tablas, entidades, DTOs, endpoints y tipos del frontend. Si cambia uno, revisa los demás en la misma tarea.
8. Justifica en `documentation/DECISIONES.md` (formato corto: contexto, decisión, consecuencias) toda decisión arquitectónica importante y toda dependencia nueva (nombre, versión, licencia, para qué).

## 3. Límites de seguridad para ti (no negociables)

- No ejecutes SQL contra ninguna base de datos sin permiso explícito del usuario. Por defecto **el usuario ejecuta** `database.sql`. Nunca toques bases de QA o producción.
- Nunca escribas secretos, contraseñas ni cadenas de conexión reales en archivos versionados.
- Nunca uses `git push --force`, no borres ni sobrescribas archivos del usuario sin que lo pida.
- No instales herramientas globales ni cambies configuración del equipo sin avisar antes.

## 4. Stack (versiones fijadas; cambiarlas requiere entrada en DECISIONES.md)

| Capa | Tecnología |
|---|---|
| Base de datos | **MySQL 8.4 LTS**, InnoDB, `utf8mb4` |
| Backend | **C# / .NET 10 LTS**, ASP.NET Core Web API (controladores) |
| Acceso a datos | **Dapper + MySqlConnector**, SQL parametrizado. **Sin EF Core ni migraciones.** |
| Validación | FluentValidation |
| Logging | Serilog (archivo rotativo + consola) |
| Pruebas backend | xUnit (+ aserciones sin licencia comercial) |
| Frontend | **Next.js 16 (App Router), React, TypeScript estricto**, **Yarn** (vía Corepack, versión fijada en `packageManager`) |
| UI frontend | Tailwind CSS + shadcn/ui · TanStack Query · TanStack Table · React Hook Form + Zod |

Reglas de dependencias: pocas y justificadas. **Revisa la licencia de cada paquete** (varias librerías populares de .NET pasaron a licencia comercial en sus versiones recientes: evita MediatR y AutoMapper; mapea a mano). Docker NO es requisito para desarrollar ni para producción.

> Por qué .NET 10 y no 8: .NET 8 termina su soporte el 10-nov-2026; .NET 10 es LTS hasta nov-2028.

## 5. Estructura física

```
ERP/
├── CLAUDE.md · README.md · .gitignore · .editorconfig
├── .vscode/                  launch.json, tasks.json, extensions.json
├── backend/
│   ├── ERP.sln               formato clásico: `dotnet new sln --format sln`
│   ├── global.json · Directory.Build.props · Directory.Packages.props
│   ├── src/
│   │   ├── ERP.Domain/           entidades, estados, reglas puras (sin dependencias externas)
│   │   ├── ERP.Application/      casos de uso, DTOs, validadores, interfaces (puertos)
│   │   ├── ERP.Infrastructure/   Dapper/MySQL, JWT, archivos, correo, reloj, FEL (adaptadores)
│   │   ├── ERP.Api/              controladores, middleware, Program.cs, appsettings
│   │   └── ERP.Worker/           procesos en segundo plano (se crea cuando haga falta)
│   └── tests/ ERP.UnitTests/ · ERP.IntegrationTests/
├── frontend/                 package.json, yarn.lock, next.config.*, tsconfig.json, .env.example, src/
├── database/
│   ├── database.sql          FUENTE OFICIAL: instalación limpia completa
│   ├── updates/              scripts incrementales para bases ya instaladas
│   └── demo-data.sql         datos de prueba (opcional, jamás en producción)
├── deploy/                   scripts PowerShell para Windows Server
└── documentation/            ESPECIFICACION, PROGRESO, DECISIONES, DESPLIEGUE-WINDOWS, etc.
```

Debe abrirse y ejecutarse desde **Visual Studio (Run/Debug) y desde VS Code** (`dotnet run`, C# Dev Kit) sin pasos manuales de reconstrucción.

## 6. Arquitectura

- **Monolito modular.** Cada módulo (Seguridad, Organización, Catálogos, Clientes, Proveedores, Productos, Inventario, Compras, Ventas, Caja, Contabilidad, CxC, CxP, RRHH, Nómina, Reportes, Auditoría) es una carpeta/namespace dentro de cada proyecto.
- Dependencias: `Api → Application → Domain` y `Infrastructure → Application`. Domain no depende de nada.
- Un módulo **no** consulta ni escribe tablas de otro; usa sus interfaces o eventos. Los módulos de Fase 2 y 3 no deben requerir cambios en las tablas transaccionales de Fase 1 (ver "Preparación contable" en la especificación).
- Las reglas de negocio viven en C# (Domain/Application), **incluido el cálculo de costos** (promedio ponderado, etc.): el SP de inventario/caja solo bloquea la fila (`FOR UPDATE`) y persiste el resultado que ya calculó el backend, nunca calcula él mismo. La base de datos garantiza integridad (PK, FK, UNIQUE, CHECK, NOT NULL). Los procedimientos almacenados se limitan a operaciones atómicas sensibles a concurrencia (correlativos, bloqueo de inventario/caja) y a consultas pesadas de reportes. Prohibido poner reglas de negocio en triggers.
- Pruebas de arquitectura automáticas (NetArchTest u otra con licencia libre) para hacer cumplir las dependencias.

## 7. Base de datos — `database/database.sql` es la fuente de verdad

- Un único script maestro, ordenado por secciones con comentarios (`-- ===== SEGURIDAD =====`), con: base de datos, tablas, FK, índices, constraints, vistas, procedimientos, funciones, triggers (solo si son imprescindibles), seeds y catálogos indispensables, configuración inicial. **Orden de secciones por dependencia de FK** (una sección no referencia una tabla de una sección posterior): CONFIGURACIÓN → ORGANIZACIÓN (empresa, sucursal, bodega, caja — sin FK a usuario) → SEGURIDAD (usuario y sus tablas, que sí referencian organización) → CATÁLOGOS → … Si una sección necesita referenciar algo que nace después por razones de negocio, la FK se agrega al final del script, en su propia sección `-- ===== RELACIONES DIFERIDAS =====`.
- **Instalación limpia** = ejecutar solo `database.sql` en un servidor vacío. Debe terminar sin errores (hay una prueba de integración que lo verifica).
- **Bases ya instaladas**: todo cambio de esquema se escribe en DOS lugares en el mismo commit: (1) integrado en `database.sql`, y (2) como script incremental `database/updates/AAAAMMDD_NN_descripcion.sql`, seguro de ejecutar una sola vez y registrado en la tabla `esquema_version`. Nunca se asume que se re-ejecuta `database.sql` sobre una base con datos.
- La aplicación **nunca** modifica el esquema (nada de `Database.Migrate()`, `Update-Database`, DDL al iniciar). El usuario MySQL de la aplicación solo tiene DML + `EXECUTE`; otro usuario con privilegios corre los scripts.
- Convenciones: tablas y columnas en `snake_case` español; PK `id BIGINT UNSIGNED AUTO_INCREMENT`; FK `<tabla>_id`; todas las FK con índice; en tablas multiempresa `empresa_id` encabeza los índices de consulta.
- Dinero `DECIMAL(18,4)` · cantidades `DECIMAL(18,6)` · tasas/porcentajes `DECIMAL(9,6)` · tipo de cambio `DECIMAL(18,8)`. En C#: siempre `decimal`, jamás `float`/`double`. Redondeo documentado, `MidpointRounding.AwayFromZero`.
- Fechas/horas técnicas en UTC (`DATETIME(6)`); fecha de negocio en `DATE`. Zona de negocio `America/Guatemala` (UTC−6, sin horario de verano). En código usa `IClock`, no `DateTime.Now`.
- Columnas de auditoría en toda tabla de negocio: `creado_en, creado_por, modificado_en, modificado_por, activo`, y `version_fila` (concurrencia optimista) donde se edite.
- Sin borrado físico de información crítica: baja lógica o estado `ANULADO`. Cuidado con los UNIQUE y la baja lógica (usa columna generada o índice que lo considere).
- Seeds: solo lo indispensable. El usuario administrador inicial se crea con contraseña temporal documentada en el README y `debe_cambiar_password = 1`. Los datos de prueba van en `demo-data.sql`, nunca en `database.sql`.

## 8. Seguridad (el backend nunca confía en el frontend)

- Cada endpoint pasa esta cadena, en este orden: **autenticado → contexto empresa/sucursal válido para el usuario → módulo habilitado (global y por empresa) → permiso requerido → regla de negocio → ejecutar**. Ocultar un menú NO es seguridad.
- Todo endpoint lleva `[Authorize]` y `[RequierePermiso("CODIGO")]`. Las excepciones (login, refresh, health) usan `[AllowAnonymous]` explícito. **Una prueba automática recorre todos los controladores y falla si algún endpoint no cumple.**
- Códigos de permiso: `{MODULO}_{RECURSO}_{ACCION}` (ej. `VENTAS_FACTURAS_ANULAR`). Acciones: VER, CREAR, EDITAR, PROCESAR, ANULAR, ELIMINAR, APROBAR, EXPORTAR, IMPRIMIR. `PROCESAR` es la transición que convierte un documento de PENDIENTE/APROBADO a PROCESADO (consume correlativo, afecta inventario/caja); es distinta de CREAR porque un usuario puede armar un documento sin autorización para confirmarlo.
- Autenticación: JWT de acceso corto (≈15 min) + refresh token con rotación y detección de reutilización (guardado como hash). Contraseñas con hash fuerte (Argon2id o PBKDF2 del framework), política de complejidad, bloqueo por intentos, rate limiting en login. Los permisos NO viajan completos en el JWT: se resuelven en servidor (con caché invalidable).
- HTTPS + HSTS, cabeceras de seguridad, CORS restringido a orígenes configurados, validación de entrada en servidor, **todo SQL parametrizado**, sin datos sensibles en logs, subida de archivos validada (tipo, tamaño) y guardada fuera del webroot con metadatos en BD.
- Configuración: User Secrets en desarrollo, variables de entorno en QA/producción. Se versionan solo `appsettings.Example.json` y `.env.example`.

## 9. Operaciones críticas (inventario, dinero, correlativos)

- Un documento se procesa en **una sola transacción** de base de datos: validar → bloquear filas (`SELECT … FOR UPDATE` en orden fijo, para evitar deadlocks) → asignar correlativo → afectar inventario/caja → escribir auditoría → confirmar. Reintento acotado ante deadlock (error 1213).
- Correlativos sin saltos ni duplicados bajo concurrencia; se asignan dentro de la transacción del documento (los borradores no consumen número oficial).
- Idempotencia: las operaciones críticas aceptan cabecera `Idempotency-Key`. Edición concurrente: `version_fila` → respuesta 409.
- Los movimientos de inventario y la auditoría son **inmutables** (solo inserción). Se corrige con anulación/reversión, nunca con UPDATE/DELETE. Un documento PROCESADO no se edita.

## 10. API

- Rutas: `/api/v1/{modulo}/{recurso}` en minúsculas, kebab-case, español, plural. Verbos HTTP estándar. DTOs de entrada/salida separados de las entidades.
- Listados: `page`, `pageSize` (máx. 100), `sort`, `q` y filtros por nombre; respuesta `{ items, page, pageSize, totalItems, totalPages }`.
- Errores: RFC 7807 `ProblemDetails` + `errorCode` estable + `traceId` + `errors{campo:[mensajes]}`. Códigos: 400 validación · 401 · 403 (incluye `MODULO_NO_DISPONIBLE`) · 404 · 409 conflicto/concurrencia · 422 regla de negocio · 500 sin detalles internos.
- OpenAPI/Swagger solo en Desarrollo y QA. Endpoint `/health` y `/health/version`.

## 11. Frontend

- TypeScript estricto; **sin lógica de negocio** (solo presentación y validación de UX). Tipos que reflejan los DTOs del backend.
- El navegador habla solo con Next.js (BFF): los *route handlers* reenvían a la API y el JWT va en **cookie httpOnly/Secure/SameSite**, nunca en `localStorage`. `proxy.ts` (reemplazó a `middleware.ts` en Next.js 16) solo mejora la UX; no es seguridad.
- Menú y botones se construyen con `GET /api/v1/seguridad/menu` y `/mis-permisos` (componente `<Puede permiso="…">` / hook `usePermiso`).
- **Diseño reemplazable (rebranding sin tocar lógica):** todo valor visual (colores, tipografías, radios, espacios, sombras, logo, nombre de la marca) vive en tokens centralizados (variables CSS / tema de Tailwind). Prohibidos colores o tamaños fijos dentro de componentes. `components/ui` son envoltorios delgados y el marco de la app (login, cabecera, menú lateral) está aislado en `components/layout`. Así un cambio de marca solo toca tokens, `ui` y `layout`.
- Toda pantalla: estados de carga, vacío y error; paginación/orden/búsqueda en servidor; confirmación en acciones destructivas; notificaciones; manejo de 401/403/409/422.
- UI en español (es-GT), moneda con `Intl` (GTQ, `Q`), fechas mostradas en `America/Guatemala`.

## 12. Idioma y nombres

Dominio en español (`Cliente`, `FacturaVenta`, `MovimientoInventario`, tabla `factura_venta`); términos técnicos en inglés (`Controller`, `Service`, `Repository`). Sin tildes ni ñ en identificadores. Mensajes al usuario en español. Commits en español con prefijo (`feat:`, `fix:`, `docs:`, `db:`).

## 13. Definición de terminado (cada módulo)

Base de datos (`database.sql` + `updates/`) → backend → API → permisos y control de módulo → validaciones → auditoría → frontend funcional → pruebas automatizadas → **compila** (`dotnet build`, `yarn build`) → **pasa pruebas** → **se ejecuta y se probó el flujo** → revisión de seguridad e integridad → documentación y `PROGRESO.md` actualizados.

## 14. Al compactar la conversación

Conserva: etapa actual, decisiones tomadas, archivos creados/modificados, comandos de verificación que funcionan, pendientes y problemas conocidos. Descarta salidas largas de build/pruebas.

## 15. Herramientas en la máquina de desarrollo

Antes de la Fase 0 debe haber: **.NET SDK 10**, **MySQL 8.4** (puede convivir con un MySQL 8.0 existente en otro puerto; no desinstales nada sin que el usuario lo pida), **Git** (si no está, pide permiso para instalarlo; si no puede instalarlo, trabaja sin commits y avisa en cada reporte de etapa que no hay control de versiones). Las pruebas de integración pueden crear y eliminar bases `erp_test_*` en el MySQL local ejecutando `database.sql`; nunca tocan la base de desarrollo/demo del usuario.

## 16. Comandos de verificación

Todos funcionan desde la carpeta indicada (verificados el 2026-10-10).

```
backend/:   dotnet build ERP.sln · dotnet test · dotnet run --project src/ERP.Api --launch-profile http   (API en http://localhost:5080)
frontend/:  corepack yarn install · corepack yarn dev · build · lint · typecheck · test                   (app en http://localhost:3000)
base:       (el usuario ejecuta database/database.sql en MySQL)
```

- Yarn se usa siempre como `corepack yarn …` (no hay `yarn` global).
- `dotnet test` incluye las pruebas de integración: necesitan `ConnectionStrings:Pruebas` en User Secrets del proyecto `tests/ERP.IntegrationTests` y crean/eliminan una base `erp_test_*`.
- Secretos de desarrollo de la API (`ConnectionStrings:Erp`, `Jwt:Clave`): User Secrets del proyecto `src/ERP.Api`.
- En este equipo Git no está en el `PATH` de la sesión: `C:\Program Files\Git\cmd\git.exe`.
