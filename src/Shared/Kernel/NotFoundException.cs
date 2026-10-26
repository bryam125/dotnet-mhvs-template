namespace MyApp.Shared.Kernel;

/// <summary>
/// El recurso referenciado no existe (o su identificador está mal formado).
/// Deriva de <see cref="DomainException"/>, pero <c>GlobalExceptionHandler</c> la comunica como 404
/// en lugar del 400 genérico de las demás reglas de negocio.
/// </summary>
public class NotFoundException(string message) : DomainException(message);
