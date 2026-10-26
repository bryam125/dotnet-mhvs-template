---
name: security-detection-response
description: Protocolo a seguir cuando una herramienta de seguridad (antivirus, EDR) marca o pone en cuarentena un archivo fuente, de test, o un artefacto de build/compilación. Detiene la ejecución, preserva evidencia, y nunca decide por su cuenta desactivar protecciones ni crear exclusiones — esa decisión es de un humano de seguridad/IT.
metadata:
  opencode/autoinvoke: "true"
---

# Skill: security-detection-response

**Cuándo usar este skill:** reconoces en tu propia salida (o el usuario
te lo dice) que una herramienta de seguridad marcó o puso en cuarentena
algo — por ejemplo: un build que falla porque "no encontró el DLL
esperado" después de compilar sin problema antes, un mensaje de
"archivo trasladado a cuarentena", o el usuario reportando una alerta
de su antivirus/EDR corporativo. No esperes a que te lo pidan
explícitamente — actívate solo ante esa señal.

## Contexto: por qué existe este skill

Este patrón ya se observó dos veces de forma independiente en la
familia de proyectos MHVS: una vez con Bitdefender sobre un ensamblado
de `Bootstrap.IntegrationTests` en un proyecto derivado del template, y
otra vez con un motor distinto durante el desarrollo de otro proyecto
también basado en esta arquitectura. Ambos casos coincidieron en el
mismo tipo de artefacto: **ensamblados de referencia intermedios**
(`obj/.../refint/*.dll`) de proyectos de tests de integración con
métodos `async` (los sufijos `d__N` que el compilador genera para cada
máquina de estado `async`/`await` parecen confundir a clasificadores de
ML agresivos). Esto no prueba que toda detección futura sea inofensiva
— pero es un patrón recurrente que vale la pena tener presente.

## El protocolo, paso a paso

### 1. Detente de inmediato

No vuelvas a correr `dotnet build`/`dotnet test` intentando que la
alerta desaparezca. Congela la secuencia ahí mismo.

### 2. Recoge evidencia, sin modificar nada todavía

Registra:
- Ruta exacta del archivo marcado.
- Nombre de la detección, si es visible (ej. `Gen:Illusion.ML.Miura...`).
- Configuración (Debug/Release), proyecto, y el comando exacto que
  se estaba ejecutando.

### 3. Compara contra el último estado conocido bueno

`git log`/`git diff` desde el último build/test exitoso, para aislar
qué cambió entre "no había alerta" y "ahora sí" — sin asumir todavía
que ese delta sea la causa.

### 4. Revisa el delta buscando comportamiento genuinamente sospechoso

Busca patrones reales, no "algo que borrar para que se calle":
ejecución de procesos/shell/PowerShell, P/Invoke o acceso nativo,
manipulación del Registro, persistencia, carga dinámica de ensamblados
o `Reflection.Emit`, manejo sospechoso de credenciales/secretos,
comunicación de red inesperada.

Si no encuentras nada de eso, dilo explícitamente ("revisé el delta,
no encontré comportamiento sospechoso en el alcance revisado") — no te
quedes callado sobre el resultado.

### 5. Si el archivo marcado es un binario compilado, inspecciónalo directamente

No te quedes solo con el código fuente. Corre, como mínimo:
```
file <archivo>
strings -n 8 <archivo> | grep -E "<namespaces/clases esperadas del proyecto>"
```
Un ensamblado .NET legítimo, sin ofuscar, muestra nombres de clases y
métodos completamente legibles que deberían coincidir con el código
que el propio proyecto escribió. Contenido ilegible, empaquetado, o de
tamaño desproporcionado son señales distintas — repórtalas si las ves.

### 6. Reporta con lenguaje preciso, nunca con conclusiones que no puedes sostener

Nunca digas "es un falso positivo" ni "es malware" de forma categórica.
Usa lenguaje como: *"detección heurística observada en [ruta], revisé
el delta y el binario, no encontré comportamiento sospechoso, pero no
puedo confirmar la causa exacta desde aquí"*.

### 7. No toques la protección — esto no es tu decisión

Nunca, bajo ninguna circunstancia:
- Desactivar, pausar, reducir o reconfigurar el antivirus/EDR.
- Crear exclusiones para el repositorio, `bin/`, `obj/`, DLLs, o
  procesos de build.
- Restaurar automáticamente un archivo de cuarentena.
- Modificar reglas de negocio, contratos de API, arquitectura, o
  **eliminar tests/assertions** solo para que la alerta desaparezca.

Estas decisiones le corresponden a un humano de seguridad/IT. Tu
trabajo termina en "aquí está la evidencia" — nunca en "ya lo resolví
desactivando/excluyendo algo".

### 8. Si encuentras un bug real, no relacionado con el antivirus, sepáralo con claridad

Está bien corregirlo si tiene justificación funcional, arquitectónica
o de calidad independiente — pero etiquétalo explícitamente como eso,
nunca como "esto resolvió el antivirus" sin evidencia de causalidad.

### 9. Si se autoriza diagnóstico por aislamiento, un cambio reversible a la vez

Nunca reescribas o elimines varios métodos de golpe "a ver cuál era".
Un cambio, documentado, reversible, preservando cobertura equivalente
— no reduciéndola.

### 10. Retoma con validación progresiva, deteniéndote de nuevo ante cualquier alerta nueva

Orden: proyecto mínimo afectado → sus tests → integración → solución
completa. Si aparece una alerta nueva en cualquier punto, vuelve al
paso 1 — no sigas adelante "a ver si esta vez no pasa nada".

## Formato del reporte final al usuario

Distingue explícitamente tres categorías de cambio, si hiciste alguno:
1. **Cambios funcionales** — con justificación de negocio/arquitectura.
2. **Cambios de calidad** — mejoras independientes del incidente.
3. **Cambios de diagnóstico temporales** — deben restaurarse o
   sustituirse por una prueba funcionalmente equivalente, nunca
   quedarse como una reducción permanente de cobertura.

Nunca mezcles estas tres categorías en una sola explicación ambigua.
