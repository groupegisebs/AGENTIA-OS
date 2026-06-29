using System.Net.Http.Json;
using AgenticFactory.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AgenticFactory.Infrastructure.Billing;

public sealed class GisebsPayGatewayClient(
    IHttpClientFactory httpClientFactory,
    IOptions<GisebsApiPayGatewayOptions> options,
    IHostEnvironment environment) : IGisebsPayGatewayClient
{
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
            response = await client.PostAsJsonAsync(
                "api/checkout/session", request, GisebsPayGatewayJson.Options, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Impossible de joindre GISEBS Pay Gateway ({config.BaseUrl}). Vérifiez GisebsApiPayGateway:BaseUrl.", ex);
        }

        var session = await GisebsPayGatewayHttp.ReadSuccessJsonAsync<CheckoutSessionResponse>(
            response, "la session de paiement", cancellationToken);

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

        try
        {
            using var response = await client.GetAsync(
                $"api/payments/{Uri.EscapeDataString(paymentCode)}", cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var payment = await GisebsPayGatewayHttp.ReadSuccessJsonAsync<PaymentResponse>(
                response, "le paiement", cancellationToken);

            return new GisebsPaymentStatus(
                payment.PaymentCode,
                payment.Status,
                payment.CustomerCode,
                payment.ProductCode,
                payment.PlanCode,
                payment.PaidAt);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
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
