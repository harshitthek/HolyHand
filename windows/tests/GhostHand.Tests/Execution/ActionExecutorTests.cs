using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using FluentAssertions;
using HolyHand.Core.Models;
using HolyHand.Core.ScreenReading;
using HolyHand.Platform.Execution;
using HolyHand.Platform.ScreenReading;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HolyHand.Tests.Execution;

public class ActionExecutorTests
{
    [Fact]
    public async Task EX03_DryRunMode_DoesNotInvokePhysicalInput()
    {
        using var executor = new ActionExecutor(NullLogger<ActionExecutor>.Instance, dryRun: true);

        var decision = new AgentDecision
        {
            Operation = AgentOperation.Click,
            TargetId = "btn1",
            TargetLabel = "Submit Form"
        };

        var element = new AccessibilityElement
        {
            Id = "btn1",
            Role = "Button",
            Label = "Submit Form",
            Frame = new Rectangle(100, 100, 80, 30)
        };

        var result = await executor.ExecuteAsync(decision, element);

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("[DRY RUN]");
    }

    [Fact]
    public void EX01_ValuePattern_TypeText_VerifiedInLiveWindow()
    {
        if (Environment.GetEnvironmentVariable("WINDOWS_UI_TESTS") != "1")
        {
            // Gated UI test per §10 Test Plan
            return;
        }

        string resultText = string.Empty;
        Exception? threadEx = null;

        var staThread = new Thread(() =>
        {
            try
            {
                var window = new Window
                {
                    Title = "HolyHand Execution Test Window",
                    Width = 400,
                    Height = 300,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = 60,
                    Top = 60
                };

                var textBox = new TextBox { Text = "Initial", Name = "txtTarget" };
                window.Content = textBox;
                window.Show();
                window.UpdateLayout();

                var hwnd = new WindowInteropHelper(window).Handle;
                var target = new AppTarget
                {
                    ProcessId = Environment.ProcessId,
                    ProcessName = "HolyHand.Tests",
                    WindowTitle = "HolyHand Execution Test Window",
                    WindowHandle = hwnd,
                    WindowBounds = new Rectangle(60, 60, 400, 300)
                };

                Task.Run(async () =>
                {
                    try
                    {
                        using var reader = new UiaScreenReader(
                            new ScreenReaderOptions { FilterOffscreen = false },
                            null,
                            NullLogger<UiaScreenReader>.Instance);

                        var elements = await reader.ReadElementsAsync(target);
                        var targetEl = elements.FirstOrDefault(e => e.Role == "Edit");

                        using var executor = new ActionExecutor(NullLogger<ActionExecutor>.Instance, dryRun: false);

                        var decision = new AgentDecision
                        {
                            Operation = AgentOperation.TypeText,
                            TargetId = targetEl?.Id,
                            TextValue = "HolyHand Automated Typing"
                        };

                        await executor.ExecuteAsync(decision, targetEl);

                        window.Dispatcher.Invoke(() =>
                        {
                            resultText = textBox.Text;
                        });
                    }
                    catch (Exception ex)
                    {
                        threadEx = ex;
                    }
                    finally
                    {
                        window.Dispatcher.Invoke(() =>
                        {
                            window.Close();
                            System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
                        });
                    }
                });

                System.Windows.Threading.Dispatcher.Run();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join(TimeSpan.FromSeconds(15));

        threadEx.Should().BeNull();
        resultText.Should().Be("HolyHand Automated Typing");
    }

    [Fact]
    public void EX02_InvokePattern_ClickButton_VerifiedInLiveWindow()
    {
        if (Environment.GetEnvironmentVariable("WINDOWS_UI_TESTS") != "1")
        {
            // Gated UI test per §10 Test Plan
            return;
        }

        int clickCount = 0;
        Exception? threadEx = null;

        var staThread = new Thread(() =>
        {
            try
            {
                var window = new Window
                {
                    Title = "HolyHand Button Test Window",
                    Width = 400,
                    Height = 300,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = 70,
                    Top = 70
                };

                var button = new Button { Content = "Count Me", Name = "btnCount" };
                button.Click += (_, _) => clickCount++;

                window.Content = button;
                window.Show();
                window.UpdateLayout();

                var hwnd = new WindowInteropHelper(window).Handle;
                var target = new AppTarget
                {
                    ProcessId = Environment.ProcessId,
                    ProcessName = "HolyHand.Tests",
                    WindowTitle = "HolyHand Button Test Window",
                    WindowHandle = hwnd,
                    WindowBounds = new Rectangle(70, 70, 400, 300)
                };

                Task.Run(async () =>
                {
                    try
                    {
                        using var reader = new UiaScreenReader(
                            new ScreenReaderOptions { FilterOffscreen = false },
                            null,
                            NullLogger<UiaScreenReader>.Instance);

                        var elements = await reader.ReadElementsAsync(target);
                        var targetEl = elements.FirstOrDefault(e => e.Role == "Button" && e.Label == "Count Me");

                        using var executor = new ActionExecutor(NullLogger<ActionExecutor>.Instance, dryRun: false);

                        var decision = new AgentDecision
                        {
                            Operation = AgentOperation.Click,
                            TargetId = targetEl?.Id,
                            TargetLabel = "Count Me"
                        };

                        await executor.ExecuteAsync(decision, targetEl);
                    }
                    catch (Exception ex)
                    {
                        threadEx = ex;
                    }
                    finally
                    {
                        window.Dispatcher.Invoke(() =>
                        {
                            window.Close();
                            System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
                        });
                    }
                });

                System.Windows.Threading.Dispatcher.Run();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join(TimeSpan.FromSeconds(15));

        threadEx.Should().BeNull();
        clickCount.Should().Be(1);
    }
}
