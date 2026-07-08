using FluentValidation;
using Planora.Shared.Constants;
using Planora.Shared.DTOs.Board;

namespace Planora.Api.Application.Validators;

/// <summary>
/// Validates partial board updates. Each field is optional (null = leave unchanged), so every rule
/// is guarded by <c>When(field is not null)</c> — mirrors <see cref="CreateBoardRequestValidator"/>.
/// </summary>
public class UpdateBoardRequestValidator : AbstractValidator<UpdateBoardRequest>
{
    public UpdateBoardRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).When(x => x.Name is not null);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
        RuleFor(x => x.CoverColor)
            .Must(color => PlanoraColors.TryNormalizeSafeBoardBackground(color, out _))
            .When(x => x.CoverColor is not null)
            .WithMessage("CoverColor must be a readable board background color.");
    }
}
