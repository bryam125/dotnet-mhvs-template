# Nueva plantilla de proyecto: MHVS
### (Modular Hexagonal Vertical Slicing)

Documento para compartir con el equipo — resume el qué, el por qué y el
cómo. Úsalo como base de una charla de 15-20 minutos.

---

## 1. El problema que resuelve

Con arquitecturas por capas horizontales tradicionales (`Controllers/`,
`Services/`, `Repositories/`), agregar **una sola feature** obliga a tocar
archivos en 4-5 carpetas distintas, muy separadas entre sí. A medida que
el proyecto crece:

- Es difícil ver "todo lo que hace" una funcionalidad de un vistazo.
- El código de negocio termina acoplado a frameworks (EF Core, ASP.NET)
  porque no hay una frontera clara.
- Los módulos de negocio se mezclan entre sí sin que nadie lo note, hasta
  que separar algo en un servicio aparte se vuelve casi imposible.

## 2. La solución: tres ideas combinadas

| Idea | Qué aporta |
|---|---|
| **Hexagonal (Ports & Adapters)** | El negocio (`domain`/`application`) nunca depende de infraestructura. Define *puertos* (interfaces); la infraestructura los implementa como *adaptadores*. |
| **Vertical Slice Architecture** | Cada caso de uso vive en su propia carpeta con todo lo que necesita — no está repartido entre capas. |
| **Modular Monolith** | El código se agrupa por dominio de negocio (`Auth`, `Orders`, `Billing`...), cada uno autocontenido, listo para separarse en microservicio si algún día hace falta. |

## 3. La regla de 3 niveles

```
Módulo        →  bounded context (Auth, Orders, Billing...)
  └─ Hexágono   →  Application / Domain / Infrastructure
       └─ Slice    →  un caso de uso por carpeta (CreateOrder, RenameOrder...)
```

```
src/Modules/{Modulo}/{Application|Domain|Infrastructure}/{Slice}/
```

**Regla de dependencias (no negociable):**

```
infrastructure → application → domain
```
`domain` no depende de nada externo. Si mañana cambiamos de SQL Server a
Postgres, o de SMTP a SendGrid, solo se toca `infrastructure`.

## 4. Qué trae la plantilla lista para usar

- ✅ **.NET 10**, con el SDK fijado en `global.json`. Cambiar de versión mayor implica tocar los `.csproj`, `global.json`, el `Dockerfile` y revisar los paquetes ligados al runtime (ver `ARCHITECTURE.md` → "Versión de .NET y del SDK"); no es una sola línea.
- ✅ **Logging con log4net** desde el arranque (consola + archivo rotado
  por día), inyectado como `ILogger<T>` estándar — los módulos nunca
  dependen de log4net directamente, solo de la abstracción de .NET.
- ✅ **Credenciales desacopladas de `appsettings.json`**: viven en
  `config/connectionstrings.local.json`, un archivo que está en
  `.gitignore` y nunca se sube al repositorio. Se versiona en su lugar
  un `.example` que indica qué llaves hacen falta.
- ✅ **Módulo de ejemplo (`Sample`)** funcionando de punta a punta (con
  repositorio en memoria, sin necesidad de configurar BD) para que
  cualquiera vea la convención corriendo antes de escribir su primer
  módulo real.
- ✅ `ARCHITECTURE.md` — la convención completa por escrito, con
  checklist de PR.

## 5. Cómo se ve un módulo en la práctica

```
Modules/Orders/
├── Application/
│   ├── OrdersModule.cs          # registro de DI + endpoints
│   ├── CreateOrder/
│   │   ├── CreateOrderCommand.cs
│   │   └── CreateOrderHandler.cs
│   └── CancelOrder/
│       ├── CancelOrderCommand.cs
│       └── CancelOrderHandler.cs
├── Domain/
│   ├── Order.cs
│   └── OrderRepository.cs        # puerto
└── Infrastructure/
    └── SqlOrderRepository.cs      # adaptador
```

## 6. Por qué esto nos conviene como equipo

- **Onboarding más rápido**: alguien nuevo abre una carpeta de módulo y
  entiende de inmediato el alcance de ese pedazo del sistema.
- **Cambios más seguros**: tocar un caso de uso no arriesga romper otro
  por accidente, porque cada slice es autocontenido.
- **Testeable de verdad**: el `domain` y `application` no dependen de
  base de datos ni de framework, así que se prueban con mocks simples y
  rápido.
- **Preparados para escalar**: si un módulo crece mucho, separarlo a su
  propio servicio es un ejercicio de infraestructura, no una reescritura
  de lógica de negocio.
- **Seguridad desde el día 1**: nunca más una credencial de BD commiteada
  por accidente en `appsettings.json`.

## 7. Próximos pasos sugeridos

1. Clonar la plantilla y correrla localmente (instrucciones en `README.md`).
2. Leer `ARCHITECTURE.md` completo — es corto, 10 minutos.
3. Definir juntos el primer módulo real del proyecto y armarlo copiando
   `Sample` como base.
4. Dudas o desacuerdos con la convención → se discuten y, si aplica, se
   actualiza `ARCHITECTURE.md` — es un documento vivo, no una ley grabada
   en piedra.
