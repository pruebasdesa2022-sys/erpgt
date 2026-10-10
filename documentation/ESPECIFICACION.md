# ERP Modular — Especificación funcional y técnica

> Documento de referencia. **No lo leas completo en cada sesión**: lee la sección de la etapa en curso (Parte C) y las secciones transversales que ella cite (Parte B).
> Reglas de trabajo permanentes: `CLAUDE.md`. Estado: `documentation/PROGRESO.md`.
> Marcas: **[IMPLEMENTAR]** se construye en esa etapa · **[PREPARAR]** solo estructura/columnas/interfaces, sin pantallas ni lógica · **[FUTURO]** no tocar aún · **[CONFIRMAR]** decisión por defecto que el dueño puede cambiar.

---

# PARTE A — Visión y decisiones

## A1. Alcance

Plataforma ERP para Guatemala, un solo sistema en tres fases: **Fase 1** operación comercial · **Fase 2** contabilidad, CxC y CxP · **Fase 3** RR. HH. y nómina. Las tres comparten seguridad, usuarios, configuración, auditoría, empresas, sucursales, clientes, proveedores y productos. Se despliega en **Windows Server** en producción.

## A2. Decisiones técnicas tomadas

| # | Decisión | Motivo |
|---|---|---|
| D1 | .NET 10 LTS (no .NET 8) | .NET 8 sale de soporte el 10-nov-2026 |
| D2 | MySQL 8.4 LTS | MySQL 8.0 llegó a fin de vida en abril 2026 |
| D3 | Dapper + MySqlConnector, sin EF Core | El esquema vive en SQL, no en el código; evita migraciones ocultas |
| D4 | `database.sql` + `updates/` + tabla `esquema_version` | Un script maestro solo sirve para instalar; para bases con datos hacen falta cambios incrementales |
| D5 | Reglas en C#; SP solo para operaciones atómicas/concurrentes y reportes | "El backend es la autoridad" sin renunciar a bloqueos y correlativos seguros en BD |
| D6 | Frontend con BFF y JWT en cookie httpOnly | El token no queda expuesto a XSS; el navegador no llama directo a la API |
| D7 | Nombres de dominio en español | Es el idioma del negocio y de quien mantendrá el sistema **[CONFIRMAR]** |
| D8 | Solución `.sln` clásica | `dotnet new sln` en .NET 10 genera `.slnx` por defecto; se fuerza `--format sln` por compatibilidad |
| D9 | Procesos en segundo plano en `ERP.Worker` (Windows Service), no dentro de IIS | IIS recicla procesos y mata tareas de fondo |
| D10 | Monolito modular, sin microservicios | Complejidad justificada solo si hay razón técnica/de negocio |

## A3. Correcciones al texto original (ya resueltas aquí)

- **Seguridad en Fase 0**: Usuarios, Perfiles (=Roles), Accesos (=Permisos) y Menús pasan de "Maestros 1A" a Fase 0.
- **Sinónimos unificados**: Traslados = Transferencias (un solo documento). Perfiles = Roles. Accesos = Permisos.
- **"Existencia" no es columna del producto**: se deriva del inventario por bodega.
- **Crédito como forma de pago en Fase 1** choca con CxC en Fase 2: si el saldo vive como columna de la factura, Caja tendría que escribir en tablas de Ventas, y Fase 2 tendría que modificarlas. Por eso se crea un módulo pequeño **Cartera** desde **1D** (B14a), dueño de una tabla neutral `documento_saldo` que Compras, Ventas y Caja usan por interfaz; Fase 2 la hereda sin romper nada.
- **"Instalar BD de Fase 2 deshabilitada"**: las tablas de cada fase se agregan a `database.sql` cuando esa fase se construye; "desplegar sin exponer" = publicar código + script incremental con el módulo en estado NO_LIBERADO.
- **Diseño → implementación sin pausa**: ahora hay puertas de aprobación entre etapas.
- **Guatemala** deja de ser condicional: el sistema es para Guatemala.
- Estructura de pruebas unificada: `backend/tests` y `frontend` (tests propios); no hay `tests/` suelto en la raíz.

---

# PARTE B — Estándares transversales

## B1. Arquitectura y módulos

Monolito modular (ver `CLAUDE.md §5–6`). Módulos y etapa en que se implementan:

| Módulo | Etapa | | Módulo | Etapa |
|---|---|---|---|---|
| Identity / Usuarios / Roles / Permisos | 0 | | Compras, Cartera | 1D |
| Control de módulos y fases | 0 | | Ventas | 1E |
| Menú dinámico | 0 | | Caja y Bancos (maestro) | 1F |
| Auditoría base | 0 | | Reportes, auditoría avanzada | 1G |
| Configuración | 0 / 1A | | Contabilidad, CxC, CxP | 2 |
| Empresas, Sucursales, Bodegas, Cajas | 0 (mínimo) / 1A | | RR. HH., Nómina | 3 |
| Catálogos y Maestros | 1A | | Notificaciones, Worker | 1G (diseño desde 0) |
| Clientes, Proveedores, Productos | 1B | | Archivos | 1B (infraestructura) |
| Inventario | 1C | | Integraciones (FEL, bancos…) | ver B16 |

## B2. Base de datos — reglas adicionales a `CLAUDE.md §7`

- Orden de secciones en `database.sql`: CONFIGURACIÓN · SEGURIDAD · ORGANIZACIÓN · CATÁLOGOS · CLIENTES · PROVEEDORES · PRODUCTOS · INVENTARIO · COMPRAS · VENTAS · CAJA · AUDITORÍA · VISTAS · PROCEDIMIENTOS · FUNCIONES · SEEDS. Cada sección con cabecera comentada.
- Tablas transaccionales (cabecera/detalle) llevan desde Fase 1: `empresa_id`, `sucursal_id`, `tipo_documento_id`, `serie`, `numero`, `fecha_documento`, `estado_id`, `moneda_id`, `tipo_cambio`, totales en moneda del documento **y** en moneda local, `centro_costo_id` (nullable), `motivo_anulacion`, `anulado_por/en`, y trazabilidad de origen (ver B14).
- `esquema_version(id, version, script, aplicado_en, aplicado_por)`: cada script registra su ejecución.
- Índices: documentar el motivo de los no obvios; revisar planes (`EXPLAIN`) de consultas de listado y kardex.
- Vistas solo de lectura (listados, kardex, reportes). Nunca se escribe a través de una vista.
- Aislamiento: nivel por defecto de InnoDB (REPEATABLE READ) + bloqueos explícitos donde se necesite.

## B3. Seguridad y acceso — modelo

Acceso permitido = **módulo habilitado** (global y para la empresa) **+ usuario autenticado y con acceso a la empresa/sucursal/bodega** **+ permiso**. Conocer la URL no basta.

Tablas mínimas en **Fase 0**: `usuario`, `rol`, `permiso`, `rol_permiso`, `usuario_rol`, `usuario_permiso` (concede o deniega, con vigencia), `usuario_empresa`, `usuario_sucursal`, `refresh_token`, `intento_login`, `politica_password`. `usuario_bodega` y `usuario_caja` se agregan en **1A**, cuando nacen `bodega` y `caja` (B9) — hasta entonces el contexto de trabajo llega solo a nivel sucursal.

- Precedencia: **denegación explícita de usuario > concesión explícita de usuario > unión de permisos de sus roles**. Sin atajos "superadmin" en código: el rol administrador recibe todos los permisos por seed y también queda sujeto al estado del módulo.
- Contraseñas: longitud mínima, complejidad, historial, expiración configurable, bloqueo temporal tras N intentos, recuperación por correo con token de un solo uso y expiración corta, cambio forzado en primer ingreso.
- Sesiones: refresh con rotación; cerrar sesión revoca; revocación masiva por usuario (al desactivarlo o cambiar contraseña).
- Contexto de trabajo: el usuario elige empresa/sucursal activa (y bodega/caja desde 1A); el backend valida cada solicitud contra `usuario_empresa/sucursal/bodega/caja`.
- 2FA: **[FUTURO]**, pero el modelo de usuario no debe impedirlo.

## B4. Control de módulos, fases y liberación progresiva

Tres cosas distintas: **existe** en el código · está **habilitada** en el ambiente/empresa · el usuario **tiene permiso**. Hace falta que se cumplan las tres.

- `modulo(codigo, nombre, fase, orden, estado_global)` y `modulo_empresa(modulo_id, empresa_id, estado, vigente_desde, cambiado_por, motivo)`.
- Estados: `EN_DESARROLLO · EN_PRUEBAS · NO_LIBERADO · LIBERADO · SUSPENDIDO · RETIRADO`.
- **Estado efectivo = el más restrictivo entre el global y el de la empresa.** `LIBERADO` → disponible según permisos. `EN_PRUEBAS` → solo usuarios con el permiso `{MODULO}_PRUEBAS`. Los demás → bloqueado (403 con `errorCode = MODULO_NO_DISPONIBLE`).
- Un administrador autorizado cambia estados desde la aplicación, sin tocar código; cada cambio exige motivo y queda auditado.
- Los módulos de Fases 2 y 3 se publican técnicamente en `NO_LIBERADO`, con usuarios sin permisos; tras pruebas pasan a `EN_PRUEBAS` (testers) y finalmente a `LIBERADO`.
- Ambientes (DESARROLLO → QA → PRODUCCIÓN): cada uno con su propia base, configuración y estado de módulos. Que algo avance de ambiente no implica que se libere a todos los usuarios.
- La empresa B puede tener Fase 2 liberada y la empresa A no: el modelo lo soporta desde el inicio aunque la UI de gestión por empresa llegue después **[PREPARAR]**.

## B5. Menú dinámico

`menu(id, padre_id, modulo_id, tipo{MODULO|GRUPO|PANTALLA}, codigo, nombre, icono, ruta, orden, estado, permiso_requerido_id)`.

- `GET /api/v1/seguridad/menu` devuelve el árbol ya filtrado (módulo efectivo + permiso VER). `GET /api/v1/seguridad/mis-permisos` devuelve los códigos para habilitar/ocultar botones.
- El catálogo de permisos y el menú inicial se siembran en `database.sql`; agregar una pantalla = insertar menú + permisos en el `updates/` correspondiente.
- El permiso pertenece al módulo dueño del recurso aunque el menú lo muestre en otro lugar (ej. "Clientes" visible bajo Ventas).

## B6. Auditoría

`auditoria(id, fecha_utc, usuario_id, empresa_id, sucursal_id, modulo, entidad, entidad_id, accion, resultado, valores_anteriores JSON, valores_nuevos JSON, ip, user_agent, operacion_id, mensaje)` y `auditoria_acceso` (logins, bloqueos, refresh sospechosos).

- Solo inserción (el usuario de la aplicación no tiene UPDATE/DELETE sobre estas tablas).
- En procesos financieros y de inventario se escribe **en la misma transacción** que el cambio. En el resto puede ser asíncrono.
- Enmascarar datos sensibles (contraseñas, tokens, números de tarjeta). `operacion_id` = correlation id de la solicitud, el mismo que aparece en los logs.
- Política de retención y consulta con filtros y exportación: etapa 1G.

## B7. Estados de documentos

Conjunto base: `BORRADOR → PENDIENTE → APROBADO → PROCESADO`, con `ANULADO` (y reversión cuando corresponda). Cada tipo de documento puede añadir estados propios (ej. cotización: ENVIADA, ACEPTADA, RECHAZADA, VENCIDA, CONVERTIDA; orden de compra: PARCIAL, RECIBIDA, CERRADA).

- Las transiciones válidas viven en el dominio (C#) y se reflejan en un catálogo `estado_documento` + `documento_transicion(tipo, origen, destino, permiso)`.
- **Un documento PROCESADO no se edita.** Se corrige anulando (con motivo, permiso `ANULAR` y contra-movimientos) o con nota de crédito/débito.
- `fecha_bloqueo` por empresa: prohíbe crear/anular documentos con fecha anterior (preparación para cierres contables de Fase 2) **[IMPLEMENTAR en 1A]**.

## B8. Numeración

`tipo_documento` + `serie_documento(empresa_id, sucursal_id?, caja_id?, tipo_documento_id, serie, prefijo, incluir_anio, longitud, siguiente_correlativo, vigente_desde/hasta, activo)`.

- Se asigna al **procesar**, dentro de la transacción del documento (sin saltos si hay rollback). Los borradores usan solo el `id` interno.
- `UNIQUE(empresa_id, tipo_documento_id, serie, numero)`. Generación atómica con bloqueo de la fila de la serie (SP `sp_siguiente_correlativo`).
- Probar con ejecución concurrente real (ver B17).

## B9. Multiempresa y organización

`empresa`, `sucursal`, `bodega`, `caja`, `departamento`, `centro_costo`. Toda tabla de negocio lleva `empresa_id` y el backend filtra siempre por las empresas permitidas al usuario. Hoy habrá una sola empresa (seed), pero agregar otra no debe requerir cambios de esquema. Las tablas compartidas (ej. catálogo de unidades estándar) pueden ser globales con `empresa_id` nulo.

## B10. Unidades y cantidades

`unidad_medida`, `producto_unidad(producto_id, unidad_id, factor_a_base DECIMAL(18,6), usar_en_compra, usar_en_venta, codigo_barras)`. El inventario siempre se guarda en **unidad base**; compra y venta pueden usar otra (1 caja = 12 unidades). Redondeo y decimales permitidos por unidad (`decimales`). Pruebas con casos límite (factores fraccionarios, redondeo acumulado).

## B11. Impuestos

`impuesto(codigo, nombre, tipo, porcentaje, vigente_desde, vigente_hasta, empresa_id?)`, `producto_impuesto`, exenciones por producto/cliente/empresa. `lista_precio` indica si el precio incluye impuesto. La línea del documento **guarda una copia** (snapshot) de porcentaje y montos aplicados. Política de redondeo configurable (por defecto por línea a 2 decimales; contrastar con los requisitos del certificador FEL). Las tasas son **parámetros con vigencia**, nunca constantes en código.

## B12. Monedas

`moneda`, `tipo_cambio(moneda_id, fecha, tasa)`. Moneda local: GTQ. Cada documento guarda moneda, tipo de cambio aplicado, montos en moneda del documento y en moneda local. La moneda de pago puede diferir de la del documento. Fase 1 opera en GTQ con la estructura lista para USD **[PREPARAR]**.

## B13. Archivos

Los archivos (PDF, adjuntos, expedientes) se guardan en disco/almacenamiento configurado, **fuera del webroot**; en MySQL solo metadatos (`archivo(id, nombre, mime, tamano, hash_sha256, ruta, entidad, entidad_id, creado_por)`). Descarga solo vía endpoint autorizado. Límites de tamaño y tipos permitidos configurables. Respaldos del almacenamiento incluidos en la estrategia de backup.

## B14. Preparación contable y trazabilidad entre documentos

Flujos a poder rastrear sin rediseñar tablas:

```
Cotización → Pedido → Factura → (saldo/CxC) → Cobro → Caja/Banco → Contabilidad
Orden de compra → Recepción → Factura proveedor → (saldo/CxP) → Pago → Caja/Banco → Contabilidad
```

Mecanismos desde Fase 1:

1. `documento_relacion(tipo_origen, id_origen, tipo_destino, id_destino, cantidad_o_monto, creado_en)` para vincular documentos y líneas (parcialidades incluidas).
2. `evento_contable` (patrón outbox): cada documento procesado/anulado en Fase 1 inserta un evento con tipo, documento, montos, moneda y fecha, **en la misma transacción**. En Fase 2 un procesador los convierte en partidas. Estado del evento: PENDIENTE / CONTABILIZADO / ERROR.
3. Cada línea guarda costo histórico (inventario) e impuestos aplicados; cada cabecera guarda `centro_costo_id` y condición de pago.
4. Cobros y pagos: `cobro`/`pago_proveedor` + `*_aplicacion` (a qué documento se aplicó cuánto) → permiten pagos parciales y anticipos.

## B14a. Módulo Cartera (dueño del saldo a crédito desde 1D)

Las ventas y compras a crédito (A3) necesitan un dueño del saldo que no sea ni Ventas ni Compras, para que Caja no escriba en sus tablas y Fase 2 no tenga que modificarlas. **Cartera** es ese módulo, pequeño y transversal, que nace en **1D** (junto con Compras, antes de Ventas y Caja).

- `documento_saldo(id, tipo_documento, documento_id, tercero_tipo{CLIENTE|PROVEEDOR}, tercero_id, moneda_id, monto_original, saldo_pendiente, fecha_vencimiento, estado{PENDIENTE|PARCIAL|SALDADO|ANULADO}, empresa_id, sucursal_id)`. Una fila por documento con saldo (factura de venta, factura de proveedor); se crea al **procesar** el documento (acción `PROCESAR`, B3) y es de solo lectura para todos salvo Cartera.
- `documento_saldo_aplicacion(documento_saldo_id, origen_tipo{COBRO|PAGO|NOTA_CREDITO|NOTA_DEBITO}, origen_id, monto, fecha, creado_por)`: cada abono resta contra `saldo_pendiente` dentro de la misma transacción del cobro/pago/nota.
- Interfaz `ICartera` (puerto en Application): `RegistrarSaldo(...)`, `AplicarPago(...)`, `ConsultarSaldo(...)`, `ConsultarVencidos(...)`. Ventas y Compras la llaman al procesar un documento; Caja la llama al aplicar un cobro o pago. Ningún módulo lee ni escribe `documento_saldo*` directamente.
- Fase 2 (CxC/CxP) **hereda** esta tabla en vez de duplicarla: agrega estados de cuenta, envejecimiento y el evento contable de la cartera, sin tocar su esquema base.

## B15. Preparación para Recursos Humanos

`empleado` referencia a `persona`/datos comunes y a `empresa`, `sucursal`, `departamento`, `puesto`. Historial laboral **append-only** (contrataciones, cambios salariales, traslados, suspensiones, terminaciones) con vigencia. Información de empleados con permisos propios y auditoría de lectura de datos sensibles. Nómina como motor de **conceptos configurables** (ingreso/deducción, tipo de valor, fórmula o monto, frecuencia, vigencia, empleados aplicables, impacto en nómina y contable). Reglas de Guatemala aisladas (ver B20).

## B16. Integraciones y FEL

Puertos (interfaces) en Application y adaptadores en Infrastructure: facturación electrónica, bancos, pasarelas de pago, correo, WhatsApp, APIs de terceros. Credenciales por empresa, cifradas, nunca en el código.

**FEL (SAT Guatemala):** una factura sin certificación FEL no es fiscalmente válida. Plan por defecto **[CONFIRMAR]**: en 1E se diseña `IProveedorFel` y los campos fiscales del documento (UUID/autorización, serie y número fiscal, fecha de certificación, XML, estado de certificación, referencia de anulación); la **integración real con el certificador** que elija el negocio es una sub-etapa 1H. Hasta entonces las facturas son documentos internos, rotulados como tales. Anular un DTE debe pasar por el certificador, no solo por la BD.

## B17. Concurrencia e integridad

Casos que deben tener prueba automatizada: dos ventas del mismo producto con stock limitado · dos usuarios tomando el mismo correlativo · dos cajas simultáneas · dos procesos actualizando el mismo saldo · reenvío del mismo POST (idempotencia) · edición simultánea (409). Herramientas: transacciones, `SELECT … FOR UPDATE`, orden fijo de bloqueo, `version_fila`, `Idempotency-Key` (`solicitud_idempotente(clave, usuario, endpoint, hash, respuesta, estado, expira_en)`), reintento acotado ante deadlock.

## B18. API

Ver `CLAUDE.md §10`. Además: versionado en la ruta (`/v1`); validación con FluentValidation devolviendo `errors` por campo; `Idempotency-Key` documentada; ordenamiento con lista blanca de columnas (nunca columnas arbitrarias del cliente); correlation id en cabecera de respuesta.

## B19. Frontend

Ver `CLAUDE.md §11`. Estructura sugerida: `src/app` (rutas), `components` (ui, layout, módulos), `services` (clientes de API), `hooks`, `types`, `utils`, `contexts` (sesión, empresa activa). Layout con navegación y menú dinámico, formularios, tablas con filtros/orden/paginación en servidor, modales, notificaciones, confirmaciones, errores y loading. Los reportes complejos se generan en el backend; el frontend solo los muestra o descarga.

**Identidad visual intercambiable:** desde la Fase 0 el frontend usa un tema neutro definido solo con tokens (colores, tipografía, radios, espaciado, logo y nombre de marca en un único módulo de configuración de tema). Ningún componente lleva valores visuales fijos. El brief de diseño vigente se documenta en `documentation/DISENO-UI.md`. Con esto un rebranding completo (Prompt 7 de `PROMPTS.md`) no toca lógica, servicios, permisos ni rutas.

## B20. Reglas específicas de Guatemala (aisladas del núcleo)

Van en un paquete/carpeta propia (ej. `Localizacion/GT`) y en tablas de parámetros con vigencia, no en el núcleo. **Todos los valores numéricos deben validarse con el contador/asesor laboral y tener vigencia; ninguno se hardcodea.**

- Moneda GTQ (Q). Zona horaria America/Guatemala.
- Impuestos: IVA y regímenes tributarios por empresa (general, pequeño contribuyente, etc.); retenciones; monto máximo para consumidor final sin identificación (parámetro).
- Identificación: validación de NIT (dígito verificador), CUI/DPI, "CF" consumidor final.
- Facturación: FEL, tipos de DTE (factura, nota de crédito, nota de débito, factura cambiaria, pequeño contribuyente, factura especial, recibo) — confirmar catálogo vigente con el certificador.
- Libros y reportes legales: libro de compras y ventas, diario, mayor, inventarios, estados financieros (Fase 2).
- Nómina y prestaciones (Fase 3, todo parametrizable): salario mínimo y bonificación incentivo (Decreto 78-89), Bono 14 (Decreto 42-92), Aguinaldo (Decreto 76-78), vacaciones, indemnización (Código de Trabajo), cuotas IGSS laboral/patronal, IRTRA, INTECAP, ISR por relación de dependencia, séptimo día, horas extras.

## B21. Configuración — tres tipos separados

1. **Técnica** (appsettings/variables de entorno): conexiones, URLs, secretos, APIs externas.
2. **De negocio** (tablas `configuracion` por empresa): impuestos, correlativos, monedas, método de costeo, política de stock negativo, `fecha_bloqueo`, parámetros de nómina.
3. **Datos maestros** (tablas propias): clientes, proveedores, productos, bodegas, sucursales.

## B22. Notificaciones y procesos automáticos

`notificacion(usuario_id, tipo, titulo, mensaje, entidad_ref, leida, canal, creado_en)` + abstracción `INotificador` (en pantalla primero; correo/WhatsApp después). `ERP.Worker` ejecuta: vencimientos, alertas (inventario bajo, cuentas vencidas, compras/nómina pendientes), procesador de `evento_contable`, reintentos de integraciones, backups programados si aplica. Tareas idempotentes, con registro de ejecución y bloqueo para que no corran dos instancias a la vez. Mecanismo de planificación: `BackgroundService` o Quartz.NET (documentar si se agrega).

## B23. Reportes

Las consultas pesadas usan vistas/SP de solo lectura; exportación a Excel/PDF desde el backend (verificar licencias de las librerías). Catálogo `reporte` con permiso por reporte. Listados de cada módulo exportables desde su etapa; reportería avanzada y auditoría avanzada en 1G.

## B24. Pruebas

- **Unitarias** (dominio, cálculos de impuestos/costos/unidades, transiciones de estado).
- **Integración** contra una base MySQL local de pruebas creada **desde `database.sql`** (así se valida a la vez el script maestro).
- **API/seguridad**: sin token → 401; sin permiso → 403; módulo no liberado → 403; endpoint sin atributo de permiso → falla la prueba.
- **Concurrencia** (B17), **inventario**, **dinero**, **arquitectura** (dependencias entre capas/módulos).
- **Frontend**: pruebas de componentes críticos (login, guardas de permiso, formularios clave).
- Los módulos críticos (inventario, caja, ventas, compras, contabilidad, nómina) no se dan por terminados sin pruebas.

## B25. DevOps, ambientes y Windows Server

- Git desde el inicio: rama por etapa, commits pequeños, `.gitignore` correcto. CI (GitHub Actions o equivalente) con build + pruebas de backend y frontend **[1G]**.
- **Backend:** `ASP.NET Core` publicado en IIS con el *Hosting Bundle* (in-process) o como Windows Service con Kestrel detrás de IIS como proxy inverso; la decisión se documenta en 1G y por defecto la API escucha solo en la red interna.
- **Frontend:** `next build` con `output: 'standalone'`; se ejecuta con `node` como Windows Service (NSSM o WinSW) y IIS (URL Rewrite + ARR) como proxy inverso con HTTPS.
- **HTTPS** con certificado válido (corporativo o ACME) en IIS; HSTS; solo el 443 abierto al exterior; MySQL y la API no expuestos.
- **MySQL en Windows:** instalación, `my.ini` (utf8mb4, zona horaria, binlog), usuarios de mínimo privilegio, respaldo con `mysqldump --single-transaction --routines --triggers --events` programado, retención, copia fuera del servidor, binlog para recuperación a un punto en el tiempo y **prueba de restauración periódica**.
- **Logs:** Serilog a archivos rotativos con retención; correlation id; sin datos sensibles.
- **Despliegue y rollback:** scripts PowerShell en `deploy/` (publicar API, publicar frontend, aplicar scripts de `updates/`, respaldar antes de actualizar, volver a la versión anterior). Versión visible en `/health/version` y en el pie de la app.
- **Variables por ambiente:** `ASPNETCORE_ENVIRONMENT`, `appsettings.{Env}.json` (sin secretos), `.env.production` solo en el servidor. `NEXT_PUBLIC_*` nunca contiene secretos.
- Documentar todo paso a paso en `documentation/DESPLIEGUE-WINDOWS.md` para que otra persona lo ejecute. No asumir que desarrollo y producción son iguales.

## B26. Requisitos faltantes detectados (agregados)

**Críticos**
- Camino de actualización de BD para instalaciones con datos (D4).
- Decisión de FEL antes de facturar en producción (B16).
- Política de inventario: costeo por defecto **promedio ponderado móvil por empresa+producto** (estructura lista para capas FIFO), stock negativo **no permitido** por defecto, costo de venta guardado en cada movimiento **[CONFIRMAR]**.
- Ventas/compras a crédito con saldo básico en Fase 1, resuelto con el módulo Cartera (A3, B14a).
- Almacenamiento seguro del token (D6), reglas de precedencia de permisos (B3), prueba automática de "todo endpoint tiene permiso".
- `fecha_bloqueo` de documentos (B7).
- Usuario de BD de la aplicación sin DDL.

**Importantes**
- Listas de precios y descuentos; `tipo_documento` y series configurables; impresión/PDF de documentos (factura, cotización, orden de compra); importación masiva (Excel/CSV) de clientes, productos y **saldos iniciales de inventario**; arqueo y cierre de caja; datos de demostración separados; búsqueda rápida por código de barras; manejo de 409 en la UI; health checks y correlation id; cambio de contraseña y perfil del usuario; unicidad con baja lógica.

**Recomendados**
- Comentarios y adjuntos en documentos; dashboard de inicio; atajos de teclado en facturación; modo oscuro; OpenTelemetry; rate limiting general.

**Futuro**
- 2FA/SSO; POS sin conexión; app móvil; BI; conciliación bancaria automática; multi-idioma; WhatsApp; remisión/entrega separada de la factura; retenciones avanzadas.

---

# PARTE C — Etapas

> Cada etapa termina con: build verde, pruebas verdes, flujo probado, documentación y `PROGRESO.md` al día, commit, y **parada para aprobación**.

## C0. Diseño maestro (sin código de producción)

Entrega en `documentation/DISENO.md` (no solo en el chat): **A** análisis (duplicados, inconsistencias, ambigüedades, dependencias) · **B** faltantes clasificados Crítico/Importante/Recomendado/Futuro (parte de B26 como punto de partida; amplía o corrige) · **C** arquitectura justificada · **D** estructura física de carpetas · **E** modelo de datos (entidades y relaciones principales, diagrama Mermaid) · **F** dependencias entre módulos · **G** flujo de información · **H** preparación contable · **I** preparación RR. HH. · **J** roadmap por etapa con criterios de aceptación · **K** riesgos · **L** decisiones arquitectónicas (→ `DECISIONES.md`). Termina con la lista de preguntas abiertas. **Detente.**

## C1. Fase 0 — Fundamentos [IMPLEMENTAR]

Es una **rebanada vertical** que valida toda la arquitectura antes de multiplicar módulos.

Alcance: solución backend (`.sln` clásico, proyectos, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `launchSettings.json`, `appsettings*.json` + `appsettings.Example.json`) · frontend Next.js con Yarn, `.env.example` y tema centralizado por tokens (B19) · `.vscode/` · `database.sql` inicial (configuración, seguridad, módulos, menú, auditoría, `esquema_version`, `empresa` y `sucursal` mínimas, seeds) · Serilog · manejo global de errores (ProblemDetails) · health checks · autenticación (login, refresh, logout, bloqueo, cambio y recuperación de contraseña) · autorización por permiso y por módulo · usuarios, roles, permisos (pantallas de administración) · control de módulos/fases (pantalla de administración) · menú dinámico · auditoría base · **un catálogo de referencia completo de punta a punta (Sucursales)** que fije el patrón CRUD para el resto · README inicial · pruebas.

Criterios de aceptación:
1. `database.sql` se instala en MySQL 8.4 vacío sin errores.
2. `dotnet build`, `dotnet test`, `yarn build` pasan; `yarn dev` y la API arrancan; abre en Visual Studio y en VS Code.
3. Login/refresh/logout/bloqueo funcionan; contraseña inicial se obliga a cambiar.
4. Usuario sin permiso → 403; módulo no liberado → 403; sin token → 401; la prueba "todo endpoint tiene permiso" existe y pasa.
5. El menú cambia según rol y estado del módulo; saltarse la UI llamando a la API directamente no evita los controles.
6. Cada operación del catálogo de referencia deja registro de auditoría.
7. La aplicación corre con un usuario MySQL sin DDL.

## C2. Fase 1A — Maestros y organización [IMPLEMENTAR]

Empresas (edición), bodegas, cajas, departamentos y centros de costo (estructura), tipos de producto, categorías, marcas, unidades y conversiones estándar, tipos de proveedor, monedas y tipo de cambio, impuestos, formas de pago, bancos y cuentas bancarias (maestro), tipos de documento y series/correlativos, configuración general de negocio (incl. `fecha_bloqueo`). Analiza y propone otros catálogos realmente necesarios.

## C3. Fase 1B — Clientes, proveedores y productos [IMPLEMENTAR]

- Clientes y proveedores: datos fiscales (NIT validado), contactos, direcciones, condiciones de crédito, estado.
- Productos: código interno, código(s) de barras, descripción, marca, categoría, tipo, unidad base y alternativas con conversiones, costo, listas de precio, impuestos, estado, stock mínimo/máximo, imagen/archivos.
- **Lotes, vencimiento y series: [PREPARAR]** (banderas en producto + tablas), salvo que el dueño pida implementarlos **[CONFIRMAR]**.
- Importación masiva (Excel/CSV) con validación y reporte de errores. Infraestructura de archivos (B13).

## C4. Fase 1C — Inventario [IMPLEMENTAR]

- Existencias por empresa/bodega/producto; **libro de movimientos inmutable** (`movimiento_inventario` + detalle con saldo, costo unitario y total) del que se deriva el **kardex**.
- Entradas, salidas, ajustes (con motivo y aprobación), traslados entre bodegas (con estado "en tránsito" opcional), devoluciones, carga de saldos iniciales, historial.
- Valoración: promedio ponderado móvil; costos histórico y actual; anulación = contra-movimiento al costo original (documentar el tratamiento de casos con fecha retroactiva). Capas FIFO **[PREPARAR]**.
- Reservas por pedido (configurable) y política de stock negativo.
- Todo transaccional con bloqueo ordenado; pruebas de concurrencia y de costeo.

## C5. Fase 1D — Compras [IMPLEMENTAR]

Órdenes de compra → recepciones (parciales) → facturas de proveedor → devoluciones; relación entre documentos (B14). Solicitudes de compra y cotizaciones de proveedores: **[PREPARAR]** salvo que el dueño las pida. El costo de entrada y el tratamiento de diferencias entre OC, recepción y factura se decide en el diseño de la etapa. **Incluye el módulo Cartera (B14a)**: compras a crédito registran su saldo en `documento_saldo` vía `ICartera`.

## C6. Fase 1E — Ventas [IMPLEMENTAR]

Cotizaciones (vigencia, conversión) → pedidos (reserva opcional) → facturas; devoluciones; notas de crédito/débito; precios por lista y descuentos con permisos; ventas a crédito registran su saldo vía `ICartera` (B14a); impresión/PDF; campos fiscales FEL preparados (B16). Descuento de inventario y `evento_contable` en la transacción de factura.

## C7. Fase 1F — Caja y bancos [IMPLEMENTAR]

Cajas por sucursal, apertura (una sesión abierta por caja), ingresos, egresos, cobros (efectivo, tarjeta, transferencia, cheque), pagos a proveedores, pagos parciales y aplicación a documentos vía `ICartera` (B14a), anticipos (básico), cierre con arqueo y diferencias, historial. Cuentas bancarias como maestro; conciliación **[FUTURO]**.

## C8. Fase 1G — Reportería, auditoría avanzada y puesta en producción [IMPLEMENTAR]

Reportes de inventario, kardex, compras, ventas, caja, clientes, proveedores; consulta/exportación de auditoría; notificaciones y `ERP.Worker`; CI/CD; scripts `deploy/`; `DESPLIEGUE-WINDOWS.md`; estrategia y prueba de backup/restauración; revisión de seguridad completa.

## C9. Fase 1H — Integración FEL [IMPLEMENTAR cuando se elija certificador]

Adaptador al certificador, certificación al emitir, anulación fiscal, reintentos, manejo de errores y reportes de estado.

## C10. Fase 2 — Contabilidad, CxC, CxP [al llegar su turno]

Catálogo de cuentas, naturaleza, ejercicios y períodos, partidas, diario, mayor, balance de comprobación, estados financieros, cierres. CxC y CxP: créditos, facturas, abonos, pagos, anticipos, saldos, vencimientos, estados de cuenta, envejecimiento. Procesador de `evento_contable` → partidas; integración con ventas, compras, caja, bancos e inventario. Antes de implementarla: nuevo diseño detallado y aprobación.

## C11. Fase 3 — Recursos humanos y nómina [al llegar su turno]

Empleados, expedientes, datos laborales, puestos, departamentos, salarios, historial laboral, contrataciones, cambios salariales, traslados, suspensiones, permisos, vacaciones, terminaciones. Nómina: motor de conceptos configurables (ingresos, deducciones, impuestos, seguridad social, préstamos, anticipos), períodos, cálculo, aprobación, pago, recibo, historial; prestaciones (Bono 14, Aguinaldo, indemnización) y beneficios configurables (seguros, viáticos, préstamos); impacto contable. Antes de implementarla: nuevo diseño detallado y aprobación.
