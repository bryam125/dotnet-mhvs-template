using FluentValidation;

namespace MyApp.Modules.Sample.Application.RenameSampleItem;

/// <summary>
/// Valida el cuerpo de la solicitud en el borde HTTP (<c>WithCommandValidation</c>), igual que PingSample.
/// El dominio (<c>SampleItem.Rename</c>) vuelve a aplicar las mismas reglas como invariante.
/// </summary>
public sealed class RenameSampleItemValidator : AbstractValidator<RenameSampleItemRequest>
{
    public RenameSampleItemValidator()
    {
        // El formato de sampleItemId no se valida aquí: un valor que no es un GUID válido
        // se trata igual que un item no encontrado (404), ver RenameSampleItemHandler.

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}
