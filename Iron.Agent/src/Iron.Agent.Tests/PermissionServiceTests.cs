namespace Iron.Agent.Tests;

using Iron.Agent.Core;
using Xunit;

public class PermissionServiceTests
{
    private class StubEventPublisher : IEventPublisher
    {
        public event EventHandler<AgentEvent> EventRaised;
        public void Publish(AgentEvent ev) { }
    }

    private static PermissionRequest NewRequest(ActionClass actionClass = ActionClass.Read, string target = "file.txt")
    {
        return new PermissionRequest(
            Id: Guid.NewGuid(),
            ActionClass: actionClass,
            Target: target,
            Description: "desc",
            StepDescription: "step",
            CreatedAt: DateTime.UtcNow);
    }

    [Fact]
    public void IsAllowed_ReturnsFalse_BeforeAddAllowlist()
    {
        var service = new PermissionService(new StubEventPublisher());
        Assert.False(service.IsAllowed(ActionClass.Read, "file.txt"));
    }

    [Fact]
    public void IsAllowed_ReturnsTrue_AfterAddAllowlist()
    {
        var service = new PermissionService(new StubEventPublisher());
        service.AddAllowlist(ActionClass.Read, "file.txt");
        Assert.True(service.IsAllowed(ActionClass.Read, "file.txt"));
    }

    [Fact]
    public async Task RequestPermissionAsync_ReturnsFalse_WhenNoSubscribers()
    {
        var service = new PermissionService(new StubEventPublisher());
        var result = await service.RequestPermissionAsync(NewRequest());
        Assert.False(result);
    }

    [Fact]
    public async Task CompletePermission_MakesRequestPermissionAsync_ReturnTrue()
    {
        var service = new PermissionService(new StubEventPublisher());
        service.PermissionRequested += (_, _) => { };
        var request = NewRequest();
        var task = service.RequestPermissionAsync(request);
        service.CompletePermission(request.Id, true);
        Assert.True(await task);
    }
}
