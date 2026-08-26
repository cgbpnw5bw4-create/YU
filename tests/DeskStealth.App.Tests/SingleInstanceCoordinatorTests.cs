namespace DeskStealth.App.Tests;

public sealed class SingleInstanceCoordinatorTests
{
    [Fact]
    public void SecondaryInstance_SignalsPrimaryListener()
    {
        var instanceId = $"test-{Guid.NewGuid():N}";
        using var primary = new SingleInstanceCoordinator(instanceId);
        using var secondary = new SingleInstanceCoordinator(instanceId);
        using var activated = new ManualResetEventSlim(false);

        Assert.True(primary.IsPrimary);
        Assert.False(secondary.IsPrimary);

        primary.StartListening(activated.Set);
        secondary.SignalPrimary();

        Assert.True(activated.Wait(TimeSpan.FromSeconds(2)));
    }
}
