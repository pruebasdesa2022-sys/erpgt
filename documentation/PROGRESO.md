# ERP Modular — Progreso

> Se lee al iniciar cada sesión (`CLAUDE.md §2.1`). Mantener corto: estado, decisiones, pendientes.

## Estado actual

| | |
|---|---|
| **Etapa en curso** | **Fase 0 — Fundamentos**, solo la parte de base de datos |
| **Estado** | `database/database.sql` (v0.1.0) escrito y revisado de forma estática. **Aún no se ha ejecutado en MySQL**: falta la prueba del dueño |
| **Siguiente sesión** | Backend y frontend de Fase 0 (no iniciar sin que el dueño lo pida) |
| **Última actualización** | 2026-10-09 |

## Etapas

| Etapa | Estado |
|---|---|
| Diseño (C0) | Aprobado |
| Fase 0 — base de datos | Lista para probar |
| Fase 0 — backend y frontend | Pendiente |
| 1A · 1B · 1C · 1D · 1E · 1F · 1G | No iniciadas |
| 1H (FEL, flotante) | Pendiente de certificador |
| Fase 2 · Fase 3 | No iniciadas |

## Base de datos de Fase 0

- Base `erpgt`, 23 tablas: `esquema_version` · `configuracion`, `seguridad_version` · `empresa`, `sucursal` · 16 de seguridad (usuarios, roles, permisos, sesiones, módulos, menú) · `auditoria`, `auditoria_acceso`.
- Sin vistas, procedimientos, funciones ni triggers en esta fase.
- Seeds: política de contraseñas, empresa `EMP01` y sucursal `SUC01` genéricas, 18 módulos, 19 permisos, rol `ADMINISTRADOR` con todos, usuario `admin` (contraseña temporal `Cambiar.2026!`, cambio obligatorio en el primer ingreso), menú inicial de 8 elementos.
- Revisión estática hecha: paréntesis y comillas balanceados, ninguna FK apunta a una tabla aún no creada, sin nombres de índice o restricción repetidos, toda FK tiene índice, archivo en UTF-8 sin BOM.

### Cómo probarla

Requiere que la base `erpgt` no exista. Desde la raíz del proyecto:

```
PowerShell:  mysql -u root -p --default-character-set=utf8mb4 -e "source database/database.sql"
cmd / bash:  mysql -u root -p --default-character-set=utf8mb4 < database/database.sql
```

Comprobación rápida después de instalar:

```sql
USE erpgt;
SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'erpgt';  -- 23
SELECT (SELECT COUNT(*) FROM modulo) modulos, (SELECT COUNT(*) FROM permiso) permisos,
       (SELECT COUNT(*) FROM rol_permiso) rol_permiso, (SELECT COUNT(*) FROM menu) menu;  -- 18, 19, 19, 8
SELECT nombre FROM modulo WHERE codigo = 'ORGANIZACION';  -- 'Organización', con tilde
```

## Decisiones

- **2026-10-09 — Diseño aprobado.** Preguntas P6–P17 de `DISENO.md` quedan con sus valores por defecto.
- **2026-10-09 — MySQL 8.0 en lugar de 8.4** para desarrollo (ADR-021). El equipo tiene 8.0.46 en `localhost:3306`.
- **2026-10-09 — Esquema de Fase 0** (ADR-022): base `erpgt`; solo el rol `ADMINISTRADOR`; `usuario_rol` admite empresa opcional; `creado_por`/`modificado_por` sin FK; `solicitud_idempotente` pasa a 1C.
- **2026-10-09 — Formato del hash de contraseñas** fijado en ADR-015; el backend debe respetarlo.
- **2026-10-09 — Cambios respecto al plan presentado:** no se sembró el permiso `AUDITORIA_BITACORA_EXPORTAR` (la exportación de auditoría es de 1G) ni permisos para administrar el menú (Fase 0 no tiene esa pantalla).
- ADR-012, 016, 017, 018 y 020 siguen **propuestas**; se confirman con el plan de backend de Fase 0.
- Decisiones anteriores de C0 (Cartera, `PROCESAR`, costeo en C#, orden de secciones, `proxy.ts`): ver `DECISIONES.md`.

## Pendientes y problemas conocidos

1. **`database.sql` sin ejecutar.** Una prueba en una base temporal `erp_test_fase0` fue bloqueada por los permisos de la sesión; no se insistió. Lo prueba el dueño (o se habilita el permiso).
2. **`CLAUDE.md §4`/`§15` y `ESPECIFICACION.md`** (D2, criterio 1 de C1) siguen diciendo MySQL 8.4; conviene que el dueño los ajuste a ADR-021.
3. **Usuario MySQL de la aplicación** (solo DML, sin `UPDATE`/`DELETE` en auditoría): plantilla `database/usuarios-ejemplo.sql` pendiente para la sesión de backend. El desarrollo no debe usar `root`.
4. **.NET SDK 10 sin instalar** (hay 9.0.312): hace falta antes del backend. Yarn sin activar (Corepack presente).
5. `ESPECIFICACION.md §B2` conserva el orden antiguo de secciones; prevalece `CLAUDE.md §7`.
6. `PROMPTS.md` y `DISENO-UI.md`, citados en la especificación, no existen (`DISENO-UI.md` se crea con el frontend).
7. Los diagramas Mermaid de `DISENO.md` no se han comprobado en un visor.

## Comandos de verificación

Aún no hay proyectos de backend ni frontend. Base de datos: ver "Cómo probarla".
