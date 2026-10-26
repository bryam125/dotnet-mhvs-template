using FluentValidation;
using MyApp.Modules.Tasks.Domain;
using MyApp.Shared.Ports;

namespace MyApp.Modules.Tasks.Application.CreateTask;

/// <summary>
/// Valida el cuerpo en el borde HTTP (<c>WithCommandValidation</c>), una línea por regla de
/// HU-TASKS-001. <see cref="TaskItem.Create"/> vuelve a aplicar las mismas reglas como invariante (regla 10).
/// </summary>
public sealed class CreateTaskValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskValidator(IClock clock)
    {
        RuleFor(command => command.Title)
            .Cascade(CascadeMode.Stop)
            // Regla 1
            .Must(title => !string.IsNullOrWhiteSpace(title)).WithMessage(TaskItemErrors.TitleRequired)
            // Regla 2
            .Must(title => TaskItem.IsValidTitleLength(title!.Trim())).WithMessage(TaskItemErrors.TitleLength);

        // Regla 3
        RuleFor(command => command.Description)
            .Must(description => TaskItem.NormalizeDescription(description) is not { Length: > TaskItem.MaxDescriptionLength })
            .WithMessage(TaskItemErrors.DescriptionTooLong);

        RuleFor(command => command.DueDate)
            .Cascade(CascadeMode.Stop)
            // Regla 4
            .Must(dueDate => TaskItem.TryParseDueDate(dueDate!, out _)).WithMessage(TaskItemErrors.DueDateInvalidFormat)
            // Regla 5
            .Must(dueDate => TaskItem.TryParseDueDate(dueDate!, out var date) && !TaskItem.IsInThePast(date, clock.UtcNow))
            .WithMessage(TaskItemErrors.DueDateInPast)
            .When(command => command.DueDate is not null);
    }
}
