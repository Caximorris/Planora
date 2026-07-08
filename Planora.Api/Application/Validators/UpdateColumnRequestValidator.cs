using FluentValidation;
using Planora.Shared.Constants;
using Planora.Shared.DTOs.Column;

namespace Planora.Api.Application.Validators;

/// <summary>
/// Validates partial column updates. Fields are optional (null = leave unchanged); the color rule is
/// skipped when the caller is explicitly clearing the color. Mirrors <see cref="CreateColumnRequestValidator"/>.
/// </summary>
public class UpdateColumnRequestValidator : AbstractValidator<UpdateColumnRequest>
{
    public UpdateColumnRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(100).When(x => x.Title is not null);
        RuleFor(x => x.Color)
            .Must(color => PlanoraColors.TryNormalizeSafeSurfaceBackground(color, out _))
            .When(x => x.Color is not null && !x.ClearColor)
            .WithMessage("Color must be a readable card or column background color.");
    }
}
