---
name: hu-to-slice-scaffold
description: Genera el código de un slice MHVS completo (Command, Handler, Validator, entidad de dominio, puerto, adaptador in-memory y tests) a partir de una historia de usuario ya validada. Usar cuando piden implementar, generar código o armar el slice de una HU específica, tanto para crear una entidad nueva como para buscar y modificar una existente.
metadata:
  opencode/autoinvoke: "true"
---

# Skill: hu-to-slice-scaffold

**Cuándo usar este skill:** te piden implementar, generar código o
"armar el slice" para una historia de usuario específica. Requisito
previo: la HU ya pasó `hu-validation` (o el usuario confirmó
explícitamente que quiere continuar sin esa validación).

El módulo `Sample` (`src/Modules/Sample`) es la referencia viva de los
dos patrones que cubre este skill: `PingSample` (crear) y
`RenameSampleItem` (buscar + modificar). Léelo antes de generar código.
Si la HU pide explícitamente persistencia real (no en memoria), la
referencia es `Tasks` (`src/Modules/Tasks`) — ver sección 5.

## 1. Mapeo HU → estructura de carpetas MHVS

A partir del título `HU-{modulo}-{numero} — {Verbo} {Entidad}`:

```
Módulo   → {modulo}, en PascalCase → src/Modules/{Modulo}/
Slice    → {Verbo}{Entidad}, en PascalCase → Application/{Verbo}{Entidad}/
```

Ejemplo con `HU-SAMPLE-002 — Renombrar un item` (módulo `Sample`). Es un ID **ilustrativo**: no existe un archivo `HU-SAMPLE-002.md` en el repo; el slice `RenameSampleItem` sí existe:

```
src/Modules/Sample/
├── Application/
│   ├── SampleModule.cs                     (si no existe, créalo)
│   └── RenameSampleItem/
│       ├── RenameSampleItemCommand.cs
│       ├── RenameSampleItemRequest.cs      (cuerpo HTTP, si el id viaja en la ruta)
│       ├── RenameSampleItemResult.cs
│       ├── RenameSampleItemValidator.cs
│       └── RenameSampleItemHandler.cs
├── Domain/
│   ├── SampleItem.cs
│   └── SampleItemRepository.cs             (puerto)
└── Infrastructure/
    └── InMemorySampleItemRepository.cs     (adaptador por defecto)

tests/Modules/Sample/RenameSampleItem/
├── RenameSampleItemHandlerTests.cs
└── RenameSampleItemValidatorTests.cs
```

**Nombres de clases en inglés:** aunque el título de la HU esté en
español ("Renombrar un item"), los nombres de archivos y clases van en
inglés (`RenameSampleItem...`), según la convención de `CONTRIBUTING.md`.

## 2. Si el módulo ya existe: reutiliza, no reinventes

**Si el módulo ya existe, no lo recrees.** Agrega el slice nuevo dentro
de su `Application/` existente y reutiliza entidades/puertos de
`Domain/` — no dupliques una entidad existente.

Cuando la HU **opera sobre una entidad que ya existe** (buscar +
modificar, no solo crear), no copies el patrón de creación. Sigue el
patrón de `RenameSampleItem`:

1. **Buscar con el puerto existente.** Usa `GetByIdAsync` del
   repositorio que ya define el módulo. Si el puerto todavía no tiene
   `UpdateAsync`, agrégalo al puerto y a su adaptador in-memory — no
   crees un segundo repositorio para la misma entidad.
2. **Un método de dominio inmutable.** Agrega a la entidad un método
   (`Rename`, `Archive`, ...) que valide la regla y devuelva una
   **instancia nueva** (`MemberwiseClone()` + cambio) en lugar de mutar
   `this`. Así un fallo de guardado no deja el objeto en memoria a
   medias. **Siempre** que generes un `MemberwiseClone()`, pon este
   comentario justo encima de la línea, sin acortarlo:

   ```csharp
   // ADVERTENCIA: MemberwiseClone hace una copia SUPERFICIAL (shallow copy).
   // Es seguro solo mientras la entidad tenga campos de valor o inmutables
   // (string, Guid, DateTime, enum...). Si la entidad gana un campo de
   // colección (List<T>, Dictionary<,>...) u otro objeto mutable, la copia y
   // el original lo compartirían por referencia: reemplaza esto por una copia
   // profunda explícita de ese campo. Ver ROADMAP.md → "Copia superficial".
   var renamed = (SampleItem)MemberwiseClone();
   ```

   Si la entidad **ya** tiene un campo de colección u otro objeto
   mutable, no uses `MemberwiseClone()`: construye la instancia nueva
   copiando ese campo de forma explícita (ej. `new List<T>(original)`).
3. **`NotFoundException` si no existe.** Si `GetByIdAsync` devuelve
   `null` (o el identificador viene mal formado, ej. no es un GUID),
   lanza `NotFoundException` (`Shared/Kernel`) → el manejador global la
   traduce a 404.
4. **Persiste y protege el fallo.** Llama `UpdateAsync` dentro de
   `try/catch`; ver la sección siguiente.
5. **El id viaja en la ruta, el cuerpo en un `{Verbo}{Entidad}Request`.**
   El validador y `.WithCommandValidation<{Verbo}{Entidad}Request>()`
   se aplican sobre el tipo que llega como argumento del endpoint (el
   cuerpo). El handler recibe un `Command` que combina ambos.

## 3. Fallos de persistencia: SIEMPRE 500, nunca 400

En cualquier slice nuevo que persista (`AddAsync` o `UpdateAsync`),
envuelve la llamada al repositorio así:

```csharp
try
{
    await repository.UpdateAsync(updated, ct);
}
catch (Exception exception) when (exception is not OperationCanceledException)
{
    logger.LogError(exception, "No se pudo guardar ... {EntityId}", updated.Id);
    throw new PersistenceException("La operación no se realizó.");
}
```

- El handler **registra la causa original con contexto** (el id de la
  entidad) antes de lanzar: el manejador global no vuelve a registrar
  `PersistenceException`, para no duplicar el log.
- Excepción: si el slice tiene una regla de conflicto (409), el `catch`
  deja pasar `ConflictException`. Ver 3.1.
- `PersistenceException` **no** deriva de `DomainException` y se traduce
  a 500. Nunca la conviertas en `DomainException` ni la respondas como
  400: un fallo de guardado no es una regla de negocio violada por el
  cliente.
- Genera siempre un test que fuerce el fallo del puerto y verifique que
  se lanza `PersistenceException` y que la entidad almacenada no cambió.

### 3.1 Contrato de errores completo (qué excepción → qué código)

| Situación | Excepción (`Shared/Kernel`) | HTTP |
|---|---|---|
| Forma de la entrada inválida (vacío, longitud, patrón, rango) | ninguna: validador + `.WithCommandValidation<T>()` | 400 (Formato A) |
| Regla de negocio violada que depende del estado, pero no es un conflicto | `DomainException` | 400 |
| Recurso inexistente o id mal formado | `NotFoundException` | 404 |
| **Conflicto de unicidad o de estado** | `ConflictException` (ver abajo) | **409** |
| El repositorio no pudo guardar | `PersistenceException` | 500 |

**409 — `ConflictException`.** Úsala cuando la solicitud tiene la forma
correcta, pero choca con algo que ya existe o con el estado actual del
recurso. No aplica a un dato mal formado. Ejemplos: "no puede haber dos
tareas con el mismo título el mismo día", "no se puede archivar un item
ya archivado", si una HU lo pide. **No existe todavía** en el template:
la primera HU que la necesite la crea en el mismo PR, así:

1. `src/Shared/Kernel/ConflictException.cs`, con el mismo patrón que
   `NotFoundException`:
   `public class ConflictException(string message) : DomainException(message);`
2. Un caso en `GlobalExceptionHandler` que la mapee a
   `StatusCodes.Status409Conflict`, **antes** del caso `DomainException`.
   Si va después, cae en el 400 genérico: es el mismo motivo por el que
   `NotFoundException` va primero.
3. Una fila en la tabla "Excepciones → respuesta" de `ARCHITECTURE.md`,
   y un test en `tests/Bootstrap.IntegrationTests` que fije el 409.
   `ARCHITECTURE.md` exige actualizar esos tests cuando cambia el
   contrato de errores.
4. En el handler, compruébalo **antes** de persistir, con un método del
   puerto con nombre de dominio (ej. `ExistsWithTitleOnDateAsync`), y
   lanza `ConflictException`. Si además el adaptador real detecta el
   conflicto al guardar (ej. violación de índice único en MySQL), debe
   traducirlo a `ConflictException`, y el `catch` del handler debe
   dejarla pasar para que no se convierta en un 500:
   `when (exception is not OperationCanceledException and not ConflictException)`.

**401/403 no se generan todavía.** El template no tiene autenticación
implementada: solo el punto de extensión documentado en `Program.cs`
(bloque "Punto de extensión — Autenticación/autorización") y la
convención de declarar `.RequireAuthorization()` / `.AllowAnonymous()`
en cada endpoint. No inventes excepciones ni respuestas 401/403 en un
slice. Se definirán cuando se implemente ese punto de extensión. Si
una HU exige permisos o roles, dilo y pregunta antes de generar código.

## 4. Reglas de generación, sección por sección de la HU

**`Datos de entrada` → `{Verbo}{Entidad}Command.cs`**
Un record con un campo por fila. Las restricciones no se validan aquí,
van en el Validator.

**`Reglas de negocio` + restricciones de `Datos de entrada` → `{Verbo}{Entidad}Validator.cs`**
FluentValidation, conectado al endpoint con `.WithCommandValidation<T>()`
(`Shared/Web`) para que todos los slices produzcan el mismo cuerpo 400.
No valides a mano dentro del handler ni inventes un formato de error
propio. Cada regla numerada debe tener una línea de validación
identificable — no agrupes varias reglas en una condición genérica, se
pierde la trazabilidad HU → código. El dominio vuelve a aplicar las
mismas reglas como invariante.

**`Datos de entrada` + `Valores generados por el sistema` → entidad de `Domain/`**
Constructor privado + factory estático `Create(...)`. Los campos de
"Valores generados por el sistema" se calculan adentro del `Create` y
nunca vienen del `Command` (el usuario no los envía). Los catálogos se
modelan como `enum`.

**Timestamps: siempre `IClock`, nunca `DateTime.UtcNow`.** Si un valor
generado es una fecha u hora ("ahora", `CreatedAt`, `UpdatedAt`), el
handler recibe `IClock` (`Shared/Ports`) por constructor y le pasa
`clock.UtcNow` al método de dominio como parámetro (ej.
`Create(..., DateTime utcNow)`). Ni la entidad ni el handler llaman a
`DateTime.UtcNow` / `DateTime.Now`. El módulo lo registra con
`services.TryAddSingleton<IClock, SystemClock>()`, y los tests fijan el
reloj con un `FixedClock`. Esa es la única excepción a "se calcula
adentro del `Create`": el valor sigue sin venir del usuario. La
referencia es `Tasks` (`CreateTaskHandler`, `TasksModule`,
`tests/Modules/Tasks/Support/`).

**`Flujo principal` → `{Verbo}{Entidad}Handler.cs`**
El handler sigue el mismo orden que describe la HU: recibir comando →
(buscar entidad, si aplica) → aplicar dominio → persistir vía puerto →
devolver resultado. Agrega `ILogger<T>` con al menos un log informativo
en el camino exitoso.

**Cada `Criterio de aceptación` → un test**
Nombra los tests con el ID de la HU para trazabilidad:

```csharp
[Fact]
public async Task CA01_RenombrarExitoso_GuardaElNuevoNombreYLoRetorna() { ... }

[Fact]
public async Task CA02_ItemInexistente_LanzaNotFoundException() { ... }
```

El prefijo `CA0x_` (y el español) es solo para tests que trazan a un CA.
Un test de invariante de dominio o de regla de negocio sin CA asociado
va en inglés y sin prefijo (`Rename_ReturnsNewInstance_AndLeavesOriginalUntouched`).
Usa mocks del puerto del repositorio — los tests de `Application`/`Domain`
nunca tocan infraestructura real.

**Test de integración del endpoint.** Además de los tests unitarios del
handler, existe `tests/Bootstrap.IntegrationTests/`, que levanta la app
real en memoria (`WebApplicationFactory<Program>`). Si el slice agrega un
endpoint HTTP nuevo, considera agregar ahí un test mínimo que fije el
contrato observable que el mock no ve: la ruta, el filtro de validación
conectado y el código que llega al cliente. Por ejemplo: un 2xx con
entrada válida, un 400 con entrada inválida y un 404 con un id
inexistente, si aplica. Sigue `SampleEndpointsTests.cs` como modelo. Si
el adaptador del módulo es una BD real, sigue `TasksEndpointsTests.cs`:
prueba solo entradas inválidas, porque un caso válido escribiría en la
base configurada en la máquina. Si decides no agregarlo, dilo en el
reporte final.

**`Trazabilidad` → comentario XML doc**
```csharp
/// <summary>
/// Implementa HU-SAMPLE-002 — Renombrar un item.
/// </summary>
public sealed record RenameSampleItemCommand(...);
```

## 5. Qué NO hacer

- No inventar reglas de negocio, catálogos o restricciones ausentes en
  la HU. Pregunta si falta algo necesario.
- No cambiar la estructura de carpetas MHVS para "acomodar" una HU
  rara — si no encaja, dilo antes de forzarla.
- No mezclar dos historias en un mismo slice, aunque compartan entidad.
- No tocar un adaptador de infraestructura real ya configurado — el
  `InMemory*` es solo el default para HUs sin almacenamiento definido.
- Si el usuario pide explícitamente un adaptador de persistencia real
  (MySQL, o cualquier otra BD vía ADO.NET puro), **no inventes el
  patrón desde cero**: sigue `Tasks/Infrastructure/MySqlTaskRepository.cs`
  como referencia (SQL parametrizado, una conexión por operación,
  lectura de la cadena de conexión en cada operación — no en el
  constructor —, y `schema.sql` junto al adaptador como documentación
  que la app nunca ejecuta). Ver ARCHITECTURE.md → "Dos ejemplos de
  referencia: `Sample` vs. `Tasks`".
- No mutar la entidad original en un método de dominio de modificación.
- No generar un `MemberwiseClone()` sin el comentario de advertencia de
  copia superficial (sección 2).
- No usar `DateTime.UtcNow`/`DateTime.Now` en dominio ni en handlers;
  usa `IClock` (sección 4).
- No agregar proyectos a `MyApp.slnx` a mano ni desde el IDE; usa `dotnet sln MyApp.slnx add`.
- No cerrar el trabajo sin que los tests generados realmente pasen.

## 6. HU que toca 2 o más módulos: DETENTE y pregunta

Si la HU implica leer o modificar entidades, reglas o datos de **dos o
más módulos** a la vez, **no generes código todavía**. Nunca elijas en
silencio qué módulo es el dueño. Ejemplos: "al crear una factura,
descontar stock del inventario", "asignar un ticket a un usuario y
validar que el usuario exista". Pregunta explícitamente:

1. ¿Qué módulo es **dueño** de la entidad y de la lógica principal de
   esta HU? El slice vive ahí.
2. ¿Qué necesita ese módulo del otro? ¿Solo consultar un dato, o
   provocar un cambio?

Mientras no haya respuesta, no apliques ninguno de estos atajos, porque
todos rompen la arquitectura: agregar un `ProjectReference` al proyecto
principal de otro módulo, un `using MyApp.Modules.{Otro}...` que no sea su
`Contracts`, copiar la entidad del otro módulo, o mover la lógica o el
contrato a `Shared` (ni siquiera a `Shared/Ports`, que es solo para puertos
técnicos) para "compartirla".

Con las respuestas, **propón** el mecanismo de `ARCHITECTURE.md` →
"Contrato entre módulos", y espera a que el humano lo confirme antes de
generar código:

- **Si es una consulta:** el módulo que expone define la interfaz y sus DTOs
  (`record`) en `src/Modules/{Otro}.Contracts/{Otro}.Contracts.csproj`, que
  es una carpeta hermana, no dentro del módulo. Ese `Contracts` puede
  referenciar `Shared`, pero nada de `src/Modules`. La implementa dentro de su
  módulo y la registra en su `Add{Otro}Module()`. El módulo dueño de la HU
  define su propio puerto en `Domain/` y un adaptador en `Infrastructure/`,
  que es lo único que usa ese `Contracts`. Si el `Contracts` todavía no
  existe, crearlo es un proyecto nuevo (`.csproj`, `dotnet sln add`, `COPY`
  en el `Dockerfile`): dilo explícitamente en tu propuesta.
- **Si es provocar un cambio en el otro módulo:** no es atómico con la
  persistencia del módulo dueño de la HU (no hay transacción distribuida ni
  outbox). **Detente** y decídelo con el humano. No hay mecanismo de eventos
  en el template y no lo inventas tú.

## 7. Módulo nuevo: checklist exhaustivo

Si el módulo de la HU **no existe**, crearlo exige **todos** estos
pasos. No es una lista de ejemplos: si falta uno, el módulo queda roto
en algún entorno. Ya pasó una vez, con el `Dockerfile` de `Tickets` en
`mhvs-demo-tickets`: CI compilaba en verde, pero la imagen Docker
fallaba en el `restore`, y nadie lo vio porque CI no construye Docker.

- [ ] **`.csproj` del módulo:** `src/Modules/{Modulo}/{Modulo}.csproj`,
      copiado de `Sample.csproj` (o de `Tasks.csproj` si usa BD real),
      con `<RootNamespace>MyApp.Modules.{Modulo}</RootNamespace>`.
- [ ] **Solución:** `dotnet sln MyApp.slnx add --in-root src/Modules/{Modulo}/{Modulo}.csproj`.
      Siempre por comando, **nunca editando `MyApp.slnx` a mano** ni
      desde el IDE. El diff debe ser una sola línea.
- [ ] **`Bootstrap.csproj`:** agrega
      `<ProjectReference Include="../Modules/{Modulo}/{Modulo}.csproj" />`
      junto a los de `Sample` y `Tasks`.
- [ ] **`Dockerfile`:** agrega
      `COPY src/Modules/{Modulo}/{Modulo}.csproj src/Modules/{Modulo}/`
      junto a las demás líneas `COPY ...csproj`, **antes** del
      `RUN dotnet restore`. El `restore` de la imagen solo ve los
      `.csproj` que se copian explícitamente.
- [ ] **`Program.cs`:** una línea de DI (`builder.Services.Add{Modulo}Module();`)
      y una de mapeo (`app.Map{Modulo}Module();`), junto a las de los
      módulos existentes.
- [ ] **`{Modulo}Module.cs`** en `Application/`, con `Add{Modulo}Module`
      y `Map{Modulo}Module`, y cada endpoint con `.RequireAuthorization()`
      o `.AllowAnonymous()` explícito.
- [ ] **Proyecto de tests:** `tests/Modules/{Modulo}/{Modulo}.Tests.csproj`
      (copiado de `Sample.Tests.csproj`), con `ProjectReference` al
      proyecto del módulo, y agregado con
      `dotnet sln MyApp.slnx add --in-root tests/Modules/{Modulo}/{Modulo}.Tests.csproj`.
- [ ] **Verificación:** `dotnet build` + `dotnet test`. Si Docker está
      disponible, también `docker build .`. Si no lo está, di en el
      reporte que el `Dockerfile` se editó pero no se construyó.

## 8. Flujo de trabajo

```
1. Confirma que la HU pasó hu-validation (o el usuario aceptó continuar sin eso)
2. Mapea módulo + slice — si el módulo es ambiguo, pregunta el nombre real.
   Si la HU toca 2+ módulos, DETENTE y pregunta cuál es el dueño (sección 6)
3. Si el módulo no existe, aplica el checklist completo de la sección 7
4. Decide el patrón: ¿crea una entidad nueva o busca + modifica una existente?
   Si es lo segundo, reutiliza puerto y entidad (sección 2)
5. Genera/extiende: Command, (Request), Validator, entidad, puerto, adaptador
   in-memory, Handler, registro en {Modulo}Module.cs, tests unitarios y
   (si hay endpoint nuevo) considera el test de integración (sección 4)
6. Corre dotnet build + dotnet test
7. Reporta qué CA quedó cubierto por qué test, uno a uno
8. Señala explícitamente cualquier parte de la HU que no se pudo
   mapear a código de forma inequívoca — no adivines
```
