# Contexto de agente — dotnet-mhvs-template

Este archivo es el punto de entrada para cualquier agente (Open Code u
otro) que trabaje en este repo. No contiene los procedimientos
detallados — esos viven en skills separados bajo `.opencode/skills/`,
cada uno enfocado en una sola capacidad. Carga solo el skill que
corresponda a la tarea que te pidan; no cargues los tres de una vez si
solo te pidieron una cosa.

## Qué es este repo

Plantilla base en .NET 10 (`net10.0`, SDK fijado en `global.json`) que sigue la
arquitectura **MHVS (Modular Hexagonal Vertical Slicing)** — ver
`ARCHITECTURE.md`, es la fuente de verdad sobre la convención de
carpetas y dependencias. No te desvíes de ella sin que un humano lo
apruebe explícitamente (ver `CODEOWNERS`).

## Skills disponibles

| Cuándo te piden esto | Usa este skill |
|---|---|
| "¿Esta HU está completa/bien escrita?" | `.opencode/skills/hu-validation/SKILL.md` |
| "Genera el código para esta HU" / "implementa esta HU" | `.opencode/skills/hu-to-slice-scaffold/SKILL.md` |
| "Revisa este PR" / "¿esto respeta la arquitectura?" | `.opencode/skills/architecture-review/SKILL.md` |
| Un antivirus/EDR marca o pone en cuarentena un archivo durante build/test | `.opencode/skills/security-detection-response/SKILL.md` |

Si la tarea combina varias (ej. "valida esta HU y si está bien,
impleméntala"), carga los skills en orden: primero
`hu-validation`, y solo si pasa, `hu-to-slice-scaffold`.

## Reglas generales que aplican sin importar el skill

- Logging: log4net vía `ILogger<T>`, nunca referenciado directo en
  módulos de negocio.
- Credenciales: nunca en `appsettings.json`. Siempre en
  `config/connectionstrings.local.json` (gitignored). Si generas código
  que necesita una cadena de conexión, léela de `IConfiguration`, no la
  hardcodees.
- Commits: Conventional Commits (`feat`, `fix`, `chore`, `refactor`,
  `test`, `docs`, `perf`) — ver `CONTRIBUTING.md`.
- No inventes reglas de negocio, catálogos o nombres de campos que no
  estén explícitos en la HU o en `ARCHITECTURE.md`. Ante una ambigüedad,
  pregunta antes de asumir.
- Antes de dar por terminada cualquier tarea de generación de código,
  corre `dotnet build` y `dotnet test`, y reporta el resultado.

## Otros documentos relevantes

- `ARCHITECTURE.md` — convención MHVS completa.
- `CONTRIBUTING.md` — ramas, commits, reglas de PR.
- `CHANGELOG.md` — historial de cambios a la plantilla/arquitectura.
- `CODEOWNERS` — qué rutas son sensibles y quién debería revisarlas; la
  lista vive solo en ese archivo. Hoy es un recordatorio, no un control (ver
  `CONTRIBUTING.md`).
- `ROADMAP.md` — deuda técnica conocida y mejoras pendientes; consúltalo
  antes de asumir que algo "ya funciona" o "ya está cubierto".
- `docs/templates/HU-TEMPLATE.md` — plantilla en blanco para historias
  de usuario nuevas.
- `scripts/lint_hu.py` — validación estructural (sin IA) de las HU; corre
  en CI sobre los `HU-*.md` que cambian en un PR. Es la primera línea de
  defensa: `hu-validation` (semántica) solo tiene sentido cuando este
  script ya pasa.
