using FluentValidation;
using Planora.Shared.DTOs.Checklist;

namespace Planora.Api.Application.Validators;

/// <summary>
/// Validates partial checklist-item updates. Text is optional (null = leave unchanged); limit matches
/// the <c>ChecklistItem</c> schema (≤ 500). IsCompleted/Position carry no shape constraints.
/// </summary>
public class UpdateChecklistItemRequestValidator : AbstractValidator<UpdateChecklistItemRequest>
{
    public UpdateChecklistItemRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(500).When(x => x.Text is not null);
    }
}
