namespace MyApp.Modules.Sample.Application.RenameSampleItem;

/// <summary>
/// Cuerpo HTTP del slice. El identificador del item viaja en la ruta, no aquí.
/// El filtro de validación se aplica sobre este tipo, que es el que llega como argumento del endpoint.
/// </summary>
public sealed record RenameSampleItemRequest(string? Name);
