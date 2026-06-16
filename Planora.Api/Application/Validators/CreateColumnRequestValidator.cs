using FluentValidation;
using Planora.Shared.DTOs.Column;

namespace Planora.Api.Application.Validators;

public class CreateColumnRequestValidator : AbstractValidator<CreateColumnRequest>
{
    public CreateColumnRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(100);
        RuleFor(x => x.BoardId).NotEmpty();
    }
}
