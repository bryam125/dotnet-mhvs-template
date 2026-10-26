# MHVS — Modular Hexagonal Vertical Slicing

Esta es la convención de arquitectura para **todos** los proyectos del
equipo, no solo para un dominio en particular. Este documento es la
fuente de verdad: si hay duda de "dónde va este archivo", la respuesta
está aquí.

## Los 3 niveles

```
src/Modules/{Modulo}/{Application|Domain|Infrastructure}/{Slice}/Archivo.cs
```

### Nivel 1 — Módulo (bounded context)
Cada carpeta bajo `src/Modules/` es un dominio de negocio autocontenido.
Regla: **un módulo nunca referencia directamente las clases internas de
otro módulo**. Si un módulo necesita algo de otro, lo hace a través del
proyecto `{Modulo}.Contracts` del otro (solo interfaces y DTOs), nunca
referenciando su proyecto principal. Regla completa, y lo que el compilador
impone y lo que no, en "Contrato entre módulos".

Un módulo bien cortado se podría convertir en microservicio sin
reescribir lógica de negocio.

### Nivel 2 — Hexágono (dirección de dependencias)
```
infrastructure → application → domain
                       ↑
                  (domain no depende de nadie)
```
- **domain/**: entidades, invariantes de negocio, y los *puertos*
  (interfaces) que el negocio necesita. Cero referencias a EF Core,
  ASP.NET, Redis, etc.
- **application/**: casos de uso. Orquesta el domain, usa los puertos.
- **infrastructure/**: *adaptadores*. Implementaciones concretas de los
  puertos definidos en domain.

### Nivel 3 — Slice (caso de uso)
Dentro de `Application/`, cada caso de uso vive en su propia carpeta:
```
{Verbo}{Entidad}/
  {Verbo}{Entidad}Command.cs
  {Verbo}{Entidad}Handler.cs
  {Verbo}{Entidad}Validator.cs   (si aplica)
```
Con 1-3 casos de uso es aceptable dejarlos sueltos en `Application/`.
**Con 4+ casos de uso, se agrupan en subcarpetas** — evita que
`Application/` se vuelva una sopa de archivos.

## Módulo de ejemplo incluido: `Sample`

Este repo trae `src/Modules/Sample` ya funcionando (con repositorio en
memoria, sin necesidad de BD) para que el equipo vea la convención
corriendo de verdad antes de escribir su primer módulo real:

```
Modules/Sample/
├── Application/
│   ├── SampleModule.cs         # registro de DI + endpoints del módulo
│   ├── PingSample/             # patrón 1: crear una entidad
│   │   ├── PingSampleCommand.cs
│   │   ├── PingSampleHandler.cs
│   │   └── PingSampleValidator.cs
│   └── RenameSampleItem/       # patrón 2: buscar + modificar una entidad existente
│       ├── RenameSampleItemCommand.cs
│       ├── RenameSampleItemHandler.cs
│       ├── RenameSampleItemRequest.cs   # cuerpo HTTP (el id del item viaja en la ruta)
│       ├── RenameSampleItemResult.cs
│       └── RenameSampleItemValidator.cs
├── Domain/
│   ├── SampleItem.cs           # Rename devuelve una instancia nueva (inmutabilidad)
│   └── SampleItemRepository.cs  # puerto: GetByIdAsync, AddAsync, UpdateAsync
└── Infrastructure/
    └── InMemorySampleItemRepository.cs  # adaptador
```

Los dos slices demuestran patrones distintos:

- **`PingSample` (crear):** valida en el borde HTTP y persiste con `AddAsync`.
- **`RenameSampleItem` (buscar + modificar):** busca con `GetByIdAsync` y lanza
  `NotFoundException` (404) si no existe; modifica con un método de dominio
  que devuelve una **instancia nueva** en vez de mutar la original, para que
  un fallo de guardado no deje el objeto en memoria a medias; persiste con
  `UpdateAsync` y, si el repositorio falla, registra la causa y lanza
  `PersistenceException` (500).

**Bórralo** (junto con `tests/Modules/Sample` y sus referencias en
`Program.cs`, en el `ProjectReference` de `Bootstrap.csproj`, en `MyApp.slnx`
y en el `COPY` de `Sample.csproj` del `Dockerfile`) en cuanto vayan a crear el
primer módulo real del proyecto. Además, `tests/Bootstrap.IntegrationTests/SampleEndpointsTests.cs`
usa los endpoints de `Sample`: bórralo o adáptalo a los de tu primer módulo real,
conservando lo que verifica (qué formato de error sale de cada camino).
`BootstrapTests.cs` no depende de `Sample` y se queda.

## Dos ejemplos de referencia: `Sample` (en memoria) vs. `Tasks` (MySQL real)

El repo trae dos módulos de ejemplo, y no es redundancia: cada uno enseña
algo que el otro no puede.

- **`Sample`** — patrón CRUD básico, sin ninguna dependencia externa. Mira
  aquí si tu módulo nuevo empieza sin decidir todavía cómo persiste, o si
  quieres ver el patrón "crear" y "buscar + modificar" en su forma más
  simple (repositorio en memoria, cero configuración). Es el punto de
  partida para copiar la estructura de carpetas (ver más abajo).
- **`Tasks`** (`src/Modules/Tasks`, HU-TASKS-001) — el mismo patrón MHVS,
  pero con un adaptador de persistencia **real**: MySQL vía ADO.NET puro
  (`MySqlConnector`, sin ORM). Mira aquí cuando tu módulo vaya a hablar con
  una base de datos de verdad, para ver:
  - **ADO.NET puro, bien hecho:** `Infrastructure/MySqlTaskRepository.cs` —
    SQL siempre parametrizado (nunca concatenado), una conexión por
    operación (abierta y cerrada con `await using`, nunca compartida entre
    requests), y un único `INSERT` atómico para que la regla de "todo o
    nada" (ninguna escritura parcial) se cumpla sin transacción explícita.
  - **Fechas UTC de punta a punta:** columnas `DATETIME(6)` (no
    `TIMESTAMP`, que MySQL reinterpreta según la zona horaria de la
    sesión) y una conversión explícita de ida y vuelta
    (`ToStorageUtc`/`FromStorageUtc`) para que lo que se lee sea
    exactamente lo que se escribió, sin depender de opciones de la cadena
    de conexión.
  - **Uso correcto de `IClock`:** `CreateTaskHandler` recibe `IClock` por
    constructor y usa `clock.UtcNow` para "ahora", nunca
    `DateTime.UtcNow` a mano — así los tests fijan el reloj
    (`FixedClock` en `tests/Modules/Tasks/Support/`) y una fecha límite
    "hoy" es determinista sin importar cuándo corre el test. Esto es lo
    que `Sample` nunca necesitó y por eso no lo enseña.
  - **La cadena de conexión se lee en cada operación, no en el
    constructor:** así la app arranca aunque `ConnectionStrings:MysqlTodo`
    no esté configurada (el error aparece recién cuando algo intenta usar
    `Tasks`, no al iniciar), y un cambio en el archivo local se toma sin
    reiniciar.
  - **Por qué `schema.sql` es solo documentación:**
    `src/Modules/Tasks/Infrastructure/schema.sql` describe la tabla, pero
    la aplicación **nunca lo ejecuta**. La tabla se crea a mano, una sola
    vez, contra la instancia real de MySQL — la plantilla no trae (ni
    pretende traer) un mecanismo de migraciones. Si tu proyecto real
    necesita migraciones versionadas, esa es una decisión aparte que
    tampoco está resuelta aquí; `Tasks` solo muestra cómo se ve un
    adaptador real, no cómo evoluciona un esquema en el tiempo.
  - **Los tests de integración de `Tasks` (`TasksEndpointsTests.cs`) solo
    prueban entradas inválidas.** Un caso válido escribiría de verdad en
    la base configurada en `config/connectionstrings.local.json`, que en
    una máquina de desarrollo es real — por eso el borde HTTP (validación,
    400) se prueba en `Bootstrap.IntegrationTests`, pero el camino feliz
    completo (guardar y volver a leer) solo se verifica con
    `FakeTaskRepository` en `tests/Modules/Tasks/`, nunca contra MySQL de
    verdad en CI.

`Tasks` **no reemplaza a `Sample`** ni depende de él — son dos módulos
independientes que coexisten a propósito, para que puedas comparar ambos
patrones lado a lado en el mismo repo en vez de tener que imaginar cómo
se vería el segundo.

## Neutralidad de motor de base de datos

Este template NO prescribe ningún motor de base de datos específico.
Sample (repositorio en memoria) y Tasks (MySQL/MariaDB vía ADO.NET
puro) son EJEMPLOS de referencia que ilustran el patrón
puerto/adaptador — no una recomendación de tecnología. Un equipo que
adopte este template puede implementar su propio adaptador contra SQL
Server, PostgreSQL, Oracle, o cualquier otro motor, sin tocar Domain/
ni Application/ de ningún módulo existente.

Esto también aplica a la infraestructura de desarrollo local: por
eso, deliberadamente, este template NO incluye un docker-compose.yml
funcional atado a un motor específico todavía — se considera deuda
técnica de largo plazo (ver ROADMAP.md), precisamente para evitar que
la elección de infraestructura de desarrollo local termine
imponiendo, de facto, un motor de base de datos sobre todos los
proyectos derivados.

> **Nota sobre el `docker-compose.yml` que existe hoy:** levanta un
> contenedor de SQL Server, pero **ningún módulo lo usa**. Ese contenedor
> alimenta `ConnectionStrings:Default`, y nada en el código lee esa clave:
> `Sample` es en memoria y `Tasks` lee `ConnectionStrings:MysqlTodo`. Es un
> residuo de una versión anterior de la plantilla, no una elección de
> motor. Queda sin tocar a propósito, junto con el resto de Docker (ver
> `ROADMAP.md` → "Docker, diferido a largo plazo").

## Cómo agregar un módulo nuevo

1. Copia la carpeta `src/Modules/Sample` completa.
2. Renómbrala a tu módulo nuevo y ajusta los namespaces.
3. Define primero las entidades y puertos en `Domain/`.
4. Ajusta o crea el primer caso de uso en `Application/{Verbo}{Entidad}/`.
5. Implementa el adaptador real en `Infrastructure/` (reemplaza el
   repositorio en memoria por EF Core, Dapper, etc. — si tu adaptador habla
   con MySQL vía ADO.NET puro, sigue el patrón de
   `Tasks/Infrastructure/MySqlTaskRepository.cs`, ver arriba).
6. Registra el módulo en `Program.cs` (una línea de DI + una de mapeo de
   endpoints), agrégalo a `MyApp.slnx`, añade su `ProjectReference` en
   `Bootstrap.csproj` y su `COPY ...csproj` en el `Dockerfile` (el `restore` de
   la imagen solo ve los `.csproj` que se copian explícitamente).

> **Convención — proyectos a la solución (`MyApp.slnx`):** agrégalos siempre con
> `dotnet sln MyApp.slnx add <ruta/al.csproj>`, sin editar el archivo a mano.
>
> La solución usa el formato **`.slnx`**, un XML simple: una línea por proyecto,
> sin GUIDs, sin BOM y sin secciones de configuración por plataforma. Eso lo
> hace inherentemente más resistente al ruido de diffs que tenía el `.sln`
> clásico. El motivo del cambio fue medido: `dotnet sln add` con el SDK 10 sobre
> el `.sln` clásico reescribía el archivo entero (63 líneas añadidas y 2 borradas:
> BOM, plataformas `x64`/`x86` inyectadas y proyectos existentes renombrados), y
> el mismo comando sobre el `.slnx` añade **una sola línea** y `remove` deja el
> archivo idéntico byte a byte.
>
> Requisito para quien lo abra con un IDE: **Visual Studio 2022 17.13+** (o una
> versión equivalente de Rider, o VS Code con C# Dev Kit); un IDE más antiguo no
> reconoce `.slnx`. Por línea de comandos basta el SDK fijado en `global.json`.

## Cuándo algo es "compartido" entre módulos

Si dos módulos necesitan el mismo puerto técnico (`IClock`, `IEmailSender`,
`ICurrentUser`), va en `src/Shared/Ports/`. Tipos de dominio realmente
universales (`Money`, `Email` como value object) van en `src/Shared/Kernel/`.

Regla de oro: `Shared` crece muy poco y con mucho cuidado. Si dudas si algo
es compartido o de un módulo, empieza asumiendo que es del módulo.

`Shared/Ports/` es solo para puertos **técnicos**. Un contrato de negocio entre
dos módulos no va en `Shared`: va en el `Contracts` del módulo que lo expone
(ver "Contrato entre módulos").

## Contrato entre módulos

> **Hoy no existe ningún proyecto `Contracts`:** ningún módulo expone nada a
> otro. Esta sección fija la regla para cuando haga falta. `ArchitectureTests`
> (`tests/Bootstrap.IntegrationTests`) ya la verifica.

**Qué puede referenciar cada proyecto:**

| Proyecto | Puede referenciar | Nunca |
|---|---|---|
| Módulo (`src/Modules/{Modulo}/{Modulo}.csproj`) | `Shared`; su propio `{Modulo}.Contracts`, para implementarlo; el `{Otro}.Contracts` de otro módulo, para consumirlo | El proyecto principal de otro módulo |
| `{Modulo}.Contracts` | `Shared`, y nada de `src/Modules` | Ningún proyecto de `src/Modules`, ni siquiera otro `Contracts` |
| `Shared` | — | Ningún proyecto de `src/Modules`, tampoco un `Contracts` |

`Bootstrap.csproj` sigue referenciando todos los módulos y `Shared` (ver
"Cómo agregar un módulo nuevo").

**Qué es un `Contracts`:**

- Vive en `src/Modules/{Modulo}.Contracts/{Modulo}.Contracts.csproj`: una
  carpeta **hermana** del módulo, no dentro de `src/Modules/{Modulo}/`. Dentro,
  el SDK compila por defecto todos los `.cs` de la carpeta del proyecto
  principal, así que el módulo compilaría también los archivos del `Contracts`
  y llevaría su propia copia de los tipos. Comprobado: desde el segundo build
  falla con `CS0579` (atributos duplicados).
- Namespace `MyApp.Modules.{Modulo}.Contracts`.
- Solo interfaces y DTOs inmutables (`record`). Sin lógica.
- Puede referenciar `Shared`. No referencia ningún proyecto de `src/Modules`,
  ni siquiera otro `Contracts`.
- Se crea únicamente cuando un módulo expone algo a otro. Es un proyecto más:
  se agrega con `dotnet sln MyApp.slnx add` y necesita su `COPY` en el
  `Dockerfile`, igual que el `.csproj` de un módulo.

**Quién hace qué:**

1. **El módulo que expone** define la interfaz y sus DTOs en su `Contracts`, la
   implementa dentro de su módulo y la registra en `Add{Modulo}Module()`
   (`Application/{Modulo}Module.cs`), donde ya registra sus servicios.
2. **El consumidor** define **su propio** puerto en su `Domain/`, con el
   lenguaje de su dominio.
3. El adaptador del consumidor en `Infrastructure/` implementa ese puerto
   llamando al `Contracts` del otro módulo. Es lo único del consumidor que lo
   usa: su `Domain/` y su `Application/` nunca usan el `Contracts` de otro
   módulo. El adaptador se registra en el `Add{Modulo}Module()` del
   consumidor.

El punto 3 es una regla de documentación y revisión (`architecture-review`):
un test por ensamblado no distingue capas dentro de un módulo.

**Consultas y cambios de estado.** La interfaz síncrona sirve para
**consultar**. Un cambio de estado en otro módulo **no es atómico** con la
persistencia del llamador: no hay transacción distribuida ni outbox. Si uno de
los dos guarda y el otro falla, los datos quedan inconsistentes. Ante una HU
que necesite eso, hay que detenerse y decidir con el humano; esta regla no lo
resuelve.

**Eventos.** No hay mecanismo de eventos en memoria y esta regla no adopta
ninguno. Si hace falta, es una decisión nueva que iría encima de esta regla,
con los tipos de evento en el `Contracts` del módulo que los publica.

**Qué impone el compilador y qué no.** Todos los tipos de los módulos son
`public`, y los tests de cada módulo dependen de ello. Lo que impide usar las
"clases internas" de otro módulo no es la visibilidad: es que la única
`ProjectReference` permitida hacia otro módulo sea a su `Contracts`.
`ArchitectureTests` lo comprueba con dos fuentes, porque ninguna basta sola:

- Las `ProjectReference` de los `.csproj`. Ven una referencia aunque ningún
  tipo se use todavía, pero no evalúan propiedades ni condiciones de MSBuild.
- `Assembly.GetReferencedAssemblies()`. Solo ve lo que el IL usa de verdad,
  pero también ve lo que llega por transitividad.

`Shared` → módulo, además, lo rechaza el propio build (dependencia circular)
mientras el módulo referencie `Shared`.

**Ejemplo (ilustrativo, no compilado; ningún módulo de ejemplo lo usa).**
`Orders` necesita saber si existe un cliente de `Customers`. Los dos módulos
son ficticios. Los puertos no llevan prefijo `I`, como `TaskRepository` y
`SampleItemRepository`.

```csharp
// 1) Contracts del módulo que expone: src/Modules/Customers.Contracts/CustomerDirectory.cs
namespace MyApp.Modules.Customers.Contracts;

public sealed record CustomerSummary(Guid Id, string Name);

public interface CustomerDirectory
{
    Task<CustomerSummary?> FindAsync(Guid customerId, CancellationToken ct);
}
// Customers la implementa dentro de su módulo y la registra en AddCustomersModule().

// 2) Puerto propio del consumidor: src/Modules/Orders/Domain/CustomerExistence.cs
namespace MyApp.Modules.Orders.Domain;

public interface CustomerExistence
{
    Task<bool> ExistsAsync(Guid customerId, CancellationToken ct);
}

// 3) Adaptador del consumidor: src/Modules/Orders/Infrastructure/CustomerExistenceAdapter.cs
using MyApp.Modules.Customers.Contracts;
using MyApp.Modules.Orders.Domain;

namespace MyApp.Modules.Orders.Infrastructure;

public sealed class CustomerExistenceAdapter(CustomerDirectory directory) : CustomerExistence
{
    public async Task<bool> ExistsAsync(Guid customerId, CancellationToken ct) =>
        await directory.FindAsync(customerId, ct) is not null;
}
// Orders.csproj: <ProjectReference Include="../Customers.Contracts/Customers.Contracts.csproj" />
// y el adaptador se registra en AddOrdersModule().
```

## Logging

El proyecto trae **log4net** configurado desde el arranque
(`src/Bootstrap/log4net.config` + `Program.cs`). Se inyecta como
`ILogger<T>` estándar de .NET — los módulos nunca referencian log4net
directamente, solo la abstracción de Microsoft. Si el día de mañana el
equipo cambia a Serilog, solo se toca `Program.cs` y el `.csproj` de
`Bootstrap`.

Salida configurada por defecto:
- Consola (útil en desarrollo).
- Archivo rotado por día en `logs/app.log` (útil en producción). La ruta es
  relativa a la carpeta de los binarios de la app, no a la raíz del repo ni
  a la carpeta desde donde se lanza: con `dotnet run` queda en
  `src/Bootstrap/bin/Debug/net10.0/logs/app.log`, y los tests de integración
  escriben en `tests/Bootstrap.IntegrationTests/bin/Debug/net10.0/logs/app.log`.

Cada request recibe un `X-Correlation-ID`. El middleware lo guarda en
`log4net.LogicalThreadContext.Properties["CorrelationId"]`, y eso es lo que
llega al patrón de log4net (`%property{CorrelationId}`). También abre un scope
de `ILogger` con la misma clave, pero el patrón actual no lo usa. Se reutiliza el que envía el cliente **solo si**
es un único valor de hasta 128 caracteres con letras ASCII, dígitos y guiones
(el formato de un GUID o de un id de traza). Si no, el servidor genera uno nuevo.
Nunca se rechaza la request, porque es un header de diagnóstico y no una
validación de negocio.
Úsalo para rastrear una petición completa entre logs. El header se devuelve en
**todas** las respuestas, también en las de error (400/404/500): se escribe al
iniciar la respuesta porque `ExceptionHandlerMiddleware` limpia los headers antes
de invocar a `GlobalExceptionHandler`. En las respuestas de `GlobalExceptionHandler`
el campo `traceId` del cuerpo vale lo mismo que ese header. Ver también
"Observabilidad".

log4net se configura **una sola vez**, con `AddLog4Net` en `Program.cs`. No llames
además a `XmlConfigurator.Configure`: abre dos veces el appender de archivo y
log4net imprime `log4net:ERROR ... Unable to acquire lock` en cada arranque.
`Bootstrap.IntegrationTests` falla si eso reaparece.

**Inyección de líneas de log.** Los layouts de `log4net.config` usan
`%safemessage`, nunca `%message`. El converter
(`Bootstrap/Logging/SafeMessagePatternConverter.cs`) escapa `\r` y `\n` (y los
separadores de línea Unicode) del mensaje renderizado. Así, un valor de
entrada como `"x\nERROR [admin] ..."` queda en la misma línea, con un `\n`
literal, y no como un evento falso. Como `Program.cs` deja a log4net como único
proveedor, esto cubre **todos** los logs, de los módulos y del framework, sin que
ningún módulo tenga que sanear a mano. Si agregas un appender, registra el
converter y usa `%safemessage`. `LogInjectionTests` lo verifica contra el
archivo de log real.

Hay un límite conocido: `%exception` no se sanea, porque el stack trace es
multilínea a propósito. Si el mensaje de una excepción incluye texto del
usuario con saltos de línea, ese texto sí puede ocupar varias líneas.

## Observabilidad

**Qué hay hoy:** logs locales (log4net, ver "Logging") y el `X-Correlation-ID`.

- **Límites del correlation ID:** se acepta el del cliente solo si es un único
  valor de hasta 128 caracteres con letras ASCII, dígitos y guiones. Si no, el
  servidor genera un GUID sin guiones. Nunca se rechaza la solicitud.
- **Dónde aparece:**
  - En el header `X-Correlation-ID` de **todas** las respuestas, también las de
    error.
  - En cada línea de log escrita durante la solicitud, entre corchetes
    (`%property{CorrelationId}`).
  - En el campo `traceId` del cuerpo, pero **solo en el Formato B** (errores de
    `GlobalExceptionHandler`, ver "Contrato de errores"). El Formato A (filtro de
    validación) no lleva `traceId`.
- **Un 404 de una ruta inexistente sale sin cuerpo:** solo con el header
  `X-Correlation-ID`. No pasa por `GlobalExceptionHandler`, así que no hay
  `traceId`.

**Qué NO hay (no implementado):**
- Trazas distribuidas.
- Métricas.
- Exportadores (OTLP, Application Insights, Prometheus, etc.).
- Propagación de ningún identificador a llamadas salientes. Hoy la app no hace
  ninguna llamada HTTP saliente: no hay `HttpClient` ni `IHttpClientFactory` en
  `src/`.

### `X-Correlation-ID` y `traceparent` hoy: independientes

Son dos identificadores distintos, y hoy no se relacionan:

- **El correlation ID manda en todo lo visible:** el header de respuesta, el
  `traceId` del cuerpo (Formato B) y los logs. Si llegan `X-Correlation-ID` y
  `traceparent` a la vez, se usa el `X-Correlation-ID`. Si solo llega
  `traceparent`, el correlation ID se genera y **no** se deriva del trace-id.
- **El `Activity.TraceId` existe, pero nada lo expone.** No aparece en logs, ni en
  headers, ni en cuerpos. El único sitio donde .NET 10 lo pondría por su cuenta,
  el `traceId` del Formato A, lo quita `Program.cs` (ver "Versión de .NET y del
  SDK").
- **El `traceparent` entrante, solo lo demostrado:** bajo `TestServer`
  (`WebApplicationFactory`), la `Activity` del hosting
  (`Microsoft.AspNetCore.Hosting.HttpRequestIn`) adopta un `traceparent` W3C
  válido como padre: mismo trace-id, y el parent span id del header. Uno
  inválido se ignora. Esa `Activity` ya existe antes del primer middleware. En
  la app publicada **no es observable hoy**, porque nada la expone. Este
  documento no afirma que el hosting cree siempre la `Activity` ni que el
  `traceparent` se propague siempre.

`TraceparentCorrelationTests` fija lo visible: un `traceparent` (válido o basura)
no rompe la solicitud ni el correlation ID, y el correlation ID generado no es
el trace-id del `traceparent`.

### Punto de extensión: OpenTelemetry (no implementado)

**Dónde.** En `Program.cs`, en el bloque de servicios, junto a
`AddHealthChecks()` y antes de `builder.Build()`. Ahí hay un comentario que marca
el lugar. Se registraría `OpenTelemetry.Extensions.Hosting` con la
instrumentación de ASP.NET Core y un exportador OTLP. **No hace falta ningún
`app.Use…`:** la instrumentación se engancha a la `Activity` que el hosting ya
crea antes del primer middleware (`UseForwardedHeaders`). El template no trae
esos paquetes ni código de ejemplo.

**Convención:**
- El **correlation ID** es para humanos y soporte: es lo que el cliente ve en la
  respuesta y lo que se busca en los logs.
- El **`traceparent` (W3C)** es para la herramienta de trazas.
- Si se adopta OpenTelemetry, **la herramienta de trazas manda sobre las
  trazas**, y mostrará el `Activity.TraceId`. Soporte seguirá viendo el
  correlation ID, que **debe seguir** en los logs, en el header de respuesta y en
  el `traceId` del Formato B. No se reemplaza.
- **Decisión abierta, no implementada:** cómo enlazar los dos identificadores,
  por ejemplo agregando el correlation ID como tag de la `Activity` para poder
  buscar una traza a partir de lo que reporta soporte. Se decide al adoptar
  OpenTelemetry, no antes. `TraceparentCorrelationTests` fija que hoy no se
  derivan uno del otro. Si se decide cambiarlo, ese test se actualiza como
  decisión de diseño.

**Regla para llamadas salientes (cuando las haya):** todo `HttpClient` que se
agregue debe reenviar el `X-Correlation-ID` de la solicitud en curso. No
reenvíes el `traceparent` a mano: si OpenTelemetry está activo, su
instrumentación de `HttpClient` ya lo propaga, y hacerlo a mano lo duplicaría o
lo contradiría. Hoy no hay ningún mecanismo que reenvíe el correlation ID
(ningún `DelegatingHandler`): el primer `HttpClient` que se agregue tiene que
traerlo.

## Plomería HTTP transversal

La plantilla trae en `Bootstrap` el manejo global de excepciones
(`ExceptionHandling/GlobalExceptionHandler.cs`, registrado con
`UseExceptionHandler` en `Program.cs`), health checks (`/health`) y
Swagger/OpenAPI habilitado solo en `Development`. Los módulos no dependen de
esos detalles de arranque: solo lanzan excepciones de `Shared/Kernel` y el
manejador las traduce.

Para evitar boilerplate repetido por slice, los helpers HTTP reutilizables
y agnósticos de negocio viven en `src/Shared/Web/`. Ejemplo: el filtro
genérico de validación con FluentValidation
(`.WithCommandValidation<TRequest>()`), que valida en el borde HTTP antes de
que el handler reciba el comando y siempre produce el mismo cuerpo 400
(el "Formato A" de abajo), sin importar el endpoint. Esta carpeta no debe
contener lógica de negocio ni tipos internos de módulos.

### Endpoint `/version`

`GET /version` responde qué build está corriendo:

```json
{
  "version": "1.0.0",
  "commit": "b4c79f97fcaa",
  "commitFull": "b4c79f97fcaa219cbf749da1878851e2cb474f1a",
  "buildConfiguration": "Release",
  "dotnetVersion": ".NET 10.0.11"
}
```

**Para qué sirve:** durante un incidente, responder rápido "¿este servicio ya
tiene el fix X?" comparando `commit` con el historial de git. No hace falta
acceso al pipeline de build ni a los logs del despliegue.

**De dónde sale:** el SDK ya embebe `"{Version}+{commit de git}"` en
`AssemblyInformationalVersionAttribute` en cada build. `Bootstrap/Diagnostics/BuildInfo.cs`
lo separa en `version` y `commitFull`, y `commit` es el hash corto (12
caracteres). Se calcula una vez al arrancar. Vive en `Bootstrap` y no en un
módulo porque es información del host, no de negocio.

**Qué versión es:** `version` es la versión del ensamblado de la aplicación,
no la de la plantilla. Cada proyecto que nace de la plantilla define la suya.
La versión de la plantilla es su tag de Git (por ejemplo `v1.1.0`). Hoy ningún
`.csproj` define `<Version>`, así que el SDK usa su valor por defecto y
`version` responde `1.0.0`, como en el ejemplo de arriba, aunque el tag de la
plantilla sea otro (ver `ROADMAP.md` → "Residuos menores").

- Se lee de `typeof(Program).Assembly`, **no** de `Assembly.GetEntryAssembly()`.
  En los tests (`WebApplicationFactory`), el ensamblado de entrada es el host de
  pruebas: `commit` saldría `null` o sería el de otro producto.
  `VersionEndpointTests` lo fija.
- **Nunca rompe el arranque ni da 500.** Si el ensamblado no trae el atributo
  o el sufijo `+hash` (un build sin información de git), responde
  `"commit": null` y `"commitFull": null`.
- **Limitación conocida, Docker:** `.dockerignore` excluye `.git/`, así que
  dentro de `docker build` el SDK no tiene historial de git y la imagen
  responde `"commit": null`. La corrección ya se verificó, pero todavía no
  está aplicada: `ARG GIT_COMMIT` + `dotnet publish -p:SourceRevisionId=$GIT_COMMIT`.
  Ver `ROADMAP.md` → 7.9.

**`AllowAnonymous`, a propósito**, igual que `/health`: su utilidad está en poder
consultarlo sin credenciales durante un incidente. En un despliegue real donde
no se quiera exponer versión y commit públicamente, el punto de extensión está
marcado con un comentario en `Program.cs`, junto al `MapGet`. Ahí se cambia a
`.RequireAuthorization()` (una vez conectado el esquema de autenticación, ver
"Autenticación y autorización"), o se restringe la ruta por red o por el
reverse proxy.

### Contrato de errores → HTTP

Una respuesta de error tiene **uno de dos formatos de cuerpo, ambos
intencionales**. No es una inconsistencia: distinguen *qué* falló.

- **Formato A — "el cuerpo no tiene la forma esperada".** Lo produce el filtro
  de validación (`.WithCommandValidation<TRequest>()` sobre un validador de
  FluentValidation, con `Results.ValidationProblem`).
- **Formato B — "el cuerpo tenía forma válida, pero violó una regla o algo
  falló".** Lo produce `GlobalExceptionHandler` a partir de una excepción de
  `Shared/Kernel` (o de un error del framework).

| | Formato A (filtro de validación) | Formato B (`GlobalExceptionHandler`) |
|---|---|---|
| Status | 400 | 400, 404 o 500 (ver tabla de excepciones) |
| Campos | `type` (RFC 9110), `title`, `status`, **`errors`** (por campo) | `type`, `title`, `status`, `detail`, **`traceId`** |
| `traceId` | **No** lleva | **Sí** (igual al `X-Correlation-ID`) |
| Detalle | Un mensaje por campo inválido | Un solo mensaje, sin stack trace ni detalles internos |
| `Content-Type` hoy | `application/problem+json` | `application/json` |

Ambos llevan siempre el header `X-Correlation-ID`. `Content-Type` y la URL de
`type` difieren hoy entre los dos formatos por cómo se produce cada uno, no por
una decisión de diseño: no los uses para distinguirlos (distínguelos por
`errors` frente a `traceId`).

**Cuál usar en cada caso** (para que el próximo módulo no reintroduzca la
confusión). Pregunta: *¿puedo decidirlo mirando solo el cuerpo del request,
sin consultar el repositorio ni el estado?*

| Situación | Cómo se expresa | Formato |
|---|---|---|
| Campo obligatorio vacío, longitud, patrón, rango: la **forma** de la entrada | Regla en el validador del slice + `.WithCommandValidation<TRequest>()` | **A** (400) |
| Hace falta cargar algo o mirar el estado para decidir (regla de negocio, recurso inexistente, fallo de guardado) | Lanzar la excepción de `Shared/Kernel` que corresponda (tabla de abajo) | **B** |
| Cuerpo ausente o JSON mal formado | Lo detecta el framework; lo traduce `GlobalExceptionHandler` | **B** (400) |

No inventes un tercer formato: nada de `Results.BadRequest(...)` con un cuerpo
propio, validar a mano dentro del handler, ni lanzar
`FluentValidation.ValidationException` desde un handler (saldría por el Formato B,
con `errors` **y** `traceId`, distinto del Formato A que espera el cliente). La
validación de forma va siempre por el filtro.

**Excepciones → respuesta (Formato B).** Los errores de negocio y de
infraestructura se expresan con excepciones de `Shared/Kernel`, y el middleware
de `Bootstrap` las traduce sin filtrar stack traces ni detalles internos:

| Excepción | HTTP | Cuándo usarla |
|---|---|---|
| `NotFoundException` (deriva de `DomainException`) | 404 | El recurso referenciado no existe (o su identificador está mal formado). |
| `PersistenceException` (**no** deriva de `DomainException`) | 500 | El repositorio no pudo guardar el cambio. No es una regla de negocio violada por el cliente. |
| `DomainException` y el resto de sus derivados | 400 | Una regla de negocio fue violada. |
| `FluentValidation.ValidationException` | 400 | Solo si algo la lanza fuera del filtro; **no la lances tú** (ver arriba). |
| `BadHttpRequestException` | 400 | Cuerpo ausente o mal formado (no se expone el mensaje interno del framework). |
| Cualquier otra excepción | 500 | Error inesperado; respuesta genérica, sin detalles internos. |

**Registro (logging) de errores.** `GlobalExceptionHandler` registra los 4xx como
`Warning` y los 5xx como `Error` (con la excepción). Quien lanza una
`PersistenceException` registra la causa original con contexto (ej. el id de la
entidad); el manejador global no la vuelve a registrar. ASP.NET, además,
registraba por su cuenta un `ERROR` con stack trace (categoría
`Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware`) por cada excepción,
**incluso las manejadas**: un simple 404 disparaba cualquier alerta por nivel
ERROR. Por eso esa categoría se silencia (`"None"`) en `appsettings.json`, y
solo esa. Consecuencia a tener presente: si el propio manejador global fallara,
el mensaje del framework sobre ese fallo también quedaría silenciado.

**Cómo se verifica.** `tests/Bootstrap.IntegrationTests` levanta la app real en
memoria (`WebApplicationFactory<Program>`, sin mocks ni BD) y fija: arranque sin
errores (incluidos los mensajes internos de log4net), `/health`, el
`X-Correlation-ID` en éxito y en cada tipo de error, el Formato A (incluido que
no lleva `traceId`), el Formato B (404 con `traceId`; 400 de cuerpo mal formado
sin exponer el mensaje del framework) y que los 4xx no generen logs de nivel
`Error`. Si cambias el contrato o `Bootstrap`, actualiza esos tests en el mismo PR.

> **Precedencia:** cuando una solicitud viola tanto la validación de entrada
> como una regla de "recurso no encontrado" al mismo tiempo, gana la
> validación de entrada (400): se ejecuta primero, en el borde HTTP, antes de
> que el handler busque el recurso. Si algún caso de uso necesita otro orden,
> debe declararlo explícitamente en sus criterios de aceptación.

> **Por qué `NotFoundException` y `PersistenceException` viven en
> `Shared/Kernel`:** no son "tipos de dominio universales" en el sentido
> estricto del resto de esa carpeta (`Money`, `Email`). Son el contrato de
> errores compartido entre módulos, consumido por `Bootstrap`; por eso van
> ahí y no dentro de un módulo.

## Configuración y credenciales

Ver `config/README.md` para el detalle completo. Resumen: las cadenas de
conexión y llaves sensibles **no** van en `appsettings.json` (que sí se
versiona). Van en `config/connectionstrings.local.json`, que está en
`.gitignore`. El repo trae `config/connectionstrings.local.json.example`
como plantilla de qué copiar.

En producción, las mismas claves llegan como **variables de entorno**, con
`__` en lugar de `:` (`ConnectionStrings__MysqlTodo`). Las variables de entorno
ganan sobre el archivo local y sobre todo lo demás. La tabla de precedencia
completa está en `config/README.md`, incluida la advertencia de que los
argumentos de línea de comandos pierden contra el archivo local y contra las
variables de entorno. `ProductionSecretsConfigurationTests` fija las dos cosas:
la traducción de `__` y que la variable gana sobre el archivo.

**Punto de extensión: gestor de secretos.** El template **no** integra Azure
Key Vault ni ningún otro gestor de secretos, y no trae paquetes para hacerlo.
Si un proyecto lo necesita, el proveedor de configuración correspondiente se
conecta en `Program.cs`, en el mismo punto donde está
`builder.Configuration.AddEnvironmentVariables()`, el bloque "1) Configuración
en capas". El orden decide quién gana. Agregado después de
`AddEnvironmentVariables()`, el gestor pisa a las variables de entorno.
Agregado antes, las variables pueden sobrescribirlo, por ejemplo para una
corrección puntual en un despliegue. Es un punto de extensión documentado, igual que el de
autenticación (ver "Autenticación y autorización"). Los módulos no se enteran,
porque siguen leyendo `IConfiguration`.

## Entornos

**Cómo se elige el entorno.** Lo decide la variable `ASPNETCORE_ENVIRONMENT`.
Si no está definida, ASP.NET Core usa `DOTNET_ENVIRONMENT`. Si tampoco está
definida, usa **`Production`**: es el valor por defecto del framework
(`Environments.Production`), no una decisión de este template. Arrancar la app
sin ninguna de las dos variables y sin perfil de lanzamiento lo confirma. El
log de arranque escribe `Aplicación iniciada en el entorno Production`
(`Program.cs`, justo después de `builder.Build()`), y esa línea es la forma de
comprobar con qué entorno arrancó un despliegue.

Dos sitios fijan `Development` explícitamente:
`src/Bootstrap/Properties/launchSettings.json` (lo usa `dotnet run` y el IDE) y
`docker-compose.yml`. Para arrancar localmente en otro entorno, usa
`dotnet run --no-launch-profile` con la variable definida, porque si no el
perfil de lanzamiento la pisa. `--no-launch-profile` es una opción de
`dotnet run`. Una app publicada (`dotnet MyApp.Bootstrap.dll`) no lee
`launchSettings.json`, así que basta con la variable. No le pases ese flag al
dll: lo tomaría como clave de configuración y se tragaría el argumento
siguiente como su valor. El `Dockerfile` no la fija, así que la imagen
arranca en `Production`.

**Qué cambia hoy entre entornos.** Es lo que hace el código, no lo deseado. El
código solo distingue `Development` del resto (`IsDevelopment()`). No hay
`IsStaging()`, `IsProduction()` ni listas de entornos.

| | Development | Staging | Production |
|---|---|---|---|
| Swagger (`/swagger`) | Activo | Apagado (404) | Apagado (404) |
| `UseHttpsRedirection` + `UseHsts` | No | Sí | Sí |
| `Logging:LogLevel:Default` | `Debug` (`appsettings.Development.json`) | `Information` (`appsettings.Staging.json`) | `Information` (`appsettings.json`) |
| Nivel de log efectivo | INFO (ver abajo) | INFO | INFO |
| Aviso si falta `config/connectionstrings.local.json` | WARN | INFO | INFO |
| Origen de credenciales | Igual en todos (ver "Configuración y credenciales") | Igual | Igual |
| CORS (`Cors:AllowedOrigins`) | Igual en todos (vacío) | Igual | Igual |
| Rate limiting (`RateLimiting:*`) | Igual en todos (100 req / 60 s) | Igual | Igual |

**Decisión: Staging se comporta como Production**, salvo lo que configure
`appsettings.Staging.json`. Swagger está apagado en Staging a propósito, y
HTTPS/HSTS se aplican igual que en Production. Hoy `appsettings.Staging.json`
solo fija el nivel de log, con el mismo valor que la base. Existe para que el
lugar donde se ajusta Staging ya esté creado. No lleva credenciales, hosts ni
cadenas de conexión: esas llegan por variables de entorno (ver `config/README.md`).

**Límite del nivel de log.** La raíz de log4net está en `INFO`
(`log4net.config`), y log4net descarta lo que esté por debajo, aunque
`Logging:LogLevel` lo deje pasar. Por eso el `Debug` de
`appsettings.Development.json` no tiene efecto hoy. Mientras la raíz de log4net
siga en `INFO`, el nivel de un `appsettings.{Env}.json` solo puede **subir** el
umbral (por ejemplo a `Warning`), no bajarlo de INFO. Está anotado en
`ROADMAP.md`.

**Agregar un entorno nuevo** (por ejemplo `QA`) no requiere cambiar código.
Basta con crear `src/Bootstrap/appsettings.QA.json` y arrancar con
`ASPNETCORE_ENVIRONMENT=QA`. El Web SDK copia cualquier `appsettings.*.json` al
output y al publish sin tocar el `.csproj`. Como `QA` no es `Development`, se
comporta como Production: sin Swagger y con HTTPS/HSTS. Si un entorno nuevo
necesitara un comportamiento distinto en el pipeline, eso sí sería un cambio de
código en `Program.cs`, y debe pasar por revisión de arquitectura.

`StagingEnvironmentTests` fija el comportamiento de Staging (Swagger en 404,
HSTS, y que se carga `appsettings.Staging.json` y no en Production), y
`SwaggerDevelopmentTests` que Swagger responde en Development.

## HTTPS y el proxy delante del contenedor

La topología de producción esperada es: un load balancer / reverse proxy
delante del contenedor termina TLS y le habla a la app en HTTP plano (por
eso el `Dockerfile` expone `8080` sin certificado — terminar TLS dentro del
propio contenedor sería redundante con el trabajo que ya hace el proxy, y
complica la rotación de certificados sin ganar nada). Bajo esa topología:

- `app.UseForwardedHeaders()` (primero en el pipeline, antes de cualquier
  middleware que lea `Request.Scheme` o la IP del cliente) confía en
  `X-Forwarded-Proto`/`X-Forwarded-For` para que la app sepa que la request
  original sí llegó por HTTPS al proxy, aunque internamente le llegue en HTTP.
- `app.UseHttpsRedirection()` y `app.UseHsts()` corren solo fuera de
  `Development` (en local no hay proxy real terminando TLS, y forzarlo ahí
  solo complica el flujo con certificados autofirmados sin aportar nada).
  Con el header ya confiado, `Request.IsHttps` es `true` y ninguno de los dos
  redirige ni hace falta: el `Strict-Transport-Security` se agrega igual.

**Si en cambio la app se expone directo a internet sin proxy delante**
(sin nadie que agregue `X-Forwarded-Proto`), `Request.Scheme` sigue siendo
`http` de verdad y ahí sí aplica la redirección real de
`UseHttpsRedirection()` — pero eso exige que Kestrel tenga configurado un
endpoint HTTPS con certificado propio, que el `Dockerfile` de la plantilla
no trae (asume el proxy). Si tu despliegue no tiene proxy, agrega ese
endpoint HTTPS antes de confiar en la redirección automática.

> **Seguridad de los headers reenviados:** por defecto,
> `ForwardedHeadersOptions` solo confía en el primer salto
> (`ForwardLimit = 1`) y trae precargados como proxies de confianza el
> loopback (`127.0.0.1/8` y `::1`) — suficiente para probar en local, pero
> **no para producción**. Un despliegue real con el proxy en otra IP debe
> fijar `KnownProxies`/`KnownNetworks` a la IP real de ese load balancer.
> Sin esa restricción, cualquier cliente externo podría enviar
> `X-Forwarded-Proto: https` directamente y hacerle creer a la app que la
> conexión es segura sin que lo sea, saltándose la redirección y el `Hsts`.
>
> **Nota de verificación:** `HstsOptions.ExcludedHosts` excluye
> `localhost`/`127.0.0.1`/`[::1]` por defecto (para no interferir con
> desarrollo local) — al probar el header `Strict-Transport-Security` a
> mano contra `localhost`, no vas a verlo aunque todo esté bien configurado.
> Prueba con otro `Host` (real o simulado) o revisa `Bootstrap.IntegrationTests`.

## CORS

Sin orígenes permitidos por defecto (`Cors:AllowedOrigins` vacío en
`appsettings.json`) es la postura **intencional** del template, no un
olvido: una API pensada para consumirse desde otro backend, o desde el
mismo origen que la sirve, no necesita CORS. Cuando un proyecto real agrega
un frontend en otro origen, se agregan sus URLs a `Cors:AllowedOrigins`
(por entorno, si Staging y Production usan dominios distintos) — nunca se
relaja a `AllowAnyOrigin()` como atajo.

## Rate limiting

Limitador global nativo de ASP.NET Core (`Microsoft.AspNetCore.RateLimiting`,
sin paquete NuGet adicional), particionado por IP del cliente, ventana fija.
Los valores (`RateLimiting:PermitLimit`, `RateLimiting:WindowSeconds` en
`appsettings.json`) son un punto de partida para una API interna; ajústalos
al perfil de tráfico real del proyecto. Al superarse el límite, responde
`429 Too Many Requests`.

## Autenticación y autorización

La plantilla no trae ningún esquema de autenticación — cada proyecto real
trae el suyo (JWT propio, Entra ID, OAuth2 de un proveedor externo...) y no
tiene sentido adivinarlo de antemano. `Program.cs` tiene un bloque de
comentario extenso, en el punto exacto del pipeline (después de CORS y del
limitador de tasa, antes de mapear los endpoints), con un ejemplo comentado
de JWT Bearer que muestra dónde va `AddAuthentication()`/`AddAuthorization()`
en los servicios y `UseAuthentication()`/`UseAuthorization()` en el pipeline.

**Convención, exigible desde ahora aunque todavía no haya ningún esquema
configurado:** todo endpoint nuevo debe declarar explícitamente
`.RequireAuthorization()` o `.AllowAnonymous()` — nunca se deja implícito.
Los dos endpoints de `Sample` (`SampleModule.cs`) ya siguen esta convención
con `.AllowAnonymous()` explícito, como ejemplo del patrón esperado. Sin
ningún esquema de autenticación registrado, `.RequireAuthorization()`
haría fallar cualquier request con 401/403 tan pronto como exista un
`[Authorize]`-equivalente activo — decláralo solo cuando el endpoint de
verdad vaya a necesitar autenticación.

## Versión de .NET y del SDK

La plantilla apunta a **.NET 10** (`net10.0`). El SDK se fija en `global.json`
(`10.0.102`, con `rollForward: latestMinor`: acepta versiones posteriores dentro
de la línea 10.x, pero nunca salta de versión mayor); `ci.yml` lee la versión de
ese archivo (`global-json-file`), así que no se duplica.

Cambiar de versión de .NET **no es una sola línea**. Hay que tocar juntos:
`TargetFramework` en cada `.csproj`, `global.json`, las imágenes base del
`Dockerfile` (`sdk` y `aspnet`), y revisar los paquetes ligados al runtime:
- `Microsoft.AspNetCore.Mvc.Testing` debe ser de la misma versión mayor. La 8.x
  sobre .NET 10 **compila sin avisos y falla en ejecución** (su `TestHost` no
  implementa `PipeWriter.UnflushedBytes`, que exige `System.Text.Json` 10): solo
  los tests de integración lo delatan.
- `Microsoft.Extensions.Logging.Log4Net.AspNetCore` tiene una versión por cada
  versión mayor de .NET; conviene usar la que tiene el grupo del TFM actual.

Comportamientos que cambiaron con el salto y que `Bootstrap.IntegrationTests`
fija: en .NET 10, `Results.ValidationProblem` añade por su cuenta un `traceId`
(el id de la `Activity`, que no coincide con el `X-Correlation-ID`) al Formato A
de error; `Program.cs` lo quita para conservar el contrato descrito arriba.

## Checklist antes de un PR

- [ ] ¿El domain de mi módulo importa algo de `infrastructure` o de otro módulo? → refactor.
- [ ] ¿Mi `Application/` o mi `Infrastructure/` importa algo del proyecto principal de otro módulo, o mi `Domain/`/`Application/` usa el `Contracts` de otro módulo? → refactor. De otro módulo solo se usa su `Contracts`, y solo desde el adaptador de `Infrastructure/` (ver "Contrato entre módulos").
- [ ] ¿Agregué una `ProjectReference` al `.csproj` de un módulo? → solo a `Shared` o a un `*.Contracts`, nunca al proyecto principal de otro módulo. `ArchitectureTests` lo comprueba.
- [ ] ¿Mi carpeta `Application/` tiene 4+ archivos sueltos sin agrupar en slices? → crear subcarpetas.
- [ ] ¿Agregué un puerto nuevo? → vive en `Domain/` (de negocio) o `Shared/Ports/` (técnico), nunca en `Infrastructure/`.
- [ ] ¿El nombre del caso de uso sigue `{Verbo}{Entidad}`?
- [ ] ¿Alguna credencial quedó hardcodeada o en `appsettings.json`? → muévela a `config/connectionstrings.local.json`.
- [ ] ¿Agregué un proyecto nuevo? → añadido a `MyApp.slnx` con `dotnet sln MyApp.slnx add` (el diff debe ser una línea), no editando el archivo a mano.
- [ ] ¿Mi slice responde persistencia=5xx, no encontrado=404 y regla de negocio=400 lanzando excepciones de `Shared/Kernel` (Formato B), y la validación de forma=400 con `.WithCommandValidation<T>()` (Formato A), sin inventar un formato de error propio?
- [ ] ¿Cambié `Bootstrap/` o el contrato de errores? → actualizado `tests/Bootstrap.IntegrationTests` en el mismo PR.
- [ ] ¿Mi endpoint nuevo declara explícitamente `.RequireAuthorization()` o `.AllowAnonymous()`? → nunca lo dejes implícito (ver "Autenticación y autorización").
