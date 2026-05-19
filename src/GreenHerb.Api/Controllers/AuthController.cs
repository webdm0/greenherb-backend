using GreenHerb.Api.Configuration;
using GreenHerb.Api.Contracts.Auth;
using GreenHerb.Api.Extensions;
using GreenHerb.Api.Services;
using GreenHerb.Application.Common.Exceptions;
using GreenHerb.Application.Features.Auth.Dtos;
using GreenHerb.Application.Features.Auth.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GreenHerb.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private const string RefreshTokenCookieName = "refreshToken";

    private readonly IAuthService _authService;
    private readonly AuthCookieOptions _authCookieOptions;
    private readonly ISessionHintService _sessionHintService;

    public AuthController(
        IAuthService authService,
        IOptions<AuthCookieOptions> authCookieOptions,
        ISessionHintService sessionHintService)
    {
        _authService = authService;
        _authCookieOptions = authCookieOptions.Value;
        _sessionHintService = sessionHintService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _authService.RegisterAsync(
                new RegisterUserRequest
                {
                    Username = request.Username,
                    Email = request.Email,
                    Password = request.Password,
                    CartItems = request.CartItems.Select(MapCartItem).ToList()
                },
                Request.Headers.UserAgent.ToString(),
                GetClientIp(),
                cancellationToken);

            SetRefreshCookie(session);
            _sessionHintService.SetSessionHintCookie(HttpContext, session.SessionId);
            return Ok(MapAuthResponse(session));
        }
        catch (UsernameAlreadyTakenException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (EmailAlreadyTakenException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _authService.LoginAsync(
                new LoginUserRequest
                {
                    Identifier = request.Identifier,
                    Password = request.Password,
                    CartItems = request.CartItems.Select(MapCartItem).ToList()
                },
                Request.Headers.UserAgent.ToString(),
                GetClientIp(),
                cancellationToken);

            SetRefreshCookie(session);
            _sessionHintService.SetSessionHintCookie(HttpContext, session.SessionId);
            return Ok(MapAuthResponse(session));
        }
        catch (InvalidCredentialsException exception)
        {
            return Unauthorized(new { message = exception.Message });
        }
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _authService.LoginWithGoogleAsync(
                new Application.Features.Auth.Dtos.GoogleAuthRequest
                {
                    IdToken = request.IdToken,
                    CartItems = request.CartItems.Select(MapCartItem).ToList()
                },
                Request.Headers.UserAgent.ToString(),
                GetClientIp(),
                cancellationToken);

            SetRefreshCookie(session);
            _sessionHintService.SetSessionHintCookie(HttpContext, session.SessionId);
            return Ok(MapAuthResponse(session));
        }
        catch (InvalidGoogleTokenException exception)
        {
            return Unauthorized(new { message = exception.Message });
        }
        catch (GoogleEmailNotVerifiedException exception)
        {
            return Unauthorized(new { message = exception.Message });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var session = await _authService.RefreshSessionAsync(
            ReadRefreshToken(),
            Request.Headers.UserAgent.ToString(),
            GetClientIp(),
            cancellationToken);

        if (session is null)
        {
            ClearRefreshCookie();
            _sessionHintService.ClearSessionHintCookie(HttpContext);
            return Unauthorized(new { message = "Unauthorized." });
        }

        SetRefreshCookie(session);
        _sessionHintService.SetSessionHintCookie(HttpContext, session.SessionId);
        return Ok(MapAuthResponse(session));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(ReadRefreshToken(), cancellationToken);
        ClearRefreshCookie();
        _sessionHintService.ClearSessionHintCookie(HttpContext);
        return Ok(new { message = "Logged out." });
    }

    [HttpGet("session")]
    public async Task<IActionResult> Session(CancellationToken cancellationToken)
    {
        var session = await _authService.GetSessionAsync(ReadRefreshToken(), cancellationToken);
        if (session is null)
        {
            ClearRefreshCookie();
            _sessionHintService.ClearSessionHintCookie(HttpContext);
            return Unauthorized(new { message = "Unauthorized." });
        }

        _sessionHintService.SetSessionHintCookie(HttpContext, session.SessionId);
        return Ok(MapUserResponse(session.User));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userId = User.GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(new { message = "Unauthorized." });
        }

        var user = await _authService.GetCurrentUserAsync(userId.Value, cancellationToken);
        return user is null
            ? Unauthorized(new { message = "Unauthorized." })
            : Ok(MapUserResponse(user));
    }

    private string? ReadRefreshToken()
    {
        return Request.Cookies.TryGetValue(RefreshTokenCookieName, out var refreshToken)
            ? refreshToken
            : null;
    }

    private string? GetClientIp()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private void SetRefreshCookie(AuthSessionDto session)
    {
        Response.Cookies.Append(RefreshTokenCookieName, session.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = _authCookieOptions.UseSecureAuthCookies,
            SameSite = _authCookieOptions.UseCrossSiteAuth ? SameSiteMode.None : SameSiteMode.Lax,
            Expires = new DateTimeOffset(session.RefreshTokenExpiresAtUtc)
        });
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = _authCookieOptions.UseSecureAuthCookies,
            SameSite = _authCookieOptions.UseCrossSiteAuth ? SameSiteMode.None : SameSiteMode.Lax
        });
    }

    private static AuthResponse MapAuthResponse(AuthSessionDto session)
    {
        return new AuthResponse
        {
            AccessToken = session.AccessToken,
            User = MapUserResponse(session.User)
        };
    }

    private static AuthenticatedUserResponse MapUserResponse(CurrentUserDto user)
    {
        return new AuthenticatedUserResponse
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            IsAdmin = user.IsAdmin
        };
    }

    private static AuthCartItemInput MapCartItem(AuthCartItemRequest item)
    {
        return new AuthCartItemInput
        {
            ProductId = item.ProductId,
            Quantity = item.Quantity
        };
    }
}
