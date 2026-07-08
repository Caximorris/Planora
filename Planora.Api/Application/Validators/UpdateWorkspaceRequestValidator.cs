using FluentValidation;
using Planora.Shared.DTOs.Workspace;

namespace Planora.Api.Application.Validators;

/// <summary>
/// Validates partial workspace updates. Fields are optional (null = leave unchanged). Mirrors
/// <see cref="CreateWorkspaceRequestValidator"/>.
/// </summary>
public class UpdateWorkspaceRequestValidator : AbstractValidator<UpdateWorkspaceRequest>
{
    public UpdateWorkspaceRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).When(x => x.Name is not null);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
    }
}
