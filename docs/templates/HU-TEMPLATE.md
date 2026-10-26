# HU-{MODULO}-{NUMERO} — {Verbo} {entidad}

> [!abstract] Objetivo
> {Una frase: qué capacidad funcional representa esta historia y de qué diseño técnico se desprende, si aplica.}

## Historia de usuario

**Como** {rol/tipo de usuario},
**quiero** {acción concreta},
**para** {beneficio/razón de negocio}.

## Alcance

Esta historia incluye:

- {...}

Esta historia no incluye:

- {...}

## Datos de entrada

| Campo | Tipo conceptual | Obligatorio | Restricciones |
|---|---|---:|---|
| `{campo}` | {tipo} | Sí/No | {restricción exacta, no "válido"} |

## Valores generados por el sistema

| Campo | Valor o regla |
|---|---|
| `id` | {regla de generación} |
| `status` | {estado inicial} |
| `created_at` | {regla} |

## Flujo principal

1. {paso}
2. {paso}

## Reglas de negocio

1. {regla verificable}
2. {regla verificable}

## Errores que deben comunicarse

| Situación | Comportamiento esperado |
|---|---|
| {situación} | {qué debe informar el sistema, sin exponer detalles internos} |

## Criterios de aceptación

### CA-01 — {caso exitoso}

**Dado** {contexto/datos válidos},
**cuando** {acción},
**entonces** {resultado observable}.

### CA-02 — {caso de fallo, una regla de negocio específica}

**Dado** {condición inválida},
**cuando** {acción},
**entonces** {comportamiento de error esperado, sin guardar nada parcial}.

<!-- Repite un CA de fallo por cada regla de negocio que pueda violarse -->

## Lista de comprobación

- [ ] La historia describe valor para la persona usuaria, no una tarea técnica.
- [ ] Las entradas y los valores generados por el sistema están separados.
- [ ] Los catálogos/enums están completos y coinciden con el diseño técnico, si existe.
- [ ] Hay al menos un CA exitoso y un CA de fallo por cada regla de negocio.
- [ ] Ningún fallo describe una escritura parcial como aceptable.
- [ ] El alcance dice explícitamente qué NO incluye.

## Trazabilidad

| Elemento | Referencia |
|---|---|
| Diseño fuente | {enlace al DT, si existe} |
| Caso de uso | `CU-{numero} — {nombre}` |
| Reglas principales | {qué reglas del DT cubre esta HU} |

> [!success] Definición de terminado
> Esta historia está lista para implementarse cuando cada criterio de aceptación se puede convertir en una prueba automatizada y no quedan dudas sobre entradas, reglas, resultado exitoso o comportamiento ante errores.
