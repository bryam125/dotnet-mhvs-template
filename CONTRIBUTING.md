# CONTRIBUTING — Guía de contribución

Convenciones de trabajo del equipo para cualquier proyecto basado en la
plantilla MHVS. Complementa a `ARCHITECTURE.md` (que dice *cómo* se
organiza el código); este documento dice *cómo* trabajamos juntos sobre
ese código.

---

## 1. Ramas

Formato: `tipo/descripcion-corta-en-kebab-case`. El tipo es el mismo que el del
commit (ver "2. Commits"), para que rama, commit y PR digan lo mismo.

| Tipo | Uso |
|---|---|
| `feat/` | Funcionalidad nueva |
| `fix/` | Corrección de bug, también las urgentes |
| `docs/` | Solo documentación |
| `refactor/` | Cambio interno sin alterar comportamiento |
| `test/` | Solo tests |
| `chore/` | Tareas de mantenimiento (deps, config, CI) |
| `perf/` | Mejora de rendimiento sin cambiar comportamiento |

Ejemplos: `feat/create-order-endpoint`, `fix/null-reference-auth-login`.

No hay `hotfix/`: todo sale de `main` y vuelve a `main` por PR, sin ramas de
release, así que una corrección urgente es un `fix/` que se revisa primero.
Ningún hook ni paso de CI valida el nombre de la rama: es una convención, no
un control.

- `main` siempre debe estar desplegable.
- No se commitea directo a `main` — todo pasa por PR, sin excepción, ni
  siquiera cambios "chiquitos".

**Hook local `pre-push` (se activa solo, en el primer `dotnet build`):**
`Directory.Build.targets`, en la raíz del repo, corre
`git config core.hooksPath .githooks` automáticamente en el primer build
local de cualquier proyecto, sin que haga falta el paso manual que antes
nadie estaba obligado a recordar (no hace nada en CI, sin repo git, ni si
`core.hooksPath` ya tenía otro valor configurado a propósito — no lo
sobreescribe). El hook `.githooks/pre-push` bloquea localmente el push
directo a `main` (y también a `master`, por si un proyecto derivado usa ese
nombre); en casos realmente excepcionales se salta con
`ALLOW_MAIN_PUSH=1 git push`. Existe porque en repositorios privados GitHub
exige un plan de pago (Pro o Team) para aplicar protección de rama real;
este hook es la mitigación local mientras tanto. No reemplaza la protección
de rama de GitHub: es una red de seguridad a nivel de máquina — pero, a
diferencia de `CODEOWNERS` (ver más abajo), es una barrera **técnica** real,
aunque saltable a propósito por quien conoce el escape.

**`CODEOWNERS` — qué protege de verdad y qué no.** El archivo lista rutas
sensibles con dueño asignado. La lista, y el porqué de cada grupo, vive solo
en el propio `CODEOWNERS`, que es la fuente. Pero en un repo privado sin plan de pago
**no hay ninguna forma de exigirlo**: GitHub solo aplica "Require review
from Code Owners" cuando la protección de rama real está disponible, y eso
requiere el plan de pago que este repo no tiene. Hoy, `CODEOWNERS` es un
**recordatorio visual** en la interfaz del PR (GitHub sugiere revisores),
no una barrera técnica — cualquiera puede mergear sin esa revisión si nadie
la exige a mano. No lo confundas con el hook `pre-push`: ese sí bloquea
técnicamente (aunque saltable a propósito); `CODEOWNERS`, hoy, no bloquea
nada por sí solo.

## 2. Commits

Seguimos [Conventional Commits](https://www.conventionalcommits.org/):

```
tipo(alcance opcional): descripción en presente e imperativo

[cuerpo opcional explicando el porqué, no el qué]
```

Tipos permitidos: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `perf`.

Ejemplos:
```
feat(orders): agregar caso de uso CreateOrder
fix(auth): corregir validación de token expirado
docs: actualizar ARCHITECTURE.md con nuevo módulo Billing
```

Reglas:
- Un commit = un cambio lógico. Si el mensaje necesita "y" para describirlo, probablemente son dos commits.
- Nunca commitear `config/connectionstrings.local.json` ni ningún archivo con credenciales reales (ya está en `.gitignore`, pero revisa el diff antes de hacer push si tocaste algo en `config/`).

## 3. Pull Requests

**Antes de abrir el PR**, repasa el checklist de
[`ARCHITECTURE.md` → "Checklist antes de un PR"](ARCHITECTURE.md#checklist-antes-de-un-pr).
Es la única fuente: no se copia aquí para que no se desincronice.

**Tamaño del PR:** preferimos PRs pequeños y frecuentes sobre uno gigante.
Si un PR toca más de ~400 líneas o más de un módulo a la vez, probablemente
conviene partirlo.

**Descripción del PR** debe incluir:
- Qué problema resuelve (1-2 líneas).
- Cómo probarlo manualmente, si aplica.
- Capturas o ejemplo de request/response para endpoints nuevos.

**Revisión:**
- Mínimo 1 aprobación antes de mergear (2 si el cambio toca `Shared/` o `Bootstrap/`, porque afecta a todos los módulos).
- El autor del PR no se aprueba a sí mismo.
- Si el reviewer pide cambios, se resuelven con commits nuevos (no rebase silencioso) hasta que el reviewer apruebe; ahí sí se puede hacer squash antes de mergear.

**Aprobaciones — qué se cumple de verdad hoy y qué no.** Las dos reglas de
arriba (1 o 2 aprobaciones, y que el autor no se apruebe a sí mismo) son la
convención para cuando haya equipo. Hoy el repo tiene **un solo
colaborador**, que abre y mergea sus propios PR, así que **ninguna de las dos
se cumple ni se puede exigir**. GitHub solo exige aprobaciones con la
protección de rama real, que en un repo privado requiere el plan de pago que
este repo no tiene (igual que `CODEOWNERS`, ver "Ramas"). Tampoco se exige
que CI esté en verde para mergear: eso también depende de la protección de
rama. La única barrera técnica es el hook `pre-push` contra el push directo a
`main`. El resto (abrir PR, esperar a CI y revisar antes del squash) depende
de quien mergea. Cuando se sume un segundo colaborador, las reglas siguen
siendo un acuerdo del equipo, no una barrera, hasta que exista protección de
rama.

**Merge:** squash and merge por defecto, para que `main` tenga un commit limpio por PR con mensaje en formato Conventional Commits. Al hacer el merge en GitHub, confirma explícitamente que el botón dice **"Squash and merge"** — no asumas que es la opción por defecto.

## 4. Código y estilo

- `dotnet format` antes de cada commit (o configurarlo como pre-commit hook). **Hoy no está impuesto:** no hay `.editorconfig` en el repo, así que `dotnet format` solo aplica sus valores por defecto, y CI no tiene ningún paso que lo verifique. Es una recomendación, no un control.
- Nombres en inglés para código (clases, métodos, variables); comentarios y documentación pueden ir en español si el equipo lo prefiere así — mantener consistencia dentro de un mismo archivo.
- **Excepción — nombres de tests que trazan a Criterios de Aceptación:** cuando un test unitario corresponde directamente a un CA de una historia de usuario (`CA-01`, `CA-02`...), su nombre puede ir en español y debe incluir el ID del CA como prefijo, ej. `CA01_RenombrarExitoso_GuardaElNuevoNombreYLoRetorna`. Se prioriza la trazabilidad HU → test sobre la consistencia de idioma en este caso puntual — cualquiera que lea el nombre del test debe poder ubicar de inmediato a qué criterio de aceptación corresponde, sin traducir mentalmente. El resto del código (clases, métodos de producción, nombres de variables) sigue en inglés sin excepción.
  - **Esta excepción NO aplica** a tests que trazan a una regla de negocio o a un invariante de dominio sin un CA asociado (ej. `Rename_ReturnsNewInstance_AndLeavesOriginalUntouched`): esos van en inglés, como el resto del código, y sin prefijo `CA0x_`. Un prefijo `CA0x_` promete trazabilidad a un criterio concreto; no lo uses si no existe.
- Sin comentarios que solo repitan lo que el código ya dice. Comentar el *por qué*, no el *qué*.
- Métodos públicos de un handler/servicio con nombres que describan la acción de negocio, no detalles técnicos (`HandleAsync`, no `ProcessRequestAsync`).

## 5. Tests

- Todo caso de uso nuevo en `Application/` lleva al menos un test en `tests/Modules/{Modulo}/`.
- Las historias de usuario (`HU-*.md`) que cambien en un PR pasan por `scripts/lint_hu.py` en CI (estructura, no contenido). Para probarlo antes de subir: `python scripts/lint_hu.py --scan`.
- `tests/Bootstrap.IntegrationTests` levanta la app completa en memoria (sin BD ni servicios externos) y fija el contrato HTTP; a diferencia de los tests de `infrastructure`, corre en cada `dotnet test`. Si tocas `Bootstrap/` (manejo de errores, middleware, arranque) o el contrato de errores, corre y actualiza esos tests en el mismo PR.
- Los tests de `domain`/`application` no deben requerir base de datos real ni levantar la app completa — usa mocks de los puertos.
- Los tests de `infrastructure` (que sí tocan la BD real o servicios externos) van separados como tests de integración, y no corren en cada build local, solo en CI o bajo demanda. **Hoy no existe ninguno:** no hay tests contra MySQL (`MySqlTaskRepository` no tiene tests) ni ningún paso de CI que los corra. La regla describe dónde irían cuando se agreguen.

## 6. Cuándo tocar `ARCHITECTURE.md`

Si en una discusión de PR se decide algo que cambia o aclara la convención
(por ejemplo, una nueva regla sobre cómo se comunican los módulos), ese
mismo PR debe actualizar `ARCHITECTURE.md`. La documentación de arquitectura
no debe quedar desactualizada respecto al código real.

## 7. Dudas o desacuerdos

Este documento y `ARCHITECTURE.md` son documentos vivos. Si algo no
tiene sentido en la práctica, se discute en equipo y se actualiza —
no se ignora la convención en silencio ni se hacen excepciones sin
dejarlas documentadas.

## 8. CI y GitHub Actions

**Actions fijadas por SHA.** Cada `uses:` en `.github/workflows/*.yml` va
pineado al SHA de commit completo, con un comentario al lado indicando la
versión/tag a la que corresponde (ej. `uses: actions/checkout@11d5960...
# v4.4.0`), no a un tag mutable (`@v4`). Un tag como `@v4` puede
repuntar a un commit distinto sin que nadie del equipo lo note — el SHA no
cambia nunca sin que el diff lo muestre. **No se actualizan a mano:**
`.github/dependabot.yml` vigila las actions semanalmente y abre un PR con
el SHA nuevo cuando sale una versión; ese PR se revisa y mergea como
cualquier otro, nunca se edita el SHA directamente sin ese PR de por medio.

**Dependabot también vigila NuGet**, semanalmente, sobre los `.csproj` de
todo el repo. Además, cada PR corre `dotnet list package --vulnerable` en
CI y falla el build si encuentra algo — Dependabot es la vía normal para
mantenerse al día, y ese paso es la red que evita que algo vulnerable se
mergee mientras un PR de Dependabot todavía no se revisó.
