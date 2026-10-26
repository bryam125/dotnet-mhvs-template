---
name: architecture-review
description: Audita un PR o código ya escrito contra la convención MHVS — verifica dirección de dependencias (imports y ProjectReference entre módulos), organización de slices, nomenclatura, manejo seguro de credenciales, logging, contrato HTTP consistente entre slices (incluido 409 y precedencia), uso de IClock y de la advertencia de MemberwiseClone, composición completa de módulos nuevos (Bootstrap.csproj, Dockerfile, Program.cs), plomería de runtime de Bootstrap y cobertura de tests. Usar para revisar PRs, no para generar código nuevo.
metadata:
  opencode/autoinvoke: "true"
---

# Skill: architecture-review

**Cuándo usar este skill:** te piden revisar un PR, un diff, o código ya
escrito (propio o de un compañero) para confirmar que respeta la
convención MHVS. Es una auditoría, no una generación de código nuevo —
si detectas una violación, repórtala y propone la corrección; no la
apliques directo salvo que te lo pidan explícitamente.

## Checklist de revisión

### Dirección de dependencias (lo más importante, revísalo primero)
- [ ] Ningún archivo en `Domain/` de ningún módulo importa algo de
      `Infrastructure/`, de `Application/`, de ASP.NET, EF Core, o de
      cualquier paquete de infraestructura.
- [ ] De otro módulo, **ninguna capa** (`Domain/`, `Application/` ni
      `Infrastructure/`) importa nada de su proyecto principal. Lo único
      que se puede usar de otro módulo es su `{Otro}.Contracts`, y **solo
      desde `Infrastructure/`** del consumidor: el adaptador que implementa
      un puerto propio de su `Domain/`. `Domain/` y `Application/` nunca
      usan el `Contracts` de otro módulo (ver `ARCHITECTURE.md` → "Contrato
      entre módulos"). Señala cualquier `using MyApp.Modules.{Otro}...` que
      no sea `MyApp.Modules.{Otro}.Contracts`, y cualquier uso de un
      `Contracts` ajeno fuera de `Infrastructure/`. No hay mecanismo de
      eventos: si el diff introduce uno, es una decisión nueva que debe
      aprobar un humano.
- [ ] **Referencias de proyecto, no solo `using`.** Revisa el diff de
      cada `src/Modules/*/*.csproj`. Buscar `using` no basta: una
      referencia de proyecto recién agregada, que el código todavía no
      usa, no deja ningún `using`, pero ya abre la puerta a importar las
      clases internas del otro módulo. Cada módulo es un solo proyecto
      que contiene `Domain/`, `Application/` e `Infrastructure/`, así que
      una `<ProjectReference>` a otro módulo da acceso a **todas** sus
      capas. Las únicas `ProjectReference` permitidas en el `.csproj` de
      un módulo son `Shared/Shared.csproj` y proyectos `*.Contracts`
      (`src/Modules/{Modulo}.Contracts/{Modulo}.Contracts.csproj`): el
      propio, para implementarlo, o el de otro módulo, para consumirlo.
      Cualquier `<ProjectReference Include="...Modules/{Otro}/{Otro}.csproj">`
      (el proyecto principal de otro módulo) es una violación de
      dependencia aunque ningún archivo `.cs` la use todavía. Un
      `*.Contracts` puede referenciar `Shared`, pero no referencia ningún
      proyecto de `src/Modules`, ni siquiera otro `Contracts`, y vive en una
      carpeta hermana del módulo, no dentro. Solo `Bootstrap.csproj` y el proyecto de tests del propio
      módulo referencian proyectos principales de módulos.
      **Control automático:** `tests/Bootstrap.IntegrationTests/ArchitectureTests.cs`
      falla en CI si un módulo referencia el proyecto principal de otro, si
      `Shared` referencia algo de `src/Modules` o si un `Contracts`
      referencia algo de `src/Modules`. Mira tanto los `.csproj` como los
      ensamblados compilados. **No distingue capas**: que solo
      `Infrastructure/` use un `Contracts` ajeno lo revisas tú.
      Comando útil: `git diff <base> -- 'src/Modules/**/*.csproj'` y
      `grep -n "ProjectReference" src/Modules/*/*.csproj`.

### Organización de carpetas
- [ ] Si un módulo tiene 4+ casos de uso en `Application/`, están
      agrupados en subcarpetas por slice (`{Verbo}{Entidad}/`), no
      sueltos.
- [ ] Los puertos (interfaces) viven en `Domain/` (si son de negocio) o
      en `Shared/Ports/` (si son técnicos y cruzan módulos) — nunca
      definidos dentro de `Infrastructure/`.
- [ ] No hay repositorios genéricos tipo `IRepository<T>` — cada puerto
      expresa el lenguaje del dominio específico.
- [ ] `Shared/Web/` contiene solo helpers HTTP agnósticos de negocio;
      ninguna lógica ni tipo interno de un módulo.

### Nomenclatura
- [ ] Los casos de uso siguen `{Verbo}{Entidad}` de forma consistente
      (`CreateOrder`, no `OrderCreator` mezclado con `CreateOrder` en el
      mismo módulo).
- [ ] Los nombres de clases/campos están en inglés si esa es la
      convención vigente del repo (revisa `CONTRIBUTING.md`), consistente
      dentro de un mismo archivo. **Excepción documentada:** los nombres
      de métodos de test que trazan directamente a un Criterio de
      Aceptación de una HU (prefijo `CA01_`, `CA02_`...) pueden ir en
      español — no la marques como violación si sigue ese patrón.
      **La excepción aplica solo a tests que trazan a un CA:** un test
      que traza a una regla de negocio o invariante de dominio sin CA
      asociado debe ir en inglés y sin prefijo `CA0x_`; si lo encuentras
      con ese prefijo (o en español sin él), repórtalo como hallazgo.

### Seguridad y configuración
- [ ] Ninguna cadena de conexión, API key o credencial está
      hardcodeada o en `appsettings.json` — debe leerse de
      `IConfiguration`, respaldada por `config/connectionstrings.local.json`.
- [ ] No se agregó nada nuevo a `appsettings.json` que debería estar en
      el archivo de credenciales locales.
- [ ] Todo endpoint nuevo (`.MapGet`/`.MapPost`/`.MapPut`/...) declara
      explícitamente `.RequireAuthorization()` o `.AllowAnonymous()`
      (ver `ARCHITECTURE.md` → "Autenticación y autorización"). Si no
      declara ninguno de los dos, **recházalo** — no asumas que "sin
      auth configurada da igual"; es la convención exigible desde antes
      de que exista ningún esquema de autenticación real.

### Consistencia de contrato HTTP entre slices
- [ ] Un fallo de **persistencia** (el repositorio no puede guardar)
      responde con un código **5xx** (`PersistenceException`), nunca 400
      — un error de guardado no es una regla de negocio violada por el
      cliente. Compara contra cómo lo resuelven otros handlers ya
      existentes en el mismo módulo o en otros; si alguno usa 400 para
      esto, es la inconsistencia a corregir, no el nuevo caso.
- [ ] Un recurso no encontrado (entidad inexistente o identificador mal
      formado) responde con **404** (`NotFoundException`), un error de
      **validación** de entrada con **400** — no mezcles estas
      categorías entre sí en ningún handler nuevo.
- [ ] La validación de entrada pasa por `.WithCommandValidation<T>()`
      (`Shared/Web`) con un validador de FluentValidation. Un slice nuevo
      **no reinventa su propio formato de error 400** (por ejemplo
      `Results.BadRequest(...)` con un cuerpo ad hoc, o validar a mano
      dentro del handler) distinto al de los demás slices del mismo
      módulo: todos deben producir el mismo cuerpo 400.
- [ ] Quien lanza `PersistenceException` registra antes la causa original
      con contexto (ej. el id de la entidad); el manejador global no la
      vuelve a registrar.
- [ ] Si detectas que un slice nuevo resuelve esto distinto a como ya
      lo resolvió un slice anterior del mismo módulo, repórtalo como
      hallazgo — la inconsistencia entre slices es un problema aunque
      cada uno, visto aislado, parezca razonable.
- [ ] **Precedencia validación vs. no encontrado.** Por defecto, gana la
      validación de entrada (400), porque corre en el borde HTTP antes de
      que el handler busque el recurso. Si el slice cambia ese orden (ej.
      busca el recurso antes de validar, o valida a mano después de
      `GetByIdAsync`), la HU debe declararlo en un CA (ver
      `ARCHITECTURE.md` → "Precedencia"). Si no hay un CA que lo
      declare, es un hallazgo.
- [ ] **Conflicto = 409, no 500 ni 400.** Una regla de unicidad o de
      conflicto de estado (ej. "no puede haber dos con el mismo título")
      debe lanzar `ConflictException` (mapeada a 409 en
      `GlobalExceptionHandler`, antes del caso `DomainException`). No
      debe acabar envuelta en `PersistenceException` por un
      `catch (Exception)` genérico. 401/403 no aplican todavía, porque
      no hay autenticación implementada.

### Dominio: reloj e inmutabilidad
- [ ] **Timestamps vía `IClock`.** El código nuevo de dominio o de
      handlers que genera fechas u horas ("ahora", `CreatedAt`,
      `UpdatedAt`, comparar contra "hoy") usa `Shared/Ports/IClock`
      (`clock.UtcNow`, inyectado en el handler y pasado al dominio como
      parámetro), **no** `DateTime.UtcNow` / `DateTime.Now` /
      `DateTimeOffset.UtcNow` directo. Comando útil:
      `grep -rn "DateTime\(Offset\)\?\.\(Utc\)\?Now" src/Modules`.
      Señala cada aparición nueva. La referencia correcta es `Tasks`
      (`CreateTaskHandler`).
- [ ] **Advertencia de copia superficial.** Todo método de dominio
      inmutable que usa `MemberwiseClone()` tiene, justo encima, el
      comentario que advierte que es una copia **superficial** y que,
      si la entidad gana un campo de colección u objeto mutable, hace
      falta una copia profunda explícita. Si falta el comentario, es un
      hallazgo. Si la entidad **ya** tiene una colección u objeto mutable
      y aun así usa `MemberwiseClone()`, es un hallazgo más grave: la
      copia y el original comparten ese campo, y la inmutabilidad se
      rompe en silencio.

### Logging
- [ ] El código nuevo usa `ILogger<T>` inyectado, no referencia log4net
      directamente ni usa `Console.WriteLine` para logging real.

### Plomería de runtime de `Bootstrap`
Aplica si el PR toca `GlobalExceptionHandler`, `Program.cs`, el pipeline
de middleware, `appsettings*.json` (sección `Logging`) o `log4net.config`.
Un review de diff tiende a no ver estos errores, porque solo aparecen en
runtime. Confírmalo explícitamente, no lo des por hecho:

- [ ] **Sin logging duplicado** (ya se corrigió una vez):
  - `GlobalExceptionHandler` sigue sin volver a registrar
    `PersistenceException`, porque ya la registró quien la lanzó.
  - Registra los 4xx como `Warning`, no como `Error`.
  - La categoría `Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware`
    sigue en `"None"` en `appsettings.json`.
  - log4net se configura una sola vez: `AddLog4Net`, sin un
    `XmlConfigurator.Configure` adicional.
- [ ] **Orden del pipeline intacto.** El orden actual de `Program.cs` es:
      `UseForwardedHeaders` → `UseHttpsRedirection`/`UseHsts` (fuera de
      `Development`) → `CorrelationIdMiddleware` → `UseExceptionHandler`
      → (Swagger en `Development`) → `UseCors` → `UseRateLimiter` →
      (autenticación/autorización, cuando exista) → endpoints.
      `UseForwardedHeaders` debe seguir primero: HTTPS, HSTS y el
      limitador por IP dependen de él. `CorrelationIdMiddleware` debe
      seguir antes del manejo de excepciones, para que las respuestas de
      error lleven `X-Correlation-ID`. Si el PR mueve cualquiera de estos
      pasos, exige la justificación y un test que lo fije.
- [ ] Si cambió algo de esto, `tests/Bootstrap.IntegrationTests` se
      actualizó en el mismo PR (convención de `ARCHITECTURE.md`).

### Tests
- [ ] Todo `Handler` nuevo en `Application/` tiene al menos un test en
      `tests/Modules/{Modulo}/`.
- [ ] Los tests de `Domain`/`Application` no requieren base de datos
      real ni levantar la app completa — usan mocks de los puertos.
- [ ] Si el PR referencia una HU, cada criterio de aceptación de esa HU
      tiene un test correspondiente identificable por nombre.
- [ ] Si el PR agrega un endpoint HTTP nuevo, revisa si trae un test
      mínimo en `tests/Bootstrap.IntegrationTests/` (2xx / 400 / 404,
      según aplique). Los tests unitarios con mock no ven la ruta, el
      filtro de validación ni el código que recibe el cliente. Si falta,
      repórtalo como observación, no como bloqueo, salvo que el PR
      cambie el contrato de errores: en ese caso es obligatorio.

### Registro y composición
- [ ] Si se agregó un módulo o slice nuevo, está registrado en su
      `{Modulo}Module.cs` correspondiente, y ese módulo está enlazado
      desde `Program.cs`/`Bootstrap`.
- [ ] Si se agregó un **módulo** nuevo, están **todas** estas piezas; si
      falta una, el módulo queda roto en algún entorno. Ya pasó con el
      `Dockerfile` de `Tickets` en `mhvs-demo-tickets`: CI en verde,
      imagen Docker rota.
      - `<ProjectReference>` al módulo en `src/Bootstrap/Bootstrap.csproj`.
      - `COPY src/Modules/{Modulo}/{Modulo}.csproj src/Modules/{Modulo}/`
        en el `Dockerfile`, antes del `RUN dotnet restore`. CI no
        construye Docker, así que nadie más lo va a ver.
      - `Add{Modulo}Module()` y `Map{Modulo}Module()` en `Program.cs`.
      - El proyecto de tests `tests/Modules/{Modulo}/{Modulo}.Tests.csproj`.
      - El módulo y su proyecto de tests en `MyApp.slnx`.
- [ ] Si se agregó un proyecto nuevo, está agregado a `MyApp.slnx` con
      `dotnet sln MyApp.slnx add` (el diff debe ser una sola línea de
      proyecto nuevo; un diff grande indica que el archivo se regeneró o se
      editó a mano).

## Cómo responder

Estructura tu revisión así:

1. **Resumen en una línea**: aprobado / aprobado con observaciones /
   requiere cambios.
2. **Violaciones de dependencia** (si las hay) — son las más graves,
   van primero, con archivo y línea exacta.
3. **Resto de hallazgos**, agrupados por las categorías de arriba.
4. **Lo que sí está bien hecho** — menciónalo brevemente; una revisión
   que solo señala problemas es menos útil que una que también confirma
   qué no hace falta tocar.

No apruebes un PR que rompa la dirección de dependencias
(`domain` → nada externo) aunque el resto esté bien — esa es la regla
no negociable de toda la arquitectura MHVS.
