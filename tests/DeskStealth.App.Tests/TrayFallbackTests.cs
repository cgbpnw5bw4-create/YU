namespace DeskStealth.App.Tests;

public sealed class TrayFallbackTests
{
    [Fact]
    public void TrayInitializationFailure_FallsBackToTaskbarVisibility()
    {
        bool? showInTaskbar = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new MainForm(TimeProvider.System, simulateTrayFailure: true);
                showInTaskbar = form.ShowInTaskbar;
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
        Assert.Null(failure);
        Assert.True(showInTaskbar);
    }
}
