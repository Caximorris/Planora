using FluentValidation;
using Planora.Shared.Constants;
using Planora.Shared.DTOs.Board;

namespace Planora.Api.Application.Validators;

public class CreateBoardRequestValidator : AbstractValidator<CreateBoardRequest>
{
    public CreateBoardRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.CoverColor)
            .Must(color => PlanoraColors.TryNormalizeSafeBoardBackground(color, out _))
            .When(x => x.CoverColor is not null)
            .WithMessage("CoverColor must be a readable board background color.");
    }
}
