namespace MyApp.Shared.Ports;

// Puertos verdaderamente cruzados entre módulos viven aquí, no dentro
// de un módulo específico. Úsalo con moderación: la mayoría de las
// cosas SÍ pertenecen a un módulo.
public interface IClock
{
    DateTime UtcNow { get; }
}
