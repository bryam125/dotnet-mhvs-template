# Roadmap — dotnet-mhvs-template

Documento vivo con la deuda técnica conocida y mejoras futuras
identificadas. No es un compromiso de fechas, es una lista de "esto
ya lo sabemos, lo dejamos anotado a propósito" para no perderlo ni
redescubrirlo cada vez.

Cuando se resuelva algo de aquí, muévelo a `CHANGELOG.md` bajo
"Agregado" o "Cambiado" en la versión correspondiente, y bórralo de
este archivo.

---

## Auditoría de pre-lanzamiento (2026-09-21)

> **Estado de este registro.** Se pega completo, tal como se emitió, para no perder el trabajo mientras los hallazgos se resuelven en PRs separados. Al resolver uno, muévelo a `CHANGELOG.md` y márcalo aquí como resuelto (no borres el informe original).
>
> **Ya atendido:**
> - En el PR #4: **1.1** (ruta de las credenciales y aviso al arrancar) y la parte de **2.9** sobre archivos inexistentes (`docs/templates/HU-TEMPLATE.md`, `scripts/lint_hu.py` y `AGENTS.md` ya existen; además el lint corre en `ci.yml`).
> - Con `tests/Bootstrap.IntegrationTests`: **1.3** (log4net configurado dos veces), **2.1** (ERROR duplicado por cada 4xx) y **2.3** (`X-Correlation-ID` perdido en las respuestas de error), cada uno con su test. Además **2.2** queda documentado en `ARCHITECTURE.md` como una distinción intencional entre dos formatos de error (A y B); las diferencias de `Content-Type` y de la URL de `type` entre ambos siguen sin unificarse, y ahí se aclara. Y la deuda "sin tests de integración para `Bootstrap`" (base de **6.1**) ya no aplica; se movió al `CHANGELOG`.
> - En `fix/cerrar-huecos-skills-originales` (2026-09-30): **3.1–3.9** (3.5 solo parcialmente). Qué se demostró y qué no, en "Actualización (`fix/cerrar-huecos-skills-originales`, 2026-09-30)" dentro de la auditoría de seguimiento. En `docs/contrato-entre-modulos` (2026-10-16), **3.5** quedó resuelto como documentación y test de arquitectura; **NO existe ningún proyecto `Contracts` real**.
> - En `fix/consistencia-sample` (2026-10-13): **2.6** y **2.7**.
> - En estos puntos el informe describe el estado previo, a propósito.
>
> **Todo lo demás sigue pendiente.**

### Auditoría pre-lanzamiento de `dotnet-mhvs-template`

**Cómo se hizo:** revisé el código de `main` (`b771dd2`, ya con el PR #3 mergeado). Compilé y corrí los tests: 16 en verde. Levanté la app real (`dotnet run --project src/Bootstrap`) y probé 12 caminos infelices con curl. No modifiqué nada: los archivos temporales que creé los borré y `git status` está limpio.

**Lo que no pude verificar:**
- Docker: no está instalado.
- Configuración de GitHub: el repo es privado (404 sin autenticación) y la API de branch protection pide autenticación.
- Mac/Linux: solo probé en Windows.
- El camino `PersistenceException`: lo infiero por lectura, no lo ejecuté.

**Dos premisas del prompt de la auditoría no coincidían con el repo:**
- No existe `lint-hu.yml`. El único workflow es `ci.yml`, y sí corre `restore`, `build` y `test` en cada PR.
- Tampoco existían `AGENTS.md`, `scripts/`, `docs/`, `global.json` ni `.editorconfig`.

#### 1. Resumen ejecutivo

Las ideas de fondo son razonables, pero la capa `Bootstrap` tiene cuatro defectos de runtime reproducidos, y son exactamente los que el ROADMAP admite que nadie prueba. La promesa estrella (credenciales resueltas) está silenciosamente rota. Toda la gobernanza (hook, CODEOWNERS, revisiones, CI) es voluntaria: en un repo privado sin plan de pago nada impide mergear con CI en rojo. Los skills, que son el producto real, tienen huecos que las IAs replicarán en cada proyecto derivado. README y PRESENTATION prometen más de lo que el código hace.

#### 2. Hallazgos por categoría

##### Categoría 1. Onboarding en el camino infeliz

**1.1 Crítico. Las credenciales del README nunca se leen.**
- **Evidencia:** `src/Bootstrap/Program.cs:19` usa `AddJsonFile("config/connectionstrings.local.json", optional: true)`, que se resuelve contra el content root, es decir `src/Bootstrap/`. Lo probé con `Logging:LogLevel:Default=Critical`. En `config/` (raíz, lo que dice el README) el mensaje de arranque siguió apareciendo. En `src/Bootstrap/config/` desapareció.
- **Por qué importa:** con `optional: true` no hay error ni aviso. El primer módulo real con BD recibirá una cadena de conexión nula. El atajo previsible es meterla en `appsettings.json`, justo lo que la plantilla quiere evitar.
- **Corrección:** resolver la ruta desde la raíz del repo, o mover el `.example` a `src/Bootstrap/config/` y corregir README, `config/README.md` y ARCHITECTURE. Añadir un smoke test que lo verifique (ver 4.3).

**1.2 Alto. El hook está apagado por defecto y nada avisa.**
- **Evidencia:** `git config core.hooksPath` está vacío en este mismo clon, el del autor. Sin la activación manual, la protección es cero, sin ningún mensaje.
- **Por qué importa:** el hook es la única protección a nivel de máquina. Fallar en silencio se parece a estar protegido.
- **Corrección:** un target MSBuild en `Directory.Build.targets` que ejecute `git config core.hooksPath .githooks` si no está seteado. Complementar con detección posterior en CI (ver 4.1).

> **Resuelto en `chore/auto-activar-hook-y-completar-codeowners` (2026-09-25):** exactamente la corrección propuesta arriba — `Directory.Build.targets` en la raíz, con `BeforeTargets="Build"`, condicionado a que no haya `CI`/`GITHUB_ACTIONS` en `true`, que exista `.git`, y que `core.hooksPath` no tenga ya un valor. Verificado en los 4 escenarios: se activa solo cuando está vacío, no repite el mensaje en el segundo build, no pisa un valor custom ya configurado, y no hace nada con `CI=true`, `GITHUB_ACTIONS=true` ni sin `.git`.

**1.3 Medio. Error de log4net en cada arranque.**
- **Evidencia:** en el primer `dotnet run` aparece `log4net:ERROR [RollingFileAppender] ... Unable to acquire lock on file ...logs\app.log`. La causa es doble configuración en `Program.cs:25-29`: `XmlConfigurator.Configure` y `AddLog4Net` abren el mismo appender.
- **Por qué importa:** el README promete que "arranca funcionando", y lo primero que se ve es un ERROR.
- **Corrección:** quitar las líneas 25-26 (`AddLog4Net` ya configura), o usar `MinimalLock`.

**1.4 Medio. No hay paso para renombrar `MyApp`.**
- **Evidencia:** la sección "Cómo empezar tu proyecto real" no menciona namespaces, `AssemblyName`, el `.sln`, ni `MyApp.Bootstrap.dll` del `ENTRYPOINT` del Dockerfile. El `.sln` usa GUIDs de placeholder (`1111...`) que heredarán todos los proyectos.
- **Corrección:** documentar o automatizar el renombrado.

**1.5 Bajo. Docker.**
- **Evidencia:** el README solo menciona Docker en un paréntesis (puerto 8080). No dice que es opcional ni cómo usarlo, y `docker-compose.yml` no aparece en ningún documento.
- **Corrección:** una línea que diga que Docker es opcional.

**Revisé y no encontré fallas en:**
- **Hook en Mac/Linux:** `#!/usr/bin/env bash`, modo `100755` en el índice, LF forzado por `.gitattributes`, sintaxis POSIX.
- **Efecto secundario menor:** `core.hooksPath` desactiva `.git/hooks`, por ejemplo el hook de git-lfs. También bloquea `main` de un fork.

##### Categoría 2. Documentación vs. código

**2.1 Alto. Cada error de cliente genera un log ERROR con stack trace.**
- **Evidencia:** por cada 400/404 aparece `ERROR ... ExceptionHandlerMiddleware - An unhandled exception has occurred` (el middleware de .NET 8), más el `WARN` de `GlobalExceptionHandler`. Se ve en las líneas 10/38, 66/75 y 102/115 del log.
- **Por qué importa:** `GlobalExceptionHandler.cs:65-77` y ARCHITECTURE afirman que se evita el log duplicado. Es falso. Cualquier alerta por nivel ERROR se dispara con un simple 404. Por el mismo mecanismo, `PersistenceException` también se loguea dos veces (inferido).
- **Corrección:** apagar esa categoría en `appsettings.json` con `"Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware": "None"`, o mover a .NET 9. Añadir un test que lo verifique.

**2.2 Medio. El contrato de errores no es uniforme, aunque ARCHITECTURE dice que sí.**

| | Validación 400 | 400 por dominio / cuerpo mal formado |
|---|---|---|
| Content-Type | `application/problem+json` | `application/json` |
| `type` | `tools.ietf.org/...rfc9110` | `httpstatuses.com/400` |
| `traceId` | no | sí |

- **Otros casos sin contrato:** el 404 de ruta desconocida y el 415 devuelven cuerpo vacío.
- **Corrección:** un único camino con `IProblemDetailsService`.

**2.3 Medio. Las respuestas de error pierden el `X-Correlation-ID`.**
- **Evidencia:** en el 400 por JSON mal formado y en el 404 de `PUT /sample/xx/name` no viene el header. Sí viene en las respuestas exitosas y en la de validación.
- **Por qué importa:** ARCHITECTURE dice que "cada request" lo recibe. Justo en los errores hace falta. Solo queda el `traceId` del cuerpo.
- **Corrección:** setear el header con `OnStarting`, o re-añadirlo en el handler.

**2.4 Medio. El idioma de los errores depende de la máquina.**
- **Evidencia:** en este equipo la validación responde `'Name' no debería estar vacío.`. Con `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` responde `'Name' must not be empty.`. Además `Title` va en inglés y los mensajes de dominio en español.
- **Por qué importa:** el mismo código responde en distinto idioma según dónde corre, y los clientes no pueden depender de los textos.
- **Corrección:** fijar la cultura explícitamente o devolver códigos de error.

**2.5 Alto. "Cero referencias a ASP.NET en domain" no se cumple.**
- **Evidencia:** `Sample.csproj` referencia `Microsoft.AspNetCore.App` para todo el proyecto, y `Shared.csproj` también más FluentValidation. `Domain/` importa `Shared.Kernel`, que vive en el mismo ensamblado que `Shared/Web`. No hay tests de arquitectura.
- **Por qué importa:** la regla que la plantilla llama "no negociable" solo la vigila un skill de IA. Un `using Microsoft.AspNetCore` en `Domain/` compila sin problema.
- **Corrección:** separar `Shared.Kernel` de `Shared.Web` y añadir tests de arquitectura (NetArchTest), o un proyecto por capa.

**2.6 Medio. El slice de referencia "crear" viola la regla del propio skill.**
- **Evidencia:** `PingSampleHandler.cs` llama `AddAsync` sin `try/catch`. El skill dice "en cualquier slice que persista (`AddAsync` o `UpdateAsync`), envuelve...", y `architecture-review` lo marcaría.
- **Corrección:** alinear el ejemplo con la regla.
> **Resuelto en `fix/consistencia-sample` (2026-10-13):** `PingSampleHandler` envuelve `AddAsync` con el mismo patrón que `RenameSampleItemHandler`. Registra la excepción original con el id del item y lanza `PersistenceException("El item no fue creado.")`, que se traduce a 500. Lo fija `HandleAsync_StorageFailure_ThrowsPersistenceException_AndStoresNothing`.

**2.7 Medio. El dominio no aplica las "mismas reglas" que el validador.**
- **Evidencia (por lectura):** `SampleItem.Create` (`SampleItem.cs:16-22`) no limita a 100 caracteres ni recorta espacios, y `Rename` sí. Los tests solo cubren `Rename`, no hay test de `Create` con nombre vacío ni largo.
- **Por qué importa:** el código dice que el dominio es la última barrera.
- **Corrección:** compartir la normalización y añadir los tests.
> **Resuelto en `fix/consistencia-sample` (2026-10-13):** `Create` y `Rename` usan el mismo método privado `NormalizeName`, que recorta, rechaza vacío y rechaza más de `MaxNameLength`, con los mensajes que ya usaba `Rename`. `SampleItemTests` cubre ahora `Create` con null, `""`, `"   "`, recorte, 100 caracteres (válido) y 101 (inválido).

**2.8 Medio. PRESENTATION.md está desactualizado.**
- **Evidencia:** afirma ".NET 8 / .NET 10, una línea por `.csproj`". Falso: el Dockerfile fija `sdk:8.0` y `aspnet:8.0`, `ci.yml` fija `8.0.x` y hay un paquete `Log4Net.AspNetCore 8.0.0`. Tampoco menciona contrato de errores, hooks, skills ni ROADMAP.
- **Corrección:** actualizarlo o corregir esa promesa.

**2.9 Medio. Referencias colgantes.**
> **En `docs/limpieza-documentacion` (2026-10-15):** la cita de `checklist-release-template.md` en `CODEOWNERS` se cambió por `CONTRIBUTING.md`; `opencode-prompt-mhvs.md` queda marcado como referencia histórica y `HU-SAMPLE-002` como ID ilustrativo. Sigue abierto: `RenameSampleItemCommand` sin `/// Implementa HU-...`.
- **En el repo no existen:** `docs/templates/HU-TEMPLATE.md` (citado por `hu-validation`), `AGENTS.md`, `scripts/`, `docs/templates/` (en CODEOWNERS), `checklist-release-template.md`, `opencode-prompt-mhvs.md` y `HU-SAMPLE-002`.
- **Sin cumplir:** el skill exige `/// Implementa HU-...` en el `Command`, y `RenameSampleItemCommand` no lo tiene.

**2.10 Bajo. Otras discrepancias.**
> **En `docs/limpieza-documentacion` (2026-10-15):** casing corregido; el checklist de `CONTRIBUTING.md` ahora enlaza a la fuente única de ARCHITECTURE; tests de infraestructura y `dotnet format` documentados como inexistentes o no impuestos (solo texto, sin `.editorconfig` ni pasos de CI nuevos).
- **Checklist de PR:** el de `CONTRIBUTING.md` tiene 5 ítems y el de ARCHITECTURE 7 (faltan `.sln` y contrato HTTP).
- **Tests de infraestructura:** CONTRIBUTING dice que van separados y no corren en cada build, pero no existe ningún mecanismo.
- **`dotnet format`:** lo exige, pero no hay `.editorconfig` y CI no lo verifica.
- **Casing:** ARCHITECTURE usa `domain/` y `application/` en minúscula, y las carpetas reales van en PascalCase.

**Verificado sin discrepancias:** el árbol de `Sample`, comparado archivo por archivo, y los códigos 400/404 que la tabla de contrato documenta.

##### Categoría 3. Skills, con casos adversariales

**`hu-validation`**
- **3.1 Alto. El conteo de reglas contra CA de fallo tiene falsos negativos.** Caso: una HU con 3 reglas y 3 CA de fallo, donde dos CA ejercen la regla 1 y la 3 queda sin cubrir. El checklist dice "cuenta las reglas, cuenta los CA" y pasa. El texto pide "al menos uno por regla", pero el procedimiento es contar. Además la excepción de "invariante no violable" es subjetiva.
- **3.2 Medio. Contradicciones que el checklist no atrapa.** Caso: Datos de entrada dice "máx 100", Regla 2 dice "máx 50", los CA usan 100. O el Alcance excluye "items archivados" y el Flujo principal tiene un paso que los restaura. No hay ítem que cruce Datos ↔ Reglas ↔ Flujo, ni uno para la sección "Errores que deben comunicarse", que el propio formato incluye. La comparación con el Alcance es unidireccional y solo contra los CA.
- **3.3 Medio. Choque entre skills.** `hu-validation` prohíbe que un CA mencione HTTP. ARCHITECTURE pide que un caso con otra precedencia (validación vs. no encontrado) la declare "en sus criterios de aceptación", y no explica cómo escribirlo sin HTTP. El autor cae en un rechazo o en omitirlo.
- **3.4 Medio. Nota de documentos externos.** Una Regla del tipo "el cálculo sigue DT-042" se clasifica como informativa. Solo es bloqueante si la HU es "ambigua sin ese documento", y eso queda a criterio del modelo.

**`hu-to-slice-scaffold`**
- **3.5 Alto. HU que toca dos módulos: el skill calla.**
  - **En `docs/contrato-entre-modulos` (2026-10-16):** resuelto como documentación y test de arquitectura; **NO existe ningún proyecto `Contracts` real**. El mecanismo está en `ARCHITECTURE.md` → "Contrato entre módulos", y lo verifica `ArchitectureTests`. El texto de abajo describe el estado de la auditoría.
  - **Qué dice hoy:** solo prohíbe mezclar dos historias en un slice y pregunta si el nombre del módulo es ambiguo.
  - **Qué falta:** ARCHITECTURE habla de "contrato público (interfaz/evento)" entre módulos, pero no existe mecanismo, carpeta ni ejemplo. `architecture-review` prohíbe `using MyApp.Modules.{Otro}`.
  - **Resultado:** el agente improvisa (ProjectReference directo, copiar la entidad o meter lógica en `Shared`).
  - **Corrección:** instrucción explícita de detenerse y preguntar, y definir el mecanismo.
- **3.6 Alto. Módulo nuevo incompleto.** El paso 4 del flujo no incluye `.csproj`, `dotnet sln add`, `ProjectReference` en `Bootstrap.csproj`, `COPY` en el Dockerfile, `Program.cs` ni proyecto de tests. ARCHITECTURE los exige. Ya se rompió en la vida real (bug `Tickets` del demo). CI compila en verde y la imagen Docker falla, sin que nadie lo vea porque CI no construye Docker.
- **3.7 Alto. Contrato HTTP sin 409.** El skill dice "SIEMPRE 500" y envuelve `catch (Exception)`. Una regla como "no puede haber dos facturas con el mismo folio" acaba en 500, igual que un bug de programación o un `DomainException` lanzado por el adaptador. El contrato no tiene 409, 401 ni 403.
- **3.8 Medio. El skill genera la deuda del ROADMAP.** Manda calcular "valores generados por el sistema" dentro de `Create` (o sea `DateTime.UtcNow`, deuda `IClock`) y manda `MemberwiseClone()` sin la advertencia que el ROADMAP pide.

**`architecture-review`**
- **3.9 Alto. Cobertura del historial.** Cosas que ya se rompieron y que el checklist no cazaría:
  - **Dockerfile:** `COPY` del `.csproj` y `ProjectReference` de `Bootstrap` (bug real, ítem "Registro y composición").
  - **Precedencia:** validación contra "no encontrado".
  - **Dominio y reloj:** `DateTime.UtcNow` en el dominio en vez de `IClock`.
  - **`MemberwiseClone`:** copia superficial con colecciones.
  - **Referencias entre módulos:** solo busca `using`, no `ProjectReference`. (**En `docs/contrato-entre-modulos` (2026-10-16):** además, `ArchitectureTests` lo comprueba automáticamente en los `.csproj` y en los ensamblados.)
  - **Pruebas de `Bootstrap`:** solo exige tests unitarios.
- Lo que la propia app hace en runtime (2.1, 2.2, 2.3) un review de diff tampoco lo ve.
- **Nota:** que `metadata: opencode/autoinvoke` tenga algún efecto no se puede verificar desde el repo, y no había `AGENTS.md` que indicara cuándo usar cada skill.

##### Categoría 4. Seguridad y gobernanza

**4.1 Alto. Ninguna protección es obligatoria.**
- **CODEOWNERS:** solo tiene efecto real con "Require review from Code Owners" en una branch protection, que este repo no puede activar. Es un recordatorio. Aun con branch protection, el único dueño es el autor, y GitHub no deja que un autor apruebe su propio PR (bloquearía los merges o exigiría bypass de admin).
- **Huecos de cobertura:** no cubre `/src/Shared/` ni `/src/Bootstrap/` (CONTRIBUTING exige 2 aprobaciones ahí), ni `/CODEOWNERS`, `/README.md`, `/ROADMAP.md`, `/Dockerfile`, `/docker-compose.yml` ni `/.githooks/`. Cuatro de las rutas que sí lista no existían.
> **Resuelto en `chore/auto-activar-hook-y-completar-codeowners` (2026-09-25):** las 7 rutas del hueco de cobertura ya están en `CODEOWNERS` (`/src/Shared/`, `/src/Bootstrap/`, `/README.md`, `/ROADMAP.md`, `/Dockerfile`, `/docker-compose.yml`, `/.githooks/`), cada una verificada contra el árbol real antes de agregarla, con un comentario explicando por qué esa ruta merece protección. **Lo que sigue sin resolver, y no tiene solución técnica en este repo:** todo lo demás de este hallazgo — sin plan de pago no hay "Require review from Code Owners" que hacerlo cumplir, así que `CODEOWNERS` sigue siendo un recordatorio visual, no una barrera. Documentado ahora explícitamente en `CONTRIBUTING.md`, junto a la explicación de por qué el hook de pre-push sí es una barrera técnica y esto no.
- **CI:** `ci.yml` existe, así que el escenario grave del prompt no aplica. En un repo privado sin plan de pago no se puede exigir como check, así que un rojo no impide mergear.
- **Sobreventa en docs:** CONTRIBUTING dice "todo pasa por PR, sin excepción" (hay `ALLOW_MAIN_PUSH=1` y `--no-verify`). Presenta como reglas "mínimo 1 aprobación", "el autor no se aprueba" y "2 aprobaciones en Shared/Bootstrap". Nada de eso se hace cumplir. Ojo: el propio archivo aclara que el hook "no reemplaza la protección de GitHub", así que la sobreventa es parcial.
- **Corrección:** pasar a una organización con plan Team (o hacer público) y activar rulesets con CI requerido. Interino: un workflow en `push` a `main` que falle si el commit no viene de un PR.

**4.2 Medio. Credenciales de ejemplo en el repo.**
- **Evidencia:** `docker-compose.yml:10` y `:18` (`Your_password123`). Además `:20` expone el puerto 1433 al host y `:9` fija `ASPNETCORE_ENVIRONMENT: Development`.
- **Por qué importa:** es un placeholder de desarrollo. Aun así contradice "ninguna credencial hardcodeada" y PRESENTATION ("nunca más una credencial"). El checklist de `architecture-review` solo mira `appsettings.json`.
- **Otros:** el Dockerfile corre como root (sin `USER`) y no tiene `HEALTHCHECK`.
> **Diferido (2026-09-30):** todo 4.2 depende de Docker y queda en "Docker, diferido a largo plazo", por decisión explícita.

**4.3 Medio. CI no cubre lo que ya falló.** El workflow no tiene `permissions:`, las acciones están fijadas por tag y no por SHA, no hay Dependabot y no se construye Docker. Tampoco hay smoke test de la app, que habría cazado 1.1, 1.3, 2.1 y 2.3. Sí revisé vulnerabilidades: `dotnet list package --vulnerable` no reporta ninguna hoy.

**4.4 Medio. Inyección en logs.**
- **Evidencia:** un `name` con `\n` produjo una línea falsa `FORGED ... ERROR [admin] Security - login ok` en `logs/app.log`. Un `X-Correlation-ID` de 4000 caracteres se aceptó, se devolvió y se logueó completo. El `traceId` del cuerpo es controlable por el cliente.
- **Corrección:** validar longitud y charset del header, y sanear lo que va al log.
> **Resuelto en `fix/sanear-logs-y-gitignore` (2026-09-30):**
> - Los layouts de `log4net.config` usan `%safemessage` (`Bootstrap/Logging/SafeMessagePatternConverter.cs`), que escapa `\r`/`\n` y los separadores Unicode del mensaje. Es un solo punto que cubre todos los logs, porque log4net es el único proveedor.
> - `CorrelationIdMiddleware` solo reutiliza el header del cliente si es un único valor de hasta 128 caracteres con `[A-Za-z0-9-]`. Si no, genera uno nuevo y nunca rechaza la request. Con eso, el `traceId` del cuerpo deja de ser arbitrario.
> - `LogInjectionTests` verifica las dos cosas contra el archivo de log real y el header devuelto. Con las correcciones revertidas, los dos tests fallan.
> - Límite conocido: `%exception` no se sanea (ver ARCHITECTURE.md → "Logging").

**4.5 Medio. Patrones sensibles no cubiertos por `.gitignore`.**
- **Qué cubre hoy:** `bin/`, `obj/`, `*.user`, `config/connectionstrings.local.json`, `config/secrets.local.json`, `*.local.json`, `.vs/`, `.idea/`, `*.suo`, `logs/`, `*.log`.
- **Qué falta:**
  - `.env` y `.env.*`
  - `*.pfx`, `*.pem`, `*.key`, `*.p12`
  - `*.pubxml`
  - `docker-compose.override.yml`
  - `*.tfvars`
  - `*.bak`, `*.mdf`, `*.sqlite`
  - `.vscode/`, `TestResults/`
- Todo depende de que la gente nombre sus archivos `*.local.json`. `.dockerignore` tampoco excluye `.env`.
> **Resuelto en `fix/sanear-logs-y-gitignore` (2026-09-30):**
> - **Agregados a `.gitignore`:** todos los patrones de la lista que no dependen de Docker: `.env`, `.env.*`, `*.pfx`, `*.pem`, `*.key`, `*.p12`, `*.pubxml`, `*.tfvars`, `*.bak`, `*.mdf`, `*.sqlite`, `.vscode/` y `TestResults/`.
> - **Pasados al ítem de Docker:** `docker-compose.override.yml` y `.env` en `.dockerignore`. No quedan pendientes dentro de 4.5, sino en "Docker, diferido a largo plazo".

**Revisé y no encontré fallas en:** el historial completo de git, sin secretos reales (solo los placeholders `CAMBIA_ESTO` y `Your_password123`).

##### Categoría 5. Residuos de contexto específico

**Explícitos**
- `Program.cs:40` menciona la "plantilla de referencia (Auth/Courses/Products)".
- ROADMAP describe la deuda en términos de `Ticket`, `AssignTicket`, `HU-TICKETS-002` y `mhvs-demo-tickets`.
- CODEOWNERS lleva el usuario personal y una referencia a otro documento.

**Implícitos (decisiones de un sistema de tickets, aplicadas a cualquier dominio)**
- **5.1 Alto. Sin concurrencia ni conflicto.**
  - `UpdateAsync` es un upsert a ciegas (`Store[item.Id] = item`) y "buscar + modificar" es último-en-escribir-gana. En facturación o inventario se pierden actualizaciones.
  - Un fallo de unicidad devuelve 500 (ver 3.7). Nada de esto está en el ROADMAP.
- **5.2 Medio. Sin transacciones.** Ninguna guía para un caso de uso que modifica dos agregados (factura y asiento contable).
- **5.3 Medio. Sin identidad.** No hay `ICurrentUser` ni auditoría (quién y cuándo), y los endpoints son anónimos.
- **5.4 Medio. Repositorio en memoria.** El almacén es `static` pero se registra `Scoped`. Comparte estado entre requests y entre los tests de integración que el ROADMAP quiere agregar.
- **5.5 Bajo. Nombres de puertos.** `SampleItemRepository` es una interfaz sin prefijo `I`, mientras `IClock` sí lo lleva. El skill genera `{Entidad}Repository` sin `I` en cada proyecto.
- **Cómo lo verifiqué:** por lectura del código, no del repositorio de tickets.

##### Categoría 6. Honestidad del alcance

**6.1 Alto. Lo que se promete frente a lo que el ROADMAP admite.**
- El ROADMAP dice que `Bootstrap` es "la única capa sin cobertura automatizada". Todos los defectos de runtime de este informe (1.1, 1.3, 2.1, 2.2, 2.3) viven ahí. La deuda no es cosmética: es el requisito previo para llamarla estándar.
- README dice "cualquier proyecto nuevo... configuración de credenciales ya resuelta" (falso, ver 1.1). PRESENTATION dice "Testeable de verdad" y "Seguridad desde el día 1" (ver 2.8 y 4.2).
- Ni README ni PRESENTATION enlazan el ROADMAP ni avisan: solo repo en memoria, sin auth, sin HTTPS/HSTS, sin CORS, sin persistencia real, sin tests de integración.

**6.2 Medio. No hay camino de actualización.** Los arreglos (como el de `.dockerignore`) no llegan a los proyectos ya derivados. El CHANGELOG y la línea de versión del README son manuales. Con "decenas de proyectos", cada defecto se corrige decenas de veces a mano.

#### 3. Los 3 que corregiría primero

1. **1.1 más un smoke test en CI que arranque la app real.**
   - **Por qué:** es la promesa central, está rota sin ningún aviso y se copia a cada proyecto. El smoke test (arrancar la app, `/health`, un 400, un 404, y comprobar que el archivo local de config se lee) cierra de una vez 1.3, 2.1, 2.2 y 2.3, y activa el ítem del ROADMAP con más valor.
2. **4.1: gobernanza que se pueda exigir.**
   - **Por qué:** hoy hook, CODEOWNERS, revisiones y CI son voluntarios. Sin esto, todo lo demás se degrada con el tiempo y nadie se entera. Lo interino es barato: hook autoactivado por MSBuild y un workflow que detecte pushes directos a `main`.
3. **3.5, 3.6 y 3.7: los huecos del skill de scaffold.**
   - **Por qué:** los skills generan código a escala. Multi-módulo, módulo nuevo y conflictos de negocio son los casos donde un agente improvisa y el error se replica sin que nadie lo revise.
   - **En `docs/contrato-entre-modulos` (2026-10-16):** 3.5 resuelto como documentación y test de arquitectura; **NO existe ningún proyecto `Contracts` real**.
   - **Por qué no otros:** el log injection (4.4) y la deriva de documentación (2.8, 2.10) tienen menor radio de daño y son correcciones baratas para después.

No se aplicó ninguna corrección en el momento de la auditoría.

#### Verificación posterior (mismo día)

Se comprobó con `git cat-file` y `git log --all` que `scripts/lint_hu.py`, `docs/templates/HU-TEMPLATE.md` y `AGENTS.md` no existían ni en el disco, ni en `HEAD`, ni en el historial de ninguna rama. `.opencode/skills/hu-validation/SKILL.md:25` citaba `docs/templates/HU-TEMPLATE.md` ("Si el repo trae una plantilla en blanco (por ejemplo `docs/templates/HU-TEMPLATE.md`)..."); el skill solo mencionaba de forma genérica "un linter en CI" (líneas 13-14), sin nombrar `lint_hu.py`. `CODEOWNERS:21` listaba `/AGENTS.md` y `CODEOWNERS:36` `/docs/templates/`, rutas inexistentes. Corrección al informe: la ausencia de `scripts/lint_hu.py` no se había verificado antes de esta comprobación.

---

## Auditoría de seguimiento (2026-09-25)

> **Estado de este registro.** Reconcilia, hallazgo por hallazgo, la auditoría de pre-lanzamiento de arriba contra el estado real del código en `main` (`99ac24b`, PR #7), verificado de nuevo — no asumido por lo que el propio ROADMAP decía. Agrega una categoría 7 nueva (preparación empresarial). Se pega completo, igual que el informe original.
>
> **Resuelto en `feat/https-hsts-y-punto-extension-auth` (2026-09-25):** **7.3** (HTTPS/HSTS con `UseForwardedHeaders`, CORS explícito, rate limiting) y **7.4** (punto de extensión de autenticación documentado y exigido por el skill de revisión).
>
> **Resuelto en `chore/endurecer-ci-y-escaneo-vulnerabilidades` (2026-09-25):** **7.6** (`.github/dependabot.yml` para NuGet y GitHub Actions, semanal; paso nuevo en `ci.yml` que corre `dotnet list package --vulnerable` y falla el build si encuentra algo) y el resto de **4.3** que quedaba abierto (`permissions: contents: read` a nivel de workflow; las dos actions pineadas por SHA de commit completo, con comentario de a qué tag corresponden). Sigue sin resolver de 4.3: build de Docker en CI.
>
> **Resuelto en `chore/auto-activar-hook-y-completar-codeowners` (2026-09-25):** **1.2** (`Directory.Build.targets` activa `core.hooksPath` solo en el primer build local, sin pisar un valor ya configurado a propósito, y sin hacer nada en CI ni sin repo git) y **4.1** (las 7 rutas que faltaban en `CODEOWNERS` ya están, todas verificadas contra el árbol real; queda documentado en `CONTRIBUTING.md` que `CODEOWNERS` sigue siendo un recordatorio visual, no una barrera técnica, a diferencia del hook).
>
> **Resuelto en `feat/portar-adaptador-mysql-real` (2026-09-29):** **7.5** — módulo `Tasks` (`src/Modules/Tasks`, HU-TASKS-001) portado desde `mhvs-todo-app` como ejemplo de referencia de persistencia real: MySQL vía ADO.NET puro (`MySqlConnector`), sin ORM. Coexiste con `Sample` (no lo reemplaza); documentado en `ARCHITECTURE.md` → "Dos ejemplos de referencia: `Sample` vs. `Tasks`".
>
> **Resuelto en `fix/cerrar-huecos-skills-originales` (2026-09-30):** **3.1–3.9** de la auditoría de pre-lanzamiento, con **3.5** solo parcialmente. Son correcciones a los tres skills originales (`hu-validation`, `hu-to-slice-scaffold`, `architecture-review`). Ver "Actualización (`fix/cerrar-huecos-skills-originales`, 2026-09-30)" más abajo. **3.5** se completó en `docs/contrato-entre-modulos` (2026-10-16), como documentación y test de arquitectura; **NO existe ningún proyecto `Contracts` real**.
>
> **Resuelto en `feat/endpoint-version` (2026-09-30):** **7.9** — `GET /version` expone versión y commit de git. En Docker, la imagen sigue respondiendo `commit: null`, porque `.dockerignore` excluye `.git/`. El mecanismo para arreglarlo (`SourceRevisionId` como build-arg) ya se verificó, pero el `Dockerfile` queda sin cambiar a propósito. Ver la nota junto al hallazgo.
>
> **Resuelto en `fix/sanear-logs-y-gitignore` (2026-09-30):**
> - **4.4** (inyección en logs y `X-Correlation-ID` sin límite).
> - **4.5** (`.gitignore`). Sus dos patrones que dependen de Docker pasaron al ítem de Docker.
> - Todo lo de Docker (4.2, lo de Docker de 4.3 y 4.5, 1.5 y el `Dockerfile` de 7.9) queda consolidado en "Docker, diferido a largo plazo", por decisión explícita.
>
> **Resuelto en `fix/consistencia-sample` (2026-10-13):** **2.6** (`PingSampleHandler` sin `try/catch` en `AddAsync`) y **2.7** (`SampleItem.Create` sin la normalización de `Rename`). Ver las notas junto a cada hallazgo.
>
> **Resuelto en `docs/secretos-en-produccion` (2026-10-14):** **7.1**, como documentación y punto de extensión; no hay integración real con Key Vault. Ver la nota junto al hallazgo.
>
> **Resuelto en `docs/multi-entorno` (2026-10-14):** **7.8**, como documentación, `appsettings.Staging.json` y tests. Ver la nota junto al hallazgo.
>
> **En `docs/observabilidad` (2026-10-15):** **7.2**, resuelto como documentación y punto de extensión; NO hay OpenTelemetry implementado. Ver la nota junto al hallazgo.
>
> **En `docs/limpieza-documentacion` (2026-10-15):** limpieza de documentación. 2.9 y 2.10 avanzan, y cuatro ítems de deuda y mejoras quedan como resuelto por trabajo previo. Ver "Actualización (`docs/limpieza-documentacion`, 2026-10-15)".
>
> El detalle de qué se implementó vive en `CHANGELOG.md`; los hallazgos se dejan anotados como resueltos, no se borran.

### Reconciliación de la auditoría de pre-lanzamiento (1.1–6.2)

| # | Estado | Evidencia de la verificación de hoy |
|---|---|---|
| 1.1 | Resuelto | `dotnet run`: sin archivo → `WARN No se encontró ...`; con archivo en `config/` de la raíz → `INFO Configuración local cargada desde ...`. |
| 1.2 | Resuelto en `chore/auto-activar-hook-y-completar-codeowners` | `Directory.Build.targets` activa `core.hooksPath` solo en el primer build local; verificado en 4 escenarios (vacío, ya configurado, `CI=true`, sin `.git`). |
| 1.3 | Resuelto | 0 líneas `log4net:ERROR` en varios arranques. `Bootstrap.IntegrationTests` lo fija (`Startup_EmitsNoLog4NetInternalErrors`). |
| 1.4 | Abierto | README sigue sin mencionar namespaces ni `AssemblyName` al renombrar `MyApp`. |
| 1.5 | Abierto | README menciona Docker de pasada; no dice que es opcional ni referencia `docker-compose.yml`. |
| 2.1 | Resuelto | 0 líneas `ERROR` en consola tras 4 tipos de petición de error. `appsettings.json` silencia solo `ExceptionHandlerMiddleware`. |
| 2.2 | Resuelto parcial | `ARCHITECTURE.md` documenta dos formatos (A/B) como intencionales. La diferencia de `Content-Type` sigue sin unificarse (admitido en el propio documento); el 404 de ruta desconocida y el 415 siguen con cuerpo vacío, sin documentar. **Resuelto por trabajo previo (`docs/limpieza-documentacion`, 2026-10-15):** el 404 de ruta inexistente ya está documentado (ARCHITECTURE.md → "Observabilidad", 7.2); el 415 sigue sin documentar. |
| 2.3 | Resuelto | `X-Correlation-ID` presente en las 4 respuestas de error probadas (antes faltaba en 2). |
| 2.4 | Abierto | Sin `CultureInfo`/`RequestLocalization`/`InvariantGlobalization` en `Program.cs`. |
| 2.5 | Abierto | `Shared.csproj`/`Sample.csproj` siguen con `FrameworkReference Microsoft.AspNetCore.App` a nivel de proyecto completo; sin tests de arquitectura. |
| 2.6 | Abierto → **resuelto en `fix/consistencia-sample` (2026-10-13)** | `PingSampleHandler.HandleAsync` sigue sin `try/catch` alrededor de `AddAsync`. Ahora lanza `PersistenceException` (500), con test. |
| 2.7 | Abierto → **resuelto en `fix/consistencia-sample` (2026-10-13)** | `SampleItem.Create` sigue sin `Trim()` ni el límite de 100 caracteres que `Rename` sí aplica. Ahora comparte `NormalizeName` con `Rename`, con tests de `Create`. |
| 2.8 | Resuelto parcial | Las cifras de .NET se corrigieron; PRESENTATION sigue sin mencionar el contrato de errores, los hooks, los skills ni el ROADMAP. |
| 2.9 | Resuelto parcial | `docs/templates/HU-TEMPLATE.md`, `AGENTS.md`, `scripts/lint_hu.py` ya existen. `checklist-release-template.md`, `opencode-prompt-mhvs.md` y `HU-SAMPLE-002` siguen colgando (documentado como residuo cosmético, no arreglado). **En `docs/limpieza-documentacion` (2026-10-15):** los tres quedaron resueltos como texto (ver la nota junto a 2.9). |
| 2.10 | Abierto, brecha creció | CONTRIBUTING: 5 ítems. ARCHITECTURE: 9 ítems (creció dos veces desde la auditoría original). Sin `.editorconfig`. `domain/`/`application/` siguen en minúscula en ARCHITECTURE contra `Domain/`/`Application/` reales. **En `docs/limpieza-documentacion` (2026-10-15):** casing y checklist resueltos; `.editorconfig` sigue sin existir (ver la nota junto a 2.10). |
| 3.1–3.9 | Abiertos, sin cambios → **resueltos en `fix/cerrar-huecos-skills-originales`** (3.5 parcial) | `git diff b771dd2 -- .opencode/skills/` (antes de esta rama) solo mostraba el cambio `.sln`→`.slnx`; nada de multi-módulo, 409/401/403, ni la advertencia de `MemberwiseClone`/`IClock`. Qué quedó demostrado en cada punto: ver "Actualización (`fix/cerrar-huecos-skills-originales`, 2026-09-30)". **En `docs/contrato-entre-modulos` (2026-10-16):** 3.5 resuelto como documentación y test de arquitectura; NO existe ningún proyecto `Contracts` real. |
| 4.1 | Resuelto parcial → cobertura de rutas completa en `chore/auto-activar-hook-y-completar-codeowners` | Las 15 rutas de `CODEOWNERS` existen y están verificadas contra el árbol real. Sigue sin resolver, y sin solución técnica posible en este repo: `CODEOWNERS` no es exigible sin plan de pago (documentado explícitamente en `CONTRIBUTING.md`). **En `chore/codeowners-archivos-sensibles` (2026-10-15):** ahora son **21 rutas**. Se agregaron `/.gitignore`, `/.dockerignore`, `/Directory.Build.targets`, `/global.json`, `/MyApp.slnx` y `/CODEOWNERS`. `/CODEOWNERS` estaba en el hueco original y se había omitido al declarar la cobertura completa; este PR lo cierra. Sigue siendo un recordatorio visual, no un control. |
| 4.2 | Abierto → **diferido a largo plazo** (ver "Docker, diferido a largo plazo") | `docker-compose.yml` sigue con `Your_password123`, puerto 1433 expuesto, sin `USER`/`HEALTHCHECK` en el Dockerfile. |
| 4.3 | Resuelto parcial → resuelto casi del todo en `chore/endurecer-ci-y-escaneo-vulnerabilidades` | 2 pasos de test, `global-json-file`, `permissions: contents: read`, actions pineadas por SHA, `dependabot.yml`, escaneo de vulnerabilidades en cada PR. Sigue sin build de Docker en CI. |
| 4.4 | Abierto → **resuelto en `fix/sanear-logs-y-gitignore`** | Reproducido de nuevo: `\n` en `name` sigue forjando una línea en `logs/app.log`; `X-Correlation-ID` de 4000 caracteres se sigue aceptando. Ahora se corrige con `%safemessage` + validación del header; lo fija `LogInjectionTests`. |
| 4.5 | Abierto → **resuelto en `fix/sanear-logs-y-gitignore`** | `.gitignore`/`.dockerignore` sin cambios; siguen faltando `.env`, `*.pfx`/`*.pem`/`*.key`, etc. Ahora `.gitignore` tiene todos los patrones que no dependen de Docker; los dos que sí dependen pasaron al ítem "Docker, diferido a largo plazo". |
| 5.1–5.5 | Abiertos, sin cambios | `InMemorySampleItemRepository.UpdateAsync` sigue siendo upsert a ciegas; repositorio `static` con registro `Scoped`; sin `ICurrentUser`; sin prefijo `I` en `SampleItemRepository`. |
| 6.1 | Resuelto parcial | "Configuración de credenciales ya resuelta" del README ya no es falsa (1.1 corregido). "Testeable de verdad"/"Seguridad desde el día 1" de PRESENTATION siguen sin matizar frente a 2.5/4.2. |
| 6.2 | Abierto | CHANGELOG y línea de versión del README (sigue en `v1.1.0`, varios commits detrás de `main`) siguen siendo manuales. |

**Resumen de la reconciliación (antes de esta rama):** de 36 hallazgos, 8 resueltos, 8 resueltos parciales, 20 abiertos sin tocar.

### Categoría 7 — Preparación empresarial

**7.1 Baja → Resuelto en `docs/secretos-en-produccion` (2026-10-14), como documentación y punto de extensión; no hay integración real con Key Vault.** `config/README.md:33` menciona Key Vault/GitHub Secrets, pero sin ejemplo de la convención de nombres (`Cors__AllowedOrigins__0`, doble guion bajo) ni de cómo se integraría un proveedor real (`AddAzureKeyVault`) en `Program.cs`.
- **Resuelto como documentación y punto de extensión; no hay integración real con Key Vault:**
  - `config/README.md`:
    - la regla `:` → `__`;
    - una tabla de las tres claves del `.example`, con una columna que dice si el código las consume (solo `ConnectionStrings:MysqlTodo`);
    - ejemplos en bash y PowerShell, con placeholders;
    - la precedencia real de los proveedores de configuración.
    - Además se corrigió la frase que presentaba Key Vault como si ya funcionara.
  - `ARCHITECTURE.md` → "Configuración y credenciales": el punto de extensión. Un gestor de secretos se conectaría junto a `AddEnvironmentVariables()` en `Program.cs`, igual que el de autenticación.
  - `Program.cs`: solo se completó el comentario del bloque de configuración.
  - No se agregaron paquetes NuGet ni código de Key Vault.
- **Tests:** `ProductionSecretsConfigurationTests` fija dos cosas:
  - `ConnectionStrings__X` llega como `ConnectionStrings:X`. Falla si se usa `_` en lugar de `__`.
  - La variable de entorno gana sobre el archivo local. Falla si se quita el `AddEnvironmentVariables()` de `Program.cs`.
- **Verificado a mano con la app publicada en una carpeta temporal:**
  - Sin cadena de conexión: 500, y el log dice "Falta la cadena de conexión ConnectionStrings:MysqlTodo".
  - Con la variable de entorno: el error pasa a ser un fallo de conexión de MySQL.
  - Con un archivo local falso y la variable a la vez: gana la variable.

**7.2 Alta → En `docs/observabilidad` (2026-10-15): resuelto como documentación y punto de extensión; NO hay OpenTelemetry implementado.** Sin OpenTelemetry/Application Insights/Prometheus/trazas distribuidas, ni implementado ni declarado "fuera de alcance a propósito". El `X-Correlation-ID` no se propaga a llamadas salientes.
- **Qué se hizo (resuelto como documentación y punto de extensión; NO hay OpenTelemetry implementado):**
  - `ARCHITECTURE.md` → "Observabilidad":
    - qué hay hoy: el correlation ID, sus límites y dónde aparece (header en todas las respuestas, logs, `traceId` solo en el Formato B; un 404 de ruta inexistente sale sin cuerpo);
    - qué no hay: trazas distribuidas, métricas, exportadores, propagación a llamadas salientes (hoy no hay ninguna);
    - `X-Correlation-ID` y `traceparent` son independientes hoy. El correlation ID manda en todo lo visible, y el `Activity.TraceId` existe pero nada lo expone. Que el hosting adopta el `traceparent` como padre de la `Activity` solo se demostró bajo `TestServer`;
    - el punto de extensión: `OpenTelemetry.Extensions.Hosting` con exportador OTLP, registrado como servicio junto a `AddHealthChecks()` y antes de `builder.Build()`, sin ningún `app.Use`;
    - la convención (correlation ID para soporte, `traceparent` para la herramienta de trazas), la decisión abierta de cómo enlazarlos y la regla para llamadas salientes.
  - `ARCHITECTURE.md` → "Logging": se corrigió la frase sobre el patrón de log4net. Lo que llega es `LogicalThreadContext["CorrelationId"]`, no el scope de `ILogger`.
  - `Program.cs`: solo un comentario que marca el punto de inserción.
  - Tests: `TraceparentCorrelationTests` (9). Con `traceparent` válido o basura, la solicitud no falla y el correlation ID se mantiene (también en un 404). El correlation ID generado no es el trace-id del `traceparent`.
- **Sigue sin hacer:** OpenTelemetry, métricas, exportadores y el reenvío del correlation ID a llamadas salientes. Hoy no hay llamadas salientes; el primer `HttpClient` que se agregue tiene que traerlo.

**7.3 Alta → Resuelto en esta rama.** Sin `UseHttpsRedirection`/`UseHsts`/`AddCors`/`AddRateLimiter` en `Program.cs`; sin headers de seguridad; sin política CORS explícita; el límite de tamaño de request era el default de Kestrel (30MB), no uno decidido por la plantilla.
- **Resuelto:** `UseForwardedHeaders()` (confía en `X-Forwarded-Proto`/`X-Forwarded-For` del proxy, con el caveat de `KnownProxies`/`KnownNetworks` documentado), `UseHttpsRedirection()`/`UseHsts()` fuera de `Development`, política CORS nombrada "Default" sin orígenes por defecto (configurable vía `Cors:AllowedOrigins`), y un limitador de tasa global nativo (`Microsoft.AspNetCore.RateLimiting`, sin paquete NuGet), particionado por IP, configurable vía `RateLimiting:PermitLimit`/`WindowSeconds`. Todo documentado en ARCHITECTURE.md → "HTTPS y el proxy delante del contenedor", "CORS" y "Rate limiting", y cubierto por `Bootstrap.IntegrationTests` (`HttpsHstsTests`, `CorsPolicyTests`, `RateLimitingTests`).
- **Sigue sin resolver:** el tamaño de request sigue siendo el default de Kestrel (30MB), sin una decisión explícita documentada para este template.

**7.4 Alta → Resuelto en esta rama.** Sin ninguna mención a `AddAuthentication`/`AddAuthorization`/`[Authorize]`/`AllowAnonymous` en `Program.cs` ni en ARCHITECTURE.md; los endpoints de `Sample` eran anónimos por ausencia, no por decisión.
- **Resuelto:** bloque de comentario extenso en `Program.cs`, en el punto exacto del pipeline, con un ejemplo comentado de JWT Bearer; convención documentada en ARCHITECTURE.md → "Autenticación y autorización" ("todo endpoint declara `.RequireAuthorization()` o `.AllowAnonymous()`, nunca implícito"); los dos endpoints de `Sample` ya declaran `.AllowAnonymous()` explícito como ejemplo; el checklist de PR de ARCHITECTURE.md y el skill `architecture-review` lo exigen y lo rechazan si falta.

**7.5 Media → Resuelto en `feat/portar-adaptador-mysql-real`.** Solo menciones de pasada a EF Core/Dapper; sin adaptador de ejemplo ni convención de migraciones.
- **Resuelto:** el módulo Tasks (adaptador MySQL real, ADO.NET puro) se portó desde mhvs-todo-app hacia el template base, conviviendo junto a Sample sin romper el arranque en frío. Verificado en el template el 2026-09-30: un POST /tasks guardó correctamente una fila en una instancia real de MySQL, con Sample funcionando normal sin configurar MysqlTodo. Commit df196d4531a61c5dce3f7aee071910ffa3c5dd2c en feat/portar-adaptador-mysql-real. ARCHITECTURE.md documenta ambos patrones de referencia (Sample vs. Tasks) y cuándo mirar cada uno.
- **Sigue sin resolver, y a propósito:** convención de migraciones versionadas. `Tasks` muestra cómo se ve un adaptador real, no cómo evoluciona un esquema en el tiempo — son dos problemas distintos.

**7.6 Alta → Resuelto en `chore/endurecer-ci-y-escaneo-vulnerabilidades`.** Sin paso `dotnet list package --vulnerable` en `ci.yml`; sin `.github/dependabot.yml` ni Renovate.
- **Resuelto:** `.github/dependabot.yml` con dos ecosistemas (`nuget` y `github-actions`, ambos apuntando a `/`, semanal). Paso nuevo en `ci.yml` ("Escaneo de vulnerabilidades en dependencias NuGet") que corre `dotnet list package --vulnerable --include-transitive` sobre `MyApp.slnx` y falla el build si la salida contiene la frase que indica que un proyecto tiene paquetes vulnerables — verificado en ambos sentidos: pasa hoy (cero vulnerabilidades) y falla de verdad al inyectar temporalmente una referencia a `System.Net.Http` 4.3.0 (GHSA-7jgj-8wvc-jh57, alta), quitada después de confirmar.

**7.7 Media. Gobernanza a escala — CODEOWNERS con un solo dueño.** El propio archivo ya reconoce el problema (comentario + TODO) y deja el camino de crecimiento anotado; no es un diseño que asuma un solo dueño para siempre.

**7.8 Alta → Resuelto en `docs/multi-entorno` (2026-10-14), como documentación, `appsettings.Staging.json` y tests.** Solo `appsettings.json`/`appsettings.Development.json`; cero menciones de "staging"/"production" en la documentación; el único `if` de entorno en `Program.cs` compara contra `Development`.
- **Nota sobre el texto original:** quedó desactualizado antes de resolverse. "Cero menciones de staging" ya no era cierto: `ARCHITECTURE.md` → "CORS" mencionaba Staging. Se deja como estaba.
- **Resuelto:**
  - `ARCHITECTURE.md` → "Entornos":
    - cómo se elige el entorno (`ASPNETCORE_ENVIRONMENT`), y que sin definirla el framework usa `Production` (verificado arrancando la app sin la variable);
    - la tabla real de qué cambia por entorno (Swagger, HTTPS/HSTS, nivel de log, aviso del archivo local, credenciales, CORS, rate limiting);
    - la decisión: Staging se comporta como Production salvo lo que configure `appsettings.Staging.json` (Swagger apagado, HTTPS/HSTS activos);
    - un entorno nuevo (por ejemplo QA) se agrega con su `appsettings.{Env}.json`, sin cambiar código.
  - `src/Bootstrap/appsettings.Staging.json`: solo `Logging:LogLevel:Default = Information`. Sin credenciales, hosts ni cadenas de conexión.
  - `config/README.md`: enlace a esa sección y cómo encaja `appsettings.{Env}.json` en la precedencia.
  - Tests: `StagingEnvironmentTests` (Swagger 404, HSTS, `appsettings.Staging.json` cargado en Staging y no en Production) y `SwaggerDevelopmentTests` (`BootstrapFactory` corre en Development, Swagger 200).
  - `Program.cs` no cambió: el comportamiento ya era el decidido.

**7.9 Media → Resuelto en `feat/endpoint-version` (salvo en Docker, ver abajo).** El SDK ya embebe el commit de git en el ensamblado (`AssemblyInformationalVersionAttribute`, automático), pero nada en `Program.cs` lo expone (sin `/version`, sin header, sin log de arranque).
- Resuelto para el escenario principal (dotnet run): el endpoint
  /version funciona y se verificó que el commit coincide con
  git rev-parse HEAD. El mecanismo para que también funcione dentro
  de una imagen Docker (SourceRevisionId como build-arg) se verificó
  de forma aislada sin necesidad de Docker, y SÍ FUNCIONA — pero el
  cambio al Dockerfile mismo queda sin aplicar, porque hoy no hay
  trabajo activo con Docker en este proyecto. Cuando se retome
  Docker como parte real del flujo, aplicar: ARG GIT_COMMIT +
  dotnet publish -p:SourceRevisionId=$GIT_COMMIT en el Dockerfile.
- **Cómo se verificó el mecanismo (2026-09-30):**
  - Se copiaron las fuentes a una carpeta temporal sin `.git`, con `git rev-parse` fallando en ella, igual que dentro de `docker build`.
  - Se hicieron los mismos pasos del `Dockerfile`: `dotnet restore` + `dotnet publish -c Release --no-restore`.
  - Cada build publicado se arrancó y se consultó `GET /version`:
    - Sin argumento: `commit: null` y `commitFull: null`, con 200. Es lo que hoy hace una imagen Docker.
    - Con `-p:SourceRevisionId=b4c79f97fcaa219cbf749da1878851e2cb474f1a`: `commitFull` igual a `git rev-parse HEAD`.
  - La carpeta temporal se borró al terminar.
- Implementado en `Program.cs` + `Bootstrap/Diagnostics/BuildInfo.cs`, documentado en ARCHITECTURE.md → "Endpoint `/version`", cubierto por `VersionEndpointTests`. El header de versión y el log de arranque que menciona el hallazgo quedan fuera de alcance a propósito.

**Categorías sin hallazgos:** ninguna de las 9 — es la categoría con peor cobertura del informe original.

### Resumen ejecutivo de la auditoría de seguimiento

Antes de esta rama: de 36 hallazgos previos, 8 resueltos y 8 parciales; los 20 restantes intactos byte a byte. Los resueltos eran los defectos de runtime más visibles (credenciales, logs duplicados, correlation ID). Lo no tocado era justo lo que un equipo de plataforma/seguridad empresarial mira primero: sin HTTPS forzado, sin punto de extensión de auth, sin escaneo de dependencias en CI, sin observabilidad entre servicios — hallazgos de **ausencia de diseño**, más caros de cerrar tarde que un bug puntual. Esta rama cierra dos de esos (7.3, 7.4); 7.2, 7.5, 7.6 y 7.8 quedan como los más caros de los que siguen pendientes. (Después, 7.8 se resolvió en `docs/multi-entorno`, 2026-10-14, y 7.2 en `docs/observabilidad`, 2026-10-15: resuelto como documentación y punto de extensión; NO hay OpenTelemetry implementado.)

### Los 3 que corregiría primero (después de esta rama)

1. **7.6 (Dependabot + `dotnet list package --vulnerable` en CI).** Costo mínimo (un YAML), hoy no fallaría nada, y sin esto el template se congela en las versiones de hoy indefinidamente.
2. **7.2 + 7.8 juntos (un punto de extensión de observabilidad documentado + convención de entornos).** 7.8 resuelto en `docs/multi-entorno` (2026-10-14); 7.2 en `docs/observabilidad` (2026-10-15), resuelto como documentación y punto de extensión; NO hay OpenTelemetry implementado. Igual que 7.3/7.4, son ausencias de diseño baratas de documentar ahora y caras de inventar sobre la marcha después.
3. **1.2 + 4.1 (gobernanza que se pueda exigir), siguen intactas desde la primera auditoría.** Dos auditorías después, la única protección real del repo sigue siendo opt-in y silenciosa.

No se aplicó ninguna corrección de las categorías 1-6 en esta auditoría de seguimiento (7.3 y 7.4 se resolvieron como trabajo aparte, en la misma rama, después de reportarlas).

### Actualización (`chore/endurecer-ci-y-escaneo-vulnerabilidades`, 2026-09-25)

**7.6** y el resto de **4.3** (`permissions:`, pin por SHA) también quedaron resueltos — ver las notas junto a cada hallazgo arriba y `CHANGELOG.md`. De los "3 que corregiría primero" de este mismo informe, el ítem 1 (7.6) ya está hecho; siguen abiertos 7.2/7.8 y 1.2/4.1. Tampoco en esta rama se tocó ninguna de las categorías 1-6 fuera de 4.3.

### Actualización (`chore/auto-activar-hook-y-completar-codeowners`, 2026-09-25)

**1.2** y **4.1** también quedaron resueltos — ver las notas junto a cada hallazgo arriba y `CHANGELOG.md`. Con esto, el ítem 3 de los "3 que corregiría primero" también está hecho; de esa lista solo queda pendiente 7.2/7.8. El límite real de `CODEOWNERS` en un repo sin plan de pago no tiene solución técnica y se dejó documentado explícitamente en `CONTRIBUTING.md`, en vez de intentar resolverlo o dejarlo implícito.

### Actualización (`feat/portar-adaptador-mysql-real`, 2026-09-29)

**7.5** también quedó resuelto — módulo `Tasks` portado desde `mhvs-todo-app`, ver la nota junto al hallazgo arriba y `CHANGELOG.md`. No estaba en la lista de "3 que corregiría primero" original; de esa lista sigue pendiente solo 7.2/7.8. No se tocó nada de `Sample` ni de las categorías 1-6 en esta rama.

### Actualización (`fix/cerrar-huecos-skills-originales`, 2026-09-30)

Se marcan **3.1–3.9** de la auditoría de pre-lanzamiento. El detalle de
qué cambió en cada skill está en `CHANGELOG.md`. Aquí queda qué se
demostró y qué no:

3.1 (conteo vs. uno a uno) y 3.2 (coherencia entre secciones):
resueltos en el texto del skill — ahora exige una tabla explícita y
revisable de regla→clasificación→CA, en vez de un conteo agregado. Se
hicieron 4 rondas de pruebas ciegas con subagentes aislados (5
corridas en total). En las rondas 1 y 2 solo corrió la versión
corregida. La versión anterior corrió en 2 de las 5 corridas (rondas 3
y 4), comparada directamente con la corregida sobre la misma HU. En
esas dos, la versión anterior también
llegó a las mismas conclusiones semánticas con este modelo, así que la
mejora demostrada es un PROCEDIMIENTO explícito y auditable, no una
capacidad de detección nueva — el modelo subyacente ya era capaz de
verlo, pero no de forma consistente ni con un razonamiento que se
pudiera revisar después.

3.3 (prohibición de mencionar HTTP en los CA): efecto DEMOSTRADO
directamente. En pruebas ciegas repetidas, la versión anterior trató
'responde 400/404' de forma inconsistente consigo misma — bloqueante
en una corrida, informativo en otra, para el mismo tipo de contenido.
La versión corregida fue consistente en ambas corridas y alineada con
ARCHITECTURE.md → Precedencia. Esta es la única de las correcciones
con causalidad y consistencia demostradas, no solo observadas.

3.4 (documentos externos): resuelto por criterio explícito en el
texto; el caso informativo fue consistente en las 5 corridas, el caso
bloqueante no se probó.

3.6-3.9 (checklist de módulo nuevo, contrato 409, advertencia de
MemberwiseClone, cobertura ampliada de architecture-review): resueltos
por cambio de contenido explícito en los skills correspondientes, sin
prueba ciega dedicada — el cambio de comportamiento es directo
(instrucciones que antes no existían), no una corrección de
ambigüedad de juicio.

3.5: resuelto PARCIALMENTE — el skill ahora se detiene y pregunta ante
una HU multi-módulo, pero el template sigue sin un mecanismo concreto
de contrato entre módulos que mostrar como ejemplo.

### Actualización (`docs/multi-entorno`, 2026-10-14)

**7.8** también quedó resuelto, ver la nota junto al hallazgo arriba y `CHANGELOG.md`. De los "3 que corregiría primero" ya solo queda pendiente **7.2**. Las actualizaciones anteriores que dicen "sigue pendiente 7.2/7.8" se dejan como estaban, porque eran ciertas en su fecha.

### Actualización (`docs/observabilidad`, 2026-10-15)

**7.2**: resuelto como documentación y punto de extensión; NO hay OpenTelemetry implementado. Ver la nota junto al hallazgo arriba y `CHANGELOG.md`. Con esto, de los "3 que corregiría primero" no queda ningún ítem abierto. 7.2 se cerró como documentación, no como implementación. La actualización anterior, que dice "ya solo queda pendiente 7.2", se deja como estaba, porque era cierta en su fecha.

### Actualización (`docs/limpieza-documentacion`, 2026-10-15)

Limpieza de documentación acumulada, solo texto. No cambia comportamiento. Ver `CHANGELOG.md`.
- **2.9** y **2.10**: ver las notas junto a cada hallazgo.
- **Resuelto por trabajo previo:** la deuda de `IClock`, la de `ConnectionStrings` y la de `MemberwiseClone`, y la mejora "Health checks y Swagger". Ver las notas junto a cada una.
- **2.2:** el 404 de ruta inexistente ya está documentado; el 415 no.
- **Residuos:** se corrigieron los números de línea de "Referencias muertas" y el texto de `MyApp.slnx`. Se agregó un ítem bajo nuevo: versiones de paquetes de test distintas.
- **No entran en este PR:** `<None Update>` (ya registrado como residuo), `CODEOWNERS` sin `Directory.Build.targets`/`global.json`/`MyApp.slnx`, las llaves sin uso del `.example` y las menciones de capa sin `/`.

### Actualización (`chore/codeowners-archivos-sensibles`, 2026-10-15)

Solo configuración de revisión y texto. No cambia comportamiento. Ver `CHANGELOG.md`.
- **4.1:** `CODEOWNERS` pasa de 15 a **21 rutas**, con `/.gitignore`, `/.dockerignore`, `/Directory.Build.targets`, `/global.json`, `/MyApp.slnx` y `/CODEOWNERS`. Cierra la omisión de `/CODEOWNERS` (ver la nota en la fila 4.1). Sigue sin ser exigible sin plan de pago.
- Los comentarios de `CODEOWNERS` ya no prometen una aprobación obligatoria. `CONTRIBUTING.md` y `AGENTS.md` remiten al propio archivo en lugar de copiar la lista.
- **7.7** sigue abierto: un solo dueño.
- **Nuevo, de severidad media,** en "Docker, diferido a largo plazo": `.dockerignore` no excluye los `*.local.json` anidados. Está deducido de las reglas de Docker, no verificado con un build.
- **No entran:** `.gitattributes`, `CHANGELOG.md`, `PRESENTATION.md`, `src/Modules/` y `tests/` siguen sin dueño en `CODEOWNERS`.
- Se corrigió una frase que el PR de `docs/limpieza-documentacion` había movido al final de su propia viñeta "No entran en este PR". Volvió a la actualización de `docs/observabilidad`, que es donde estaba.

### Actualización (`docs/primeros-pasos`, 2026-10-16)

**Validación con clone limpio (Fase B): confirmada** el 2026-10-16 sobre `main` en `b35e922`. Se clonó desde `origin` en una carpeta vacía fuera del repo de trabajo y se siguió la documentación de arranque. Qué se validó:
- **Clone sin config local:** sin `config/connectionstrings.local.json`, sin `bin/`, `obj/` ni `logs/`. `git status --ignored` vacío y tag `v1.1.0` presente.
- **Build:** `dotnet build MyApp.slnx --no-incremental` con 0 advertencias y 0 errores. SDK instalado igual al fijado en `global.json`.
- **Tests:** 109 sin ningún archivo de config local (Sample 23, Tasks 41, Bootstrap.IntegrationTests 45). Muestra que los tests no dependen del archivo local. El riesgo de "Los tests de integración cargan el `config/connectionstrings.local.json` real del desarrollador" (en "Residuos menores") es el inverso y sigue igual.
- **Hook `pre-push`:** el primer build dejó `core.hooksPath` en `.githooks`. Se ejecutó el script directamente, sin hacer push: un push simulado a `main` o a `master` sale con 1 y el mensaje de bloqueo; a otra rama, o con `ALLOW_MAIN_PUSH=1`, sale con 0.
- **App:** `dotnet run --project src/Bootstrap` arranca en `Development`. `/health` da 200, `/swagger` da 200, y los comandos de `Sample` del README funcionan con `X-Correlation-ID` en las respuestas.
- **`Tasks` sin cadena de conexión:** falla de forma controlada (500 "Persistence failure", con la causa en el log) y `/health` sigue en 200.
- **No validado:** Docker (diferido), un restore con la caché de NuGet vacía, y una máquina sin certificado de desarrollo HTTPS.

Hallazgos del clone limpio, resueltos aquí como texto (ver `CHANGELOG.md`):
- El README pasa a este orden: requisitos previos, clonar, build, test, `dotnet run` y qué comprobar, y al final la configuración local para `Tasks`.
- `config/README.md`: la recarga en caliente aplica solo si el archivo existía al arrancar.
- README: qué responde `Tasks` sin cadena de conexión.
- `ARCHITECTURE.md` → "Logging": dónde se escribe realmente `logs/app.log`.
- `ARCHITECTURE.md` → "Endpoint `/version`": qué versión devuelve.
- **Nuevo, de severidad baja,** en "Residuos menores": `/version` devuelve `1.0.0` aunque el tag sea `v1.1.0`.

### Actualización (`docs/contrato-entre-modulos`, 2026-10-16)

**3.5**, y el ítem "Sin ejemplo de contrato entre módulos": resueltos como documentación y test de arquitectura. **NO existe ningún proyecto `Contracts` real**: ningún módulo expone nada a otro. No cambia el código de producción, los `.csproj` ni el `Dockerfile`. Ver `CHANGELOG.md`.
- **Regla:** `ARCHITECTURE.md` → "Contrato entre módulos".
  - Un módulo solo referencia `Shared` y proyectos `{Modulo}.Contracts`, nunca el proyecto principal de otro.
  - El `Contracts` vive en una carpeta hermana del módulo y solo tiene interfaces y DTOs `record`. Puede referenciar `Shared` (comprobado con un `Contracts` temporal: los 7 tests pasan), pero nada de `src/Modules`.
  - El consumidor define su propio puerto en `Domain/`, y solo su adaptador de `Infrastructure/` usa el `Contracts`.
  - Cambiar el estado de otro módulo no es atómico: hay que decidirlo con el humano. No se adoptan eventos.
- **Ubicación:** se descartó `src/Modules/{Modulo}/Contracts/`. Dentro de la carpeta del proyecto principal, el globbing por defecto del SDK compila los archivos del `Contracts` también en el módulo, y desde el segundo build falla con `CS0579`. Lo comprobé con un `Contracts` temporal.
- **Test:** `ArchitectureTests`, 7 casos. Comprueba las reglas A (módulo), B (`Shared`) y C (`Contracts`), cada una en los `.csproj` y en los ensamblados, más un caso que impide que pasen sin haber encontrado módulos. Mutaciones verificadas, todas revertidas:
  - **Referencia y uso de `Sample` desde `Tasks`:** fallan las dos comprobaciones de A.
  - **Solo la referencia, sin uso:** falla solo la de `.csproj`. Es el límite de `GetReferencedAssemblies()`.
  - **`Shared` → `Tasks`:** el build ya lo rechaza (dependencia circular). Con `--no-build` falla la de `.csproj`.
  - **`Shared` → un `Contracts`:** fallan las dos de B.
  - **`Contracts` → proyecto principal, y `Contracts` → otro `Contracts`:** fallan las dos de C.
  - **Un `Contracts` válido usado por su dueño y por otro módulo:** los 7 pasan.
- **Skills:** `architecture-review` permite `*.Contracts`, aclara qué capa puede usar qué de otro módulo y cita el test. `hu-to-slice-scaffold` sigue deteniéndose y preguntando, pero ahora propone el mecanismo y ya no sugiere `Shared/Ports`.
- **Sigue abierto, de severidad baja,** en "Residuos menores": `hu-validation` no detecta HU que tocan varios módulos. También se agregaron cuatro ítems: `HU-TASKS-001` no existe como archivo; el ID de `HU-TEMPLATE.md` supone un solo módulo; un contrato no puede devolver un tipo de otro `Contracts`; y el ejemplo usa puertos sin prefijo `I` (lo que remite a 5.5).
- La actualización de `fix/cerrar-huecos-skills-originales` ("3.5: resuelto PARCIALMENTE") se deja como estaba, porque era cierta en su fecha.

---

## Deuda técnica conocida (heredada de mhvs-demo-tickets)

### `IClock` existe pero nadie lo usa
> **Resuelto por trabajo previo (`docs/limpieza-documentacion`, 2026-10-15).** `Tasks` (7.5) ya lo usa: `CreateTaskHandler` y `CreateTaskValidator` reciben `IClock`, `TaskItem.Create` recibe el instante, y los tests usan `tests/Modules/Tasks/Support/FixedClock.cs` con valores exactos. No queda ningún `DateTime.UtcNow` en `src/` fuera de `SystemClock`. `Sample` no genera timestamps. El texto de abajo describe el estado anterior.
`Shared/Ports/IClock` está definido desde el inicio del template, pero
ninguna entidad de dominio lo usa para generar timestamps — usan
`DateTime.UtcNow` directo. Esto hace que los tests de campos como
`UpdatedAt` usen aserciones débiles (`>=` en vez de un valor exacto
inyectado y comparable).

**Por qué importa:** los tests de fechas no son tan precisos como
podrían ser, y cualquier entidad nueva que copie el patrón de
`SampleItem`/`Ticket` va a heredar la misma debilidad si no se corrige
la raíz.

**Esfuerzo estimado:** bajo-medio. Inyectar `IClock` en los métodos de
dominio que generan timestamps, y actualizar los tests para usar un
`FakeClock`/`IClock` controlado en vez de comparar con `>=`.

### Ningún módulo consume `ConnectionStrings` todavía
> **Resuelto por trabajo previo (`docs/limpieza-documentacion`, 2026-10-15).** `Tasks` (7.5) consume `ConnectionStrings:MysqlTodo` (`MySqlTaskRepository`). El texto de abajo describe el estado anterior.
El mecanismo de lectura de `config/connectionstrings.local.json` está
verificado (se lee, se recarga en caliente), pero ningún módulo del
template consume `ConnectionStrings` todavía porque `Sample` usa un
repositorio en memoria. El primer proyecto real que agregue un adaptador
de BD verdadero es quien probará el camino completo por primera vez.

---

## Copia superficial en el patrón de inmutabilidad (`MemberwiseClone`)

> **Resuelto por trabajo previo (`docs/limpieza-documentacion`, 2026-10-15), en lo que proponía el esfuerzo estimado.** La advertencia ya está junto al código (`SampleItem.cs`, encima de `MemberwiseClone()`) y en los skills `hu-to-slice-scaffold` y `architecture-review` (3.8). La limitación de diseño sigue: una entidad con colecciones necesita una copia profunda explícita.

El patrón de "retorna nueva instancia" usado en `SampleItem.Rename` (y
originalmente en `Ticket.AssignTo`) usa `MemberwiseClone()`, que hace
una copia **superficial**. Funciona bien mientras las entidades solo
tengan campos de valor simples. Si en el futuro una entidad gana un
campo de tipo colección (`List<T>`, por ejemplo), la copia y el
original compartirían esa lista por referencia — rompiendo la garantía
de inmutabilidad silenciosamente.

**Por qué importa:** es un patrón que se va a copiar a futuros módulos
sin que quien lo copie sepa de esta limitación.

**Esfuerzo estimado:** bajo. Agregar un comentario de advertencia
donde se documenta el patrón (`ARCHITECTURE.md` y/o el propio código),
y si en algún momento una entidad real necesita una colección, resolver
ahí mismo con una copia profunda explícita en vez de `MemberwiseClone`.

---

## `hu-validation`: criterio inconsistente para autorización implícita

hu-validation clasifica de forma inconsistente un requisito de
autorización implícito, deducido del rol de la historia (ej. 'una nota
que YA CREÉ' sugiere propiedad, pero la HU nunca lo declara como
regla explícita). En las pruebas ciegas de esta ronda, se marcó dos
veces como informativo (rondas 1 y 4) y dos veces como bloqueante
(rondas 2 y 3), sobre el mismo tipo de ambigüedad, incluso dentro de
la misma versión del skill: la corregida lo marcó informativo en la
ronda 1 y bloqueante en la 2, y la anterior bloqueante en la 3 e
informativo en la 4. Necesita un
criterio explícito: cuándo un requisito de propiedad/autoría implícito
debe tratarse como bloqueante vs. informativo.

Encontrado durante la verificación de 3.1-3.4 con subagentes ciegos
(`fix/cerrar-huecos-skills-originales`), fuera del alcance original de
esa corrección.

**Esfuerzo estimado:** bajo. Es una regla de decisión más en el
checklist, pero conviene tomarla junto con la decisión de cómo se
modelará la autenticación (hoy solo existe el punto de extensión en
`Program.cs`), para que el criterio no se contradiga con lo que el
template pueda implementar.

---

## Sin ejemplo de contrato entre módulos

> **Resuelto en `docs/contrato-entre-modulos` (2026-10-16) como documentación y test de arquitectura; NO existe ningún proyecto `Contracts` real.** El mecanismo y un ejemplo ilustrativo, no compilado, están en `ARCHITECTURE.md` → "Contrato entre módulos". `ArchitectureTests` verifica las referencias. El texto de abajo describe el estado anterior.

Falta un ejemplo concreto de contrato entre módulos (interfaz
pública o bus de eventos) para cuando una HU real toque dos módulos —
el skill hoy solo sabe detenerse y preguntar, no tiene un patrón de
referencia que mostrar (ver 3.5).

---

## Docker, diferido a largo plazo

Docker (Dockerfile, docker-compose.yml) queda diferido como deuda
técnica de LARGO PLAZO, por decisión explícita — no es una omisión
accidental. Incluye: corregir la contraseña de ejemplo en
docker-compose.yml, agregar USER/HEALTHCHECK al Dockerfile, decidir
si docker-compose.yml debe ofrecer un motor de base de datos de
ejemplo (y si es así, cuál — sin que eso implique que el template
se casa con ese motor para producción), y aplicar el mecanismo de
SourceRevisionId ya verificado (ver hallazgo 7.9) para que
GET /version funcione dentro de una imagen Docker.

Este ítem **consolida** todo lo pendiente de Docker que antes estaba
repartido por el ROADMAP. Los hallazgos originales de las auditorías no se
borran; apuntan aquí. Además de lo anterior, incluye:

- **4.2:** `Your_password123`, el puerto 1433 expuesto al host y
  `ASPNETCORE_ENVIRONMENT: Development` fijo en `docker-compose.yml`. En el
  `Dockerfile`, que corre como root (sin `USER`) y no tiene `HEALTHCHECK`.
- **Contenedor residual de SQL Server:** hoy `docker-compose.yml` levanta
  SQL Server para `ConnectionStrings:Default`, y ningún módulo lee esa clave.
  Ver ARCHITECTURE.md → "Neutralidad de motor de base de datos".
- **4.5, la parte que depende de Docker:** `docker-compose.override.yml` en
  `.gitignore`, y `.env` en `.dockerignore`.
- **4.3:** CI no construye la imagen Docker.
- **1.5:** el README no dice que Docker es opcional ni menciona
  `docker-compose.yml`.
- **7.9:** aplicar `ARG GIT_COMMIT` + `dotnet publish -p:SourceRevisionId=$GIT_COMMIT`,
  un mecanismo ya verificado.
- **Media. `.dockerignore` no excluye los `*.local.json` anidados.** Se
  encontró en `chore/codeowners-archivos-sensibles` (2026-10-15).
  - **Qué pasa:** `.dockerignore` excluye `config/connectionstrings.local.json` y
    `config/secrets.local.json` por nombre, y además `*.local.json`. Pero un
    patrón sin `**/` delante solo aplica en la raíz del contexto de build.
    Quedan fuera `src/Bootstrap/config/` y `src/config/`, que son dos de las tres
    rutas donde `LocalConfigLocator` busca el archivo local. Con `COPY . .`, un
    `connectionstrings.local.json` con credenciales reales en esas carpetas
    acabaría en la imagen.
  - **Cómo se sabe:** está **deducido** de las reglas de patrones de
    `.dockerignore` de Docker. **No se verificó con un `docker build`**, porque
    Docker está diferido.
  - **Corrección esperada cuando se retome Docker:** cambiar `*.local.json` por
    `**/*.local.json`, y verificarlo con un build real, por ejemplo con un
    archivo falso en `src/Bootstrap/config/` y comprobando que no llega a la
    imagen. `.dockerignore` no se tocó.
- **Nunca se ejecutó un `docker build` real (ahora sobre .NET 10).** Este
  punto se movió aquí desde "Mejoras consideradas, no urgentes". El template
  pasó a .NET 10 (imágenes `sdk:10.0` y `aspnet:10.0`, `global.json` copiado
  antes del `restore`, y `MyApp.slnx` en lugar de `MyApp.sln`), y nunca se
  ejecutó un `docker build` real, ni antes ni después del salto, por falta de
  Docker en esta máquina. Tampoco se probó después de los cambios de las fases
  1-9 del portado ni del bug puntual con `Tickets` del demo.
  - **Lo que sí se verificó sin Docker:**
    - Las etiquetas `mcr.microsoft.com/dotnet/sdk:10.0` y `aspnet:10.0` existen
      en el registro (HTTP 200), y `sdk` ya publica 10.0.401, que cumple el
      `global.json` (`10.0.102`, `rollForward: latestMinor`).
    - Se simuló la secuencia de los `RUN` en un directorio limpio y pasó:
      1. `restore` de `Bootstrap.csproj` con solo `global.json`, `MyApp.slnx` y
         los tres `.csproj` copiados (`Bootstrap`, `Sample` y `Shared`; `tests/`
         no se copia a propósito, porque la imagen solo publica `Bootstrap`).
      2. El equivalente a `COPY . .`, sin `bin/`, `obj/`, `logs/` ni
         `*.local.json`, para aproximar `.dockerignore`.
      3. `dotnet publish --no-restore`.

      El `MyApp.Bootstrap.runtimeconfig.json` resultante apunta a
      `Microsoft.AspNetCore.App` 10.0.0 (y `Microsoft.NETCore.App` 10.0.0), lo
      que coincide con la imagen `aspnet:10.0`.
  - **Lo que no cubre:** la mecánica real de Docker (capas y caché, el `COPY`
    con el `.dockerignore` verdadero, permisos), ni que el contenedor arranque
    y responda en el puerto 8080.
  - Del `.dockerignore` ya se corrigieron antes `**/bin/` y `**/obj/`. Con
    `COPY . .`, un `obj/` local podía pisar el restaurado en la imagen.

---

## Mejoras consideradas, no urgentes

Estas vienen de sugerencias más tempranas (`opencode-prompt-mhvs.md`, un
documento de trabajo externo que nunca se versionó en este repo; la cita es
solo una referencia histórica) que no se implementaron porque no hacían
falta para el ciclo de prueba con `mhvs-demo-tickets`:

- **Dockerfile: verificar un build real** — se movió a "Docker, diferido a
  largo plazo" (arriba), junto con el resto de lo pendiente de Docker.
- **Health checks y Swagger** (resuelto por trabajo previo, `docs/limpieza-documentacion`, 2026-10-15: los dos están en `Program.cs`, `AddHealthChecks()` + `MapHealthChecks("/health")` y Swagger solo en Development, ver ARCHITECTURE.md → "Entornos"): confirmar si ya están en el template
  base o si siguen pendientes — revisar `Program.cs` directamente, ya
  que hubo mención ambigua de esto en varias sesiones sin confirmación
  explícita.
- **Comandos personalizados en Open Code** (`/new-hu`,
  `/scaffold-slice`), si la herramienta los soporta — azúcar sintáctica
  sobre los skills existentes, no urgente.
- **ADRs (Architecture Decision Records)** — documentar el "por qué"
  de decisiones ya tomadas (sin MediatR, log4net sobre Serilog, etc.).
  Vale la pena cuando alguien nuevo empiece a cuestionarlas sin
  contexto.
- **Glosario de lenguaje ubicuo (DDD)** — cuando ya existan 3-4
  módulos reales en algún proyecto nacido del template, no antes.

---

## Residuos menores

Ninguno de estos rompe nada ni es urgente. Los primeros dos son puramente
cosméticos (referencias muertas, agrupación en el IDE); los dos últimos son
limitaciones de diseño o de cobertura de tests, de severidad baja pero
reales, documentadas a propósito para no dejarlas como una inconsistencia
sin explicación.

**Referencias muertas menores**, encontradas en la verificación del PR #4
(estado de `main` en `6b48bce`):

- `CODEOWNERS` (línea 16, un comentario) cita `checklist-release-template.md`,
  que no existe en el repo.
- `ROADMAP.md` cita `opencode-prompt-mhvs.md` dos veces, y tampoco existe.
- `src/Bootstrap/Program.cs` (línea 48) conserva un comentario sobre
  "Auth/Courses/Products" que ya no aplica.
- El skill `hu-to-slice-scaffold` cita un ejemplo `HU-SAMPLE-002` (líneas 28 y
  158 de su `SKILL.md`) que no existe como archivo real.

> **Resuelto en `docs/limpieza-documentacion` (2026-10-15), como texto.** Los números de línea de arriba ya estaban desactualizados. Al resolverlos, las referencias estaban en `CODEOWNERS:16`, `Program.cs:148` y `hu-to-slice-scaffold/SKILL.md:30` y `:247`.
> - `CODEOWNERS` ahora apunta a `CONTRIBUTING.md` → "Revisión".
> - `opencode-prompt-mhvs.md` queda marcado como referencia histórica en "Mejoras consideradas". La otra cita es la de esta lista.
> - El comentario de `Program.cs` ahora dice "igual que Sample y Tasks".
> - `HU-SAMPLE-002` queda marcado como ID ilustrativo en el skill.

**Carpeta de solución desigual en `MyApp.slnx`** (encontrada al migrar a
.NET 10 y `.slnx`): solo `Bootstrap.IntegrationTests` cuelga de la carpeta de
solución `/tests/`; `Sample.Tests` queda en la raíz de la solución. (Actualizado en `docs/limpieza-documentacion`, 2026-10-15: ahora también `Tasks.Tests`, agregado con el módulo `Tasks`, queda en la raíz; la solución sería la misma para los dos.) Viene del
`dotnet sln add` que se hizo al crear el proyecto de integración, que generó la
carpeta solo para ese proyecto, y la migración la conservó tal cual. Solo afecta
cómo se agrupan los proyectos en el Explorador de soluciones del IDE, no la
compilación ni los tests. Se resolvería agrupando ambos proyectos de test bajo la
misma carpeta, sin editar el archivo a mano: `dotnet sln MyApp.slnx remove
tests/Modules/Sample/Sample.Tests.csproj` y luego `dotnet sln MyApp.slnx add
--solution-folder tests tests/Modules/Sample/Sample.Tests.csproj` (verificado: `add`
a secas sobre un proyecto que ya está en la solución se rechaza y no cambia nada;
con `remove` previo, ambos proyectos quedan bajo `/tests/`).

**CORS no relee configuración en caliente, a diferencia del rate limiter**
(encontrado al agregar HTTPS/HSTS, CORS y rate limiting en
`feat/https-hsts-y-punto-extension-auth`): el rate limiter lee
`RateLimiting:PermitLimit`/`WindowSeconds` desde `IConfiguration` dentro del
delegado de cada partición, así que ve cualquier fuente de configuración
añadida después de `builder.Build()`. La política CORS, en cambio, se
construye una sola vez con `Cors:AllowedOrigins` leído al arrancar
(`services.AddCors(options => options.AddPolicy(...))` evalúa ese callback
de forma síncrona en el momento del registro): si alguien edita
`Cors:AllowedOrigins` en un archivo con `reloadOnChange` mientras la app
corre, el cambio no se refleja sin reiniciar. Es una asimetría de diseño
menor, no un bug — CORS no se pidió como "ajustable en caliente" y no lo
promete en ningún lado, a diferencia del rate limiter — pero queda
documentada aquí en vez de dejarla como una inconsistencia sin explicación
entre dos piezas de configuración que se ven parecidas.

**Falta cobertura automatizada de "sin proxy delante, HTTPS real
configurado, con redirección forzada"** (mismo trabajo): `HttpsHstsTests`
cubre el caso con `X-Forwarded-Proto: https` simulando el proxy (con y sin
el header, y que no redirija cuando ya se confía en el header). El caso
contrario — la app expuesta directo a internet, con un endpoint HTTPS real
de Kestrel configurado y `UseHttpsRedirection()` sí redirigiendo de verdad —
solo se verificó a mano contra la app real (`dotnet run`, sin
`X-Forwarded-Proto`, con Kestrel escuchando en HTTPS): confirmó el 307/redirect
esperado, pero no quedó como test. `WebApplicationFactory<Program>` no
levanta un endpoint HTTPS real por defecto (es todo en memoria vía
`TestServer`), así que cubrirlo pediría un `TestServer` con HTTPS configurado
explícitamente o una prueba distinta al patrón que ya usa el resto de la
suite.

**Baja. Los argumentos de línea de comandos pierden contra el archivo local y
las variables de entorno.** Se encontró al resolver 7.1 en
`docs/secretos-en-produccion` (2026-10-14).
- **Causa:** `WebApplication.CreateBuilder(args)` agrega los argumentos, y
  después `Program.cs` agrega `config/connectionstrings.local.json` y
  `AddEnvironmentVariables()`. Por eso un `--ConnectionStrings:MysqlTodo=...`
  pasado al arrancar no tiene efecto si la misma clave está en el archivo local
  o en una variable de entorno. Es lo contrario de lo que se suele esperar,
  porque en la configuración por defecto de ASP.NET Core los argumentos ganan.
- **Por qué no se cambió:** es una limitación de diseño, no un bug, y nada del
  template pasa configuración por línea de comandos.
- **Dónde está documentado:** en `config/README.md` (tabla de precedencia) y en
  el comentario de `Program.cs`. El código no se cambió.
- **Si un proyecto necesita que los argumentos ganen:** basta con volver a
  agregar `builder.Configuration.AddCommandLine(args)` después de
  `AddEnvironmentVariables()`.

**Baja. El nivel de log de `appsettings.{Env}.json` solo puede subir el umbral.**
Se encontró al resolver 7.8 en `docs/multi-entorno` (2026-10-14).
- **Causa:** la raíz de log4net está en `INFO` (`log4net.config`), y log4net
  descarta lo que esté por debajo aunque `Logging:LogLevel` lo deje pasar. El
  `Default: Debug` de `appsettings.Development.json` no tiene efecto hoy: en
  una corrida real en Development hubo 0 líneas DEBUG, aunque el host de .NET
  emite mensajes Debug al arrancar.
- **Consecuencia:** mientras la raíz siga en `INFO`, un `appsettings.{Env}.json`
  puede subir el umbral (por ejemplo a `Warning`), pero no bajarlo de INFO.
- **Por qué no se cambió:** fuera del alcance de 7.8. No se tocaron
  `log4net.config` ni `appsettings.Development.json`. Está documentado en
  `ARCHITECTURE.md` → "Entornos".

**Baja. Las tres entradas `<None Update>` de `Bootstrap.csproj` no hacen nada.**
Se encontró al resolver 7.8 en `docs/multi-entorno` (2026-10-14).
- **Causa:** el Web SDK ya incluye `appsettings*.json` y `log4net.config` como
  ítems `Content` y los copia al output y al publish por su cuenta.
  `dotnet msbuild -getItem:None -getItem:Content` muestra los cuatro archivos
  como `Content` y ningún `None`, así que las entradas `<None Update>` de
  `log4net.config`, `appsettings.json` y `appsettings.Development.json` no
  coinciden con nada. `appsettings.Staging.json`, sin entrada, se copió igual
  al build y al publish.
- **Por qué no se cambió:** no rompe nada y estaba fuera del alcance de 7.8.
  Es candidato para la próxima limpieza de documentación/configuración.

**Baja. Los tests de integración cargan el `config/connectionstrings.local.json`
real del desarrollador.** Se encontró al resolver 7.8 en `docs/multi-entorno`
(2026-10-14).
- **Causa:** las factories basadas en `WebApplicationFactory<Program>` usan el
  content root del repo (`src/Bootstrap/`), y `LocalConfigLocator` encuentra el
  archivo local en `config/` si existe. En CI no existe.
- **Riesgo:** un valor de ese archivo que pise algo de `appsettings` (por
  ejemplo un nivel de log restrictivo) podría alterar tests, como los de
  "0 errores en el arranque", solo en esa máquina y no en CI.
  `EnvironmentConfigBootstrapFactory` no tiene el problema, porque usa un
  content root temporal.
- **Sin acción por ahora.**

**Baja. El scope de `ILogger` que abre `CorrelationIdMiddleware` no aparece en los logs.**
Se encontró al resolver 7.2 en `docs/observabilidad` (2026-10-15).
- **Hecho comprobado:** el middleware hace `logger.BeginScope` con la clave
  `CorrelationId`, pero el patrón actual de `log4net.config` no lo usa. El
  correlation ID que se ve entre corchetes viene de
  `LogicalThreadContext.Properties["CorrelationId"]` (`%property{CorrelationId}`),
  que el middleware también fija.
- **No verificado:** si el proveedor Log4Net de `Microsoft.Extensions.Logging`
  expone ese scope de alguna otra forma.
- **Por qué no se cambió:** no rompe nada. El scope puede servir si se agrega
  otro proveedor de logging. Documentado en `ARCHITECTURE.md` → "Logging".

**Baja. Los proyectos de test usan versiones distintas de los mismos paquetes.**
Se encontró en `docs/limpieza-documentacion` (2026-10-15).
- **Hecho:** `Bootstrap.IntegrationTests.csproj` usa `Microsoft.NET.Test.Sdk`
  18.10.1 y `xunit` 2.9.2. `Sample.Tests.csproj` y `Tasks.Tests.csproj` usan
  `Microsoft.NET.Test.Sdk` 17.11.1 y `xunit` 2.9.3.
- **Por qué no se cambió:** alinearlas cambia dependencias, y eso no es
  limpieza de documentación. En `origin` hay ramas de Dependabot para estos
  paquetes (por ejemplo `xunit-2.9.3` en `Bootstrap.IntegrationTests`).
  Conviene alinearlas en un PR propio o al revisar esas ramas.

**Baja. `/version` devuelve `1.0.0` aunque el tag de la plantilla sea `v1.1.0`.**
Se encontró en la validación con clone limpio (Fase B) y se documentó en
`docs/primeros-pasos` (2026-10-16).
- **Hecho:** ningún `.csproj` define `<Version>`, así que el SDK usa su valor
  por defecto y `GET /version` responde `"version": "1.0.0"`.
- **Hoy no hay código implicado:** `BuildInfo` lee bien lo que embebe el SDK.
  Lo que falta es que alguien fije la versión. `ARCHITECTURE.md` → "Endpoint
  `/version`" aclara que `version` es la del ensamblado de la aplicación (cada
  proyecto define la suya) y que la versión de la plantilla es su tag de Git.
- **Solución real, diferida:** derivar la versión del tag en CI.

**Baja. `HU-TASKS-001` se cita, pero el archivo no existe.**
Se encontró en el inventario de 3.5 (`docs/contrato-entre-modulos`, 2026-10-16).
- **Hecho:** el código de `Tasks` (`/// Implementa HU-TASKS-001`), `README.md`,
  `ARCHITECTURE.md` y `CHANGELOG.md` citan `HU-TASKS-001`, pero no hay ningún
  `HU-TASKS-001.md` en el repo. A diferencia de `HU-SAMPLE-002`, que el skill
  `hu-to-slice-scaffold` marca como ilustrativo, nada avisa que este no está.
- **Sin acción por ahora:** falta decidir si se agrega la HU o se marca como
  ilustrativa.

**Baja. El ID de `HU-TEMPLATE.md` supone un solo módulo.**
Se encontró en el inventario de 3.5 (`docs/contrato-entre-modulos`, 2026-10-16).
- **Hecho:** el título `HU-{MODULO}-{NUMERO}` de `docs/templates/HU-TEMPLATE.md`
  tiene un solo módulo, y la plantilla no tiene ninguna sección sobre
  dependencias con otros módulos.
- **Sin acción por ahora.** Va junto con el ítem siguiente.

**Baja. `hu-validation` no detecta HU que tocan varios módulos.**
Se encontró en el inventario de 3.5 (`docs/contrato-entre-modulos`, 2026-10-16).
- **Hecho:** el freno ante una HU de dos o más módulos solo existe en
  `hu-to-slice-scaffold` (sección 6), después de validar. `hu-validation` no lo
  menciona.
- **Abierto.** No se tocó en `docs/contrato-entre-modulos`, a propósito.

**Baja. Un contrato no puede devolver un tipo de otro `Contracts`.**
Decisión de `docs/contrato-entre-modulos` (2026-10-16).
- **Hecho:** la regla es estricta. Un `{Modulo}.Contracts` no referencia ningún
  proyecto de `src/Modules`, ni siquiera otro `Contracts`, y `ArchitectureTests`
  lo hace cumplir. Si un contrato necesitara devolver un tipo de otro
  `Contracts`, la regla actual lo prohíbe.
- **Revisar con un caso real** antes de relajarla.

**Baja. El ejemplo de "Contrato entre módulos" usa puertos sin prefijo `I`.**
Documentado en `docs/contrato-entre-modulos` (2026-10-16).
- **Hecho:** el ejemplo ilustrativo de `ARCHITECTURE.md` (`CustomerDirectory`,
  `CustomerExistence`) sigue la forma actual del repo (`TaskRepository`,
  `SampleItemRepository`).
- **No es una decisión nueva:** la discusión del prefijo `I` sigue siendo el
  ítem **5.5** de la auditoría de pre-lanzamiento.

---

## Cómo se agregó cada ítem (trazabilidad)

| Ítem | Origen |
|---|---|
| `IClock` sin usar | Mismo hallazgo, punto 7 |
| Copia superficial `MemberwiseClone` | Revisión de la Fase 3 del portado de aprendizajes |
| Mejoras no urgentes | Prompt original de contexto para Open Code, nunca implementadas |
| Dockerfile: verificar build real | Prompt original de contexto para Open Code; detalle de `.dockerignore` y `.csproj` de la revisión de consistencia (docs/revision-consistencia); actualizado para .NET 10 con lo simulado en la migración (chore/upgrade-net10-y-slnx) |
| Ningún módulo consume `ConnectionStrings` | Verificación del PR #4 (lectura y recarga de la config local comprobadas; ningún consumidor real todavía) |
| Residuos menores (referencias muertas) | Verificación del PR #4 |
| Carpeta `/tests/` desigual en `MyApp.slnx` | Migración a .NET 10 y `.slnx` (chore/upgrade-net10-y-slnx) |
| Categoría 7 (preparación empresarial) y reconciliación 1.1–6.2 | Auditoría de seguimiento (feat/https-hsts-y-punto-extension-auth) |
| HTTPS/HSTS, CORS, rate limiting (7.3) y punto de extensión de auth (7.4) | Resueltos en feat/https-hsts-y-punto-extension-auth |
| CORS sin recarga en caliente (asimetría con el rate limiter) | feat/https-hsts-y-punto-extension-auth |
| Sin test automatizado de redirección HTTPS real sin proxy | feat/https-hsts-y-punto-extension-auth |
| Dependabot (NuGet + GitHub Actions) y escaneo de vulnerabilidades en CI (7.6) | Resuelto en chore/endurecer-ci-y-escaneo-vulnerabilidades |
| `permissions:` y pin por SHA en ci.yml (resto de 4.3) | Resuelto en chore/endurecer-ci-y-escaneo-vulnerabilidades |
| Hook de pre-push auto-activado por MSBuild (1.2) | Resuelto en chore/auto-activar-hook-y-completar-codeowners |
| Rutas faltantes en CODEOWNERS (4.1) | Resuelto en chore/auto-activar-hook-y-completar-codeowners |
| Módulo `Tasks` (persistencia MySQL real, 7.5) | Portado de mhvs-todo-app, resuelto en feat/portar-adaptador-mysql-real |
| `hu-validation`: criterio inconsistente para autorización implícita | Verificación de 3.1-3.4 con subagentes ciegos (fix/cerrar-huecos-skills-originales) |
| Correcciones a los skills originales (3.1–3.9, 3.5 parcial) | Resueltos en fix/cerrar-huecos-skills-originales; 3.5 completado en docs/contrato-entre-modulos |
| Sin ejemplo de contrato entre módulos | Lo que queda abierto de 3.5 (fix/cerrar-huecos-skills-originales); resuelto como documentación y test de arquitectura en docs/contrato-entre-modulos, sin ningún `Contracts` real |
| Inyección en logs y `X-Correlation-ID` (4.4), `.gitignore` (4.5) | Resueltos en fix/sanear-logs-y-gitignore |
| Docker, diferido a largo plazo (consolida 4.2, lo de Docker de 1.5, 4.3, 4.5 y 7.9, y el build real) | Decisión explícita en fix/sanear-logs-y-gitignore |
| Consistencia de `Sample`: `try/catch` en `PingSampleHandler` (2.6) y normalización compartida en `SampleItem` (2.7) | Resueltos en fix/consistencia-sample |
| Secretos en producción (7.1): documentación, punto de extensión y test, sin integración real con Key Vault | Resuelto en docs/secretos-en-produccion |
| Argumentos de línea de comandos pierden contra archivo local y variables de entorno | Encontrado al resolver 7.1 (docs/secretos-en-produccion); solo documentado |
| Multi-entorno (7.8): sección "Entornos", `appsettings.Staging.json` y tests | Resuelto en docs/multi-entorno |
| Nivel de log de `appsettings.{Env}.json` limitado por la raíz INFO de log4net | Encontrado al resolver 7.8 (docs/multi-entorno); solo documentado |
| `<None Update>` sin efecto en `Bootstrap.csproj` | Encontrado al resolver 7.8 (docs/multi-entorno); candidato a limpieza |
| Tests de integración cargan el archivo local real del desarrollador | Encontrado al resolver 7.8 (docs/multi-entorno); sin acción por ahora |
| Observabilidad (7.2): sección "Observabilidad", punto de extensión de OpenTelemetry y tests; resuelto como documentación y punto de extensión; NO hay OpenTelemetry implementado | docs/observabilidad |
| Scope de `ILogger` de `CorrelationIdMiddleware` no usado por el patrón de log4net | Encontrado al resolver 7.2 (docs/observabilidad); solo documentado |
| Limpieza de documentación (#8): 2.9, 2.10, casing, checklist, ramas, aprobaciones, deuda de `IClock`/`ConnectionStrings`/`MemberwiseClone` y Health/Swagger resueltos por trabajo previo | docs/limpieza-documentacion |
| Versiones de paquetes de test distintas entre proyectos | Encontrado en docs/limpieza-documentacion; sin acción por ahora |
| `CODEOWNERS`: archivos que controlan las protecciones (21 rutas, incluida la omisión de `/CODEOWNERS` en 4.1) | chore/codeowners-archivos-sensibles |
| `.dockerignore` no excluye `*.local.json` anidados (media; deducido, no verificado con build) | Encontrado en chore/codeowners-archivos-sensibles; en "Docker, diferido a largo plazo" |
| Validación con clone limpio (Fase B) confirmada en `b35e922`; primeros pasos del README reordenados y hallazgos documentados | docs/primeros-pasos |
| `/version` devuelve `1.0.0` aunque el tag sea `v1.1.0` (baja; sin código implicado hoy) | Encontrado en la validación con clone limpio; documentado en docs/primeros-pasos |
| Contrato entre módulos (3.5): regla `{Modulo}.Contracts`, `ArchitectureTests` y skills; NO existe ningún `Contracts` real | docs/contrato-entre-modulos |
| `HU-TASKS-001` citado sin archivo; ID de `HU-TEMPLATE.md` de un solo módulo; `hu-validation` sin detectar HU que tocan varios módulos (bajas, abiertas) | Encontrados en el inventario de 3.5 (docs/contrato-entre-modulos) |
| Contrato que necesitaría devolver un tipo de otro `Contracts` (baja; revisar con un caso real); ejemplo con puertos sin prefijo `I` (baja; remite a 5.5) | Decisiones de docs/contrato-entre-modulos |
