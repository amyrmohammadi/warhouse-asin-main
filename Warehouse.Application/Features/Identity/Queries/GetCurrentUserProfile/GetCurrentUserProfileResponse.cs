namespace Warehouse.Application.Features.Identity.Queries.GetCurrentUserProfile;

public sealed record GetCurrentUserProfileResponse(
    Guid UserId,
    string UserName,
    string Email,
    string FullName,
    string PhoneNumber,
    string? ProfileImageContentType,
    byte[]? ProfileImage);
