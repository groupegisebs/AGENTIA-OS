using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgenticFactory.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AgenticFactory.Infrastructure.Billing;

public sealed class GisebsPayGatewayClient(
    IHttpClientFactory httpClientFactory,
    IOptions<GisebsApiPayGatewayOptions> options,
    IHostEnvironment environment) : IGisebsPayGatewayClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly string[] SucceededStatuses =
    [
        "Succeeded", "2", "Paid", "Complete", "Active", "1"
    ];

    public bool IsConfigured => options.Value.IsConfigured;

    public async Task<BillingCheckoutResult> CreateCheckoutSessionAsync(
        GisebsCheckoutSessionRequest request,
        CancellationToken cancellationToken)
    {
        var config = EnsureConfigured();
        var client = CreateClient(config);

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync("api/checkout/session", request, JsonOptions, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Impossible de joindre GISEBS Pay Gateway ({config.BaseUrl}). Vérifiez GisebsApiPayGateway:BaseUrl.", ex);
        }

        var session = await ReadSuccessJsonAsync<CheckoutSessionResponse>(response, "la session de paiement", cancellationToken);
        if (string.IsNullOrWhiteSpace(session.PaymentCode))
            throw new InvalidOperationException("Pay Gateway n'a pas renvoyé de code de paiement.");

        var checkoutUrl = session.CheckoutUrl;
        if (string.IsNullOrWhiteSpace(checkoutUrl))
            throw new InvalidOperationException("Pay Gateway n'a pas renvoyé d'URL de paiement.");

        return new BillingCheckoutResult(
            Guid.Empty,
            session.PaymentCode,
            checkoutUrl,
            session.SessionId,
            session.Status);
    }

    public async Task<GisebsPaymentStatus?> GetPaymentStatusAsync(string paymentCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(paymentCode) || !IsConfigured)
            return null;

        var config = options.Value;
        var client = CreateClient(config);

        using var response = await client.GetAsync(
            $"api/payments/{Uri.EscapeDataString(paymentCode)}", cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        var payment = await ReadSuccessJsonAsync<PaymentResponse>(response, "le paiement", cancellationToken);
        return new GisebsPaymentStatus(
            payment.PaymentCode,
            payment.Status,
            payment.CustomerCode,
            payment.ProductCode,
            payment.PlanCode,
            payment.PaidAt);
    }

    public static bool IsPaymentSuccessful(GisebsPaymentStatus payment) =>
        payment.PaidAt.HasValue
        || SucceededStatuses.Any(s => string.Equals(s, payment.Status, StringComparison.OrdinalIgnoreCase));

    private HttpClient CreateClient(GisebsApiPayGatewayOptions config)
    {
        var client = httpClientFactory.CreateClient(nameof(GisebsPayGatewayClient));
        client.BaseAddress = new Uri(config.GetBaseUri().ToString().TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Remove("X-App-Code");
        client.DefaultRequestHeaders.Remove("X-Api-Key");
        client.DefaultRequestHeaders.Add("X-App-Code", config.AppCode);
        client.DefaultRequestHeaders.Add("X-Api-Key", config.ApiKey);
        return client;
    }

    private GisebsApiPayGatewayOptions EnsureConfigured()
    {
        var config = options.Value;
        if (!config.IsConfigured)
            throw new InvalidOperationException(
                "GisebsApiPayGateway n'est pas configuré. Définissez BaseUrl, AppCode et ApiKey.");

        config.EnsureSecureEndpoint(environment.IsDevelopment());
        return config;
    }

    private static async Task<T> ReadSuccessJsonAsync<T>(
        HttpResponseMessage response,
        string operationName,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = TryExtractErrorMessage(body) ?? Truncate(body);
            throw new InvalidOperationException($"Pay Gateway a refusé {operationName} ({(int)response.StatusCode}). {detail}");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(body, JsonOptions)
                ?? throw new InvalidOperationException($"Réponse Pay Gateway vide ({operationName}).");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Réponse Pay Gateway illisible ({operationName}). {Truncate(body)}", ex);
        }
    }

    private static string? TryExtractErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            foreach (var key in new[] { "error", "title", "detail", "message" })
            {
                if (root.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
                {
                    var text = prop.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                }
            }
        }
        catch (JsonException)
        {
            // ignore
        }

        return null;
    }

    private static string Truncate(string? value, int max = 280)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max] + "…";
    }

    private sealed record CheckoutSessionResponse(
        string PaymentCode,
        string CheckoutUrl,
        string SessionId,
        string Status);

    private sealed record PaymentResponse(
        string PaymentCode,
        string Status,
        string CustomerCode,
        string ProductCode,
        string PlanCode,
        DateTime? PaidAt);
}
