using System.IO;
using FluentAssertions;
using HolyHand.Core.Agent;
using HolyHand.Core.Interfaces;
using HolyHand.Core.Models;
using HolyHand.Platform.Speech;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Whisper.net;
using Xunit;

namespace HolyHand.Tests.Speech;

public class VoiceInputTests
{
    [Fact]
    public async Task VO_01_NoAudioDevice_FallsBackGracefullyToSecondaryService()
    {
        var mockFallback = new Mock<ISpeechInput>();
        mockFallback
            .Setup(f => f.TranscribeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("fallback transcript");

        using var whisperService = new WhisperSpeechService(
            fallbackService: mockFallback.Object,
            customModelDir: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            logger: NullLogger<WhisperSpeechService>.Instance,
            audioDeviceCheck: () => false);

        var result = await whisperService.TranscribeAsync(CancellationToken.None);
        result.Should().Be("fallback transcript");
        mockFallback.Verify(f => f.TranscribeAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task VO_01_NoDeviceAndNoFallback_ThrowsDescriptiveExceptionWithoutCrash()
    {
        using var whisperService = new WhisperSpeechService(
            fallbackService: null,
            customModelDir: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            logger: NullLogger<WhisperSpeechService>.Instance,
            audioDeviceCheck: () => false);

        var act = async () => await whisperService.TranscribeAsync(CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*microphone*");
    }

    [Fact]
    public async Task VO_02_TranscriptFeedsIntoSameLoopAsTypedText()
    {
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

        var transcript = await fakeSpeechInput.Object.TranscribeAsync();
        transcript.Should().Be("open calculator");

        var target = new AppTarget { ProcessId = 1234, ProcessName = "calc", WindowTitle = "Calculator" };
        var result = await loop.RunAsync(transcript, target, CancellationToken.None);

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
        var fallbackRecognizer = new Mock<ISpeechInput>();
        fallbackRecognizer
            .Setup(f => f.TranscribeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("fallback result");

        var emptyDir = Path.Combine(Path.GetTempPath(), "holyhand_nonexistent_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyDir);
        // Write invalid model data to simulate corrupt/missing model file without triggering 75MB network download
        await File.WriteAllTextAsync(Path.Combine(emptyDir, "ggml-tiny.bin"), "corrupted_model_content");

        using var whisperService = new WhisperSpeechService(
            fallbackService: fallbackRecognizer.Object,
            customModelDir: emptyDir,
            logger: NullLogger<WhisperSpeechService>.Instance);

        var result = await whisperService.TranscribeAsync(CancellationToken.None);
        result.Should().Be("fallback result");
    }

    [Fact]
    public void VO_04_WhisperProcessor_LoadsDownloadedModelSuccessfully()
    {
        var modelPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HolyHand", "models", "ggml-tiny.bin");
        if (!File.Exists(modelPath)) return;

        using var factory = WhisperFactory.FromPath(modelPath);
        using var processor = factory.CreateBuilder()
            .WithLanguage("auto")
            .WithNoContext()
            .WithSingleSegment()
            .Build();

        processor.Should().NotBeNull();
    }
}
