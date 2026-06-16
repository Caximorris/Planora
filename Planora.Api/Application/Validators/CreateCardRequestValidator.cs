using FluentValidation;
using Planora.Shared.DTOs.Card;

namespace Planora.Api.Application.Validators;

public class CreateCardRequestValidator : AbstractValidator<CreateCardRequest>
{
    public CreateCardRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(5000).When(x => x.Description is not null);
        RuleFor(x => x.ColumnId).NotEmpty();
    }
}
