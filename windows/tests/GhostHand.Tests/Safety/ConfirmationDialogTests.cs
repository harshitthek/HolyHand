using System.Drawing;
using System.Windows;
using FluentAssertions;
using HolyHand.App.Windows;
using HolyHand.Core.Models;
using Xunit;

namespace HolyHand.Tests.Safety;

public class ConfirmationDialogTests
{
    [Fact]
    public void RS03_ConfirmationDialog_DisplaysExactActionTargetAndWindow()
    {
        if (Environment.GetEnvironmentVariable("WINDOWS_UI_TESTS") != "1")
        {
            // Gated test per §10 Test Plan
            return;
        }

        string actionText = string.Empty;
        string targetText = string.Empty;
        string appText = string.Empty;
        string reasonText = string.Empty;
        Exception? threadEx = null;

        var staThread = new Thread(() =>
        {
            try
            {
                var dialog = new ConfirmationDialog();

                var decision = new AgentDecision
                {
                    Operation = AgentOperation.Click,
                    TargetId = "e42",
                    TargetLabel = "Confirm Order"
                };

                var element = new AccessibilityElement
                {
                    Id = "e42",
                    Role = "Button",
                    Label = "Confirm Order"
                };

                var appTarget = new AppTarget
                {
                    ProcessId = 4444,
                    ProcessName = "msedge",
                    WindowTitle = "Checkout - Store",
                    WindowHandle = (IntPtr)0x4444,
                    WindowBounds = new Rectangle(0, 0, 800, 600)
                };

                // Populate UI controls via SetDetails
                dialog.SetDetails(decision, element, appTarget, "Action matches sensitive verb 'Confirm'");

                dialog.Show();
                dialog.UpdateLayout();

                actionText = dialog.ActionText;
                targetText = dialog.TargetText;
                appText = dialog.AppText;
                reasonText = dialog.ReasonText;

                dialog.Close();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join(TimeSpan.FromSeconds(10));

        threadEx.Should().BeNull();
        actionText.Should().Be("Click");
        targetText.Should().Contain("Confirm Order");
        appText.Should().Contain("msedge");
        appText.Should().Contain("Checkout - Store");
        reasonText.Should().Contain("sensitive verb 'Confirm'");
    }
}
