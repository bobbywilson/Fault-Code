using System.Xml.Linq;

namespace FaultCode.Domain.Tests;

public sealed class ArchitectureTests
{
    [Theory]
    [InlineData("Domain", new string[] { })]
    [InlineData("Application", new[] { "Domain" })]
    [InlineData("Infrastructure", new[] { "Application" })]
    [InlineData("Api", new[] { "Application", "Infrastructure" })]
    public void Project_dependencies_point_inward(string component, string[] allowed)
    {
        var root = FindRoot();
        var project = XDocument.Load(Path.Combine(root, "src", $"FaultCode.{component}", $"FaultCode.{component}.csproj"));
        var references = project.Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value)
                .Replace("FaultCode.", "", StringComparison.Ordinal))
            .Order().ToArray();
        Assert.Equal(allowed.Order(), references);
        var packages = project.Descendants("PackageReference").Select(x => x.Attribute("Include")!.Value).Order();
        Assert.Equal(component == "Api"
            ? new[] { "Microsoft.AspNetCore.Authentication.JwtBearer", "Microsoft.AspNetCore.OpenApi", "Microsoft.Extensions.ApiDescription.Server", "ModelContextProtocol.AspNetCore" }
            : component == "Infrastructure" ? new[] { "Microsoft.Agents.AI.OpenAI", "OpenAI", "System.ClientModel" } : [], packages);
        if (component is "Domain" or "Application")
        {
            Assert.Empty(project.Descendants("FrameworkReference"));
            Assert.Equal("Microsoft.NET.Sdk", project.Root!.Attribute("Sdk")!.Value);
        }
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FaultCode.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Solution root not found.");
    }
}
