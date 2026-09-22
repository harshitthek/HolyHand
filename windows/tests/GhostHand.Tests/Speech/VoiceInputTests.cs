using System.IO;
using FluentAssertions;
using HolyHand.Core.Agent;
using HolyHand.Core.Common;
using HolyHand.Core.Interfaces;
using HolyHand.Core.Models;
using HolyHand.Platform.Speech;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HolyHand.Tests.Speech;

public class VoiceInputTests
{
    [Fact]
    public async Task VO_01_NoAudioDevice_FallsBackGracefullyToSecondaryService()
    {
        // Arrange: mock fallback recognizer
        var mockFallback = new Mock<ISpeechInput>();
        mockFallback
            .Setup(f => f.TranscribeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("fallback transcript");

        // Service configured with invalid model dir to avoid downloading during test
        using var whisperService = new WhisperSpeechService(
            fallbackService: mockFallback.Object,
            customModelDir: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            logger: NullLogger<WhisperSpeechService>.Instance);

        // Act
        // If WaveInEvent.DeviceCount == 0 or model fails to load, fallback is called
        var result = await whisperService.TranscribeAsync(CancellationToken.None);

        // Assert
        result.Should().Be("fallback transcript");
        mockFallback.Verify(f => f.TranscribeAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task VO_01_NoDeviceAndNoFallback_ThrowsDescriptiveExceptionWithoutCrash()
    {
        // Arrange
        using var whisperService = new WhisperSpeechService(
            fallbackService: null,
            customModelDir: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            logger: NullLogger<WhisperSpeechService>.Instance);

        // Act & Assert
        // Should throw descriptive InvalidOperationException, never an unhandled crash
        var act = async () => await whisperService.TranscribeAsync(CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task VO_02_TranscriptFeedsIntoSameLoopAsTypedText()
    {
        // Arrange: Fake recognizer returns transcript
        var fakeSpeechInput = new Mock<ISpeechInput>();
        fakeSpeechInput
            .Setup(s => s.TranscribeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("open calculator");

        var mockScreenReader = new Mock<IScreenReader>();
        mockScreenReader
            .Setup(s => s.ReadElementsAsync(It.IsAny<AppTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccessibilityElement>
            {
                new() { Id = "c1", Role = "Button", Label = "Clear", Enabled = true }
            });

        var mockDecisionModel = new Mock<IDecisionModel>();
        mockDecisionModel
            .Setup(d => d.DecideNextActionAsync(
                It.IsAny<string>(),
                It.IsAny<AppTarget>(),
                It.IsAny<IReadOnlyList<AccessibilityElement>>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentDecision
            {
                Operation = AgentOperation.Done,
                TargetId = "c1",
                TargetLabel = "Clear"
            });

        mockDecisionModel
            .Setup(d => d.VerifyCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<AppTarget>(),
                It.IsAny<IReadOnlyList<AccessibilityElement>>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var mockExecutor = new Mock<IActionExecutor>();
        mockExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<AgentDecision>(), It.IsAny<AccessibilityElement?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActionResult.SuccessResult("Clicked c1"));

        var loop = new AgentLoop(
            mockScreenReader.Object,
            mockDecisionModel.Object,
            mockExecutor.Object,
            new AgentLoopOptions { MaxSteps = 3, DryRun = true },
            NullLogger<AgentLoop>.Instance);

        // Act: Voice transcript feeds directly into loop
        var transcript = await fakeSpeechInput.Object.TranscribeAsync();
        transcript.Should().Be("open calculator");

        var target = new AppTarget { ProcessId = 1234, ProcessName = "calc", WindowTitle = "Calculator" };
        var result = await loop.RunAsync(transcript, target, CancellationToken.None);

        // Assert: Verified loop ran with the exact transcript goal
        result.Status.Should().Be(AgentRunStatus.Completed);
        mockDecisionModel.Verify(d => d.DecideNextActionAsync(
            "open calculator",
            It.IsAny<AppTarget>(),
            It.IsAny<IReadOnlyList<AccessibilityElement>>(),
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task VO_03_MissingWhisperModel_FallsBackGracefully()
    {
        // Arrange: fallback recognizer returns speech
        var fallbackRecognizer = new Mock<ISpeechInput>();
        fallbackRecognizer
            .Setup(f => f.TranscribeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("fallback result");

        // Non-existent directory, invalid file
        var emptyDir = Path.Combine(Path.GetTempPath(), "holyhand_nonexistent_" + Guid.NewGuid().ToString("N"));

        using var whisperService = new WhisperSpeechService(
            fallbackService: fallbackRecognizer.Object,
            customModelDir: emptyDir,
            logger: NullLogger<WhisperSpeechService>.Instance);

        // Act
        var result = await whisperService.TranscribeAsync(CancellationToken.None);

        // Assert
        result.Should().Be("fallback result");
    }
}
