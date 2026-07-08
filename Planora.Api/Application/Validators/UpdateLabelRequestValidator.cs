using FluentValidation;
using Planora.Shared.Constants;
using Planora.Shared.DTOs.Label;

namespace Planora.Api.Application.Validators;

/// <summary>
/// Validates partial label updates. Fields are optional (null = leave unchanged). Name/color limits
/// match the <c>WorkspaceLabel</c> schema (name ≤ 50, hex color).
/// </summary>
public class UpdateLabelRequestValidator : AbstractValidator<UpdateLabelRequest>
{
    public UpdateLabelRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50).When(x => x.Name is not null);
        RuleFor(x => x.Color)
            .Must(color => PlanoraColors.TryNormalizeHex(color, out _))
            .When(x => x.Color is not null)
            .WithMessage("Color must be a valid hex color.");
    }
}
