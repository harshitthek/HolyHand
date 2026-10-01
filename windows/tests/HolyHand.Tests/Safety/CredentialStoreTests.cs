using FluentAssertions;
using HolyHand.Platform.Safety;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HolyHand.Tests.Safety;

public class CredentialStoreTests
{
    [Fact]
    public void SP_02_CredentialManager_RoundTrip_SaveAndRetrieve()
    {
        var store = new CredentialStore(NullLogger<CredentialStore>.Instance);

        // Save previous env var if any
        var oldEnv = Environment.GetEnvironmentVariable("AI_GATEWAY_API_KEY");
        try
        {
            // Clear env var to test Credential Manager directly
            Environment.SetEnvironmentVariable("AI_GATEWAY_API_KEY", null);

            var testKey = "vck_test_roundtrip_" + Guid.NewGuid().ToString("N");
            store.SetApiKey(testKey);

            store.HasKey().Should().BeTrue();
            store.GetApiKey().Should().Be(testKey);

            // Cleanup
            store.DeleteApiKey();
        }
        finally
        {
            Environment.SetEnvironmentVariable("AI_GATEWAY_API_KEY", oldEnv);
        }
    }

    [Fact]
    public void SP_02_EnvironmentVariable_TakesPrecedenceOverCredentialStore()
    {
        var store = new CredentialStore(NullLogger<CredentialStore>.Instance);
        var oldEnv = Environment.GetEnvironmentVariable("AI_GATEWAY_API_KEY");

        try
        {
            var credKey = "vck_cred_manager_" + Guid.NewGuid().ToString("N");
            var envKey = "vck_env_override_" + Guid.NewGuid().ToString("N");

            store.SetApiKey(credKey);
            Environment.SetEnvironmentVariable("AI_GATEWAY_API_KEY", envKey);

            // Assert env var takes precedence
            store.GetApiKey().Should().Be(envKey);

            // Cleanup
            store.DeleteApiKey();
        }
        finally
        {
            Environment.SetEnvironmentVariable("AI_GATEWAY_API_KEY", oldEnv);
        }
    }
}
