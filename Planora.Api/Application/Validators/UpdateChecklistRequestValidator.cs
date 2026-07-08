using FluentValidation;
using Planora.Shared.DTOs.Checklist;

namespace Planora.Api.Application.Validators;

/// <summary>
/// Validates partial checklist updates. Title is optional (null = leave unchanged); limit matches the
/// <c>Checklist</c> schema (≤ 200).
/// </summary>
public class UpdateChecklistRequestValidator : AbstractValidator<UpdateChecklistRequest>
{
    public UpdateChecklistRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200).When(x => x.Title is not null);
    }
}
