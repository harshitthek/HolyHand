using FluentAssertions;
using HolyHand.Core.Common;
using HolyHand.Core.Jev;
using Xunit;

namespace HolyHand.Tests.Jev;

public class LaptopAndVolumeCommandTests
{
    [Theory]
    [InlineData("set volume to 69", 69)]
    [InlineData("volume 69", 69)]
    [InlineData("set volume to 69%", 69)]
    [InlineData("volume to 50%", 50)]
    [InlineData("set volume 25", 25)]
    [InlineData("turn volume to 80", 80)]
    [InlineData("max volume", 100)]
    [InlineData("maximum volume", 100)]
    [InlineData("full volume", 100)]
    [InlineData("half volume", 50)]
    [InlineData("quarter volume", 25)]
    [InlineData("zero volume", 0)]
    public void VolumeExtraction_ExtractsAccurateIntegerTarget(string goal, int expected)
    {
        var success = JevDecisionModel.TryExtractVolumeTarget(goal, out var actual);
        success.Should().BeTrue();
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData("set volume to 69")]
    [InlineData("check system volume")]
    [InlineData("mute")]
    [InlineData("unmute")]
    [InlineData("make it louder")]
    [InlineData("quieter please")]
    public void IsVolumeGoal_IdentifiesAudioRequests(string goal)
    {
        JevDecisionModel.IsVolumeGoal(goal).Should().BeTrue();
    }

    [Theory]
    [InlineData("play")]
    [InlineData("pause")]
    [InlineData("play pause")]
    [InlineData("resume")]
    [InlineData("next track")]
    [InlineData("next song")]
    [InlineData("skip song")]
    [InlineData("previous song")]
    [InlineData("prev song")]
    public void IsMediaGoal_IdentifiesMediaPlaybackRequests(string goal)
    {
        JevDecisionModel.IsMediaGoal(goal).Should().BeTrue();
    }

    [Theory]
    [InlineData("lock screen")]
    [InlineData("lock workstation")]
    [InlineData("lock pc")]
    [InlineData("show desktop")]
    [InlineData("minimize all")]
    [InlineData("take screenshot")]
    [InlineData("screenshot")]
    [InlineData("snip")]
    public void IsSystemControlGoal_IdentifiesSystemRequests(string goal)
    {
        JevDecisionModel.IsSystemControlGoal(goal).Should().BeTrue();
    }

    [Theory]
    [InlineData("brave")]
    [InlineData("chrome")]
    [InlineData("fxsound")]
    [InlineData("docker")]
    [InlineData("burp suite")]
    [InlineData("blender")]
    [InlineData("capcut")]
    [InlineData("everything")]
    [InlineData("mysql")]
    [InlineData("terminal")]
    [InlineData("bluetooth")]
    [InlineData("wifi")]
    [InlineData("battery")]
    [InlineData("downloads")]
    [InlineData("notepad")]
    [InlineData("calc")]
    public void SystemAliases_RecognizesLaptopApplicationsAndShortcuts(string alias)
    {
        SystemAliases.IsKnownAlias(alias).Should().BeTrue();
        SystemAliases.Aliases.Should().ContainKey(alias);
    }

    [Theory]
    [InlineData("open brave")]
    [InlineData("launch blender")]
    [InlineData("start docker")]
    [InlineData("notepad")]
    [InlineData("calc")]
    [InlineData("bluetooth")]
    [InlineData("wifi")]
    public void IsPureLaunchGoal_DetectsAppLaunches(string goal)
    {
        JevDecisionModel.IsPureLaunchGoal(goal).Should().BeTrue();
    }
}
