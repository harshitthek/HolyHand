using FluentAssertions;
using HolyHand.Core.Agent;
using HolyHand.Core.Models;
using Xunit;

namespace HolyHand.Tests.Agent;

public class LoopGuardTests
{
    [Fact]
    public void InitialState_NotStalled_ZeroStalls()
    {
        var guard = new LoopGuard(maxConsecutiveStalls: 3);

        guard.ConsecutiveStalls.Should().Be(0);
        guard.IsStalled.Should().BeFalse();
    }

    [Fact]
    public void SingleObservation_SetsStallCountToOne_NotStalled()
    {
        var guard = new LoopGuard(maxConsecutiveStalls: 3);
        var elements = new List<AccessibilityElement>
        {
            new() { Id = "e1", Role = "Button", Label = "Submit", Enabled = true }
        };

        bool stalled = guard.RecordObservation(elements);

        stalled.Should().BeFalse();
        guard.ConsecutiveStalls.Should().Be(1);
        guard.IsStalled.Should().BeFalse();
    }

    [Fact]
    public void ConsecutiveIdenticalObservations_IncrementsStallCount()
    {
        var guard = new LoopGuard(maxConsecutiveStalls: 3);
        var elements = new List<AccessibilityElement>
        {
            new() { Id = "e1", Role = "Button", Label = "Submit", Enabled = true }
        };

        guard.RecordObservation(elements);
        guard.RecordObservation(elements);

        guard.ConsecutiveStalls.Should().Be(2);
        guard.IsStalled.Should().BeFalse();
    }

    [Fact]
    public void MaxConsecutiveStallsReached_ReturnsStalledTrue()
    {
        var guard = new LoopGuard(maxConsecutiveStalls: 3);
        var elements = new List<AccessibilityElement>
        {
            new() { Id = "e1", Role = "Button", Label = "Submit", Enabled = true }
        };

        guard.RecordObservation(elements); // 1
        guard.RecordObservation(elements); // 2
        bool stalled = guard.RecordObservation(elements); // 3

        stalled.Should().BeTrue();
        guard.ConsecutiveStalls.Should().Be(3);
        guard.IsStalled.Should().BeTrue();
    }

    [Fact]
    public void StateChange_ResetsStallCountToOne()
    {
        var guard = new LoopGuard(maxConsecutiveStalls: 3);
        var elementsA = new List<AccessibilityElement>
        {
            new() { Id = "e1", Role = "Button", Label = "Step 1", Enabled = true }
        };
        var elementsB = new List<AccessibilityElement>
        {
            new() { Id = "e1", Role = "Button", Label = "Step 2", Enabled = true }
        };

        guard.RecordObservation(elementsA);
        guard.RecordObservation(elementsA);
        guard.ConsecutiveStalls.Should().Be(2);

        // Screen updated to state B
        bool stalled = guard.RecordObservation(elementsB);

        stalled.Should().BeFalse();
        guard.ConsecutiveStalls.Should().Be(1);
        guard.IsStalled.Should().BeFalse();
    }

    [Fact]
    public void ValueChangeInElement_TriggersStateChange()
    {
        var guard = new LoopGuard(maxConsecutiveStalls: 3);
        var elements1 = new List<AccessibilityElement>
        {
            new() { Id = "txt1", Role = "Edit", Label = "Search", Value = "initial", Enabled = true }
        };
        var elements2 = new List<AccessibilityElement>
        {
            new() { Id = "txt1", Role = "Edit", Label = "Search", Value = "typed text", Enabled = true }
        };

        guard.RecordObservation(elements1);
        guard.RecordObservation(elements2);

        guard.ConsecutiveStalls.Should().Be(1);
    }

    [Fact]
    public void FocusChangeInElement_TriggersStateChange()
    {
        var guard = new LoopGuard(maxConsecutiveStalls: 3);
        var elements1 = new List<AccessibilityElement>
        {
            new() { Id = "e1", Role = "Button", Label = "OK", Focused = false, Enabled = true }
        };
        var elements2 = new List<AccessibilityElement>
        {
            new() { Id = "e1", Role = "Button", Label = "OK", Focused = true, Enabled = true }
        };

        guard.RecordObservation(elements1);
        guard.RecordObservation(elements2);

        guard.ConsecutiveStalls.Should().Be(1);
    }

    [Fact]
    public void Reset_ClearsStateAndStallCount()
    {
        var guard = new LoopGuard(maxConsecutiveStalls: 3);
        var elements = new List<AccessibilityElement>
        {
            new() { Id = "e1", Role = "Button", Label = "Submit", Enabled = true }
        };

        guard.RecordObservation(elements);
        guard.RecordObservation(elements);
        guard.ConsecutiveStalls.Should().Be(2);

        guard.Reset();

        guard.ConsecutiveStalls.Should().Be(0);
        guard.IsStalled.Should().BeFalse();

        // After reset, next observation starts at 1
        guard.RecordObservation(elements);
        guard.ConsecutiveStalls.Should().Be(1);
    }
}
