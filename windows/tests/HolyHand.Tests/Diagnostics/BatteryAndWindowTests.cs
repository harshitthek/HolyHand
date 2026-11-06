using FluentAssertions;
using HolyHand.Core.Interfaces;
using HolyHand.Core.Jev;
using HolyHand.Core.Models;
using HolyHand.Platform.Diagnostics;
using HolyHand.Platform.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HolyHand.Tests.Diagnostics;

public class BatteryAndWindowTests
{
    [Theory]
    [InlineData("check battery")]
    [InlineData("battery status")]
    [InlineData("battery percentage")]
    [InlineData("how much battery is left")]
    [InlineData("is it charging")]
    [InlineData("power status")]
    [InlineData("power level")]
    public void IsBatteryGoal_DetectsBatteryRequests(string goal)
    {
        JevDecisionModel.IsBatteryGoal(goal).Should().BeTrue();
    }

    [Theory]
    [InlineData("maximize window")]
    [InlineData("fullscreen")]
    [InlineData("maximize")]
    [InlineData("minimize window")]
    [InlineData("minimize")]
    [InlineData("restore window")]
    [InlineData("snap window left")]
    [InlineData("snap left")]
    [InlineData("snap window right")]
    [InlineData("snap right")]
    public void IsWindowManagementGoal_DetectsWindowRequests(string goal)
    {
        JevDecisionModel.IsWindowManagementGoal(goal).Should().BeTrue();
    }

    [Fact]
    public void WindowsBatteryService_ReturnsValidBatteryInfo()
    {
        var service = new WindowsBatteryService();
        var info = service.GetBatteryInfo();

        info.Should().NotBeNull();
        info.Summary.Should().NotBeNullOrWhiteSpace();
        // Either desktop (100% / AC) or laptop battery (-1 to 100%)
        info.BatteryLifePercent.Should().BeInRange(-1, 100);
    }

    [Fact]
    public async Task ActionExecutor_CheckBattery_InvokesBatteryService()
    {
        var mockBattery = new Mock<IBatteryService>();
        mockBattery.Setup(b => b.GetBatteryInfo())
            .Returns(new BatteryInfo(85, false, false, TimeSpan.FromHours(4), "Battery is at 85% - On battery (~4h 0m remaining)"));

        using var executor = new ActionExecutor(NullLogger<ActionExecutor>.Instance, dryRun: false, batteryService: mockBattery.Object);

        var decision = new AgentDecision
        {
            Operation = AgentOperation.CheckBattery
        };

        var result = await executor.ExecuteAsync(decision, null);

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("85%");
        result.Message.Should().Contain("On battery");
        mockBattery.Verify(b => b.GetBatteryInfo(), Times.Once);
    }

    [Theory]
    [InlineData(AgentOperation.MaximizeWindow)]
    [InlineData(AgentOperation.MinimizeWindow)]
    [InlineData(AgentOperation.RestoreWindow)]
    [InlineData(AgentOperation.SnapWindowLeft)]
    [InlineData(AgentOperation.SnapWindowRight)]
    public async Task ActionExecutor_WindowManagement_ExecutesSuccessfully(AgentOperation op)
    {
        using var executor = new ActionExecutor(NullLogger<ActionExecutor>.Instance, dryRun: true);

        var decision = new AgentDecision
        {
            Operation = op
        };

        var result = await executor.ExecuteAsync(decision, null);

        result.Success.Should().BeTrue();
        result.Message.Should().Contain($"Simulated {op}");
    }
}
