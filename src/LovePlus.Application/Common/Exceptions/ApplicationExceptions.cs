namespace LovePlus.Application.Common.Exceptions;

public abstract class LovePlusException(string message) : Exception(message);

public sealed class NotFoundException(string message) : LovePlusException(message);
public sealed class ConflictException(string message) : LovePlusException(message);
public sealed class ForbiddenException(string message) : LovePlusException(message);
public sealed class RateLimitException(string message) : LovePlusException(message);
public sealed class AuthenticationException(string message) : LovePlusException(message);
