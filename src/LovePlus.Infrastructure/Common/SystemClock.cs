using LovePlus.Application.Common.Abstractions;

namespace LovePlus.Infrastructure.Common;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
