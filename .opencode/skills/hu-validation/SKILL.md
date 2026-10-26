---
name: hu-validation
description: Valida semánticamente si una historia de usuario (HU) está completa y bien formada antes de implementarla — revisa coherencia del Como/quiero/para, restricciones concretas en Datos de entrada, cobertura uno a uno de cada Regla de negocio violable por un Criterio de aceptación, y contradicciones de valores entre Datos de entrada, Reglas, Flujo principal, Errores y Alcance.
metadata:
  opencode/autoinvoke: "true"
---

# Skill: hu-validation

**Cuándo usar este skill:** te piden validar, revisar o dar el visto
bueno a una historia de usuario (HU) antes de que se implemente.

**Relación con chequeos automáticos:** si el repo tiene un chequeo
determinístico de la estructura de las HU (por ejemplo un linter en CI),
ese corre solo y no necesita IA — si falla, ni siquiera llegues a este
skill. Tu trabajo aquí es la validación **semántica**, la que un script
no puede hacer: ¿tiene sentido de negocio?, ¿los criterios de aceptación
realmente cubren las reglas?, ¿hay contradicciones?

## Formato esperado de una HU

Secciones: Historia de usuario, Alcance, Datos de entrada, Valores
generados por el sistema, Flujo principal, Reglas de negocio, Errores
que deben comunicarse, Criterios de aceptación, Trazabilidad. Si el repo
trae una plantilla en blanco (por ejemplo `docs/templates/HU-TEMPLATE.md`)
o una HU ya aprobada, úsalas como referencia de formato.

## Checklist de validación semántica

- [ ] El rol y el beneficio del "Como/quiero/para" son coherentes entre
      sí (el beneficio se sigue lógicamente de la acción, no es
      genérico tipo "para mejorar el sistema").
- [ ] Cada restricción en "Datos de entrada" es verificable y concreta
      (un rango numérico, un catálogo cerrado, un patrón) — no
      adjetivos vagos como "válido" o "correcto" sin definir qué
      significa.
- [ ] "Valores generados por el sistema" no se solapa con "Datos de
      entrada" (un campo no puede estar en ambas tablas).
- [ ] **Cobertura regla por regla** (procedimiento abajo): cada regla
      violable por el usuario tiene su propio CA de fallo, identificado
      por número. Un conteo agregado no sirve como sustituto.
- [ ] **Coherencia entre secciones** (procedimiento abajo): ningún
      límite, valor, catálogo o condición aparece con dos valores
      distintos en dos secciones.
- [ ] Ningún criterio de aceptación describe una escritura parcial
      como resultado aceptable de un error (toda regla de "todo o
      nada" debe ser explícita).
- [ ] "Alcance → no incluye" no contradice lo que un criterio de
      aceptación **o un paso del "Flujo principal"** da por hecho que
      sí pasa (ej. el Alcance excluye "items archivados" y un paso del
      Flujo los restaura).
- [ ] Los criterios de aceptación describen el contrato observable, no
      la implementación interna (ver "Qué puede y qué no puede
      mencionar un CA" abajo).
- [ ] La sección "Trazabilidad" referencia un diseño técnico o caso de
      uso real, no queda vacía ni con placeholders sin completar.

### Procedimiento: cobertura regla por regla (no un conteo)

**Contar reglas y contar CA de fallo NO es una verificación válida**, ni
siquiera como atajo. Una HU con 3 reglas y 3 CA de fallo pasa el
conteo, pero puede tener un hueco: si dos CA ejercen la regla 1 y
ninguno la regla 3, la regla 3 queda sin cubrir. Haz esto y muestra la
tabla en tu respuesta:

1. Enumera **cada** regla de "Reglas de negocio" por su número.
2. Clasifica cada una en **exactamente una** de estas categorías, con
   este criterio y no por intuición:
   - **Violable por el usuario:** existe al menos una solicitud que el
     usuario puede enviar que hace que la regla no se cumpla y el
     sistema tenga que rechazarla. Esa solicitud puede ser un valor de
     entrada, un identificador, o un estado previo que el mismo usuario
     provocó (como un duplicado). Si puedes escribir un "Dado ... cuando
     el usuario envía ..." que la viole, la regla es violable.
   - **Invariante de sistema/arquitectura:** la regla la garantiza el
     propio sistema y **ninguna entrada del usuario** puede romperla.
     Ejemplos: "el id lo genera el sistema y es único", "la fecha de
     creación es la del servidor", "no se guarda nada parcial". Si la
     regla restringe un campo que el usuario envía, **no** es
     invariante: reclasifícala como violable.
   - Si dudas entre las dos, trátala como **violable** y exige el CA. Un
     CA de más cuesta menos que un hueco.
3. Para cada regla violable, identifica el CA **específico** que la
   ejerce. Su "Dado/cuando" debe violar **esa** regla, no otra, y su
   "entonces" debe describir el rechazo. Un mismo CA cubre dos reglas
   solo si su escenario viola ambas de forma explícita. Nunca cuentes un
   CA genérico ("datos inválidos → error") como cobertura de varias
   reglas.
4. Escribe el resultado como tabla:

   | Regla | Clasificación | CA que la ejerce |
   |---|---|---|
   | 1 | Violable | CA-02 |
   | 2 | Violable | **ninguno → bloqueante** |
   | 3 | Invariante (el id lo genera el sistema) | n/a |

   Toda fila "Violable" sin CA es un hallazgo **bloqueante**, aunque el
   total de CA de fallo sea igual o mayor que el de reglas.

### Procedimiento: coherencia entre secciones

La misma restricción suele repetirse en varias secciones, y ahí aparecen
las contradicciones. No basta con cruzar el Alcance contra los CA. Cruza
entre sí estas cuatro secciones:

- **Datos de entrada** (columna "Restricciones")
- **Reglas de negocio**
- **Flujo principal**
- **Errores que deben comunicarse**

Usa además, como quinta referencia, los valores concretos que usan los
**Criterios de aceptación**.

Para cada campo, límite o catálogo que aparezca en más de una sección:

1. Extrae el valor en cada lugar. Ejemplo: "`title`: máx 100
   caracteres" en Datos de entrada, "el título no puede superar 50
   caracteres" en la Regla 2, y un CA que envía 80 caracteres y espera
   éxito.
2. Si los valores no son idénticos (otro límite, un catálogo con otros
   miembros, otra obligatoriedad, otra unidad), es un hallazgo
   **bloqueante**. Cita textualmente todas las apariciones, con su
   sección. No elijas tú cuál es la correcta: eso lo decide el autor.
3. Revisa además estos cruces:
   - **Errores ↔ Reglas:** cada situación de "Errores que deben
     comunicarse" corresponde a una regla o restricción existente, y
     cada regla violable tiene su situación ahí. Un error sin regla que
     lo origine, o una regla violable sin error comunicado, es un
     hallazgo.
   - **Flujo ↔ Reglas:** cada paso del "Flujo principal" que valida o
     rechaza algo corresponde a una regla o restricción escrita. Un paso
     que aplica una condición que no figura en ninguna otra sección es
     una regla oculta, y eso es un hallazgo.

### Qué puede y qué no puede mencionar un CA

La línea está entre **contrato observable** e **implementación
interna**:

- **Sí puede** mencionar lo que observa un cliente de la API: el código
  de estado HTTP ("responde 404", "responde 400"), los campos del cuerpo
  de respuesta que forman parte del contrato, el mensaje de error que ve
  el usuario, y que no quedó nada guardado. En una API pública, el
  código de estado **es** parte del contrato.
- **No debe** mencionar detalles internos: nombres de clases o de
  excepciones (`NotFoundException`, `RenameSampleItemHandler`), tablas o
  columnas de base de datos, middleware, filtros, librerías
  (FluentValidation), ni el orden interno de los componentes.

Así se puede cumplir la convención de `ARCHITECTURE.md` →
"Precedencia". Si un caso de uso necesita un orden distinto del de por
defecto (la validación de entrada, 400, gana sobre "no encontrado",
404), lo declara en un CA en términos observables. Por ejemplo: "Dado
un id inexistente y un nombre vacío, cuando se envía la solicitud,
entonces responde 400 por el nombre, no 404". Un CA así **no** es un
hallazgo. Un CA que dice "el validador corre antes que el handler"
**sí** lo es.

### Documentos externos referenciados (ej. `DT-xxx`)

Que un documento referenciado no exista en este repositorio **no es,
por sí solo, motivo de rechazo**. Muchos equipos mantienen el diseño
técnico en una herramienta externa (Notion, Confluence, un vault de
Obsidian). Decide con este criterio, no a juicio libre:

- **Informativo (no bloquea):** la referencia es solo trazabilidad. La
  HU se puede implementar y probar completa leyendo únicamente la HU.
  Lo típico es que aparezca solo en "Trazabilidad" o en el "Objetivo".
  Repórtalo aparte: "no pude verificar el documento X porque no está en
  este repositorio; confírmalo por tu cuenta si hace falta".
- **Bloqueante:** la HU **delega en el documento contenido que
  necesita**. Aplica si se cumple al menos uno de estos casos:
  1. Una regla de negocio, una restricción de "Datos de entrada" o un
     valor generado remite al documento en vez de enunciarse. Ejemplos:
     "el cálculo sigue DT-042", "catálogo según el DT", "formato
     definido en DT-xxx".
  2. Un CA depende de un valor que solo está en el documento, así que
     no se puede escribir su test con lo que dice la HU.
  3. Dos secciones de la HU se contradicen, y la HU dice que el
     documento resuelve cuál prevalece.

  En estos casos el hallazgo no es "falta el archivo", sino "la regla N
  (o el campo X) no es verificable sin DT-xxx". Pide que el contenido
  necesario se copie a la HU.

## Cómo responder

Separa tus hallazgos en dos categorías, siempre:

- **Bloqueantes**: hacen que la HU no esté lista para implementarse
  (una regla violable sin su CA, valores contradictorios entre
  secciones, campos ambiguos, contenido delegado a un documento
  externo). Estos sí impiden pasar a `hu-to-slice-scaffold`.
- **Informativos**: cosas que vale la pena señalar pero no impiden
  avanzar (documentos externos no verificables, sugerencias de
  redacción, inconsistencias menores de formato). Acláralo
  explícitamente: "esto no bloquea la implementación".

Incluye siempre la tabla regla → clasificación → CA, pase o no la HU.

Si la HU pasa todo: dilo explícitamente y lista qué la hace sólida
(por ejemplo "las 6 reglas violables tienen cada una su CA de fallo:
R1→CA-02, R2→CA-03, ...; la R7 es un invariante del sistema").

Si algo falla: por cada punto que falle, cita textualmente la parte de
la HU que lo causa y qué información concreta falta o qué contradicción
hay. No la reescribas tú mismo sin que te lo pidan — la corrección es
del autor humano de la HU, salvo que te pidan explícitamente redactarla.

No pases al skill `hu-to-slice-scaffold` si la validación falla,
aunque el usuario tenga prisa — recuérdaselo una vez y espera
confirmación explícita de "impleméntala igual" antes de generar código
sobre una HU incompleta.
