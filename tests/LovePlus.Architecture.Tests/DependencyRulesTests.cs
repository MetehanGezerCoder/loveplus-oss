using LovePlus.Api.Hubs;
using LovePlus.Application.Realtime.Commands;
using LovePlus.Domain.Identity;
using LovePlus.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;

namespace LovePlus.Architecture.Tests;

public sealed class DependencyRulesTests
{
    [Fact]
    public void Clean_architecture_dependencies_point_inward()
    {
        var domainReferences = typeof(User).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();
        var applicationReferences = typeof(UpdateUserStatusCommand).Assembly
            .GetReferencedAssemblies().Select(x => x.Name).ToArray();

        Assert.DoesNotContain("LovePlus.Application", domainReferences);
        Assert.DoesNotContain("LovePlus.Infrastructure", domainReferences);
        Assert.DoesNotContain("LovePlus.Api", domainReferences);
        Assert.DoesNotContain("LovePlus.Infrastructure", applicationReferences);
        Assert.DoesNotContain("LovePlus.Api", applicationReferences);
        Assert.NotNull(typeof(LovePlusDbContext));
    }

    [Fact]
    public void SignalR_hub_is_authorized_and_pair_groups_are_isolated()
    {
        Assert.NotNull(Attribute.GetCustomAttribute(typeof(StatusHub), typeof(AuthorizeAttribute)));
        Assert.NotEqual(PairGroup.Name(Guid.NewGuid()), PairGroup.Name(Guid.NewGuid()));
    }
}
