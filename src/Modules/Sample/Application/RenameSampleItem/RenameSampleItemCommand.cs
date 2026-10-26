namespace MyApp.Modules.Sample.Application.RenameSampleItem;

/// <summary>
/// Comando interno del slice: combina el id de la ruta con el cuerpo de la solicitud.
/// Es opcional-nullable a propósito: la validación de entrada ocurre en el borde HTTP.
/// </summary>
public sealed record RenameSampleItemCommand(
    string? SampleItemId,
    string? Name);
