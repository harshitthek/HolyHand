using System.Drawing;
using FluentAssertions;
using HolyHand.Core.Models;
using HolyHand.Core.ScreenReading;
using Xunit;

namespace HolyHand.Tests.ScreenReading;

public class ElementRankerTests
{
    [Fact]
    public void RankAndFilter_FocusedElement_AlwaysPlacedFirst()
    {
        var elements = new List<AccessibilityElement>
        {
            new() { Id = "raw1", Role = "Button", Label = "First Button", Focused = false, Frame = new Rectangle(10, 10, 50, 20), Enabled = true },
            new() { Id = "raw2", Role = "Edit", Label = "Search Field", Focused = true, Frame = new Rectangle(10, 50, 100, 20), Enabled = true },
            new() { Id = "raw3", Role = "Button", Label = "Submit", Focused = false, Frame = new Rectangle(10, 100, 50, 20), Enabled = true }
        };

        var ranked = ElementRanker.RankAndFilter(elements, ScreenReaderOptions.Default);

        ranked.Should().NotBeEmpty();
        ranked[0].Label.Should().Be("Search Field");
        ranked[0].Focused.Should().BeTrue();
        ranked[0].Id.Should().Be("e1"); // Re-assigned sequential ID
    }

    [Fact]
    public void RankAndFilter_InteractiveElements_PrioritizedOverStaticElements()
    {
        var elements = new List<AccessibilityElement>
        {
            new() { Id = "t1", Role = "Text", Label = "Information paragraph", Frame = new Rectangle(10, 10, 200, 40), Enabled = true },
            new() { Id = "p1", Role = "Pane", Label = "Container pane", Frame = new Rectangle(10, 60, 200, 100), Enabled = true },
            new() { Id = "b1", Role = "Button", Label = "Click Me", Frame = new Rectangle(10, 170, 80, 30), Enabled = true }
        };

        var ranked = ElementRanker.RankAndFilter(elements, ScreenReaderOptions.Default);

        ranked.Should().NotBeEmpty();
        ranked[0].Role.Should().Be("Button");
        ranked[0].Label.Should().Be("Click Me");
    }

    [Theory]
    [InlineData("Button", true)]
    [InlineData("MenuItem", true)]
    [InlineData("TabItem", true)]
    [InlineData("Hyperlink", true)]
    [InlineData("CheckBox", true)]
    [InlineData("RadioButton", true)]
    [InlineData("ComboBox", true)]
    [InlineData("ListItem", true)]
    [InlineData("Edit", true)]
    [InlineData("Document", true)]
    [InlineData("SplitButton", true)]
    [InlineData("AXButton", true)]
    [InlineData("AXCheckBox", true)]
    [InlineData("Text", false)]
    [InlineData("Pane", false)]
    [InlineData("Window", false)]
    [InlineData("ScrollBar", false)]
    public void IsInteractive_IdentifiesRolesCorrectly(string role, bool expected)
    {
        ElementRanker.IsInteractive(role).Should().Be(expected);
    }

    [Fact]
    public void RankAndFilter_OutcomeEvidence_PrioritizedOverRegularElements()
    {
        var elements = new List<AccessibilityElement>
        {
            new() { Id = "e1", Role = "Pane", Label = "Regular container", Frame = new Rectangle(10, 10, 100, 20), Enabled = true },
            new() { Id = "e2", Role = "StatusBar", Label = "Order #12345 Completed Successfully", Frame = new Rectangle(10, 40, 200, 20), Enabled = true }
        };

        var ranked = ElementRanker.RankAndFilter(elements, ScreenReaderOptions.Default);

        ranked.Should().HaveCount(2);
        ranked[0].IsOutcomeEvidence.Should().BeTrue();
        ranked[0].Role.Should().Be("StatusBar");
        ranked[0].Label.Should().Contain("Completed");
    }

    [Fact]
    public void RankAndFilter_FilterDisabled_FiltersOutDisabledElements()
    {
        var elements = new List<AccessibilityElement>
        {
            new() { Id = "d1", Role = "Button", Label = "Disabled Button", Enabled = false, Frame = new Rectangle(10, 10, 80, 30) },
            new() { Id = "e1", Role = "Button", Label = "Active Button", Enabled = true, Frame = new Rectangle(10, 50, 80, 30) }
        };

        var options = new ScreenReaderOptions { FilterDisabled = true };
        var ranked = ElementRanker.RankAndFilter(elements, options);

        ranked.Should().ContainSingle();
        ranked[0].Label.Should().Be("Active Button");
    }

    [Fact]
    public void RankAndFilter_PreservesDisabled_WhenFilterDisabledIsFalse()
    {
        var elements = new List<AccessibilityElement>
        {
            new() { Id = "d1", Role = "Button", Label = "Disabled Button", Enabled = false, Frame = new Rectangle(10, 10, 80, 30) },
            new() { Id = "e1", Role = "Button", Label = "Active Button", Enabled = true, Frame = new Rectangle(10, 50, 80, 30) }
        };

        var options = new ScreenReaderOptions { FilterDisabled = false };
        var ranked = ElementRanker.RankAndFilter(elements, options);

        ranked.Should().HaveCount(2);
    }

    [Fact]
    public void RankAndFilter_FilterOffscreen_RemovesZeroDimensionElements()
    {
        var elements = new List<AccessibilityElement>
        {
            new() { Id = "off1", Role = "Button", Label = "Invisible", Enabled = true, Frame = new Rectangle(0, 0, 0, 0) },
            new() { Id = "on1", Role = "Button", Label = "Visible", Enabled = true, Frame = new Rectangle(10, 10, 100, 30) }
        };

        var options = new ScreenReaderOptions { FilterOffscreen = true };
        var ranked = ElementRanker.RankAndFilter(elements, options);

        ranked.Should().ContainSingle();
        ranked[0].Label.Should().Be("Visible");
    }

    [Fact]
    public void RankAndFilter_CapsToMaxCandidates_AndAssignsStableSequentialIds()
    {
        var elements = Enumerable.Range(1, 100).Select(i => new AccessibilityElement
        {
            Id = $"orig_{i}",
            Role = "Button",
            Label = $"Button {i}",
            Enabled = true,
            Frame = new Rectangle(10, i * 20, 80, 18)
        }).ToList();

        var options = new ScreenReaderOptions { MaxCandidates = 15 };
        var ranked = ElementRanker.RankAndFilter(elements, options);

        ranked.Should().HaveCount(15);
        for (int i = 0; i < 15; i++)
        {
            ranked[i].Id.Should().Be($"e{i + 1}");
        }
    }

    [Fact]
    public void RankAndFilter_VisualOrder_OrdersTopToBottomThenLeftToRight()
    {
        var elements = new List<AccessibilityElement>
        {
            new() { Id = "b2", Role = "Button", Label = "Top-Right", Frame = new Rectangle(150, 10, 80, 30), Enabled = true },
            new() { Id = "b1", Role = "Button", Label = "Top-Left", Frame = new Rectangle(10, 10, 80, 30), Enabled = true },
            new() { Id = "b3", Role = "Button", Label = "Bottom-Left", Frame = new Rectangle(10, 100, 80, 30), Enabled = true }
        };

        var ranked = ElementRanker.RankAndFilter(elements, ScreenReaderOptions.Default);

        ranked.Should().HaveCount(3);
        ranked[0].Label.Should().Be("Top-Left");
        ranked[1].Label.Should().Be("Top-Right");
        ranked[2].Label.Should().Be("Bottom-Left");
    }
}
