using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FaultCode.IntegrationTests;

public sealed class ApiTestingUiTests
{
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    [InlineData("Production", true, true)]
    public async Task Separate_tester_is_development_only_and_uses_generated_contract(string environment, bool available, bool enabled = false)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment(environment).ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["ApiTesting:Enabled"] = enabled.ToString() })));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/swagger/index.html");
        Assert.Equal(available ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.NotFound, response.StatusCode);
        if (available)
        {
            var init = await client.GetStringAsync("/swagger/index.js");
            Assert.Contains("/openapi/v1.json", init);
            Assert.Contains("\"persistAuthorization\":false", init);
        }
        using var protectedResponse = await client.GetAsync("/api/v1/vehicles");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
    }
}
