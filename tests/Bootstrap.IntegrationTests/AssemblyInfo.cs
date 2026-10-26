using Xunit;

// Este proyecto levanta varias apps reales en memoria (WebApplicationFactory<Program>), algunas con
// configuración distinta (entorno Production para HSTS, límites de rate limiting bajos para el test
// de 429). Sin esto, xUnit correría esas colecciones en paralelo por defecto: varias instancias de
// log4net escribiendo el mismo logs/app.log a la vez, y ningún beneficio real de paralelizar una
// suite que ya corre en menos de un segundo. Todo el proyecto corre en serie a propósito.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
