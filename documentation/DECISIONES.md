# ERP Modular — Registro de decisiones

> Formato corto: **contexto → decisión → consecuencias**. Toda decisión arquitectónica importante y toda dependencia nueva (nombre, versión, licencia, para qué) se registra aquí (`CLAUDE.md §2.8`).
> Estados: **Aceptada** · **Propuesta** (pendiente de aprobación del dueño) · **Reemplazada por ADR-nnn**.

## Decisiones de origen (D1–D10)

Tomadas en la especificación (`ESPECIFICACION.md §A2`), estado **Aceptada**. No se repiten aquí: D1 .NET 10 LTS · D2 MySQL 8.4 LTS · D3 Dapper + MySqlConnector sin EF Core · D4 `database.sql` + `updates/` + `esquema_version` · D5 reglas en C#, SP acotados · D6 BFF con JWT en cookie httpOnly · D7 dominio en español **[CONFIRMAR]** · D8 `.sln` clásico · D9 `ERP.Worker` como servicio de Windows · D10 monolito modular.

---

## ADR-011 — Secciones de `database.sql` por dependencia de FK

**Estado:** Aceptada (decidida por el dueño en `CLAUDE.md §7`) · 2026-10-09 — el punto sobre columnas de auditoría sigue como Propuesta

- **Contexto:** el orden original (SEGURIDAD antes que ORGANIZACIÓN) hacía que `usuario_empresa` o `modulo_empresa` referenciaran tablas que aún no existían. La primera versión de este ADR proponía conservar ese orden y mover todas las FK entre secciones al final; el dueño eligió reordenar.
- **Decisión:** las secciones van en orden de dependencia: CONFIGURACIÓN → ORGANIZACIÓN (sin FK a `usuario`) → SEGURIDAD → CATÁLOGOS → … Una sección no referencia tablas de una posterior. Lo que por negocio deba hacerlo se agrega con `ALTER TABLE` en la sección final `RELACIONES DIFERIDAS` (hoy solo `configuracion.empresa_id`). No se desactiva `FOREIGN_KEY_CHECKS`.
- **Propuesta complementaria:** `creado_por` y `modificado_por` guardan el `id` del usuario sin FK. Están en todas las tablas, también en las que van antes que `usuario`; con FK habría que diferirlas todas e indexar cada una.
- **Consecuencias:** casi todas las FK quedan junto a su tabla y el script falla si algo apunta a una tabla inexistente. Sin FK en las columnas de auditoría, su integridad depende de que el backend las tome siempre del usuario autenticado.

## ADR-012 — Colaboración entre módulos

**Estado:** Propuesta · 2026-10-09

- **Contexto:** "un módulo no consulta ni escribe tablas de otro" choca con las FK y con listados que necesitan datos de varios módulos.
- **Decisión:** (1) solo el módulo dueño escribe su tabla; (2) la colaboración es por interfaces en `Application/<Modulo>/Contratos`, en proceso y dentro de la transacción en curso; (3) la lectura cruzada usa el contrato de consulta del dueño; (4) FK entre módulos permitidas; (5) los documentos guardan *snapshot* de los datos que deben congelarse; (6) solo `REPORTES` lee vistas de solo lectura que cruzan módulos; (7) grafo de dependencias acíclico, verificado por prueba.
- **Consecuencias:** más interfaces y algo de duplicación de datos en documentos, a cambio de que Fase 2 y 3 se agreguen sin tocar Fase 1.

## ADR-013 — Costeo en C#; el SP solo bloquea y persiste

**Estado:** Aceptada (decidida por el dueño en `CLAUDE.md §6`) · 2026-10-09

- **Contexto:** la especificación permitía SP para "movimientos de inventario con bloqueo", pero el costo promedio y la política de stock negativo son reglas de negocio.
- **Decisión:** `sp_siguiente_correlativo` bloquea la serie y devuelve el número. En inventario y caja hay dos SP por operación, dentro de la misma transacción: uno bloquea la fila (`FOR UPDATE`, en orden fijo) y la devuelve; el dominio C# valida y calcula saldo y costo; otro persiste el resultado ya calculado. Ningún SP calcula ni decide.
- **Consecuencias:** el costeo se prueba con pruebas unitarias; hay más idas y vueltas a la base por documento (aceptable para el volumen esperado). Si un reporte de costos resulta pesado, se resuelve con vista o SP de **solo lectura**.

## ADR-014 — Módulo Cartera como núcleo de CxC/CxP desde Fase 1

**Estado:** Aceptada (incorporada por el dueño en `ESPECIFICACION.md §A3` y `§B14a`) · 2026-10-09

- **Contexto:** Fase 1 vende y compra a crédito; Caja aplica cobros y pagos; Fase 2 no puede alterar tablas transaccionales de Fase 1. Con el saldo como columna de cada factura, Caja escribiría en tablas de Ventas/Compras y CxC tendría que modificarlas.
- **Decisión:** la definición vigente es la de `§B14a`: tablas `documento_saldo` (una fila por documento con saldo, creada al procesarlo) y `documento_saldo_aplicacion` (abonos por cobro, pago o nota; solo inserción), con dueño único, el módulo Cartera, creado en 1D. Puerto `ICartera` con `RegistrarSaldo`, `AplicarPago`, `ConsultarSaldo` y `ConsultarVencidos`: Ventas y Compras lo llaman al procesar; Caja, al cobrar o pagar. Ningún otro módulo lee ni escribe esas tablas. Fase 2 las hereda y agrega estados de cuenta, envejecimiento y contabilización.
- **Por confirmar en el plan de 1D:** que `documento_saldo_aplicacion` sea la única tabla de aplicación (`§B14` punto 4 menciona `*_aplicacion` junto a `cobro`/`pago_proveedor`), y que las facturas de contado también registren saldo, saldado en la misma transacción.
- **Consecuencias:** Caja no depende de Ventas ni de Compras; anticipos y pagos parciales tienen un solo lugar. Costo: un módulo más desde 1D y una consulta adicional para mostrar el saldo de una factura.

## ADR-015 — Hash de contraseñas con PBKDF2 del framework

**Estado:** Propuesta · 2026-10-09

- **Contexto:** `CLAUDE.md §8` admite Argon2id o PBKDF2. Argon2id requiere un paquete de terceros.
- **Decisión:** `Rfc2898DeriveBytes.Pbkdf2` con HMAC-SHA512, sal aleatoria por contraseña y número de iteraciones configurable (valor inicial según la recomendación vigente de OWASP al implementar). Formato almacenado con prefijo de versión para poder migrar al iniciar sesión.
- **Consecuencias:** cero dependencias; menor resistencia a ataques con GPU que Argon2id, compensada con iteraciones altas, bloqueo por intentos y limitación de tasa.

**Formato fijado (2026-10-09, al sembrar el administrador en `database.sql`):** `pbkdf2-sha512$<iteraciones>$<sal en base64>$<hash en base64>`, con 210000 iteraciones, sal de 16 bytes y clave derivada de 32 bytes. El backend debe verificar exactamente este formato.

## ADR-016 — Contexto empresa/sucursal en el JWT, revalidado en servidor

**Estado:** Propuesta · 2026-10-09

- **Contexto:** el usuario trabaja sobre una empresa y sucursal activas; el backend no puede confiar en lo que diga el cliente.
- **Decisión:** el token de acceso lleva los *claims* `emp` y `suc`. Cambiar de contexto es una llamada que emite un token nuevo. Un middleware revalida ambos contra `usuario_empresa`/`usuario_sucursal` en cada solicitud (con caché invalidable).
- **Consecuencias:** el frontend no maneja cabeceras de contexto; quitarle una empresa a un usuario surte efecto de inmediato aunque su token siga vigente.

## ADR-017 — Unidad de trabajo, reintentos e idempotencia

**Estado:** Propuesta · 2026-10-09

- **Contexto:** un documento debe procesarse en una sola transacción que atraviesa varios módulos.
- **Decisión:** `IUnitOfWork` por solicitud (una conexión, una transacción); reintento de la transacción completa hasta 3 veces ante errores 1213 y 1205; la fila de `solicitud_idempotente` se inserta dentro de esa misma transacción con índice único.
- **Consecuencias:** los servicios de aplicación deben ser repetibles (sin efectos externos dentro de la transacción: correo, FEL y archivos van después del `COMMIT` o por outbox).

## ADR-018 — Otorgamientos de MySQL por tabla

**Estado:** Propuesta · 2026-10-09

- **Contexto:** la auditoría y los movimientos son inmutables y el usuario de la aplicación no debe poder alterarlos. MySQL no permite restar privilegios de una tabla a un otorgamiento hecho a nivel de base.
- **Decisión:** el usuario de la aplicación recibe privilegios **tabla por tabla**: `SELECT, INSERT` en tablas inmutables; `SELECT, INSERT, UPDATE, DELETE` en el resto; `EXECUTE` en los SP. Plantilla en `database/usuarios-ejemplo.sql` (sin contraseñas); cada script de `updates/` incluye los otorgamientos de sus tablas nuevas.
- **Consecuencias:** inmutabilidad garantizada por el motor, no por disciplina. Costo: mantener la lista; una prueba de integración verifica los privilegios efectivos.

## ADR-019 — Acción `PROCESAR` y excepciones al patrón de permisos

**Estado:** `PROCESAR` Aceptada (incorporada por el dueño en `CLAUDE.md §8`); las excepciones se adoptan por defecto · 2026-10-09

- **Contexto:** las acciones estándar no cubrían "procesar un documento" ni operaciones propias de caja; `{MODULO}_PRUEBAS` no tiene tres partes.
- **Decisión:** `PROCESAR` es acción estándar: la transición de PENDIENTE/APROBADO a PROCESADO (consume correlativo, afecta inventario o caja), distinta de `CREAR`. Acciones propias de un recurso (`ABRIR`, `CERRAR`, `ARQUEAR`, `DESCUENTO`) se admiten si se documentan en el catálogo de permisos del módulo. `{MODULO}_PRUEBAS` es la única excepción al patrón `{MODULO}_{RECURSO}_{ACCION}`.
- **Consecuencias:** permite segregación de funciones (quien captura no necesariamente procesa). La prueba de permisos valida los códigos contra el catálogo sembrado, no contra una expresión regular rígida.

## ADR-020 — Dependencias previstas

**Estado:** Propuesta · 2026-10-09

- **Contexto:** `CLAUDE.md §4` exige pocas dependencias y revisar licencias.
- **Decisión:** lista prevista para Fase 0. **La versión exacta y la licencia de cada paquete se verifican y se registran en esta entrada en el momento de agregarlo**; nada se da por instalado todavía.

| Paquete | Licencia esperada | Para qué |
|---|---|---|
| Dapper | Apache-2.0 | Mapeo de consultas |
| MySqlConnector | MIT | Controlador MySQL |
| FluentValidation | Apache-2.0 | Validación de DTO |
| Serilog (+ AspNetCore, sinks de archivo y consola) | Apache-2.0 | Registro |
| Microsoft.AspNetCore.Authentication.JwtBearer | MIT | Autenticación JWT |
| Microsoft.AspNetCore.OpenApi | MIT | Documento OpenAPI |
| xUnit | Apache-2.0 | Pruebas |
| NetArchTest.Rules | MIT | Pruebas de arquitectura |
| Next.js, React, Tailwind CSS, shadcn/ui, TanStack Query/Table, React Hook Form, Zod | MIT | Frontend |
| Vitest + Testing Library | MIT | Pruebas de frontend |

Descartados o condicionados:

| Paquete | Motivo |
|---|---|
| MediatR, AutoMapper | Licencia comercial en versiones recientes (`CLAUDE.md §4`) |
| FluentAssertions | Licencia comercial desde la versión 8; se usan las aserciones de xUnit |
| EPPlus | Licencia comercial; para Excel se evaluará ClosedXML (MIT) en 1B |
| QuestPDF | Licencia gratuita condicionada a ingresos; para PDF se evaluará PDFsharp/MigraDoc (MIT) en 1E |
| Paquete de Argon2 | Innecesario con ADR-015 |

- **Consecuencias:** interfaz de usuario de OpenAPI, correo (SMTP), Excel y PDF se deciden en la etapa que los necesite, con su propia entrada.

## ADR-021 — Desarrollo sobre MySQL 8.0 (cambio al stack fijado)

**Estado:** Aceptada (decidida por el dueño) · 2026-10-09 — modifica D2

- **Contexto:** el stack fija MySQL 8.4 LTS (D2), pero el equipo de desarrollo ya tiene MySQL 8.0.46 en `localhost:3306` y el dueño prefiere usarlo en lugar de instalar una segunda instancia.
- **Decisión:** se desarrolla y se prueba contra MySQL 8.0 (mínimo 8.0.16, por los `CHECK`). `database.sql` y los scripts de `updates/` usan solo funciones presentes tanto en 8.0 como en 8.4, para que la base pueda instalarse en 8.4 sin cambios.
- **Consecuencias:** MySQL 8.0 terminó su soporte en abril de 2026 y ya no recibe parches de seguridad; es aceptable en desarrollo, pero la versión del servidor de producción debe decidirse antes de la etapa 1G. `CLAUDE.md §4` y `§15` y `ESPECIFICACION.md` (D2, criterio 1 de C1) siguen diciendo 8.4 y deben ajustarse. El criterio "se instala en MySQL 8.4 vacío" queda sin verificar mientras no haya una instancia 8.4.

## ADR-022 — Decisiones de esquema de Fase 0

**Estado:** Aceptada (plan de base de datos aprobado por el dueño) · 2026-10-09

- **Contexto:** al escribir `database.sql` hubo que fijar detalles que el diseño dejaba abiertos.
- **Decisión:**
  - La base se llama `erpgt`; el script la crea y falla si ya existe.
  - `creado_por` y `modificado_por` no llevan FK (cierra el punto pendiente de ADR-011).
  - Estados y tipos como `VARCHAR` + `CHECK`, no `ENUM`.
  - `usuario_rol.empresa_id` es opcional: vacío significa que el rol aplica en todas las empresas. Hoy solo existe el rol `ADMINISTRADOR`, asignado de forma global.
  - Sin fila en `modulo_empresa`, el backend trata el módulo como `NO_LIBERADO` para esa empresa; la empresa inicial se siembra igual que el estado global.
  - `refresh_token` guarda la empresa y sucursal activas de la sesión, para emitir el siguiente token de acceso con el mismo contexto.
  - `solicitud_idempotente` no entra en Fase 0; se crea con la primera operación crítica (1C).
  - Sin vistas, procedimientos, funciones ni triggers en Fase 0.
- **Consecuencias:** cada script de `updates/` que agregue permisos debe concederlos también al rol `ADMINISTRADOR`, porque no hay atajo de superadministrador en el código.
