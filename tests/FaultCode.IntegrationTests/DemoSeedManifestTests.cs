using System.Text.Json;
using FaultCode.Api.Contracts;
using FaultCode.Domain;

namespace FaultCode.IntegrationTests;

public sealed class DemoSeedManifestTests
{
    [Fact]
    public void Prepared_seed_is_explicitly_synthetic_and_passes_existing_domain_validation()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Seeds", "nexteer-steering-faults.json")));
        var root = json.RootElement;
        Assert.True(root.GetProperty("synthetic").GetBoolean());
        Assert.Equal("prepared-not-loaded", root.GetProperty("status").GetString());
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var ontology = JsonSerializer.Deserialize<OntologyContentRequest>(root.GetProperty("ontology"), options)!.ToDomain();
        OntologyRules.Validate(ontology);
        var codes = new HashSet<string>();
        foreach (var fault in root.GetProperty("faults").EnumerateArray())
        {
            var system = fault.GetProperty("system").GetString()!;
            var code = fault.GetProperty("code").GetString()!;
            Assert.Equal(code, FaultRules.Code(code, system));
            Assert.True(codes.Add(code));
            var content = JsonSerializer.Deserialize<FaultContentRequest>(fault.GetProperty("content"), options)!.ToDomain();
            FaultRules.Validate(content);
            Assert.Contains(ontology.Concepts, c => c.Id == content.ClassificationId && c.System == system && c.Kind == "classification");
            Assert.All(content.Remedies, remedy => Assert.False(remedy.Approved));
            Assert.Contains("not a verified Nexteer or OEM", content.Description);
        }
        Assert.Equal(9, codes.Count);
    }
}
