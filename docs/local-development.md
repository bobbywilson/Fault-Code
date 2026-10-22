# Backend development

Part of FAU-5. Requires stable .NET SDK 10.0.402 (C# 14, .NET 10 LTS). `global.json` allows later patches in the same SDK feature band and rejects previews. Install from https://dotnet.microsoft.com/download/dotnet/10.0. Native macOS (Apple Silicon/Intel), Windows and Linux SDKs are available. Docker Compose instructions are in [docker-development.md](docker-development.md).

This Mac has a checksum-verified SDK installed at `/Users/bobbywilson/Documents/Codex/.toolchains/dotnet`. In a fresh terminal:

```sh
export DOTNET_ROOT="/Users/bobbywilson/Documents/Codex/.toolchains/dotnet"
export PATH="$DOTNET_ROOT:$PATH"
cd "/Users/bobbywilson/Development/Docker/Fault Code"
dotnet --version
dotnet restore FaultCode.slnx --locked-mode
dotnet build FaultCode.slnx --configuration Release --no-restore
dotnet test FaultCode.slnx --configuration Release --no-build
dotnet run --project src/FaultCode.Api
```

Other machines only need a matching SDK on PATH; omit the Mac-specific exports and use your checkout path. Open `FaultCode.slnx` in an IDE supporting .NET 10, or use the CLI. No IDE-specific generated files are required.

Visit http://localhost:5080/health/live. Expected HTTP 200 body: `Healthy`. Stop with Ctrl+C. Liveness checks the process only, not identity, storage, AI or other providers. Versioned public health and demo example endpoints are documented in [API conventions](api-conventions.md). [Entra bearer validation](entra-authentication.md) is implemented but disabled until isolated registrations are provisioned; vehicle/driver endpoints require identity and permissions; their production storage returns 503 pending DEC-001.

`FaultCode:ApplicationName` is required, nonblank and at most 100 characters. A non-secret default lives in appsettings.json. Override with `FaultCode__ApplicationName` or `--FaultCode:ApplicationName=...`. Invalid values fail before the host starts; error messages name the setting, not its value. ASP.NET Core's standard configuration order applies: appsettings, environment-specific JSON, development user secrets, environment variables, command-line arguments. Keep secrets in environment/user-secret stores, never tracked JSON. No provider secrets or database settings are required by this foundation.

## Boundaries

- Domain: provider-free fleet records, aggregate identity and association validation; no dependencies.
- Application: provider-independent contracts and foundation status service; references Domain.
- Infrastructure: runtime-environment adapter; references Application.
- API: MVC controller composition, options/startup validation and liveness; composes Application and Infrastructure.

Application persistence contracts define typed aggregate lookup/add and cancellable unit-of-work save. They have no production adapter registration, ORM or database dependency.

The initial status service is internal composition information, not a public business API. Domain data models and persistence will land with their own tickets. No relational provider has been chosen (DEC-001).

Separate Domain.Tests and Application.Tests projects enforce project references and demonstrate adapter replacement; integration tests run the real host and verify options validation, override precedence, DI replacement, liveness and absent business routes. Delivery CI runs Linux build/tests/contracts and Linux container acceptance. Native Windows/macOS checks are optional coverage, not delivery requirements; the sole developer runs Linux Docker containers even on Mac. Native local tests are supplemental evidence.

```sh
dotnet format FaultCode.slnx --verify-no-changes --no-restore
```

## Requested local hostname

The user selected `faultcode.test` on 2026-10-20. The host allowlist includes it, and Rider can execute `src/FaultCode.Api/FaultCode.Api.http` for quick HTTP checks. The native API currently runs at http://localhost:5080. A loopback hosts entry and a port-80 reverse proxy are still required for the port-free http://faultcode.test address; neither is installed by FAU-5. Administrative access was unavailable during setup, so do not treat the hostname as active. Keep existing Homestead/Lalai mappings untouched. FAU-6 supplies the Compose reverse proxy; see [Docker development](docker-development.md) for startup and separate resolver activation.

FAU-10: [Vehicle/driver contracts and storage limitations](vehicle-driver-management.md). Test doubles are test-only and never activated by an environment flag.
