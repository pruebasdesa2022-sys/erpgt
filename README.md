# ERP Modular

ERP para empresas en Guatemala. Un solo sistema que crece por fases: operación comercial (Fase 1), contabilidad y cuentas por cobrar/pagar (Fase 2), y recursos humanos y nómina (Fase 3).

**Estado:** Fase 0 (fundamentos) — seguridad, usuarios, roles, permisos, control de módulos, menú, auditoría y el catálogo de Sucursales. El avance detallado está en [documentation/PROGRESO.md](documentation/PROGRESO.md).

## Requisitos

| Herramienta | Versión | Para qué |
|---|---|---|
| .NET SDK | 10 | Backend |
| MySQL | 8.4 LTS | Base de datos |
| Node.js | 20.9 o superior, con Corepack (probado con 24) | Frontend |
| Git | cualquiera reciente | Control de versiones |

Yarn no se instala aparte: se usa con `corepack yarn …`, que toma la versión fijada en `frontend/package.json`.

## Puesta en marcha (desarrollo)

### 1. Base de datos

Con un usuario administrador de MySQL, desde la raíz del proyecto (la base `erpgt` no debe existir):

```powershell
mysql -u root -p --default-character-set=utf8mb4 -e "source database/database.sql"
```

El script crea la base `erpgt`, las tablas y los datos indispensables. **Nunca se vuelve a ejecutar sobre una base con datos**: los cambios posteriores llegan como scripts en `database/updates/`.

La aplicación no debe conectarse como `root`. `database/usuarios-ejemplo.sql` es la plantilla para crear su usuario de mínimo privilegio.

### 2. Backend

Los secretos no van en archivos del repositorio. En desarrollo se guardan con User Secrets (ver la plantilla `backend/src/ERP.Api/appsettings.Example.json`):

```powershell
cd backend
dotnet user-secrets set "ConnectionStrings:Erp" "Server=localhost;Port=3306;Database=erpgt;User ID=<usuario>;Password=<contraseña>" --project src/ERP.Api
dotnet user-secrets set "Jwt:Clave" "<cadena aleatoria de al menos 32 caracteres>" --project src/ERP.Api
dotnet run --project src/ERP.Api --launch-profile http
```

La API queda en `http://localhost:5080`. Comprobación: `http://localhost:5080/health`. La documentación de los endpoints (solo en Desarrollo y QA) está en `http://localhost:5080/scalar`.

### 3. Frontend

```powershell
cd frontend
copy .env.example .env.local
corepack yarn install
corepack yarn dev
```

La aplicación queda en `http://localhost:3000`.

### 4. Primer ingreso

| Usuario | Contraseña temporal |
|---|---|
| `admin` | `Cambiar.2026!` |

El sistema obliga a cambiarla en el primer ingreso. La contraseña nueva debe tener al menos 10 caracteres, con mayúscula, minúscula, número y símbolo.

## Pruebas

```powershell
cd backend
dotnet test                      # unitarias, de arquitectura y de integración

cd ../frontend
corepack yarn typecheck
corepack yarn lint
corepack yarn test
corepack yarn build
```

Las pruebas de integración necesitan un MySQL 8.4 local y un usuario que pueda crear bases. Cada corrida crea una base temporal `erp_test_*` ejecutando `database/database.sql` (así se valida también el script) y la elimina al terminar; nunca toca `erpgt`.

```powershell
dotnet user-secrets set "ConnectionStrings:Pruebas" "Server=localhost;Port=3306;User ID=<usuario>;Password=<contraseña>" --project tests/ERP.IntegrationTests
```

## Estructura

```
backend/         API en .NET 10 (Domain, Application, Infrastructure, Api) y pruebas
frontend/        Next.js 16: pantallas y BFF (el navegador nunca habla directo con la API)
database/        database.sql (instalación limpia), updates/ (cambios incrementales), usuarios-ejemplo.sql
documentation/   Especificación, diseño, decisiones y progreso
```

## Cómo funciona la seguridad, en breve

- El navegador solo habla con Next.js. Los tokens viajan en cookies `httpOnly`; el código de la página no puede leerlos.
- Cada solicitud a la API pasa por: autenticación → empresa y sucursal válidas para el usuario → módulo liberado → permiso → regla de negocio.
- El menú y los botones se muestran según los permisos, pero eso es solo presentación: la API valida todo de nuevo.
- Cada operación queda en la bitácora de auditoría, que no se puede modificar ni borrar.

## Documentación

- [CLAUDE.md](CLAUDE.md) — reglas permanentes del proyecto.
- [documentation/ESPECIFICACION.md](documentation/ESPECIFICACION.md) — especificación funcional y técnica.
- [documentation/DISENO.md](documentation/DISENO.md) — diseño maestro.
- [documentation/DISENO-UI.md](documentation/DISENO-UI.md) — tema visual y cómo cambiar la marca.
- [documentation/DECISIONES.md](documentation/DECISIONES.md) — decisiones de arquitectura y dependencias.
- [documentation/PROGRESO.md](documentation/PROGRESO.md) — estado, pendientes y problemas conocidos.
