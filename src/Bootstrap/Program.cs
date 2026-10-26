using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging.Log4Net.AspNetCore;
using MyApp.Bootstrap.Configuration;
using MyApp.Bootstrap.Diagnostics;
using MyApp.Bootstrap.ExceptionHandling;
using MyApp.Bootstrap.Middleware;
using MyApp.Modules.Sample.Application;
using MyApp.Modules.Tasks.Application;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------
// 1) Configuración en capas, de menor a mayor prioridad (la última fuente gana):
//    appsettings.json (versionado, sin secretos)
//      -> appsettings.{Environment}.json (versionado, sin secretos)
//      -> variables de entorno y argumentos de línea de comandos (los agrega CreateBuilder)
//      -> config/connectionstrings.local.json (NO versionado, con secretos; lo agrega este bloque)
//      -> variables de entorno, otra vez (este bloque; pisan todo lo anterior)
//    CreateBuilder ya lee las variables de entorno; el AddEnvironmentVariables() de abajo existe para
//    que también ganen sobre el archivo local. Consecuencia: los argumentos de línea de comandos
//    pierden contra el archivo local y contra las variables de entorno (ver config/README.md).
//    El archivo local es opcional (en producción/CI se usan variables de entorno), pero se busca
//    con LocalConfigLocator y se avisa al arrancar si no se encontró: con una ruta relativa se
//    resolvía contra src/Bootstrap/ y se ignoraba en silencio.
//    Punto de extensión: un proveedor de secretos (Azure Key Vault u otro) se agregaría aquí, junto
//    a AddEnvironmentVariables(). Hoy no hay ninguno integrado (ver ARCHITECTURE.md).
// ---------------------------------------------------------------------
var localConfigPath = LocalConfigLocator.Locate(builder.Environment.ContentRootPath);
if (localConfigPath is not null)
{
    builder.Configuration.AddJsonFile(localConfigPath, optional: false, reloadOnChange: true);
}

builder.Configuration.AddEnvironmentVariables();

// ---------------------------------------------------------------------
// 2) Logging con log4net desde el arranque.
//    AddLog4Net ya lee y aplica log4net.config: NO llamar además a XmlConfigurator.Configure.
//    Configurarlo dos veces abre dos veces el appender de archivo y log4net imprime
//    "log4net:ERROR ... Unable to acquire lock on file logs/app.log" en cada arranque.
// ---------------------------------------------------------------------
builder.Logging.ClearProviders();
builder.Logging.AddLog4Net("log4net.config");

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
// Formato A de error (filtro de validación, ver ARCHITECTURE.md → "Contrato de errores"): sin traceId.
// A partir de .NET 10, Results.ValidationProblem añade por su cuenta un "traceId" con el id de la
// Activity (formato W3C), que además NO coincide con el X-Correlation-ID. Se quita para no cambiar el
// contrato con el salto de runtime; el Formato B (GlobalExceptionHandler) sí lleva su propio traceId.
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context => context.ProblemDetails.Extensions.Remove("traceId"));
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---------------------------------------------------------------------
// Punto de extensión — Observabilidad (trazas distribuidas y métricas).
//
// La plantilla NO trae OpenTelemetry ni ningún exportador: hoy solo hay logs locales (log4net) y el
// X-Correlation-ID (ver ARCHITECTURE.md → "Observabilidad"). Si un proyecto lo necesita, el lugar es
// AQUÍ, en el bloque de servicios, junto a AddHealthChecks() y antes de builder.Build():
// OpenTelemetry.Extensions.Hosting se registra como servicio (con la instrumentación de ASP.NET Core
// y un exportador OTLP). NO hace falta ningún app.Use...: se engancha a la Activity que el propio
// hosting ya crea antes del primer middleware. Sin código de ejemplo a propósito; la decisión de cómo
// enlazar el Activity.TraceId con el correlation ID sigue abierta (ver ARCHITECTURE.md).
// ---------------------------------------------------------------------

// ---------------------------------------------------------------------
// 3) Cabeceras reenviadas por el proxy (ver ARCHITECTURE.md → "HTTPS y el
//    proxy delante del contenedor"). Producción se despliega detrás de un
//    load balancer/reverse proxy que termina TLS y le habla a la app en HTTP
//    plano; sin esto, Request.Scheme siempre sería "http" y UseHttpsRedirection
//    /UseHsts no tendrían forma de saber que la request original sí fue HTTPS.
//
//    Seguridad: por defecto ForwardedHeadersOptions solo confía en el primer
//    hop (ForwardLimit = 1) y en cualquier proxy si no se restringe
//    KnownProxies/KnownNetworks — eso es aceptable en un contenedor detrás de
//    un único load balancer de confianza, pero un despliegue real con más de
//    un proxy en cadena, o expuesto directo a internet sin proxy, DEBE fijar
//    KnownProxies/KnownNetworks a la IP real del proxy. De lo contrario un
//    cliente externo podría falsificar X-Forwarded-Proto: https y saltarse la
//    redirección/HSTS sin que la conexión sea realmente HTTPS.
// ---------------------------------------------------------------------
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

// ---------------------------------------------------------------------
// 4) CORS: sin orígenes permitidos por defecto (ver ARCHITECTURE.md → "CORS").
//    Es la postura intencional del template, no un olvido: una API pensada
//    para consumirse desde otro backend o desde el mismo origen no necesita
//    CORS. Si un proyecto real agrega un frontend en otro origen, agrega sus
//    URLs a Cors:AllowedOrigins en appsettings.json (o el archivo de
//    credenciales locales/variables de entorno si son distintas por entorno)
//    — no relajes esto a AllowAnyOrigin().
// ---------------------------------------------------------------------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy("Default", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
        // Sin orígenes configurados, la política queda sin WithOrigins(...): no permite ningún
        // origen cross-origin (el comportamiento por defecto de CORS es denegar).
    }));

// ---------------------------------------------------------------------
// 5) Rate limiting (nativo de ASP.NET Core desde .NET 7, sin paquete NuGet).
//    Un único limitador global, particionado por IP del cliente, ventana fija.
//    Ajustable vía RateLimiting:PermitLimit / RateLimiting:WindowSeconds sin
//    recompilar. Los valores por defecto (100 req/60s por IP) son un punto
//    de partida razonable para una API interna; ajústalos al perfil de tráfico
//    real del proyecto.
//
//    Los valores se leen de IConfiguration DENTRO del delegado de cada
//    partición (no en una variable capturada aquí arriba): builder.Configuration
//    todavía puede recibir más fuentes antes de builder.Build() — por ejemplo
//    WebApplicationFactory<Program> en tests inyecta overrides justo en ese punto
//    — y una variable capturada antes de Build() se queda con el valor de ese
//    momento para siempre, ignorando cualquier fuente añadida después.
// ---------------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        var permitLimit = configuration.GetValue("RateLimiting:PermitLimit", 100);
        var windowSeconds = configuration.GetValue("RateLimiting:WindowSeconds", 60);
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "sin-ip";
        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromSeconds(windowSeconds),
            QueueLimit = 0
        });
    });
});

// ---------------------------------------------------------------------
// 6) Registro de módulos (MHVS). Cada módulo real que agreguen se
//    registra aquí con una sola línea, igual que Sample y Tasks.
// ---------------------------------------------------------------------
builder.Services.AddSampleModule(); // ejemplo — bórralo junto con Modules/Sample cuando arranques el proyecto real
builder.Services.AddTasksModule();  // MySQL: ConnectionStrings:MysqlTodo (config/connectionstrings.local.json o variable de entorno)

var app = builder.Build();

var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
startupLogger.LogInformation("Aplicación iniciada en el entorno {Environment}", app.Environment.EnvironmentName);

if (localConfigPath is not null)
{
    startupLogger.LogInformation("Configuración local cargada desde {ConfigPath}", localConfigPath);
}
else
{
    // En Development se espera el archivo (README, "Primeros pasos"); en otros entornos lo normal son variables de entorno.
    startupLogger.Log(
        app.Environment.IsDevelopment() ? LogLevel.Warning : LogLevel.Information,
        "No se encontró {RelativePath} (buscado desde {ContentRoot} y hasta 2 niveles arriba). " +
        "Solo se usan appsettings y variables de entorno. Para desarrollo local: cp config/connectionstrings.local.json.example config/connectionstrings.local.json",
        LocalConfigLocator.RelativePath,
        app.Environment.ContentRootPath);
}

// ForwardedHeaders va primero: todo lo que lea Request.Scheme o la IP del cliente
// (redirección HTTPS, HSTS, el limitador de tasa por IP) depende de que esto corra antes.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    // En Development no se fuerza HTTPS: complicaría el flujo local con certificados
    // autofirmados sin aportar nada, porque no hay un proxy real terminando TLS delante.
    app.UseHttpsRedirection();
    app.UseHsts();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Default");
app.UseRateLimiter();

// ---------------------------------------------------------------------
// Punto de extensión — Autenticación/autorización.
//
// La plantilla NO trae autenticación: cada proyecto real trae su propio
// esquema (JWT emitido por un IdP propio, Azure AD/Entra ID, OAuth2 de un
// proveedor externo, etc.) y no tiene sentido adivinarlo de antemano. Este es
// el lugar exacto donde se conecta, en el orden que exige el pipeline de
// ASP.NET Core: después de CORS y del limitador de tasa, antes de mapear los
// endpoints.
//
// Ejemplo con JWT Bearer (el esquema más común en APIs empresariales) — todo
// esto queda comentado a propósito, es un ejemplo, no código que deba ejecutarse:
//
//   // En la sección de servicios (antes de "var app = builder.Build();"):
//   // builder.Services
//   //     .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//   //     .AddJwtBearer(options =>
//   //     {
//   //         options.Authority = builder.Configuration["Auth:Authority"]; // ej. el IdP (Entra ID, Auth0, Keycloak...)
//   //         options.Audience = builder.Configuration["Auth:Audience"];
//   //     });
//   // builder.Services.AddAuthorizationBuilder()
//   //     .AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
//
//   // Aquí, en el pipeline:
//   // app.UseAuthentication();
//   // app.UseAuthorization();
//
// Convención esperada una vez agregado (ver ARCHITECTURE.md → "Autenticación
// y autorización"): TODO endpoint nuevo declara explícitamente
// `.RequireAuthorization()` o `.AllowAnonymous()` — nunca se deja implícito.
// Los dos endpoints de `Sample` ya siguen esa convención (`.AllowAnonymous()`
// explícito en `SampleModule.cs`), como ejemplo del patrón esperado.
// ---------------------------------------------------------------------

app.MapHealthChecks("/health");

// ---------------------------------------------------------------------
// GET /version — qué build está corriendo (ver ARCHITECTURE.md → "Endpoint /version").
// El SDK ya embebe "{Version}+{commit de git}" en AssemblyInformationalVersionAttribute en cada
// build; esto solo lo expone. Se calcula una vez al arrancar: no cambia mientras el proceso vive.
//
// AllowAnonymous a propósito, igual que /health: sirve para responder rápido, durante un
// incidente, "¿este servicio ya tiene el fix X?". En un despliegue real donde no se quiera exponer
// versión y commit públicamente, ESTE es el punto donde se cambia por .RequireAuthorization()
// (una vez conectado el esquema de auth de arriba) o donde se restringe la ruta por red/reverse proxy.
// ---------------------------------------------------------------------
var buildInfo = BuildInfo.From(typeof(Program).Assembly);
app.MapGet("/version", () => Results.Ok(buildInfo)).AllowAnonymous();

app.MapSampleModule();
app.MapTasksModule();

app.Run();

// Expone el Program generado por las top-level statements para WebApplicationFactory<Program>
// (tests/Bootstrap.IntegrationTests). No agrega comportamiento.
public partial class Program { }
