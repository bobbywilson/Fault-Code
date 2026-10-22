using System.Text;

namespace FaultCode.Api.Configuration;

// The supported custom index avoids the standalone preset's OS theme toggle
// and its string-evaluated plugins. The same strict site CSP remains in force.
public static class ApiTestingUi
{
    public static Stream OpenIndex() => new MemoryStream(Encoding.UTF8.GetBytes(Index));

    private const string Index = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <meta name="color-scheme" content="light">
          <title>Fault Code API Testing</title>
          <link rel="stylesheet" href="./swagger-ui.css">
          <link rel="stylesheet" href="./index.css">
          <style>:root{color-scheme:only light}body{background:#fff;color:#3b4151}</style>
        </head>
        <body>
          <div id="swagger-ui"></div>
          <script src="./swagger-ui-bundle.js"></script>
          <script src="./bootstrap.js"></script>
        </body>
        </html>
        """;

    public const string Bootstrap = """
        window.addEventListener("load", function () {
          window.ui = SwaggerUIBundle({
            url: "/openapi/v1.json",
            dom_id: "#swagger-ui",
            presets: [SwaggerUIBundle.presets.apis],
            layout: "BaseLayout",
            persistAuthorization: false,
            validatorUrl: null,
            supportedSubmitMethods: ["get", "post", "put", "delete"]
          });
        });
        """;
}
