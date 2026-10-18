namespace Warehouse.Server.Contracts.Account;

public sealed record ChangeCurrentUserPasswordRequest(
    string CurrentPassword,
    string NewPassword);
