using FluentValidation;
using LovePlus.Application.Common.Abstractions;
using LovePlus.Application.Common.Exceptions;
using MediatR;

namespace LovePlus.Application.Heartbeat.Commands;

public sealed record AcknowledgeHeartbeatCommand(
    Guid AuthenticatedUserId,
    Guid EventId,
    HeartbeatAcknowledgementState State) : IRequest<AcknowledgeHeartbeatResult>;

public sealed class AcknowledgeHeartbeatCommandValidator : AbstractValidator<AcknowledgeHeartbeatCommand>
{
    public AcknowledgeHeartbeatCommandValidator()
    {
        RuleFor(x => x.AuthenticatedUserId).NotEmpty();
        RuleFor(x => x.EventId).NotEmpty();
        RuleFor(x => x.State).IsInEnum();
    }
}

public sealed class AcknowledgeHeartbeatCommandHandler(
    IEphemeralHeartbeatStore store,
    IHeartbeatPublisher publisher,
    IClock clock) : IRequestHandler<AcknowledgeHeartbeatCommand, AcknowledgeHeartbeatResult>
{
    public async Task<AcknowledgeHeartbeatResult> Handle(AcknowledgeHeartbeatCommand request, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var result = await store.TryAcknowledgeAsync(
            request.EventId,
            request.AuthenticatedUserId,
            request.State,
            now,
            ct);
        if (result.State == HeartbeatAcknowledgementResultState.Forbidden)
        {
            throw new ForbiddenException("This heartbeat belongs to another pair.");
        }
        if (result.State == HeartbeatAcknowledgementResultState.Expired)
        {
            throw new NotFoundException("Heartbeat has expired.");
        }
        if (result.State == HeartbeatAcknowledgementResultState.Duplicate)
        {
            return new AcknowledgeHeartbeatResult(true, true);
        }

        await publisher.PublishAcknowledgementAsync(
            result.SenderUserId!.Value,
            new HeartbeatAcknowledgementDto(request.EventId, request.State, now),
            ct);
        return new AcknowledgeHeartbeatResult(true, false);
    }
}
