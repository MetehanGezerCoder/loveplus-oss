namespace LovePlus.Application.Pairing;

public sealed record PairingCodeDto(string Code, DateTimeOffset ExpiresAtUtc);
public sealed record PairDto(Guid PairId, Guid PartnerUserId, string PartnerDisplayName, DateTimeOffset PairedAtUtc);
