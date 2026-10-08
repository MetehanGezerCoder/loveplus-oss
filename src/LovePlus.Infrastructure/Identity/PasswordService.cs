using LovePlus.Application.Common.Abstractions;
using LovePlus.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace LovePlus.Infrastructure.Identity;

public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string password) => _hasher.HashPassword(user, password);

    public bool Verify(User user, string hash, string password) =>
        _hasher.VerifyHashedPassword(user, hash, password) is not PasswordVerificationResult.Failed;
}
