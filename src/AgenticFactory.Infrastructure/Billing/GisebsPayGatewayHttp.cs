using System.Text.Json;

namespace AgenticFactory.Infrastructure.Billing;

internal static class GisebsPayGatewayHttp
{
    public static SocketsHttpHandler CreateHandler() => new()
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        AllowAutoRedirect = false
    };

    public static async Task<T> ReadSuccessJsonAsync<T>(
        HttpResponseMessage response,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var status = (int)response.StatusCode;

        if (status is >= 300 and < 400)
        {
            var location = response.Headers.Location?.ToString();
            if (location?.Contains("/Error", StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new InvalidOperationException(
                    "Pay Gateway a rencontré une erreur interne (produit/plan manquant ou Stripe non configuré). "
                    + "Dans l'admin Pay Gateway → Produits → application AGENTIAOS : créez les produits AGENTIA-* "
                    + "avec le plan MONTHLY, puis vérifiez le menu Stripe.");
            }

            throw new InvalidOperationException(
                $"Pay Gateway a redirigé la requête {operationName} ({status}"
                + (string.IsNullOrWhiteSpace(location) ? ")." : $" → {location}).")
                + " Vérifiez GisebsApiPayGateway:BaseUrl.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var detail = MapPayGatewayError(TryExtractErrorMessage(body) ?? Truncate(body), status);
            throw new InvalidOperationException($"Pay Gateway a refusé {operationName} ({status}). {detail}");
        }

        if (LooksLikeHtml(body))
        {
            throw new InvalidOperationException(
                $"Pay Gateway a renvoyé une page HTML au lieu de JSON ({operationName}). "
                + "Vérifiez que les produits AGENTIA-* et le plan MONTHLY existent pour AGENTIAOS et que Stripe est configuré.");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(body, GisebsPayGatewayJson.Options)
                ?? throw new InvalidOperationException($"Réponse Pay Gateway vide ({operationName}).");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Réponse Pay Gateway illisible ({operationName}). {Truncate(body)}", ex);
        }
    }

    internal static string MapPayGatewayError(string? detail, int status)
    {
        if (string.IsNullOrWhiteSpace(detail))
            return status switch
            {
                401 => "Authentification refusée. Vérifiez AppCode et ApiKey dans GisebsApiPayGateway.",
                403 => "Accès refusé (domaine non autorisé pour cette application cliente).",
                _ => "Erreur inconnue."
            };

        if (detail.Contains("Application cliente invalide", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("Application invalide", StringComparison.OrdinalIgnoreCase))
        {
            return "L'application cliente AGENTIAOS n'est pas enregistrée ou inactive dans Pay Gateway. "
                + "Créez-la dans l'admin Pay Gateway → Applications (AppCode AGENTIAOS), générez une clé API, "
                + "puis mettez à jour GisebsApiPayGateway:ApiKey.";
        }

        if (detail.Contains("API Key invalide", StringComparison.OrdinalIgnoreCase))
        {
            return "Clé API Pay Gateway invalide ou expirée. Régénérez une clé dans l'admin Pay Gateway "
                + "→ Applications → AGENTIAOS et mettez à jour GisebsApiPayGateway:ApiKey.";
        }

        if (detail.Contains("AppCode et API Key requis", StringComparison.OrdinalIgnoreCase))
        {
            return "En-têtes X-App-Code et X-Api-Key manquants. Vérifiez la configuration GisebsApiPayGateway.";
        }

        if (detail.Contains("introuvable", StringComparison.OrdinalIgnoreCase))
        {
            return detail + " Créez le produit/plan dans Pay Gateway → Produits → AGENTIAOS "
                + "(voir deploy/PAYGATEWAY-CATALOG.md).";
        }

        return detail;
    }

    private static bool LooksLikeHtml(string body)
    {
        var trimmed = body.AsSpan().TrimStart();
        return trimmed.StartsWith("<!", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryExtractErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body) || LooksLikeHtml(body))
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
}

internal static class GisebsPayGatewayJson
{
    public static readonly JsonSerializerOptions Options = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
}
