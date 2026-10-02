using FluentAssertions;
using HolyHand.Core.ScreenReading;
using Xunit;

namespace HolyHand.Tests.ScreenReading;

public class SecretSanitizerTests
{
    [Theory]
    [InlineData("SuperSecretPassword123!")]
    [InlineData("123456")]
    [InlineData("password")]
    [InlineData("")]
    public void Sanitize_WhenIsPasswordIsTrue_ReturnsPasswordMask(string raw)
    {
        var result = SecretSanitizer.Sanitize(raw, isPassword: true);

        result.Should().Be("[PASSWORD]");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sanitize_NullOrEmpty_ReturnsEmptyString(string? raw)
    {
        var result = SecretSanitizer.Sanitize(raw, isPassword: false);

        result.Should().Be(string.Empty);
    }

    [Fact]
    public void Sanitize_NormalTextWithoutSecrets_RemainsUnchanged()
    {
        string text = "Welcome to the HolyHand desktop assistant. Click Next to continue.";

        var result = SecretSanitizer.Sanitize(text);

        result.Should().Be(text);
    }

    [Theory]
    [InlineData("My Visa card is 4532 1234 5678 9010 on file", "My Visa card is [REDACTED_CARD] on file")]
    [InlineData("MasterCard 5500-1234-5678-9012 expires soon", "MasterCard [REDACTED_CARD] expires soon")]
    [InlineData("Account: 4111111111111111 please confirm", "Account: [REDACTED_CARD] please confirm")]
    [InlineData("AMEX 378282246310005 valid", "AMEX [REDACTED_CARD] valid")]
    public void Sanitize_CreditCardNumbers_RedactsCardProperly(string input, string expected)
    {
        var result = SecretSanitizer.Sanitize(input);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Your Vercel token is vck_abcdef1234567890_test for deploy", "Your Vercel token is [REDACTED_KEY] for deploy")]
    [InlineData("OpenAI key: sk-abcdefghijklmnopqrstuvwxyz1234567890", "OpenAI key: [REDACTED_KEY]")]
    [InlineData("GitHub PAT: ghp_1234567890abcdefghijklmnopqrstuvwxyz", "GitHub PAT: [REDACTED_KEY]")]
    public void Sanitize_ApiKeys_RedactsKeyProperly(string input, string expected)
    {
        var result = SecretSanitizer.Sanitize(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void Sanitize_JwtTokens_RedactsJwtToken()
    {
        string jwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4ifQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";
        string input = $"User session token: {jwt} active";

        var result = SecretSanitizer.Sanitize(input);

        result.Should().Be("User session token: [REDACTED_KEY] active");
    }

    [Theory]
    [InlineData("Authorization: Bearer my_secret_token_1234567890_value", "Authorization: Bearer [REDACTED]")]
    [InlineData("bearer auth_header_very_long_secret_value_xyz", "bearer [REDACTED]")]
    public void Sanitize_BearerHeaders_RedactsBearerToken(string input, string expected)
    {
        var result = SecretSanitizer.Sanitize(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void Sanitize_MultipleSecretsInSingleString_RedactsAll()
    {
        string input = "Card: 4532 9876 5432 1098, Key: vck_deploy_token_9988776655, Header: Bearer api_token_header_secret_123";

        var result = SecretSanitizer.Sanitize(input);

        result.Should().Be("Card: [REDACTED_CARD], Key: [REDACTED_KEY], Header: Bearer [REDACTED]");
    }
}
