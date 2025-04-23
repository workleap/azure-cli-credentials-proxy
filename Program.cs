using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;

var tokenCredential = new AzureCliCredential();
var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.TypeInfoResolver = SourceGenerationContext.Default;
});

var app = builder.Build();

// Can be consumed by ManagedIdentityCredential by specifying IDENTITY_ENDPOINT and IMDS_ENDPOINT environment variables to this action URL
// See https://github.com/Azure/azure-sdk-for-net/blob/Azure.Identity_1.8.0/sdk/identity/Azure.Identity/src/AzureArcManagedIdentitySource.cs
app.MapGet("/token", async (HttpContext context, string resource, CancellationToken cancellationToken) =>
{
    var token = await tokenCredential.GetTokenAsync(new TokenRequestContext([resource]), cancellationToken);
    var result = new JsonObject()
    {
        ["access_token"] = token.Token,
        ["expiresOn"] = token.ExpiresOn.ToString("O", CultureInfo.InvariantCulture),
        ["expires_on"] = token.ExpiresOn.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
        ["tokenType"] = "Bearer",
        ["resource"] = resource,
    };
    return Results.Ok(result);
});

// Can be consumed by "az login --identity" by specifying MSI_ENDPOINT environment variable to this action URL
// https://github.com/Azure/msrestazure-for-python/blob/master/msrestazure/azure_active_directory.py#L474
app.MapPost("/token", async (HttpContext context, HttpRequest request, CancellationToken cancellationToken) =>
{
    var form = await request.ReadFormAsync(cancellationToken);
    var resource = form["resource"].ToString();
    var token = await tokenCredential.GetTokenAsync(new TokenRequestContext([resource]), cancellationToken);
    var result = new JsonObject()
    {
        ["access_token"] = token.Token,
        ["expiresOn"] = token.ExpiresOn.ToString("O", CultureInfo.InvariantCulture),
        ["expires_on"] = token.ExpiresOn.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
        ["token_type"] = "Bearer",
        ["resource"] = resource,
    };
    return Results.Ok(result);
});

app.Run();

[JsonSourceGenerationOptions]
[JsonSerializable(typeof(JsonObject))]
internal sealed partial class SourceGenerationContext : JsonSerializerContext
{
}