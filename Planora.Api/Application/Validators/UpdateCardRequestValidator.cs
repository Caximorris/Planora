using FluentValidation;
using Planora.Shared.Constants;
using Planora.Shared.DTOs.Card;

namespace Planora.Api.Application.Validators;

/// <summary>
/// Validates partial card updates. Fields are optional (null = leave unchanged); the color rule is
/// skipped when the caller is explicitly clearing the color. Mirrors <see cref="CreateCardRequestValidator"/>.
/// </summary>
public class UpdateCardRequestValidator : AbstractValidator<UpdateCardRequest>
{
    public UpdateCardRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200).When(x => x.Title is not null);
        RuleFor(x => x.Description).MaximumLength(5000).When(x => x.Description is not null);
        RuleFor(x => x.Priority).IsInEnum().When(x => x.Priority.HasValue);
        RuleFor(x => x.Color)
            .Must(color => PlanoraColors.TryNormalizeSafeSurfaceBackground(color, out _))
            .When(x => x.Color is not null && !x.ClearColor)
            .WithMessage("Color must be a readable card or column background color.");
    }
}
