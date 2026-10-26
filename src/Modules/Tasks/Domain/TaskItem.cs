using System.Globalization;
using MyApp.Shared.Kernel;

namespace MyApp.Modules.Tasks.Domain;

/// <summary>
/// Una tarea de la lista de una persona. Implementa HU-TASKS-001 — Crear una tarea.
/// </summary>
/// <remarks>Se llama <c>TaskItem</c> y no <c>Task</c> para no chocar con <see cref="System.Threading.Tasks.Task"/>.</remarks>
public sealed class TaskItem
{
    public const int MinTitleLength = 3;
    public const int MaxTitleLength = 120;
    public const int MaxDescriptionLength = 2000;
    public const string DueDateFormat = "yyyy-MM-dd";

    public Guid Id { get; private init; }
    public string Title { get; private init; } = default!;
    public string? Description { get; private init; }
    public DateOnly? DueDate { get; private init; }
    public TaskItemStatus Status { get; private init; }
    public DateTime CreatedAt { get; private init; }
    public DateTime UpdatedAt { get; private init; }

    private TaskItem() { }

    /// <summary>
    /// Crea una tarea nueva. Aplica las reglas 1 a 5 como invariante (regla 10: el dominio no depende
    /// de que el borde HTTP ya las haya validado) y genera id, estado y fechas (reglas 6 a 8).
    /// </summary>
    /// <param name="utcNow">Instante de creación; debe venir en UTC (<see cref="DateTimeKind.Utc"/>).</param>
    public static TaskItem Create(string? title, string? description, string? dueDate, DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("El instante de creación debe estar en UTC.", nameof(utcNow));

        // Regla 1: título obligatorio, no solo espacios.
        var normalizedTitle = title?.Trim() ?? string.Empty;
        if (normalizedTitle.Length == 0)
            throw new DomainException(TaskItemErrors.TitleRequired);

        // Regla 2: longitud del título después de eliminar espacios exteriores.
        if (!IsValidTitleLength(normalizedTitle))
            throw new DomainException(TaskItemErrors.TitleLength);

        // Regla 3: longitud de la descripción; vacía o solo espacios se trata como ausente.
        var normalizedDescription = NormalizeDescription(description);
        if (normalizedDescription is { Length: > MaxDescriptionLength })
            throw new DomainException(TaskItemErrors.DescriptionTooLong);

        DateOnly? parsedDueDate = null;
        if (dueDate is not null)
        {
            // Regla 4: solo AAAA-MM-DD; un valor con hora se rechaza, no se recorta.
            if (!TryParseDueDate(dueDate, out var date))
                throw new DomainException(TaskItemErrors.DueDateInvalidFormat);

            // Regla 5: no anterior a hoy, con "hoy" calculado estrictamente en UTC.
            if (IsInThePast(date, utcNow))
                throw new DomainException(TaskItemErrors.DueDateInPast);

            parsedDueDate = date;
        }

        return new TaskItem
        {
            Id = Guid.NewGuid(),                  // Regla 6
            Title = normalizedTitle,
            Description = normalizedDescription,
            DueDate = parsedDueDate,
            Status = TaskItemStatus.Pending,      // Regla 7
            CreatedAt = utcNow,                   // Regla 8: mismo instante en ambas
            UpdatedAt = utcNow
        };
    }

    /// <summary>
    /// Reconstruye una tarea ya guardada, sin volver a aplicar las reglas de creación (una tarea de
    /// ayer con fecha límite de ayer sigue siendo válida). Solo para adaptadores de persistencia.
    /// </summary>
    internal static TaskItem Rehydrate(
        Guid id, string title, string? description, DateOnly? dueDate,
        TaskItemStatus status, DateTime createdAt, DateTime updatedAt) =>
        new()
        {
            Id = id,
            Title = title,
            Description = description,
            DueDate = dueDate,
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

    public static bool IsValidTitleLength(string normalizedTitle) =>
        normalizedTitle.Length is >= MinTitleLength and <= MaxTitleLength;

    public static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    /// <summary>Formato exacto AAAA-MM-DD; rechaza hora, espacios y fechas inexistentes (ej. 2026-02-30).</summary>
    public static bool TryParseDueDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(value, DueDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    public static bool IsInThePast(DateOnly dueDate, DateTime utcNow) =>
        dueDate < DateOnly.FromDateTime(utcNow);
}
