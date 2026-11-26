using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TicketFlow.Infrastructure;
using TicketFlow.Infrastructure.Auth;

namespace TicketFlow.Tests.Api;

public class OperationalEndpointsTests(TicketFlowApiFactory factory) : IClassFixture<TicketFlowApiFactory>
{
    [Fact]
    public async Task Health_endpoint_is_public_and_reports_healthy()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }
}

public class JwtKeyConfigurationTests
{
    private static IOptions<JwtOptions> BuildOptions(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider().GetRequiredService<IOptions<JwtOptions>>();
    }

    private static Dictionary<string, string?> BaseSettings(string? signingKey, bool generate) => new()
    {
        ["Jwt:Issuer"] = "TicketFlow.Api",
        ["Jwt:Audience"] = "TicketFlow.Web",
        ["Jwt:SigningKey"] = signingKey,
        ["Jwt:GenerateKeyIfMissing"] = generate.ToString(),
    };

    [Fact]
    public void Missing_key_fails_validation_by_default()
    {
        var options = BuildOptions(BaseSettings(signingKey: null, generate: false));

        var read = () => options.Value;

        read.Should().Throw<OptionsValidationException>().WithMessage("*Jwt:SigningKey is not configured*");
    }

    [Fact]
    public void Demo_mode_generates_a_strong_random_key_when_none_is_set()
    {
        var first = BuildOptions(BaseSettings(signingKey: null, generate: true)).Value.SigningKey;
        var second = BuildOptions(BaseSettings(signingKey: null, generate: true)).Value.SigningKey;

        Convert.FromBase64String(first).Should().HaveCount(64);
        first.Should().NotBe(second);
    }

    [Fact]
    public void A_configured_key_is_never_replaced()
    {
        var key = new string('k', 40);

        BuildOptions(BaseSettings(key, generate: true)).Value.SigningKey.Should().Be(key);
    }
}
