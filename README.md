# MyApp — Plantilla en blanco (MHVS)

> Plantilla base: dotnet-mhvs-template v1.1.0

Plantilla base para arrancar **cualquier proyecto nuevo** del equipo desde
cero. Trae logging y configuración de credenciales ya resueltos, para que
el primer día se dedique a escribir el módulo real, no a plomería.

Lee **ARCHITECTURE.md** antes de tocar código — ahí está la convención
completa. Para la visión general (el "por qué" de todo esto, pensada para
presentar al equipo), revisa **PRESENTATION.md**.

## Primeros pasos al clonar

**Requisitos previos:**

- Git.
- El SDK de .NET que fija `global.json` (versión y política `rollForward`).
  Dentro del repo, `dotnet --version` muestra cuál se está usando.
- MySQL, **solo** si vas a usar el módulo `Tasks` (paso 3).
- Python, **solo** para correr `scripts/lint_hu.py` en local (ver
  `CONTRIBUTING.md` → "5. Tests").
- Certificado de desarrollo HTTPS: el perfil de `dotnet run` también escucha
  en `https://localhost:51057`. Si tu máquina no lo tiene, la guía estándar es
  `dotnet dev-certs https --trust`. *(No verificado en una máquina sin
  certificado.)*

### 1. Clonar, compilar y correr los tests

```bash
git clone <url-del-repo>
cd <carpeta-del-repo>

# El primer build activa solo el hook de pre-push
# (git config core.hooksPath .githooks, vía Directory.Build.targets;
# no hace falta correrlo a mano)
dotnet build MyApp.slnx

# Los tests deben pasar sin ninguna configuración local
dotnet test MyApp.slnx
```

El hook existe porque en repos privados GitHub requiere un plan de pago
(Pro o Team) para aplicar protección de rama real; es la mitigación local
mientras tanto (detalle en `CONTRIBUTING.md`).

### 2. Arrancar la app y comprobar que funciona

```bash
dotnet run --project src/Bootstrap
```

Con `dotnet run` la app escucha en `http://localhost:51058` (definido en
`src/Bootstrap/Properties/launchSettings.json`); en Docker es el puerto `8080`.
Arranca en `Development`. Sin archivo de configuración local, el log muestra
al arrancar un aviso "No se encontró config/connectionstrings.local.json ...":
en este punto es lo esperado.

Con la app corriendo, comprueba:

- `/health` responde 200 (`Healthy`).
- `/swagger` abre Swagger (solo en `Development`).
- `GET /version` responde qué build está corriendo: versión (la del
  ensamblado de la app, no la de la plantilla), commit de git (corto y
  completo), configuración de build y versión de .NET. Sirve para confirmar
  si un despliegue ya tiene un fix. Detalle, incluida la limitación con
  Docker, en `ARCHITECTURE.md` → "Endpoint `/version`".

```bash
curl http://localhost:51058/version
```

El módulo `Sample` incluido corre sin BD (usa un repositorio en memoria),
así que el proyecto arranca funcionando desde el primer `dotnet run`.
Prueba:

Bash / Git Bash:

```bash
# PingSample: crea un item y devuelve su id
id=$(curl -s -X POST http://localhost:51058/sample -H "Content-Type: application/json" -d '{"name":"prueba"}' | tr -d '"')

# RenameSampleItem: renombra el item (404 si el id no existe, 400 si el nombre es inválido)
curl -X PUT "http://localhost:51058/sample/$id/name" -H "Content-Type: application/json" -d '{"name":"nuevo nombre"}'
```

PowerShell (`curl` ahí es un alias de `Invoke-WebRequest`, por eso se usa `Invoke-RestMethod`):

```powershell
$id = Invoke-RestMethod -Method Post -Uri http://localhost:51058/sample -ContentType 'application/json' -Body '{"name":"prueba"}'
Invoke-RestMethod -Method Put -Uri "http://localhost:51058/sample/$id/name" -ContentType 'application/json' -Body '{"name":"nuevo nombre"}'
```

### 3. Configuración local (solo para `Tasks`)

El repo también trae `Tasks` (`POST /tasks`, HU-TASKS-001), el ejemplo de
referencia de persistencia **real** contra MySQL (ver ARCHITECTURE.md →
"Dos ejemplos de referencia: `Sample` vs. `Tasks`"). **A diferencia de
`Sample`, este sí necesita configuración**: `ConnectionStrings:MysqlTodo`
apuntando a un MySQL.

Sin esa cadena de conexión, `POST /tasks` responde **500** con
`"title": "Persistence failure"` y `"detail": "La tarea no fue creada."`. La
causa queda en el log, como `ERROR` "No se pudo guardar la tarea ...", con
este mensaje:

```
Falta la cadena de conexión ConnectionStrings:MysqlTodo (config/connectionstrings.local.json o la variable de entorno ConnectionStrings__MysqlTodo).
```

La aplicación no se cae: `/health` sigue respondiendo 200 y `Sample` funciona
normal.

La configuración local va al final a propósito: así confirmas primero que
todo (build, tests, arranque y `Sample`) funciona sin secretos, y si después
algo falla, sabes que la causa es la configuración.

```bash
# Copiar el archivo de credenciales de ejemplo
cp config/connectionstrings.local.json.example config/connectionstrings.local.json
# Editar config/connectionstrings.local.json: la clave MysqlTodo, apuntando a tu MySQL
#   (la app lo busca en la raíz del repo o junto al proyecto, y al arrancar
#   avisa en el log si lo cargó o no lo encontró)
```

El archivo se busca solo al arrancar: si lo creas con la app corriendo,
reiníciala (detalle en `config/README.md`). Está en `.gitignore`, así que no
aparece en `git status`. La tabla no la crea la app: se crea a mano una sola
vez con `src/Modules/Tasks/Infrastructure/schema.sql`.

```bash
# CreateTask: crea una tarea (500 si ConnectionStrings:MysqlTodo no está configurada)
curl -X POST http://localhost:51058/tasks -H "Content-Type: application/json" -d '{"title":"Comprar pan"}'
```

## Estructura

```
src/
├── Bootstrap/        # Program.cs, appsettings.json, log4net.config
├── Modules/
│   ├── Sample/         # módulo de ejemplo — bórralo al iniciar el proyecto real
│   └── Tasks/          # ejemplo de referencia: persistencia real contra MySQL (ADO.NET puro)
└── Shared/
    ├── Ports/           # interfaces técnicas cruzadas entre módulos
    ├── Kernel/          # tipos de dominio universales + contrato de errores compartido
    └── Web/             # helpers HTTP reutilizables y agnósticos de negocio (filtro de validación)
config/
├── connectionstrings.local.json.example   # SÍ se versiona
└── connectionstrings.local.json           # NO se versiona (.gitignore), credenciales reales
tests/
├── Modules/Sample/                # tests unitarios del módulo de ejemplo
├── Modules/Tasks/                 # tests unitarios de Tasks (FakeTaskRepository, sin tocar MySQL)
└── Bootstrap.IntegrationTests/    # levanta la app real en memoria: contrato HTTP, arranque sin errores, X-Correlation-ID
docs/templates/       # HU-TEMPLATE.md: plantilla en blanco de historias de usuario
scripts/              # lint_hu.py: validación estructural de HU (corre en CI)
AGENTS.md             # índice para agentes: qué skill de .opencode/skills/ usar
```

## Cómo empezar tu proyecto real

1. Corre el proyecto tal cual una vez para confirmar que arranca.
2. Borra `src/Modules/Sample` y `tests/Modules/Sample`, y sus referencias en
   `Program.cs`, `Bootstrap.csproj` (`ProjectReference`), `MyApp.slnx` y el
   `COPY` de `Sample.csproj` en el `Dockerfile`. Borra o adapta también
   `tests/Bootstrap.IntegrationTests/SampleEndpointsTests.cs` (usa los endpoints
   de `Sample`); `BootstrapTests.cs` no depende de él y se queda.
3. Crea tu primer módulo copiando la estructura de `Sample` (ver
   ARCHITECTURE.md → "Cómo agregar un módulo nuevo").
4. A partir de ahí, cada módulo/caso de uso nuevo sigue el mismo patrón.
