using Microsoft.AspNetCore.Http;

namespace Warehouse.Server.Contracts.Account;

public sealed class UpdateCurrentUserProfileRequest
{
    public string FullName { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public IFormFile? ProfileImage { get; init; }

    public bool RemoveProfileImage { get; init; }
}
