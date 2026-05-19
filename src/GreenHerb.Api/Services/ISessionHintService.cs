namespace GreenHerb.Api.Services;

public interface ISessionHintService
{
    void SetSessionHintCookie(HttpContext httpContext, int sessionId);
    void ClearSessionHintCookie(HttpContext httpContext);
}
