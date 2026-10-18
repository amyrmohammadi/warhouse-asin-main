using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Warehouse.Application.Common.Interfaces;

namespace Warehouse.Infrastructure.Identity;

internal sealed class HttpCurrentUserContext(
    IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    public const string SystemUserName = "System";

    private bool _hasExplicitActor;
    private Guid? _explicitUserId;
    private string? _explicitUserName;

    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            if (_hasExplicitActor)
            {
                return _explicitUserId;
            }

            var value = User?.FindFirst("sub")?.Value
                ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }

    public string UserName
    {
        get
        {
            if (_hasExplicitActor)
            {
                return _explicitUserName!;
            }

            return User?.FindFirst(ClaimTypes.Name)?.Value
                ?? User?.FindFirst(ClaimTypes.Email)?.Value
                ?? SystemUserName;
        }
    }

    public void SetCurrentUser(Guid userId, string? fullName)
    {
        _hasExplicitActor = true;
        _explicitUserId = userId;
        _explicitUserName = string.IsNullOrWhiteSpace(fullName)
            ? "نام کامل ثبت نشده"
            : fullName.Trim();
    }

    public void SetSystem()
    {
        _hasExplicitActor = true;
        _explicitUserId = null;
        _explicitUserName = SystemUserName;
    }
}
