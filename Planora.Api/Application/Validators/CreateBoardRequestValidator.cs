using FluentValidation;
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
            .Matches(@"^#[0-9A-Fa-f]{6}$")
            .When(x => x.CoverColor is not null)
            .WithMessage("CoverColor must be a valid hex color (e.g. #FF5733).");
    }
}
