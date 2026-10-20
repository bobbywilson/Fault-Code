using System.Diagnostics;

namespace FaultCode.IntegrationTests;

public sealed class EntraConfigurationTests
{
    [Theory]
    [InlineData("TenantId", null)]
    [InlineData("Enabled", "secret-invalid-boolean")]
    [InlineData("ClientId", null)]
    [InlineData("TenantId", "common")]
    [InlineData("TenantId", "organizations")]
    [InlineData("TenantId", "secret-supplied-tenant")]
    [InlineData("TenantId", "00000000-0000-0000-0000-000000000000")]
    [InlineData("ClientId", "secret-supplied-audience")]
    [InlineData("ClientId", "api://22222222-2222-4222-8222-222222222222")]
    public async Task Invalid_enabled_identity_fails_startup_with_redacted_setting_names(string setting, string? value)
    {
        var directory = Directory.CreateTempSubdirectory("faultcode-identity-");
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
                WorkingDirectory = directory.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        process.StartInfo.ArgumentList.Add(typeof(Program).Assembly.Location);
        process.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        process.StartInfo.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        process.StartInfo.Environment["FaultCode__ApplicationName"] = "Synthetic startup test";
        process.StartInfo.Environment["Entra__Enabled"] = "true";
        process.StartInfo.Environment["Entra__TenantId"] = "11111111-1111-4111-8111-111111111111";
        process.StartInfo.Environment["Entra__ClientId"] = "22222222-2222-4222-8222-222222222222";
        if (value is null) process.StartInfo.Environment.Remove("Entra__" + setting);
        else process.StartInfo.Environment["Entra__" + setting] = value;
        try
        {
            Assert.True(process.Start());
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout + await stderr;
            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains("Entra:" + setting, output);
            if (value is not null) Assert.DoesNotContain(value, output);
        }
        finally
        {
            if (process.Id != 0 && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            directory.Delete(recursive: true);
        }
    }
}
