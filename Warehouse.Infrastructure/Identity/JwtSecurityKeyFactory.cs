using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Warehouse.Infrastructure.Identity;

internal static class JwtSecurityKeyFactory
{
    private const int MinimumKeyLengthInBytes = 32;
    private static readonly SymmetricSecurityKey UnconfiguredKey =
        new(RandomNumberGenerator.GetBytes(MinimumKeyLengthInBytes));

    public static SymmetricSecurityKey Create(string secretKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);

        if (keyBytes.Length < MinimumKeyLengthInBytes)
        {
            throw new InvalidOperationException(
                $"Jwt:SecretKey must be at least {MinimumKeyLengthInBytes} bytes long.");
        }

        return new SymmetricSecurityKey(keyBytes);
    }

    public static SymmetricSecurityKey CreateForValidation(string secretKey) =>
        Encoding.UTF8.GetByteCount(secretKey) >= MinimumKeyLengthInBytes
            ? Create(secretKey)
            : UnconfiguredKey;
}
