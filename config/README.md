# Configuración local (credenciales)

Este proyecto separa la configuración **sensible** (cadenas de conexión,
API keys) de `appsettings.json`, que sí se versiona en git.

## Primera vez que clonas el repo

1. Copia el archivo de ejemplo:
   ```bash
   cp config/connectionstrings.local.json.example config/connectionstrings.local.json
   ```
2. Edita `config/connectionstrings.local.json` con tus credenciales reales.
3. Ese archivo ya está en `.gitignore` — nunca se va a subir por accidente.

La app lo busca al arrancar subiendo desde el content root (con `dotnet run`,
`src/Bootstrap/`) hasta dos niveles, y usa el **primero** que encuentra:

1. `src/Bootstrap/config/`, junto al proyecto
2. `src/config/`
3. `config/`, en la raíz del repo. Es el caso normal.

Con la app publicada, son las mismas tres posiciones relativas a la carpeta
de la app. Al arrancar escribe en el log si lo cargó (con la
ruta) o no lo encontró. Es opcional a propósito (en producción se usan
variables de entorno), así que si no aparece el mensaje "Configuración local
cargada desde ...", revisa el nombre y la ubicación. Los cambios al archivo
se recargan en caliente, sin reiniciar, **solo si el archivo ya existía al
arrancar**. Si lo creas con la app corriendo, no se detecta hasta reiniciar.
La búsqueda (`LocalConfigLocator.Locate`) se hace una sola vez, al arrancar.
`Program.cs` solo agrega la fuente (`AddJsonFile(..., reloadOnChange: true)`)
si encontró el archivo, así que, si no existía, no hay nada que vigilar.

## Por qué no usamos `appsettings.Development.json` para esto

`appsettings.Development.json` normalmente SÍ se versiona (sirve para
config no sensible de desarrollo). Si metes ahí una contraseña real y
alguien la commitea, ya quedó en el historial de git para siempre, aunque
la borres después. Por eso las credenciales viven en un archivo aparte
que ni siquiera git conoce.

## En producción / CI-CD

No se usa este archivo. Las mismas claves se inyectan como **variables de
entorno**: las fija el orquestador, el servicio de hosting o el pipeline (por
ejemplo, a partir de los secretos de GitHub Actions). El código no cambia,
porque `IConfiguration` las lee igual sin importar de dónde vengan.

**Hoy no hay integración con Azure Key Vault ni con ningún otro gestor de
secretos.** Si un proyecto lo necesita, ese proveedor se conecta en un punto de
extensión documentado: en `Program.cs`, junto a `AddEnvironmentVariables()`
(ver `ARCHITECTURE.md` → "Configuración y credenciales").

### Regla de nombres: `:` se escribe `__`

Una clave anidada del JSON (`ConnectionStrings:MysqlTodo`) se convierte en
variable de entorno cambiando cada `:` por **dos** guiones bajos:
`ConnectionStrings__MysqlTodo`. Con un solo `_` la variable no llega a la clave.

| Clave en el JSON | Variable de entorno | ¿Lo consume el código hoy? |
|---|---|---|
| `ConnectionStrings:MysqlTodo` | `ConnectionStrings__MysqlTodo` | **Sí**: módulo `Tasks` (`MySqlTaskRepository`) |
| `ConnectionStrings:Default` | `ConnectionStrings__Default` | No: está en el `.example`, pero nada la lee |
| `ExternalServices:SomeApiKey` | `ExternalServices__SomeApiKey` | No: está en el `.example`, pero nada la lee |

Ejemplos. Los valores son placeholders, nunca pongas aquí credenciales reales.

```bash
# bash
export ConnectionStrings__MysqlTodo="Server=mi-servidor;Port=3306;Database=mhvs_todo;User=CAMBIA_ESTO;Password=CAMBIA_ESTO;"
dotnet MyApp.Bootstrap.dll
```

```powershell
# PowerShell (solo para la sesión actual)
$env:ConnectionStrings__MysqlTodo = "Server=mi-servidor;Port=3306;Database=mhvs_todo;User=CAMBIA_ESTO;Password=CAMBIA_ESTO;"
dotnet MyApp.Bootstrap.dll
```

### Quién gana si la misma clave está en varios sitios

De **menor a mayor** prioridad (la última fuente gana):

| # | Fuente | La agrega |
|---|---|---|
| 1 | `appsettings.json` | `WebApplication.CreateBuilder` |
| 2 | `appsettings.{Environment}.json` | `WebApplication.CreateBuilder` |
| 3 | Variables de entorno | `WebApplication.CreateBuilder` |
| 4 | Argumentos de línea de comandos (`--ConnectionStrings:MysqlTodo=...`) | `WebApplication.CreateBuilder` |
| 5 | `config/connectionstrings.local.json` (si se encuentra) | `Program.cs` |
| 6 | Variables de entorno, otra vez | `Program.cs` (`AddEnvironmentVariables()`) |

No hay user secrets: `Bootstrap.csproj` no define `UserSecretsId`.

`appsettings.{Environment}.json` (`Development`, `Staging` o uno nuevo) es la
fuente 2 de esta tabla: el archivo local y las variables de entorno ganan sobre
él, así que no sirve para fijar credenciales por entorno. Cómo se elige el
entorno y qué cambia en cada uno: `ARCHITECTURE.md` → "Entornos".

- **Las variables de entorno ganan sobre todo**, también sobre el archivo
  local. Si en tu máquina defines la misma clave en los dos sitios, se usa la
  variable de entorno. Lo verifica `ProductionSecretsConfigurationTests`.
- **Advertencia: los argumentos de línea de comandos pierden** contra el
  archivo local y contra las variables de entorno. `Program.cs` agrega esas dos
  fuentes después de las que trae `CreateBuilder`. Un `--ConnectionStrings:...`
  pasado al arrancar no tiene efecto si la clave ya está en el archivo local o
  en una variable de entorno.
