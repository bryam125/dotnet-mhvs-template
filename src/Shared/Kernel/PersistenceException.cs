namespace MyApp.Shared.Kernel;

/// <summary>
/// Falla de infraestructura al guardar un cambio. No es una violación de regla de negocio,
/// por eso no deriva de <see cref="DomainException"/> y se comunica como error 5xx.
/// Quien la lanza es responsable de registrar la causa original con contexto (por ejemplo el id
/// de la entidad): el manejador global no vuelve a registrarla, para evitar logs duplicados.
/// </summary>
public class PersistenceException(string message) : Exception(message);
