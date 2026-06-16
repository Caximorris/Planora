using Planora.Api.Domain.Entities;

namespace Planora.Api.Application.Interfaces;

public interface ITokenService
{
    string GenerateToken(AppUser user);
    DateTime GetExpiration();
}
