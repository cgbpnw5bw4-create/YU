using System.Reflection;
using DeskStealth.Core;

namespace DeskStealth.App.Tests;

public sealed class MainFormInputTests
{
    [Fact]
    public void RepeatedEscapeKeyDown_TogglesOnlyOnceUntilKeyUp()
    {
        RunInSta(() =>
        {
            using var form = new MainForm(TimeProvider.System, simulateTrayFailure: true);
            Assert.Equal(AppMode.Clock, Snapshot(form).Mode);

            KeyDown(form, Keys.Escape);
            Assert.Equal(AppMode.Ready, Snapshot(form).Mode);

            KeyDown(form, Keys.Escape);
            KeyDown(form, Keys.Escape);
            Assert.Equal(AppMode.Ready, Snapshot(form).Mode);

            KeyUp(form, Keys.Escape);
            KeyDown(form, Keys.Escape);
            Assert.Equal(AppMode.Clock, Snapshot(form).Mode);
        });
    }

    [Fact]
    public void Deactivation_EntersClockAndClearsPressedGameplayKeys()
    {
        RunInSta(() =>
        {
            using var form = new MainForm(TimeProvider.System, simulateTrayFailure: true);
            KeyDown(form, Keys.Escape);
            KeyUp(form, Keys.Escape);
            KeyDown(form, Keys.D);
            Assert.Contains(Keys.D, PressedKeys(form));

            Invoke(form, "OnDeactivate", EventArgs.Empty);

            Assert.Equal(AppMode.Clock, Snapshot(form).Mode);
            Assert.Empty(PressedKeys(form));
        });
    }

    private static void KeyDown(MainForm form, Keys key) =>
        Invoke(form, "OnKeyDown", new KeyEventArgs(key));

    private static void KeyUp(MainForm form, Keys key) =>
        Invoke(form, "OnKeyUp", new KeyEventArgs(key));

    private static GameSnapshot Snapshot(MainForm form) =>
        ((GameSession)GetField(form, "_session")).Snapshot;

    private static HashSet<Keys> PressedKeys(MainForm form) =>
        (HashSet<Keys>)GetField(form, "_pressedKeys");

    private static object GetField(MainForm form, string name) =>
        typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(form)
        ?? throw new MissingFieldException(typeof(MainForm).FullName, name);

    private static void Invoke(MainForm form, string name, EventArgs arguments)
    {
        var method = typeof(MainForm).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(MainForm).FullName, name);
        method.Invoke(form, [arguments]);
    }

    private static void RunInSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
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
    }
}
