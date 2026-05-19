using GreenHerb.Domain.Entities;

namespace GreenHerb.Application.Abstractions.Auth;

public interface IJwtTokenProvider
{
    string GenerateAccessToken(User user);
}
