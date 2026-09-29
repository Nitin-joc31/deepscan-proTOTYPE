using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = AppLimits.MaxRequestBytes);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = AppLimits.MaxRequestBytes);
builder.Services.AddHttpClient("SightEngine", client => client.Timeout = TimeSpan.FromSeconds(45));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        context.HttpContext.Response.Headers["Retry-After"] = "60";
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new
            {
                type = "about:blank",
                title = "Scan rate limit reached",
                status = StatusCodes.Status429TooManyRequests,
                detail = "Try again in about a minute."
            },
            cancellationToken);
    };
    options.AddPolicy("scan", _ =>
    {
        return RateLimitPartition.GetFixedWindowLimiter("all-clients", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    context.Response.Headers.ContentSecurityPolicy =
        "default-src 'self'; img-src 'self' blob: data:; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdn.jsdelivr.net; font-src 'self' https://fonts.gstatic.com https://cdn.jsdelivr.net; script-src 'self' 'unsafe-inline'; connect-src 'self'; form-action 'self'; base-uri 'self'; frame-ancestors 'none'";
    await next();
});

app.UseRateLimiter();
app.MapGet("/", () => Results.File(
    Path.Combine(app.Environment.ContentRootPath, "index.html"),
    "text/html; charset=utf-8"));
app.MapGet("/api/health", (HttpContext context, IConfiguration configuration) =>
{
    var configured = !string.IsNullOrWhiteSpace(configuration["SIGHTENGINE_API_USER"]) &&
                     !string.IsNullOrWhiteSpace(configuration["SIGHTENGINE_API_SECRET"]);
    context.Response.Headers.CacheControl = "no-store";
    return Results.Ok(new { status = configured ? "ready" : "not-configured" });
});
app.MapPost("/api/analyze", AnalyzeAsync).RequireRateLimiting("scan");

app.Run();

static async Task<IResult> AnalyzeAsync(
    HttpContext context,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IHostEnvironment environment,
    CancellationToken cancellationToken)
{
    context.Response.Headers.CacheControl = "no-store";
    var apiUser = configuration["SIGHTENGINE_API_USER"];
    var apiSecret = configuration["SIGHTENGINE_API_SECRET"];
    if (string.IsNullOrWhiteSpace(apiUser) || string.IsNullOrWhiteSpace(apiSecret))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Analysis is not configured",
            detail: "The server administrator must configure the SightEngine credentials.");
    }

    if (!context.Request.HasFormContentType)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid request",
            detail: "Send an image as multipart form data.");
    }

    IFormCollection form;
    try
    {
        form = await context.Request.ReadFormAsync(cancellationToken);
    }
    catch (InvalidDataException)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status413PayloadTooLarge,
            title: "Upload is too large or invalid",
            detail: "Choose a supported image no larger than 25 MB.");
    }

    var file = form.Files.GetFile("image");
    if (file is null || file.Length == 0)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Image is required",
            detail: "Choose a JPEG, PNG, or WEBP image to analyze.");
    }

    if (file.Length > AppLimits.MaxImageBytes)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status413PayloadTooLarge,
            title: "Image is too large",
            detail: "Choose an image no larger than 25 MB.");
    }

    if (!TryGetImageType(file.ContentType, out var mediaType, out var signature))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status415UnsupportedMediaType,
            title: "Unsupported image type",
            detail: "Only JPEG, PNG, and WEBP images are supported.");
    }

    await using var imageStream = file.OpenReadStream();
    var header = new byte[12];
    var bytesRead = 0;
    while (bytesRead < header.Length)
    {
        var read = await imageStream.ReadAsync(header.AsMemory(bytesRead), cancellationToken);
        if (read == 0)
        {
            break;
        }
        bytesRead += read;
    }
    if (!HasValidSignature(header.AsSpan(0, bytesRead), signature))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid image",
            detail: "The image content does not match its declared file type.");
    }

    imageStream.Position = 0;
    using var upstreamForm = new MultipartFormDataContent();
    using var imageContent = new StreamContent(imageStream);
    imageContent.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
    upstreamForm.Add(imageContent, "media", $"upload.{signature.Extension}");
    upstreamForm.Add(new StringContent("deepfake,faces"), "models");
    upstreamForm.Add(new StringContent(apiUser), "api_user");
    upstreamForm.Add(new StringContent(apiSecret), "api_secret");

    HttpResponseMessage upstreamResponse;
    var sightEngineUrl = environment.IsDevelopment()
        ? configuration["SIGHTENGINE_API_URL"] ?? "https://api.sightengine.com/1.0/check.json"
        : "https://api.sightengine.com/1.0/check.json";
    try
    {
        using var upstreamRequest = new HttpRequestMessage(HttpMethod.Post, sightEngineUrl)
        {
            Content = upstreamForm
        };
        upstreamResponse = await httpClientFactory.CreateClient("SightEngine")
            .SendAsync(upstreamRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }
    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status504GatewayTimeout,
            title: "SightEngine timed out",
            detail: "The analysis service took too long to respond. Please retry.");
    }
    catch (HttpRequestException)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status502BadGateway,
            title: "SightEngine is unavailable",
            detail: "The analysis service could not be reached. Please retry later.");
    }

    using (upstreamResponse)
    {
        if (!upstreamResponse.IsSuccessStatusCode)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "SightEngine rejected the request",
                detail: "Check the server's SightEngine account and quota, then retry.");
        }

        byte[] responseBytes;
        try
        {
            if (upstreamResponse.Content.Headers.ContentLength > AppLimits.MaxProviderResponseBytes)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "Invalid response from SightEngine",
                    detail: "The analysis service returned an oversized response.");
            }

            await using var responseStream = await upstreamResponse.Content.ReadAsStreamAsync(cancellationToken);
            var boundedResponse = await ReadLimitedAsync(responseStream, cancellationToken);
            if (boundedResponse is null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "Invalid response from SightEngine",
                    detail: "The analysis service returned an oversized response.");
            }
            responseBytes = boundedResponse;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status504GatewayTimeout,
                title: "SightEngine timed out",
                detail: "The analysis service took too long to respond. Please retry.");
        }
        catch (Exception exception) when (exception is IOException or HttpRequestException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Invalid response from SightEngine",
                detail: "The analysis service returned an unreadable response. Please retry later.");
        }

        JsonDocument response;
        try
        {
            response = JsonDocument.Parse(responseBytes);
        }
        catch (JsonException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Invalid response from SightEngine",
                detail: "The analysis service returned an unreadable response. Please retry later.");
        }

        using (response)
        {
            var root = response.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("status", out var status) ||
                status.ValueKind != JsonValueKind.String ||
                status.GetString() != "success" ||
                root.TryGetProperty("error", out _))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "SightEngine could not analyze this image",
                    detail: "Check the server's SightEngine credentials and account quota, then retry.");
            }

            if (!TryGetScore(root, out var score))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "SightEngine did not return a valid score",
                    detail: "No analysis result was generated. Please retry later.");
            }

            var probability = (int)Math.Round(score * 100);
            var verdict = probability >= 65 ? "fake" : probability <= 35 ? "real" : "unsure";
            var faceCount = root.TryGetProperty("faces", out var faces) && faces.ValueKind == JsonValueKind.Array
                ? faces.GetArrayLength()
                : 0;

            return Results.Ok(new { verdict, probability, faceCount, source = "SightEngine API" });
        }
    }
}

static bool TryGetScore(JsonElement root, out double score)
{
    score = 0;
    return (root.TryGetProperty("deepfake", out var deepfake) &&
            deepfake.ValueKind == JsonValueKind.Object &&
            deepfake.TryGetProperty("score", out var deepfakeScore) &&
            deepfakeScore.ValueKind == JsonValueKind.Number &&
            deepfakeScore.TryGetDouble(out score) ||
            root.TryGetProperty("type", out var type) &&
            type.ValueKind == JsonValueKind.Object &&
            type.TryGetProperty("deepfake", out var typeScore) &&
            typeScore.ValueKind == JsonValueKind.Number &&
            typeScore.TryGetDouble(out score)) &&
           double.IsFinite(score) && score is >= 0 and <= 1;
}

static async Task<byte[]?> ReadLimitedAsync(Stream stream, CancellationToken cancellationToken)
{
    using var content = new MemoryStream();
    var buffer = new byte[8192];
    while (true)
    {
        var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
        if (bytesRead == 0)
        {
            return content.ToArray();
        }

        if (content.Length + bytesRead > AppLimits.MaxProviderResponseBytes)
        {
            return null;
        }

        content.Write(buffer, 0, bytesRead);
    }
}

static bool TryGetImageType(string contentType, out string mediaType, out (string Extension, byte[] Bytes) signature)
{
    switch (contentType.ToLowerInvariant())
    {
        case "image/jpeg":
        case "image/jpg":
            mediaType = "image/jpeg";
            signature = ("jpg", [0xFF, 0xD8, 0xFF]);
            return true;
        case "image/png":
            mediaType = "image/png";
            signature = ("png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
            return true;
        case "image/webp":
            mediaType = "image/webp";
            signature = ("webp", [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50]);
            return true;
        default:
            mediaType = "";
            signature = ("", []);
            return false;
    }
}

static bool HasValidSignature(ReadOnlySpan<byte> header, (string Extension, byte[] Bytes) signature)
{
    if (signature.Extension == "webp")
    {
        return header.Length >= 12 &&
               header[..4].SequenceEqual(signature.Bytes[..4]) &&
               header[8..12].SequenceEqual(signature.Bytes[8..12]);
    }

    return header.StartsWith(signature.Bytes);
}

static class AppLimits
{
    public const long MaxImageBytes = 25 * 1024 * 1024;
    public const long MaxRequestBytes = MaxImageBytes + 64 * 1024;
    public const long MaxProviderResponseBytes = 1024 * 1024;
}
