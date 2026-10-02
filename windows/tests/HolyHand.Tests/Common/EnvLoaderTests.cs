using System.IO;
using FluentAssertions;
using HolyHand.Core.Common;
using Xunit;

namespace HolyHand.Tests.Common;

public class EnvLoaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _envKeysToClean = new();

    public EnvLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "holyhand_env_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        foreach (var key in _envKeysToClean)
        {
            Environment.SetEnvironmentVariable(key, null, EnvironmentVariableTarget.Process);
        }

        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in temp dir
        }
    }

    [Fact]
    public void Load_ValidKeyValuePairs_LoadsIntoEnvironment()
    {
        string key1 = "HH_TEST_KEY1_" + Guid.NewGuid().ToString("N")[..8];
        string key2 = "HH_TEST_KEY2_" + Guid.NewGuid().ToString("N")[..8];
        _envKeysToClean.Add(key1);
        _envKeysToClean.Add(key2);

        string envContent = $"{key1}=AlphaValue\n{key2}=BetaValue\n";
        File.WriteAllText(Path.Combine(_tempDir, ".env"), envContent);

        EnvLoader.Load(_tempDir);

        Environment.GetEnvironmentVariable(key1).Should().Be("AlphaValue");
        Environment.GetEnvironmentVariable(key2).Should().Be("BetaValue");
    }

    [Fact]
    public void Load_CommentsAndEmptyLines_AreIgnored()
    {
        string key = "HH_TEST_VALID_" + Guid.NewGuid().ToString("N")[..8];
        _envKeysToClean.Add(key);

        string envContent = $"# This is a comment\n\n   # Indented comment\n{key}=RealValue\n\n";
        File.WriteAllText(Path.Combine(_tempDir, ".env"), envContent);

        EnvLoader.Load(_tempDir);

        Environment.GetEnvironmentVariable(key).Should().Be("RealValue");
    }

    [Fact]
    public void Load_QuotedValues_StripsQuotes()
    {
        string doubleQuotedKey = "HH_TEST_DQ_" + Guid.NewGuid().ToString("N")[..8];
        string singleQuotedKey = "HH_TEST_SQ_" + Guid.NewGuid().ToString("N")[..8];
        _envKeysToClean.Add(doubleQuotedKey);
        _envKeysToClean.Add(singleQuotedKey);

        string envContent = $"{doubleQuotedKey}=\"value with spaces\"\n{singleQuotedKey}='single quoted value'\n";
        File.WriteAllText(Path.Combine(_tempDir, ".env"), envContent);

        EnvLoader.Load(_tempDir);

        Environment.GetEnvironmentVariable(doubleQuotedKey).Should().Be("value with spaces");
        Environment.GetEnvironmentVariable(singleQuotedKey).Should().Be("single quoted value");
    }

    [Fact]
    public void Load_ExistingEnvironmentVariable_IsNotOverwritten()
    {
        string key = "HH_TEST_EXISTING_" + Guid.NewGuid().ToString("N")[..8];
        _envKeysToClean.Add(key);
        Environment.SetEnvironmentVariable(key, "OriginalValue", EnvironmentVariableTarget.Process);

        string envContent = $"{key}=OverwriteValue\n";
        File.WriteAllText(Path.Combine(_tempDir, ".env"), envContent);

        EnvLoader.Load(_tempDir);

        Environment.GetEnvironmentVariable(key).Should().Be("OriginalValue");
    }

    [Fact]
    public void Load_MissingEnvFile_DoesNotThrow()
    {
        string emptyDir = Path.Combine(Path.GetTempPath(), "holyhand_empty_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyDir);

        var act = () => EnvLoader.Load(emptyDir);

        act.Should().NotThrow();

        try { Directory.Delete(emptyDir); } catch { }
    }
}
