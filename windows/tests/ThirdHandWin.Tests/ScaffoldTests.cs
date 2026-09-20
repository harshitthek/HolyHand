using FluentAssertions;
using Xunit;

namespace HolyHand.Tests;

public class ScaffoldTests
{
    [Fact]
    public void TestEnvironment_ShouldBeHealthy()
    {
        // Assert that the test framework and assertions are operational
        true.Should().BeTrue();
    }
}
