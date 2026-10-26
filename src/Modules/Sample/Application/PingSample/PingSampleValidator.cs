using FluentValidation;

namespace MyApp.Modules.Sample.Application.PingSample;

public sealed class PingSampleValidator : AbstractValidator<PingSampleCommand>
{
    public PingSampleValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}
