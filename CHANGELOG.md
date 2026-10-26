# Changelog

Todos los cambios notables a esta plantilla y a la convención MHVS se
documentan aquí. Formato basado en
[Keep a Changelog](https://keepachangelog.com/es-ES/1.0.0/); versionado
basado en [Semantic Versioning](https://semver.org/lang/es/).

Cuando cambies algo en `ARCHITECTURE.md` o en los skills de
`.opencode/`, agrega la entrada correspondiente aquí en el mismo PR.

## [Sin publicar]

### Agregado
- **Contrato público entre módulos y test de arquitectura** (completa 3.5 de
  la auditoría de pre-lanzamiento en `ROADMAP.md`). Se resolvió como
  documentación y test de arquitectura. **NO existe ningún proyecto `Contracts`
  real**, y no cambian el código de producción, los `.csproj` ni el
  `Dockerfile`.
  - **`ARCHITECTURE.md` → "Contrato entre módulos"** (sección nueva):
    - Un módulo solo referencia `Shared` y proyectos `{Modulo}.Contracts`.
    - El `Contracts` va en `src/Modules/{Modulo}.Contracts/`, una carpeta
      hermana del módulo, no dentro de él, por el globbing del SDK. Tiene solo
      interfaces y DTOs `record`. Puede referenciar `Shared`, pero nada de
      `src/Modules`.
    - El módulo que expone la interfaz la registra en `Add{Modulo}Module()`.
      El consumidor tiene su propio puerto en `Domain/` y su adaptador en
      `Infrastructure/`.
    - Las consultas van por esa interfaz. Un cambio de estado en otro módulo
      no es atómico y se decide con el humano. No se adoptan eventos.
    - Explica qué impone el compilador: los tipos son `public`; la frontera es
      la `ProjectReference`.
    - Incluye un ejemplo ilustrativo, no compilado.
  - **`ARCHITECTURE.md`, otros cambios:** "Nivel 1" remite a esa sección en
    lugar del "contrato público (interfaz/evento)". "Cuándo algo es
    compartido" aclara que un contrato de negocio no va en `Shared/Ports`. El
    checklist suma dos ítems: imports de `Application/` e `Infrastructure/`, y
    `ProjectReference`.
  - **`tests/Bootstrap.IntegrationTests/ArchitectureTests.cs`** (7 tests, sin
    paquetes nuevos):
    - Comprueba las reglas A (módulo), B (`Shared`) y C (`Contracts`), cada
      una en los `.csproj` y en los ensamblados (`GetReferencedAssemblies()`).
    - Un caso impide que pasen sin haber encontrado módulos.
    - Mutaciones verificadas y revertidas: ver la actualización de
      `docs/contrato-entre-modulos` en `ROADMAP.md`.
  - **Skills:**
    - `architecture-review`: permite `*.Contracts`, aclara qué capa puede usar
      qué de otro módulo y cita el test.
    - `hu-to-slice-scaffold`: sigue deteniéndose y preguntando, pero ahora
      propone el mecanismo y ya no sugiere `Shared/Ports`.
    - `hu-validation` no cambia: detectar HU que tocan varios módulos queda
      abierto en `ROADMAP.md`.
- **Observabilidad documentada y OpenTelemetry como punto de extensión**
  (7.2 de la auditoría de seguimiento en `ROADMAP.md`). Se resolvió como
  documentación y punto de extensión: **NO hay OpenTelemetry implementado**,
  ni paquetes nuevos, y no cambia el comportamiento.
  - **`ARCHITECTURE.md` → "Observabilidad":**
    - Qué hay hoy: el correlation ID, sus límites y dónde aparece. El `traceId`
      del cuerpo solo existe en el Formato B, y un 404 de ruta inexistente sale
      sin cuerpo.
    - Qué no hay: trazas distribuidas, métricas, exportadores y propagación a
      llamadas salientes.
    - `X-Correlation-ID` y `traceparent` son independientes hoy, con lo
      demostrado y nada más.
    - El punto de extensión: OpenTelemetry se registra como servicio junto a
      `AddHealthChecks()`, sin `app.Use`.
    - La convención, la decisión abierta de cómo enlazar los dos
      identificadores, y la regla de reenviar el correlation ID en todo
      `HttpClient` futuro.
  - **`ARCHITECTURE.md` → "Logging":** se corrigió la frase sobre el patrón de
    log4net. Lo que llega es `LogicalThreadContext["CorrelationId"]`, no el
    scope de `ILogger`.
  - **`Program.cs`:** solo un comentario que marca el punto de inserción.
  - **Tests.** `TraceparentCorrelationTests` agrega 9.
    - Con `traceparent` válido o basura, la solicitud no falla y el
      `X-Correlation-ID` se genera o se devuelve el del cliente, también en
      un 404.
    - El correlation ID generado no es el trace-id del `traceparent`.
    - Mutaciones verificadas: si no se escribe el header cuando llega
      `traceparent`, fallan 8 tests; si se usa `Activity.Current.TraceId` como
      respaldo, falla el de independencia.
- **Entornos Development, Staging y Production documentados** (resuelve 7.8
  de la auditoría de seguimiento en `ROADMAP.md`). No cambia el
  comportamiento: el código ya trataba Staging como Production.
  - **`ARCHITECTURE.md` → "Entornos":**
    - Cómo se elige el entorno (`ASPNETCORE_ENVIRONMENT`) y que, sin
      definirla, el framework usa `Production`.
    - La tabla real de qué cambia por entorno.
    - La decisión: Staging se comporta como Production (Swagger apagado,
      HTTPS/HSTS activos), salvo lo que configure `appsettings.Staging.json`.
    - Un entorno nuevo (por ejemplo QA) se agrega con su
      `appsettings.{Env}.json`, sin cambiar código.
    - El límite del nivel de log: con la raíz de log4net en `INFO`, un
      `appsettings.{Env}.json` solo puede subir el umbral.
  - **`src/Bootstrap/appsettings.Staging.json`:** solo
    `Logging:LogLevel:Default = Information`. Sin credenciales, hosts ni
    cadenas de conexión.
  - **`config/README.md`:** enlace a "Entornos" y dónde encaja
    `appsettings.{Env}.json` en la precedencia.
  - **Tests.** 6 nuevos, con una factory propia (`StagingBootstrapFactory`).
    - `StagingEnvironmentTests`: en Staging, `/swagger` responde 404, hay
      HSTS y se carga `appsettings.Staging.json`. En Production no se carga.
      El de Swagger falla si Swagger se condiciona con `!IsProduction()`.
    - `SwaggerDevelopmentTests`: `BootstrapFactory` corre en Development, y
      ahí `/swagger` responde 200.
- **Secretos en producción: documentación, punto de extensión y test**
  (resuelve 7.1 de la auditoría de seguimiento en `ROADMAP.md`). Se resolvió
  como documentación y punto de extensión: **no hay integración real con
  Key Vault**, ni paquetes nuevos.
  - **`config/README.md`:**
    - La regla `:` → `__` para las variables de entorno.
    - Una tabla de las tres claves del `.example`, con una columna "¿Lo
      consume el código hoy?": solo `ConnectionStrings:MysqlTodo`; `Default`
      y `ExternalServices:SomeApiKey` no.
    - Ejemplos en bash y PowerShell, con placeholders.
    - La precedencia real de los proveedores de configuración, de menor a
      mayor, con la advertencia de que los argumentos de línea de comandos
      pierden contra el archivo local y contra las variables de entorno.
    - La frase que presentaba Key Vault como si ya funcionara ahora dice que
      no hay integración y que es un punto de extensión.
    - Las tres rutas reales donde se busca el archivo local, incluida
      `src/config/`.
  - **`ARCHITECTURE.md` → "Configuración y credenciales":** el punto de
    extensión. Un gestor de secretos se conecta en `Program.cs`, junto a
    `AddEnvironmentVariables()`, igual que el de autenticación.
  - **`Program.cs`:** solo el comentario del bloque de configuración, que no
    mencionaba los argumentos ni que `CreateBuilder` ya lee las variables
    de entorno.
  - **Tests.** `ProductionSecretsConfigurationTests` agrega 2, con una
    factory propia (`EnvironmentConfigBootstrapFactory`). La factory usa un
    content root temporal fuera del repo, con un archivo local falso y
    variables de entorno de prueba que restaura al terminar.
    - El primer test fija que `ConnectionStrings__X` llega como
      `ConnectionStrings:X`. Falla si se usa `_`.
    - El segundo fija que la variable gana sobre el archivo local. Falla si
      se quita `AddEnvironmentVariables()`, porque entonces gana el archivo.
- **`GET /version`** (resuelve 7.9 de la auditoría de seguimiento en
  `ROADMAP.md`, salvo en Docker). Expone la versión y el commit de git
  que el SDK ya embebía en `AssemblyInformationalVersionAttribute` y que
  nada mostraba. Responde `version`, `commit` (12 caracteres),
  `commitFull`, `buildConfiguration` y `dotnetVersion`.
  - **Dónde vive:** en `Program.cs` (`Bootstrap`), porque es información
    del host y no de un módulo. Se declara `.AllowAnonymous()` y tiene
    comentado el punto donde se restringiría.
  - **De dónde lee:** el parseo está en `Bootstrap/Diagnostics/BuildInfo.cs`
    y lee de `typeof(Program).Assembly`, no de `GetEntryAssembly()`.
    Comprobado: con `GetEntryAssembly()`, bajo `WebApplicationFactory`, el
    test falla con `commitFull` en `null`, porque el ensamblado de entrada
    es el host de pruebas.
  - **Sin información de git:** responde `commit: null`, nunca un 500.
  - **Tests:** 2 nuevos en `VersionEndpointTests`. Uno de integración (200,
    las 5 claves, commit no nulo e igual al del ensamblado de la app) y uno
    del caso sin atributo, con un ensamblado dinámico.
  - **Verificado con `dotnet run`:** `commitFull` coincidió con
    `git rev-parse HEAD`.
  - **Limitación conocida:** en una imagen Docker, `commit` sale `null`
    porque `.dockerignore` excluye `.git/`. La corrección
    (`ARG GIT_COMMIT` + `-p:SourceRevisionId=$GIT_COMMIT` en el
    `publish`) se verificó de forma aislada, en una carpeta sin `.git`,
    publicando y consultando `/version`. El `Dockerfile` no se modificó,
    porque hoy no hay trabajo activo con Docker. Los detalles están en
    `ROADMAP.md` → 7.9.
- **Módulo `Tasks`** (`src/Modules/Tasks`, HU-TASKS-001, resuelve 7.5 de la
  auditoría de seguimiento en `ROADMAP.md`): portado desde `mhvs-todo-app`
  como segundo ejemplo de referencia, junto a `Sample` — el mismo patrón
  MHVS, pero con persistencia **real** contra MySQL vía ADO.NET puro
  (`MySqlConnector`, sin ORM), en vez de un repositorio en memoria.
  Coexiste con `Sample`, no lo reemplaza ni lo toca. Incluye:
  - `Domain/`: `TaskItem` (con `Rehydrate` para reconstruir desde el
    adaptador sin volver a aplicar las reglas de creación),
    `TaskItemStatus`, `TaskItemErrors`, el puerto `TaskRepository`.
  - `Application/CreateTask/`: comando, handler (usa `IClock` inyectado,
    nunca `DateTime.UtcNow` a mano), resultado, validador — HU-TASKS-001
    completa, con sus 10 reglas de negocio.
  - `Infrastructure/MySqlTaskRepository.cs`: SQL siempre parametrizado,
    una conexión por operación, columnas `DATETIME(6)` con conversión UTC
    explícita de ida y vuelta, y la cadena de conexión
    (`ConnectionStrings:MysqlTodo`) se lee en cada operación, no en el
    constructor — la app arranca aunque falte, el error aparece al usarla.
  - `Infrastructure/schema.sql`: solo documentación; la app nunca lo
    ejecuta, la tabla se crea a mano contra la instancia real.
  - `tests/Modules/Tasks/`: 41 tests (dominio, handler con
    `FakeTaskRepository`, validador), ninguno toca MySQL real.
  - `tests/Bootstrap.IntegrationTests/TasksEndpointsTests.cs`: 2 tests,
    solo con entradas inválidas a propósito — un caso válido escribiría de
    verdad en la BD configurada en una máquina de desarrollo.
  - `POST /tasks` registrado en `Program.cs` (`AddTasksModule`/
    `MapTasksModule`, junto a `Sample`), `Tasks.csproj` agregado a
    `MyApp.slnx` con `dotnet sln add --in-root` (mismo nivel que `Sample`),
    `ProjectReference` en `Bootstrap.csproj`, `COPY` en el `Dockerfile`, y
    la clave `ConnectionStrings:MysqlTodo` en
    `config/connectionstrings.local.json.example` (con `CAMBIA_ESTO`).
  - `ARCHITECTURE.md` → "Dos ejemplos de referencia: `Sample` vs. `Tasks`":
    cuándo mirar cada uno. `README.md`: mención del endpoint, con la
    aclaración de que requiere `ConnectionStrings:MysqlTodo` pero el resto
    de la app arranca sin eso — verificado arrancando en frío sin esa
    configuración. El skill `hu-to-slice-scaffold` ahora indica que, ante
    un pedido explícito de persistencia real, el patrón a seguir es
    `Tasks/MySqlTaskRepository`, no inventar uno desde cero.
- **`Directory.Build.targets`** (resuelve 1.2 de la auditoría de
  pre-lanzamiento en `ROADMAP.md`): activa `git config core.hooksPath
  .githooks` automáticamente en el primer `dotnet build` local de
  cualquier proyecto de la solución, en vez de depender de un paso manual
  del README que nadie estaba obligado a recordar. No hace nada en CI
  (`CI`/`GITHUB_ACTIONS` en `true`), sin repo git, ni si `core.hooksPath`
  ya tenía cualquier otro valor configurado a propósito (nunca lo
  sobreescribe). Idempotente entre los 5 proyectos de la solución: solo el
  primero en construirse imprime el mensaje de confirmación. Verificado en
  4 escenarios (vacío, ya configurado con otro valor, `CI=true`,
  `GITHUB_ACTIONS=true`, sin `.git`).
- **`CODEOWNERS` completo** (resuelve el hueco de cobertura de 4.1): 7
  rutas nuevas — `/src/Shared/`, `/src/Bootstrap/`, `/README.md`,
  `/ROADMAP.md`, `/Dockerfile`, `/docker-compose.yml`, `/.githooks/` —,
  cada una verificada contra el árbol real antes de agregarla, con un
  comentario explicando por qué esa ruta merece protección.
- **Dependabot** (`.github/dependabot.yml`, resuelve 7.6 de la auditoría de
  seguimiento en `ROADMAP.md`): dos ecosistemas, `nuget` y `github-actions`,
  ambos apuntando a `/` (cubre los `.csproj` de todo el repo y las actions de
  `ci.yml`), frecuencia semanal.
- **Escaneo de vulnerabilidades en cada PR** (resuelve el resto de 7.6): paso
  nuevo en `ci.yml` ("Escaneo de vulnerabilidades en dependencias NuGet") que
  corre `dotnet list package --vulnerable --include-transitive` sobre
  `MyApp.slnx`. El comando no falla por sí solo con vulnerabilidades
  encontradas (solo imprime texto); el paso captura la salida y falla si
  contiene la frase `has the following vulnerable packages`. Verificado en
  ambos sentidos: pasa hoy (cero vulnerabilidades) y falla de verdad al
  inyectar temporalmente `System.Net.Http` 4.3.0 (GHSA-7jgj-8wvc-jh57, alta),
  quitada después de confirmar — no queda en este commit.
- **HTTPS/HSTS, CORS y rate limiting** (resuelve 7.3 de la auditoría de
  seguimiento en `ROADMAP.md`):
  - `app.UseForwardedHeaders()` (primero en el pipeline), configurado para
    confiar en `X-Forwarded-For`/`X-Forwarded-Proto` — la topología esperada
    en producción es un proxy delante del contenedor que termina TLS y le
    habla a la app en HTTP plano (ver `Dockerfile`/`ARCHITECTURE.md` →
    "HTTPS y el proxy delante del contenedor").
  - `app.UseHttpsRedirection()`/`app.UseHsts()`, condicionados a
    `!IsDevelopment()`. En Development no se fuerzan (sin proxy real
    terminando TLS, complicaría el flujo con certificados sin aportar nada).
  - Política CORS nombrada `"Default"`, sin orígenes permitidos por defecto
    (`Cors:AllowedOrigins` vacío en `appsettings.json`) — postura intencional,
    documentada en `ARCHITECTURE.md` → "CORS", no un olvido.
  - Rate limiting nativo de ASP.NET Core (`Microsoft.AspNetCore.RateLimiting`,
    sin paquete NuGet), un limitador global particionado por IP, ventana fija,
    configurable vía `RateLimiting:PermitLimit`/`RateLimiting:WindowSeconds`
    en `appsettings.json` (100 req/60s por defecto). Los valores se leen
    dentro del delegado de partición, no en una variable capturada antes de
    `builder.Build()`: una variable capturada antes se queda con el valor de
    ese momento para siempre e ignora cualquier fuente de configuración
    añadida después (por ejemplo, la que inyecta
    `WebApplicationFactory<Program>` en tests).
- **Punto de extensión de autenticación/autorización** (resuelve 7.4):
  - Bloque de comentario extenso en `Program.cs`, en el punto exacto del
    pipeline (después de CORS y del rate limiter, antes de mapear los
    endpoints), con un ejemplo comentado de JWT Bearer.
  - `SampleModule.cs`: los dos endpoints de `Sample` ahora declaran
    `.AllowAnonymous()` explícito (antes eran anónimos por ausencia de
    declaración, no por decisión), como ejemplo del patrón esperado.
  - Convención nueva en `ARCHITECTURE.md` → "Autenticación y autorización":
    todo endpoint nuevo declara `.RequireAuthorization()` o
    `.AllowAnonymous()`, nunca implícito. El skill `architecture-review` la
    exige en su checklist y rechaza un endpoint que no declare ninguno.
- `tests/Bootstrap.IntegrationTests`: `HttpsHstsTests` (con
  `ProductionBootstrapFactory`, entorno `Production`: en Development
  `Program.cs` no registra `UseHttpsRedirection`/`UseHsts`), `CorsPolicyTests`
  (inspecciona el servicio `IOptions<CorsOptions>` registrado, sin simular un
  request cross-origin real) y `RateLimitingTests` (con
  `RateLimitedBootstrapFactory`, límite bajado a 3 solo para el test: agota el
  límite y confirma 429 en la request siguiente). `AssemblyInfo.cs` desactiva
  el paralelismo del proyecto (`DisableTestParallelization`): varias apps
  reales en memoria a la vez, cada una abriendo el mismo `logs/app.log`
  físico (la ruta relativa del appender resuelve siempre al mismo directorio
  de salida del ensamblado, sin importar `ContentRoot` ni el directorio de
  trabajo del proceso), necesitan estar serializadas.
- `tests/Bootstrap.IntegrationTests` (xUnit + `Microsoft.AspNetCore.Mvc.Testing`,
  `WebApplicationFactory<Program>`): levanta la app real en memoria, sin mocks
  ni BD, y verifica que arranca sin logs de nivel Error (incluidos los mensajes
  internos de log4net, que no pasan por `ILogger`), `GET /health`, el
  `X-Correlation-ID` en éxito y en cada tipo de error, el Formato A de error
  (filtro de validación, sin `traceId`), el Formato B (`GlobalExceptionHandler`,
  con `traceId`; cuerpo mal formado sin exponer el mensaje del framework) y que
  los 4xx no generen logs de nivel Error. Resuelve la deuda técnica "sin tests
  de integración para `Bootstrap`" que estaba en `ROADMAP.md` (movida aquí).
  `SampleEndpointsTests` depende del módulo `Sample`; `BootstrapTests` no.
- `ci.yml`: el paso de tests se separa en "Test (unitarios)" (la solución,
  excluyendo `MyApp.Tests.Bootstrap`, de modo que los tests de un módulo nuevo
  entren solos) y "Test (integración de Bootstrap)".
- `public partial class Program` al final de `Program.cs`, para que
  `WebApplicationFactory<Program>` pueda referenciar el tipo generado por las
  top-level statements. No agrega comportamiento.
- `docs/templates/HU-TEMPLATE.md` y `scripts/lint_hu.py` (validación
  estructural de historias de usuario, sin IA), portados de
  `mhvs-demo-tickets`. El skill `hu-validation` ya los referenciaba pero no
  existían en la plantilla.
- Paso "Lint de historias de usuario" en `.github/workflows/ci.yml`: en cada
  PR corre `scripts/lint_hu.py` sobre los `HU-*.md` que cambiaron. Calcula los
  archivos con `git diff` (checkout con `fetch-depth: 0`) en vez de una action
  de terceros; es un paso del workflow existente, no un workflow nuevo.
- `AGENTS.md`: índice para agentes que apunta a los tres skills de
  `.opencode/skills/` (resuelve la referencia de `CODEOWNERS` a un archivo que
  no existía).
- `Bootstrap/Configuration/LocalConfigLocator`, y un mensaje de log al
  arrancar que indica si `config/connectionstrings.local.json` se cargó (con la
  ruta) o no se encontró (aviso `Warning` en `Development`, `Information` en
  otros entornos, donde lo normal son variables de entorno).
- Registro de la auditoría de pre-lanzamiento (2026-09-21) al inicio de
  `ROADMAP.md`, para resolver sus hallazgos en PRs separados.
- `CODEOWNERS`: usernames individuales en vez de placeholders de equipo
  (cuenta personal, no organización).
- `ROADMAP.md`: deuda técnica conocida y mejoras futuras, separado del
  historial de cambios.

### Corregido
- **`CODEOWNERS` cubre los archivos que controlan las protecciones del repo**
  (amplía 4.1 de la auditoría de pre-lanzamiento en `ROADMAP.md`). Solo
  configuración de revisión y texto: no cambia comportamiento.
  - Rutas nuevas: `/.gitignore`, junto a `/config/`, porque es lo que impide
    commitear `config/connectionstrings.local.json`; `/.dockerignore`, con
    Docker; `/Directory.Build.targets`, `/global.json` y `/MyApp.slnx`, como
    build y SDK; y `/CODEOWNERS`, que se había omitido al declarar completa la
    cobertura de 4.1. Pasa de 15 a 21 rutas, todas de `@estebanSCO`.
  - Los comentarios de `CODEOWNERS` ya no prometen una aprobación
    obligatoria ni que `CONTRIBUTING.md` "exige" 2 aprobaciones. Con un solo
    dueño y sin protección de rama, es un recordatorio visual.
  - `CONTRIBUTING.md` y `AGENTS.md` remiten al propio `CODEOWNERS` como fuente
    de la lista, en lugar de enumerar rutas.
  - **`ROADMAP.md`, ítem nuevo de severidad media en "Docker, diferido a
    largo plazo":** `.dockerignore` no excluye los `*.local.json` anidados
    (`src/Bootstrap/config/`, `src/config/`). Está deducido de las reglas de
    patrones de Docker, no verificado con un build. La corrección esperada es
    `**/*.local.json`. `.dockerignore` no se tocó.
- **`ROADMAP.md`: frase devuelta a su párrafo.** El PR de
  `docs/limpieza-documentacion` insertó su actualización a mitad de un
  párrafo. La última frase de la actualización de `docs/observabilidad` ("La
  actualización anterior, que dice…") quedó al final de la viñeta "No entran
  en este PR" de la nueva. Volvió a su sitio.
- **Limpieza de documentación acumulada** (#8; avanza 2.9 y 2.10 de la
  auditoría de pre-lanzamiento en `ROADMAP.md`). Solo texto y comentarios: no
  cambia comportamiento.
  - **Casing:** `ARCHITECTURE.md` usa `Application/`, `Domain/` e
    `Infrastructure/`, igual que las carpetas reales.
  - **`CONTRIBUTING.md`:**
    - Los tipos de rama son los de Conventional Commits (`feat/`, `fix/`,
      `docs/`, `refactor/`, `test/`, `chore/`, `perf/`). Se quitó `hotfix/`:
      sin ramas de release, una corrección urgente es un `fix/`. Se aclara que
      nada valida el nombre de la rama.
    - El checklist de PR ya no es una copia parcial: enlaza a
      `ARCHITECTURE.md` → "Checklist antes de un PR", la única fuente.
    - Se documenta el límite real de las aprobaciones con un solo
      colaborador: no se cumplen ni se pueden exigir, y tampoco que CI esté
      en verde para mergear.
    - Hoy no hay tests de infraestructura contra MySQL ni un paso de CI para
      ellos, y `dotnet format` no está impuesto (no hay `.editorconfig` ni
      paso de CI).
  - **`AGENTS.md`:** agrega `perf` a los tipos de commit.
  - **Referencias colgantes (2.9):**
    - `CODEOWNERS` (solo el comentario) cita `CONTRIBUTING.md` en lugar de
      `checklist-release-template.md`.
    - `opencode-prompt-mhvs.md` queda como referencia histórica y
      `HU-SAMPLE-002` como ID ilustrativo en `hu-to-slice-scaffold`.
  - **Comentarios:** `Program.cs` ya no menciona "Auth/Courses/Products", y
    `Directory.Build.targets` lista los 7 proyectos reales.
  - **`ROADMAP.md`:**
    - La deuda de `IClock`, `ConnectionStrings` y `MemberwiseClone` y la
      mejora "Health checks y Swagger" quedan como resueltas por trabajo
      previo. 2.2 anota que el 404 de ruta inexistente ya está documentado.
    - Se corrigieron los residuos de referencias muertas (números de línea) y
      de `MyApp.slnx`.
    - Ítem bajo nuevo: versiones de paquetes de test distintas entre
      proyectos.
- **Consistencia del módulo `Sample`** (resuelve 2.6 y 2.7 de la auditoría
  de pre-lanzamiento en `ROADMAP.md`).
  - **2.7, `SampleItem.Create`:** `Create` no recortaba espacios ni
    limitaba el nombre a 100 caracteres, aunque `Rename` sí lo hacía.
    Ahora los dos usan el mismo método privado `NormalizeName`, con los
    mensajes que ya usaba `Rename`. `Rename` no cambia de comportamiento.
  - **2.6, `PingSampleHandler`:** `AddAsync` no tenía `try/catch`, a
    diferencia de `RenameSampleItemHandler`. Ahora sigue el mismo patrón:
    registra la excepción original con el id del item y lanza
    `PersistenceException("El item no fue creado.")`, que se traduce a 500.
  - **`SampleItem.Rename`:** se agrega encima de `MemberwiseClone()` el
    comentario de advertencia de copia superficial que exige el skill
    `hu-to-slice-scaffold`, con su texto literal. Es solo un comentario,
    sin cambio de lógica, y es la única llamada a `MemberwiseClone` en `src/`.
  - **Tests:** 7 nuevos en `Sample.Tests`.
    - `Create` con null, `""`, `"   "`, recorte, 100 caracteres (válido) y
      101 (inválido).
    - Fallo de `AddAsync` → `PersistenceException`, que no deriva de
      `DomainException`, sin nada almacenado.
    - `FakeSampleItemRepository` gana `FailOnAdd` y `Count`.
    - Contra el código anterior fallan 3: recorte, 101 caracteres y
      `PersistenceException`. Los otros 4 fijan comportamiento que ya
      existía.
- **Inyección en logs y `X-Correlation-ID` sin límite** (resuelve 4.4 de la
  auditoría de pre-lanzamiento en `ROADMAP.md`).
  - **Saltos de línea en el log.** Un valor de entrada con `\n` podía
    falsificar líneas completas en `logs/app.log`, por ejemplo un
    `ERROR [admin] Security - login ok` que nunca ocurrió. Ahora los dos
    layouts de `log4net.config` usan `%safemessage`
    (`Bootstrap/Logging/SafeMessagePatternConverter.cs`), que escapa `\r`,
    `\n` y los separadores de línea Unicode (NEL, LS, PS) del mensaje. Es un
    solo punto que cubre todos los logs, de módulos y framework, porque
    log4net es el único proveedor. Ningún módulo sanea a mano.
  - **Límite conocido:** `%exception` sigue sin sanear, porque el stack
    trace es multilínea a propósito.
  - **`X-Correlation-ID`.** `CorrelationIdMiddleware` ya no refleja cualquier
    valor del cliente. Solo reutiliza un único valor de hasta 128 caracteres
    con `[A-Za-z0-9-]`. Si no cumple, genera uno nuevo y nunca rechaza la
    request.
  - **Tests.** `LogInjectionTests` agrega 2. Uno verifica contra el archivo
    de log real que no aparece ninguna línea falsificada y que el texto
    inyectado queda en la misma línea, con un `\n` literal. El otro verifica
    que un header de 5000 caracteres se reemplaza por uno generado. Con las
    correcciones revertidas temporalmente, los dos fallan.
- **Los tres skills originales de `.opencode/skills/`** (resuelve
  3.1–3.9 de la auditoría de pre-lanzamiento en `ROADMAP.md`; 3.5 solo
  parcialmente). Solo cambia el texto de los `SKILL.md`, no el código.
  - **`hu-validation`:**
    - **Cobertura uno a uno, no por conteo (3.1).** Contar reglas y CA
      ya no sirve como verificación. Ahora cada regla se clasifica como
      "violable por el usuario" o "invariante del sistema", con un
      criterio escrito, y la respuesta siempre incluye una tabla regla →
      clasificación → CA.
    - **Cruce entre secciones (3.2).** Compara Datos de entrada, Reglas,
      Flujo principal y Errores (y los valores de los CA) para detectar
      límites o catálogos contradictorios, errores sin regla y reglas
      ocultas en el Flujo. El Alcance también se cruza contra el Flujo,
      no solo contra los CA.
    - **Qué puede decir un CA (3.3).** Un CA puede mencionar el código
      HTTP, que es contrato observable. Lo que no puede mencionar son
      detalles internos (clases, tablas, middleware). Con eso queda
      alineado con `ARCHITECTURE.md` → "Precedencia".
    - **Documentos externos (3.4).** Criterio explícito: un documento
      externo solo bloquea si la HU le delega contenido necesario (una
      regla, un catálogo, un valor de CA, o cuál de dos secciones
      contradictorias prevalece).
  - **`hu-to-slice-scaffold`:**
    - **Varios módulos (3.5).** Si una HU toca 2 o más módulos, el skill
      se detiene y pregunta cuál es el dueño. Nunca elige en silencio.
    - **Módulo nuevo (3.6).** Checklist exhaustivo: `.csproj`,
      `dotnet sln add --in-root`, `ProjectReference` en
      `Bootstrap.csproj`, `COPY` en el `Dockerfile`, `Program.cs`,
      `{Modulo}Module.cs` y el proyecto de tests. Se cita el bug real de
      `Tickets`.
    - **Contrato de errores (3.7).** Tabla completa, con el 409 incluido.
      Patrón: una `ConflictException` en `Shared/Kernel`, que la primera
      HU que la necesite crea junto con su caso en
      `GlobalExceptionHandler` (antes de `DomainException`) y su test de
      integración. El `catch` de persistencia tiene que dejarla pasar.
      401/403 quedan fuera explícitamente hasta que exista autenticación.
    - **`IClock` y `MemberwiseClone` (3.8).** Los timestamps van por
      `IClock`, con `Tasks` como referencia. Todo `MemberwiseClone()`
      generado lleva un comentario obligatorio que advierte que es una
      copia superficial.
    - **Tests de integración.** Se menciona
      `tests/Bootstrap.IntegrationTests/` para los endpoints nuevos.
  - **`architecture-review` (3.9):** ítems nuevos para:
    - `ProjectReference` entre módulos (no solo `using`);
    - `DateTime.UtcNow` en vez de `IClock`;
    - el comentario de copia superficial en `MemberwiseClone`;
    - la composición completa de un módulo nuevo (`Bootstrap.csproj`,
      `Dockerfile`, `Program.cs`, tests);
    - la precedencia validación/404 y el 409;
    - un test de integración para los endpoints nuevos;
    - la plomería de runtime de `Bootstrap`: logging no duplicado y el
      orden real del pipeline de `Program.cs`.
  - **Cómo se verificó.** No se hizo solo revisión de texto. Hubo 4
    rondas de pruebas ciegas con subagentes aislados, cada uno con solo
    el `SKILL.md` y una HU desechable (HU-NOTES-001/002/003, fuera del
    repo), sin saber que era una prueba. En las rondas 3 y 4 se comparó
    contra la versión anterior del skill (`main`), con las mismas HU.
    Resultado, contado sin inflarlo (detalle en `ROADMAP.md`):
    - En 3.1 y 3.2, la versión anterior llegó a las mismas conclusiones.
      Lo que se gana es un procedimiento explícito y auditable.
    - En 3.3 sí hay un efecto demostrado: la versión anterior penalizaba
      "responde 400/404" de forma inconsistente, y la corregida no.
    - En 3.4, el caso informativo fue consistente en las 5 corridas; el
      caso bloqueante no se probó.
    - Las pruebas destaparon dos pendientes nuevos, anotados en
      `ROADMAP.md`: el criterio inconsistente ante una autorización
      implícita, y la falta de un ejemplo de contrato entre módulos.
- `log4net.config`: el `RollingFileAppender` usaba el `LockingModel` por
  defecto (`ExclusiveLock`). Se detectó al agregar
  `Bootstrap.IntegrationTests` con más de una app real en memoria a la vez
  (`ProductionBootstrapFactory`, `RateLimitedBootstrapFactory`): la ruta
  relativa del appender resuelve siempre al mismo `logs/app.log` físico sin
  importar `ContentRoot` ni el directorio de trabajo del proceso, así que la
  segunda instancia fallaba al arrancar con `log4net:ERROR ... Unable to
  acquire lock`. Ahora usa `MinimalLock` (adquiere/libera el lock en cada
  escritura), que permite que más de una instancia escriba el mismo archivo.
  No hace falta en un contenedor normal (una sola instancia, un archivo),
  pero deja de romperse si alguna vez hay más de una.
- log4net se configuraba dos veces al arrancar (`XmlConfigurator.Configure` y
  `AddLog4Net`), lo que imprimía `log4net:ERROR ... Unable to acquire lock on
  file logs/app.log` en cada inicio. Queda solo `AddLog4Net`.
- Cada respuesta 4xx generaba además un log `ERROR` con stack trace desde
  `ExceptionHandlerMiddleware` de ASP.NET, aunque `GlobalExceptionHandler` ya
  la manejaba y registraba como `Warning`; una alerta por nivel ERROR se
  disparaba con un simple 404. Se silencia solo esa categoría
  (`"Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware": "None"` en
  `appsettings.json`). Efecto secundario: si el propio manejador global fallara,
  el mensaje del framework sobre ese fallo también quedaría silenciado.
- `X-Correlation-ID` se perdía en las respuestas de error (400 por cuerpo mal
  formado o ausente, 404), porque `ExceptionHandlerMiddleware` limpia los
  headers antes de invocar al manejador. Ahora se escribe al iniciar la
  respuesta (`OnStarting`) y va en todas.
- `config/connectionstrings.local.json` nunca se leía: `AddJsonFile` con ruta
  relativa se resolvía contra el content root (`src/Bootstrap/`), no contra
  la raíz del repo donde el README indica crearlo, y con `optional: true` no
  había ningún aviso. Ahora se busca desde el content root hasta la raíz del
  repo (2 niveles) y se conserva la recarga en caliente (`reloadOnChange`).

### Cambiado
- **Primeros pasos ordenados y hallazgos del clone limpio documentados**
  (validación con clone limpio, Fase B, confirmada sobre `b35e922`; ver
  `ROADMAP.md`). Solo texto: no cambian el código ni la configuración.
  - **`README.md` → "Primeros pasos al clonar":** requisitos previos (SDK según
    `global.json`, MySQL solo para `Tasks`, Python solo para `lint_hu.py`,
    certificado de desarrollo HTTPS, sin verificar en una máquina sin
    certificado). Después: clonar, `dotnet build`, `dotnet test` sin
    configuración local, `dotnet run` y qué comprobar. La configuración local
    para `Tasks` va al final, para confirmar primero que todo funciona sin
    secretos. También documenta qué responde `Tasks` sin cadena de conexión:
    500 "Persistence failure", la causa en el log y la app sigue en pie.
  - **`config/README.md`:** la recarga en caliente aplica solo si el archivo
    existía al arrancar. Si se crea con la app corriendo, hay que reiniciar.
  - **`ARCHITECTURE.md` → "Logging":** `logs/app.log` es relativo a la carpeta
    de los binarios de la app (con `dotnet run`,
    `src/Bootstrap/bin/Debug/net10.0/logs/app.log`), no a la raíz del repo.
  - **`ARCHITECTURE.md` → "Endpoint `/version`":** `version` es la del
    ensamblado de la aplicación, no la de la plantilla, cuya versión es su tag
    de Git. Hoy responde `1.0.0` (ítem nuevo de severidad baja en `ROADMAP.md`).
- **`.gitignore`** (resuelve 4.5): agrega `.env`, `.env.*`, `*.pfx`,
  `*.pem`, `*.key`, `*.p12`, `*.pubxml`, `*.tfvars`, `*.mdf`, `*.sqlite`,
  `.vscode/`, `TestResults/` y `*.bak`. Los dos patrones de 4.5 que dependen
  de Docker (`docker-compose.override.yml` y `.env` en `.dockerignore`)
  pasaron al ítem "Docker, diferido a largo plazo".
- **`ARCHITECTURE.md` → "Neutralidad de motor de base de datos"**: el template
  no prescribe motor. `Sample` y `Tasks` son ejemplos del patrón
  puerto/adaptador, no una recomendación de tecnología. Una nota aclara que el
  contenedor de SQL Server del `docker-compose.yml` actual es un residuo que
  ningún módulo usa.
- **`ROADMAP.md` → "Docker, diferido a largo plazo"**: un solo ítem, por
  decisión explícita, que reúne todo lo pendiente de Docker que estaba
  disperso (4.2, lo de Docker de 1.5, 4.3 y 4.5, el `Dockerfile` de 7.9 y el
  `docker build` real que nunca se ejecutó). `Dockerfile` y
  `docker-compose.yml` no se tocaron.
- **README.md/CONTRIBUTING.md**: el paso "Activar los git hooks del repo"
  de "Primeros pasos al clonar" ya no es manual; se documenta que el
  primer build lo activa solo. `CONTRIBUTING.md` explica ahora
  explícitamente cuál protección es técnica de verdad (el hook de
  pre-push, saltable a propósito) y cuál es solo un recordatorio visual
  (`CODEOWNERS`, sin efecto exigible en un repo privado sin plan de pago)
  — para no sobrevender ninguna de las dos como más de lo que es.
- **`ci.yml` endurecido** (resuelve el resto de 4.3 de la auditoría de
  seguimiento): `permissions: contents: read` a nivel de workflow (el
  workflow solo lee el repo, nunca escribe ni publica nada — menor
  privilegio). `actions/checkout` y `actions/setup-dotnet` pasan de `@v4`
  (tag mutable) al SHA de commit completo de su versión actual
  (`v4.4.0`/`v4.3.1` respectivamente), con un comentario al lado indicando a
  qué tag corresponde. No se actualizan a mano: Dependabot abre el PR con el
  SHA nuevo cuando sale una versión (documentado en `CONTRIBUTING.md` → "CI
  y GitHub Actions").
- **Salto a .NET 10.** `TargetFramework` pasa de `net8.0` a `net10.0` en los 5
  `.csproj` (`Bootstrap`, `Sample`, `Shared`, `Sample.Tests` y
  `Bootstrap.IntegrationTests`). El SDK se fija en un `global.json` nuevo
  (`10.0.102`, `rollForward: latestMinor`: acepta versiones posteriores dentro de
  10.x, no salta de versión mayor). `Dockerfile`: imágenes `sdk:10.0` y
  `aspnet:10.0`, y `global.json` se copia antes del `restore` para que restore y
  publish usen el mismo SDK. `ci.yml`: `setup-dotnet` lee la versión de
  `global.json` (`global-json-file`) en vez de repetir `8.0.x`. Se corrigieron las
  frases que decían que cambiar de .NET era "una línea en cada `.csproj`"
  (`PRESENTATION.md`, comentario de `Bootstrap.csproj`) y se agregó a
  `ARCHITECTURE.md` la sección "Versión de .NET y del SDK".
- **Paquetes actualizados por el salto** (el resto se conservó a propósito):
  - `Microsoft.AspNetCore.Mvc.Testing` 8.0.31 → 10.0.2 (necesario). La 8.x sobre
    .NET 10 compila sin avisos pero falla en ejecución: su `TestHost` no
    implementa `PipeWriter.UnflushedBytes`, que exige `System.Text.Json` 10, y
    9 de los 17 tests de integración fallaban. Se eligió 10.0.2 por coincidir con
    el runtime de ASP.NET Core del SDK fijado.
  - `Microsoft.Extensions.Logging.Log4Net.AspNetCore` 8.0.0 → 10.0.0. La 8.0.0 no
    tiene grupo `net10.0` y depende de `log4net` 2.0.15, mientras el proyecto ya
    referenciaba `log4net` 3.4.0; la 10.0.0 tiene grupo `net10.0` y depende de
    `log4net` 3.x. No era estrictamente necesario para que funcionara.
  - Sin cambios: `Swashbuckle.AspNetCore` 6.7.3 (verificado: `/swagger` y
    `swagger.json` responden bien en net10; existe la 10.x, que cambia a
    `Microsoft.OpenApi` 2.x, y queda como mejora opcional), `FluentValidation` 11.11.0
    (su mejor grupo es `net8.0`, también en la 12.x, que es una versión mayor sin
    ventaja específica de net10), `log4net` 3.4.0, `Microsoft.NET.Test.Sdk` 17.11.1,
    `xunit` 2.9.2 y `xunit.runner.visualstudio` 2.8.2 (los tests pasan con ellos).
- **`Program.cs`: se quita el `traceId` del Formato A de error.** Con .NET 10,
  `Results.ValidationProblem` empezó a añadir por su cuenta un `traceId` con el id
  de la `Activity` (que además no coincide con el `X-Correlation-ID`). Se quita
  con `AddProblemDetails(options => options.CustomizeProblemDetails = ...)` para no
  cambiar el contrato con el salto de runtime; lo verifica
  `PostSample_EmptyName_Returns400_WithValidationFilterFormat_AndCorrelationId`.
- **Solución migrada de `.sln` a `.slnx`** (`MyApp.slnx`; se eliminó `MyApp.sln`).
  Motivo: `dotnet sln add` con el SDK 10 sobre el `.sln` clásico reescribía el
  archivo entero (63 líneas añadidas y 2 borradas: BOM, plataformas `x64`/`x86`
  inyectadas, carpetas de solución y proyectos existentes renombrados), el mismo
  ruido que ARCHITECTURE atribuía a Visual Studio. El `.slnx` es un XML simple
  (una línea por proyecto): añadir un proyecto produce **una línea** de diff y
  quitarlo deja el archivo idéntico byte a byte. Requiere Visual Studio 2022 17.13+
  (o una versión equivalente de Rider, o VS Code con C# Dev Kit) si se abre con un
  IDE. Actualizadas todas las referencias: `ci.yml`, `Dockerfile`, `README.md`,
  `ARCHITECTURE.md` (incluida la nota de convención y el checklist) y los skills
  `architecture-review` y `hu-to-slice-scaffold`.
- `ARCHITECTURE.md`: el contrato de errores ya no dice que todos los errores
  comparten formato. Documenta dos formatos intencionales: A (filtro de
  validación: la forma del cuerpo es inválida; `errors`, sin `traceId`) y B
  (`GlobalExceptionHandler`: el cuerpo era válido pero violó una regla o algo
  falló; con `traceId`), con una regla para elegir cuál usar y la advertencia de
  no inventar un tercero. También documenta la configuración única de log4net,
  el `X-Correlation-ID` en todas las respuestas, el silencio de la categoría de
  ASP.NET, cómo se verifica todo esto, y qué borrar o adaptar de
  `Bootstrap.IntegrationTests` al eliminar `Sample`.
- Revisión de consistencia de documentación: los ejemplos de `README.md`
  usan el puerto real de `launchSettings.json` (51058) e incluyen variantes
  bash y PowerShell para ambos endpoints de `Sample`; los pasos para borrar
  `Sample` y para agregar un módulo nuevo mencionan también el
  `ProjectReference` de `Bootstrap.csproj`, `tests/Modules/Sample` y el
  `COPY` del `Dockerfile`; los ejemplos usan `main`.
- Skill `hu-to-slice-scaffold`: el ejemplo de método de dominio inmutable
  usa `Archive` en lugar de `AssignTo` (nombre heredado del demo, sin
  significado en la plantilla).
- `.dockerignore`: `**/bin/` y `**/obj/` (antes `bin/` y `obj/` solo
  excluían la raíz y `COPY . .` podía sobrescribir el `obj/` restaurado con
  el de la máquina local). No se ha ejecutado un `docker build` real; ver
  `ROADMAP.md`.

## [1.1.0] - 2026-09-18

Aprendizajes del primer ciclo real de uso de la plantilla
(`mhvs-demo-tickets`), portados de forma genérica.

### Agregado
- Contrato de errores compartido en `Shared/Kernel`: `NotFoundException`
  (deriva de `DomainException`, → 404) y `PersistenceException` (no deriva
  de `DomainException`, → 500; quien la lanza registra la causa original).
- Módulo `Sample` ampliado con un segundo caso de uso, `RenameSampleItem`
  (`PUT /sample/{sampleItemId}/name`): busca con `GetByIdAsync`, modifica
  con `SampleItem.Rename` (devuelve una instancia nueva, no muta la
  original), lanza `NotFoundException` si no existe y `PersistenceException`
  si el repositorio falla; con validador propio vía `WithCommandValidation`.
- `SampleItemRepository.UpdateAsync` y su adaptador en memoria.
- Tests del módulo `Sample` con ambas convenciones de nomenclatura:
  `CA0x_` en español para los que trazan a un Criterio de Aceptación, e
  inglés puro para invariantes de dominio y reglas de validación.
- `.githooks/pre-push`: bloquea localmente el push directo a
  `main`/`master` (escape: `ALLOW_MAIN_PUSH=1`); `.gitattributes` fuerza LF
  para `.githooks/*`; paso `git config core.hooksPath .githooks` en el
  README y explicación del porqué en `CONTRIBUTING.md`.
- Skills en `.opencode/skills/`: `hu-validation` (separa hallazgos
  bloqueantes de informativos; un documento externo no verificable en
  Trazabilidad es informativo), `hu-to-slice-scaffold` (patrón de reutilizar
  un módulo existente y fallos de persistencia siempre 500) y
  `architecture-review` (consistencia de contrato HTTP entre slices).
- Este `CHANGELOG.md`.

### Cambiado
- `GlobalExceptionHandler` ahora traduce `NotFoundException` → 404,
  `PersistenceException` → 500 (sin volver a registrarla, para evitar logs
  duplicados), `DomainException` → 400, `FluentValidation.ValidationException`
  → 400, `BadHttpRequestException` → 400 (sin exponer el mensaje interno del
  framework) y cualquier otra excepción → 500 genérico. Todas las respuestas
  incluyen `traceId`.
- `ValidationEndpointFilter`: documentado el contrato de formato (siempre el
  mismo cuerpo 400 RFC 9110 con `errors`, sin `traceId`), sin cambios de
  comportamiento.
- `ARCHITECTURE.md`: sección "Plomería HTTP transversal" ampliada con la
  tabla de contrato excepciones → HTTP, la nota de precedencia (validación
  gana sobre "no encontrado"), el porqué de las excepciones en
  `Shared/Kernel`, la convención de agregar proyectos al `.sln` solo con
  `dotnet sln add`, y el árbol del módulo `Sample` con sus dos casos de uso.
- `CONTRIBUTING.md`: excepción de nomenclatura `CA0x_` (solo para tests que
  trazan a un CA, no a reglas de negocio sin CA), recordatorio de confirmar
  "Squash and merge" al mergear, y explicación del hook `pre-push`.
- Tests de `PingSample` usan un fake compartido del repositorio
  (`tests/Modules/Sample/Support`).

### Deuda técnica conocida
- Falta un proyecto de tests de integración para `Bootstrap` que verifique
  automáticamente el mapeo HTTP end-to-end (404/400/500) de
  `GlobalExceptionHandler` y del filtro de validación; hoy se verifica
  manualmente con curl/Postman/Swagger.
- El dominio no usa `Shared/Ports/IClock` para generar timestamps, por lo
  que los tests de fechas usan aserciones débiles (`>=`) en vez de comparar
  contra un valor inyectado y exacto. `Sample` todavía no genera
  timestamps; aplica en cuanto un módulo real los agregue (en el demo
  ocurrió con `Ticket.Create`/`Ticket.AssignTo`).

## [1.0.0] — Versión inicial

### Agregado
- Estructura base MHVS (`src/Modules/`, `src/Shared/`, `src/Bootstrap/`).
- Módulo de ejemplo `Sample` funcional con repositorio en memoria.
- Logging con log4net configurado desde el arranque.
- Credenciales desacopladas de `appsettings.json`
  (`config/connectionstrings.local.json`, gitignored).
- `ARCHITECTURE.md`, `README.md`, `CONTRIBUTING.md`, `PRESENTATION.md`.
