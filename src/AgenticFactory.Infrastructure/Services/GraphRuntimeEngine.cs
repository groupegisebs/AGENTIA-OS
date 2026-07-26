using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgenticFactory.Application;
using AgenticFactory.Domain;
using AgenticFactory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AgenticFactory.Infrastructure.Services;

public sealed class GraphRuntimeEngine(
    AgenticFactoryDbContext db,
    IAgentModelProvider modelProvider,
    ISecretStore secretStore,
    IKnowledgeService knowledgeService,
    IHttpClientFactory httpClientFactory,
    ILogger<GraphRuntimeEngine> logger) : IGraphRuntimeEngine
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public bool CanExecute(string? definitionJson)
        => CapabilityGraphDocument.TryParse(definitionJson, out _);

    public async Task<GraphExecutionResult> ExecuteAsync(
        Guid organizationId,
        Guid agentId,
        Guid runId,
        string definitionJson,
        Dictionary<string, object?> input,
        CancellationToken cancellationToken)
        => await ExecuteCoreAsync(organizationId, agentId, runId, definitionJson, input, persistLogs: true, cancellationToken);

    public Task<GraphExecutionResult> ExecuteDryAsync(
        Guid organizationId,
        string definitionJson,
        Dictionary<string, object?> input,
        CancellationToken cancellationToken)
        => ExecuteCoreAsync(organizationId, Guid.Empty, Guid.Empty, definitionJson, input, persistLogs: false, cancellationToken);

    private async Task<GraphExecutionResult> ExecuteCoreAsync(
        Guid organizationId,
        Guid agentId,
        Guid runId,
        string definitionJson,
        Dictionary<string, object?> input,
        bool persistLogs,
        CancellationToken cancellationToken)
    {
        if (!CapabilityGraphDocument.TryParse(definitionJson, out var graph) || graph is null)
        {
            return new GraphExecutionResult
            {
                Success = false,
                ErrorMessage = "DefinitionJson n'est pas un CapabilityGraph exécutable."
            };
        }

        var context = new Dictionary<string, object?>(input, StringComparer.OrdinalIgnoreCase)
        {
            ["__agentId"] = agentId.ToString(),
            ["__organizationId"] = organizationId.ToString(),
            ["__runId"] = runId.ToString()
        };

        var steps = new List<GraphStepResult>();
        object? lastPayload = input;

        foreach (var node in graph.TopologicalOrder())
        {
            var sw = Stopwatch.StartNew();
            var started = DateTime.UtcNow;
            string status = "Completed";
            string? error = null;
            Dictionary<string, object?> nodeOutput = new();

            try
            {
                nodeOutput = await ExecuteNodeAsync(organizationId, agentId, runId, node, context, lastPayload, cancellationToken);
                foreach (var kv in nodeOutput)
                    context[kv.Key] = kv.Value;
                if (nodeOutput.TryGetValue("payload", out var p))
                    lastPayload = p;
                else
                    lastPayload = nodeOutput;
            }
            catch (Exception ex)
            {
                status = "Failed";
                error = ex.Message;
                logger.LogWarning(ex, "Graph node {NodeId} ({Type}) failed", node.Id, node.Type);
            }

            sw.Stop();
            var step = new GraphStepResult
            {
                NodeId = node.Id,
                NodeType = node.Type,
                Label = node.Label,
                Status = status,
                DurationMs = (int)sw.ElapsedMilliseconds,
                ErrorMessage = error,
                Output = nodeOutput
            };
            steps.Add(step);

            if (persistLogs && agentId != Guid.Empty && runId != Guid.Empty)
            {
                db.ActionExecutionLogs.Add(new ActionExecutionLog
                {
                    OrganizationId = organizationId,
                    RunId = runId,
                    AgentId = agentId,
                    NodeId = node.Id,
                    NodeType = node.Type,
                    Label = node.Label,
                    ProviderType = "graph-runtime",
                    Status = status,
                    DurationMs = step.DurationMs,
                    ErrorMessage = error,
                    InputJson = JsonSerializer.Serialize(lastPayload, JsonOpts),
                    OutputJson = JsonSerializer.Serialize(nodeOutput, JsonOpts),
                    StartedAtUtc = started,
                    CompletedAtUtc = DateTime.UtcNow
                });
                await db.SaveChangesAsync(cancellationToken);
            }

            if (status == "Failed")
            {
                return new GraphExecutionResult
                {
                    Success = false,
                    Steps = steps,
                    FinalOutput = BuildFinal(graph, steps, context),
                    ErrorMessage = error
                };
            }
        }

        return new GraphExecutionResult
        {
            Success = true,
            Steps = steps,
            FinalOutput = BuildFinal(graph, steps, context)
        };
    }

    private static Dictionary<string, object?> BuildFinal(
        CapabilityGraphDocument graph,
        List<GraphStepResult> steps,
        Dictionary<string, object?> context)
        => new()
        {
            ["kind"] = CapabilityGraphDocument.KindValue,
            ["agent"] = graph.Meta.Name,
            ["mission"] = graph.Meta.Mission,
            ["steps"] = steps.Select(s => new
            {
                s.NodeId,
                s.NodeType,
                s.Label,
                s.Status,
                s.DurationMs,
                s.ErrorMessage,
                s.Output
            }).ToList(),
            ["payload"] = context.GetValueOrDefault("payload") ?? context.GetValueOrDefault("modelResponse"),
            ["contextKeys"] = context.Keys.Where(k => !k.StartsWith("__")).ToList()
        };

    private async Task<Dictionary<string, object?>> ExecuteNodeAsync(
        Guid organizationId,
        Guid agentId,
        Guid runId,
        CapabilityNode node,
        Dictionary<string, object?> context,
        object? incoming,
        CancellationToken ct)
    {
        var type = node.Type.ToLowerInvariant();

        if (type.StartsWith("trigger-") || type is "gmail" || type.StartsWith("connector-"))
        {
            return new Dictionary<string, object?>
            {
                ["payload"] = incoming ?? context,
                ["source"] = node.Type,
                ["label"] = node.Label,
                ["config"] = ConfigToDict(node)
            };
        }

        if (type is "skill-summary" or "skill-understand" or "skill-classify" or "skill-extraction"
            or "extraction-ia" or "ocr-classify" or "skill-vision" or "skill-rag")
        {
            if (type == "skill-rag")
            {
                var query = CfgString(node, "query")
                    ?? incoming?.ToString()
                    ?? context.GetValueOrDefault("payload")?.ToString()
                    ?? string.Empty;
                var hits = await knowledgeService.SearchAsync(organizationId, agentId, query, 5, ct);
                var evidence = string.Join("\n---\n", hits.Select(h => $"{h.Title}: {h.Snippet}"));
                var ragPrompt = $"Mission: {CfgString(node, "mission") ?? "Répondre avec le contexte."}\nQuestion: {query}\nContexte:\n{evidence}";
                var ragGen = await modelProvider.GenerateAsync(new ModelGenerationRequest(organizationId, ragPrompt, "Tu es un agent RAG précis."), ct);
                return new Dictionary<string, object?>
                {
                    ["payload"] = ragGen.Output,
                    ["modelResponse"] = ragGen.Output,
                    ["hits"] = hits.Count,
                    ["modelProvider"] = ragGen.Provider,
                    ["promptTokens"] = ragGen.PromptTokens,
                    ["completionTokens"] = ragGen.CompletionTokens,
                    ["estimatedCostUsd"] = ragGen.EstimatedCostUsd
                };
            }

            var model = CfgString(node, "model") ?? "gpt-4o-mini";
            var mission = CfgString(node, "mission") ?? CfgString(node, "fields") ?? CfgString(node, "categories") ?? node.Label;
            var system = CfgString(node, "systemPrompt") ?? $"Tu es la capacité « {node.Label} ». Modèle préféré: {model}.";
            var prompt = $"{mission}\n\nEntrée:\n{JsonSerializer.Serialize(incoming ?? context, JsonOpts)}";
            var gen = await modelProvider.GenerateAsync(new ModelGenerationRequest(organizationId, prompt, system), ct);
            return new Dictionary<string, object?>
            {
                ["payload"] = gen.Output,
                ["modelResponse"] = gen.Output,
                ["modelProvider"] = gen.Provider,
                ["promptTokens"] = gen.PromptTokens,
                ["completionTokens"] = gen.CompletionTokens,
                ["estimatedCostUsd"] = gen.EstimatedCostUsd
            };
        }

        if (type is "decision-if" or "validation")
        {
            var field = CfgString(node, "field") ?? "payload";
            var op = CfgString(node, "operator") ?? "est non vide";
            var expected = CfgString(node, "value");
            var actual = context.GetValueOrDefault(field)?.ToString()
                ?? context.GetValueOrDefault("payload")?.ToString()
                ?? string.Empty;
            var pass = EvaluateCondition(actual, op, expected);
            if (!pass && type == "validation")
                throw new InvalidOperationException($"Validation échouée ({node.Label}): {field} {op} {expected}");
            return new Dictionary<string, object?>
            {
                ["payload"] = incoming,
                ["branch"] = pass ? "true" : "false",
                ["passed"] = pass
            };
        }

        if (type is "decision-switch")
        {
            var field = CfgString(node, "field") ?? "payload";
            var actual = context.GetValueOrDefault(field)?.ToString() ?? string.Empty;
            return new Dictionary<string, object?>
            {
                ["payload"] = incoming,
                ["route"] = actual,
                ["default"] = CfgString(node, "default")
            };
        }

        if (type is "decision-wait")
        {
            var duration = CfgString(node, "duration") ?? "0s";
            await Task.Delay(ParseShortDelay(duration), ct);
            return new Dictionary<string, object?> { ["payload"] = incoming, ["waited"] = duration };
        }

        if (type is "action-api")
        {
            return await ExecuteHttpAsync(organizationId, node, incoming, ct);
        }

        if (type is "action-email")
        {
            return await ExecuteSmtpAsync(organizationId, node, incoming, ct);
        }

        if (type is "action-database" or "stockage-bdd")
        {
            return await ExecuteDatabaseAsync(organizationId, node, incoming, ct);
        }

        if (type is "notif-teams" or "action-teams")
        {
            var webhook = await secretStore.ResolveRefAsync(organizationId, CfgString(node, "webhookUrl"), ct)
                ?? CfgString(node, "webhookUrl");
            if (string.IsNullOrWhiteSpace(webhook))
            {
                return new Dictionary<string, object?>
                {
                    ["payload"] = incoming,
                    ["skipped"] = true,
                    ["reason"] = "Webhook Teams non configuré"
                };
            }

            var client = httpClientFactory.CreateClient();
            var body = new { text = CfgString(node, "message") ?? JsonSerializer.Serialize(incoming, JsonOpts) };
            using var resp = await client.PostAsync(webhook,
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);
            return new Dictionary<string, object?>
            {
                ["payload"] = incoming,
                ["httpStatus"] = (int)resp.StatusCode,
                ["ok"] = resp.IsSuccessStatusCode
            };
        }

        if (type.StartsWith("util-") || type.StartsWith("decision-") || type.StartsWith("action-"))
        {
            return new Dictionary<string, object?>
            {
                ["payload"] = incoming,
                ["acknowledged"] = node.Type,
                ["config"] = ConfigToDict(node)
            };
        }

        return new Dictionary<string, object?>
        {
            ["payload"] = incoming,
            ["passthrough"] = true,
            ["type"] = node.Type
        };
    }

    private async Task<Dictionary<string, object?>> ExecuteHttpAsync(
        Guid organizationId, CapabilityNode node, object? incoming, CancellationToken ct)
    {
        var url = CfgString(node, "url") ?? throw new InvalidOperationException("URL API requise.");
        var method = (CfgString(node, "method") ?? "POST").ToUpperInvariant();
        var client = httpClientFactory.CreateClient();
        using var req = new HttpRequestMessage(new HttpMethod(method), url);

        var authType = CfgString(node, "auth") ?? CfgString(node, "authType") ?? "Aucune";
        var token = await secretStore.ResolveRefAsync(organizationId, CfgString(node, "token"), ct)
            ?? CfgString(node, "token");
        if (!string.IsNullOrWhiteSpace(token) && !authType.Equals("Aucune", StringComparison.OrdinalIgnoreCase))
        {
            if (authType.Contains("Bearer", StringComparison.OrdinalIgnoreCase))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            else if (authType.Contains("API", StringComparison.OrdinalIgnoreCase))
                req.Headers.TryAddWithoutValidation("X-Api-Key", token);
            else if (authType.Contains("Basic", StringComparison.OrdinalIgnoreCase))
                req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        }

        var headersJson = CfgString(node, "headers");
        if (!string.IsNullOrWhiteSpace(headersJson))
        {
            try
            {
                using var hdoc = JsonDocument.Parse(headersJson);
                foreach (var p in hdoc.RootElement.EnumerateObject())
                    req.Headers.TryAddWithoutValidation(p.Name, p.Value.ToString());
            }
            catch { /* ignore malformed headers */ }
        }

        var body = CfgString(node, "body");
        if (method is not "GET" and not "HEAD")
        {
            var content = body ?? JsonSerializer.Serialize(incoming ?? new { }, JsonOpts);
            req.Content = new StringContent(content, Encoding.UTF8, "application/json");
        }

        var timeoutSec = CfgInt(node, "timeout") ?? 30;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeoutSec, 1, 300)));

        using var resp = await client.SendAsync(req, cts.Token);
        var respBody = await resp.Content.ReadAsStringAsync(cts.Token);
        return new Dictionary<string, object?>
        {
            ["payload"] = respBody,
            ["httpStatus"] = (int)resp.StatusCode,
            ["ok"] = resp.IsSuccessStatusCode
        };
    }

    private async Task<Dictionary<string, object?>> ExecuteSmtpAsync(
        Guid organizationId, CapabilityNode node, object? incoming, CancellationToken ct)
    {
        var host = CfgString(node, "smtpHost") ?? CfgString(node, "server");
        var to = CfgString(node, "recipient") ?? CfgString(node, "to");
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(to))
        {
            return new Dictionary<string, object?>
            {
                ["payload"] = incoming,
                ["skipped"] = true,
                ["reason"] = "SMTP incomplet (server/recipient)"
            };
        }

        var port = CfgInt(node, "port") ?? 587;
        var user = CfgString(node, "from") ?? CfgString(node, "username");
        var password = await secretStore.ResolveRefAsync(organizationId, CfgString(node, "password"), ct)
            ?? CfgString(node, "password");
        var subject = CfgString(node, "subject") ?? $"Agentia OS — {node.Label}";
        var body = CfgString(node, "body") ?? JsonSerializer.Serialize(incoming, JsonOpts);

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = CfgBool(node, "ssl") ?? true,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
        if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(password))
            client.Credentials = new System.Net.NetworkCredential(user, password);

        using var msg = new MailMessage(user ?? "noreply@agentia.local", to, subject, body);
        var cc = CfgString(node, "cc");
        if (!string.IsNullOrWhiteSpace(cc)) msg.CC.Add(cc);

        await client.SendMailAsync(msg, ct);
        return new Dictionary<string, object?>
        {
            ["payload"] = incoming,
            ["emailSent"] = true,
            ["to"] = to
        };
    }

    private async Task<Dictionary<string, object?>> ExecuteDatabaseAsync(
        Guid organizationId, CapabilityNode node, object? incoming, CancellationToken ct)
    {
        var dbType = (CfgString(node, "dbType") ?? CfgString(node, "type") ?? "PostgreSQL").ToLowerInvariant();
        var host = CfgString(node, "host") ?? "localhost";
        var port = CfgInt(node, "port") ?? 5432;
        var database = CfgString(node, "database") ?? throw new InvalidOperationException("Base de données requise.");
        var user = CfgString(node, "user") ?? CfgString(node, "username") ?? throw new InvalidOperationException("Utilisateur BDD requis.");
        var password = await secretStore.ResolveRefAsync(organizationId, CfgString(node, "password"), ct)
            ?? CfgString(node, "password")
            ?? throw new InvalidOperationException("Mot de passe BDD requis.");
        var query = CfgString(node, "query");
        var table = CfgString(node, "table");
        var mode = (CfgString(node, "mode") ?? "INSERT").ToUpperInvariant();

        if (!dbType.Contains("postgres") && !dbType.Contains("npgsql"))
        {
            // v1: only PostgreSQL is fully wired; others acknowledged
            return new Dictionary<string, object?>
            {
                ["payload"] = incoming,
                ["skipped"] = true,
                ["reason"] = $"Type BDD '{dbType}' non exécuté en v1 (PostgreSQL uniquement)."
            };
        }

        var cs = $"Host={host};Port={port};Database={database};Username={user};Password={password}";
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync(ct);

        if (string.IsNullOrWhiteSpace(query) && !string.IsNullOrWhiteSpace(table) && mode is "INSERT" or "UPSERT")
        {
            var json = JsonSerializer.Serialize(incoming ?? new { }, JsonOpts);
            query = $"INSERT INTO {SanitizeIdent(table)} (payload, created_at) VALUES (@payload, NOW())";
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("payload", json);
            var affected = await cmd.ExecuteNonQueryAsync(ct);
            return new Dictionary<string, object?>
            {
                ["payload"] = incoming,
                ["rowsAffected"] = affected,
                ["mode"] = mode
            };
        }

        if (string.IsNullOrWhiteSpace(query))
            throw new InvalidOperationException("Requête SQL ou table requise.");

        await using var raw = new NpgsqlCommand(query, conn);
        if (query.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            await using var reader = await raw.ExecuteReaderAsync(ct);
            var rows = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync(ct) && rows.Count < 100)
            {
                var row = new Dictionary<string, object?>();
                for (var i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(row);
            }
            return new Dictionary<string, object?>
            {
                ["payload"] = rows,
                ["rowCount"] = rows.Count
            };
        }

        var n = await raw.ExecuteNonQueryAsync(ct);
        return new Dictionary<string, object?>
        {
            ["payload"] = incoming,
            ["rowsAffected"] = n
        };
    }

    private static string SanitizeIdent(string name)
    {
        var cleaned = new string(name.Where(c => char.IsLetterOrDigit(c) || c is '_' or '.').ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "agent_payloads" : cleaned;
    }

    private static bool EvaluateCondition(string actual, string op, string? expected)
    {
        op = op.Trim().ToLowerInvariant();
        return op switch
        {
            "=" or "==" or "égal" => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            "!=" or "différent" => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            "contient" => actual.Contains(expected ?? "", StringComparison.OrdinalIgnoreCase),
            "est vide" => string.IsNullOrWhiteSpace(actual),
            "est non vide" => !string.IsNullOrWhiteSpace(actual),
            ">" when double.TryParse(actual, out var a) && double.TryParse(expected, out var b) => a > b,
            "<" when double.TryParse(actual, out var a2) && double.TryParse(expected, out var b2) => a2 < b2,
            _ => !string.IsNullOrWhiteSpace(actual)
        };
    }

    private static TimeSpan ParseShortDelay(string duration)
    {
        duration = duration.Trim().ToLowerInvariant();
        if (duration.EndsWith("ms") && int.TryParse(duration[..^2], out var ms))
            return TimeSpan.FromMilliseconds(Math.Clamp(ms, 0, 5000));
        if (duration.EndsWith('s') && int.TryParse(duration.TrimEnd('s'), out var s))
            return TimeSpan.FromSeconds(Math.Clamp(s, 0, 5));
        return TimeSpan.FromMilliseconds(10);
    }

    private static Dictionary<string, object?> ConfigToDict(CapabilityNode node)
    {
        var d = new Dictionary<string, object?>();
        foreach (var kv in node.Config)
            d[kv.Key] = JsonElementToObject(kv.Value);
        return d;
    }

    private static string? CfgString(CapabilityNode node, string key)
    {
        if (!node.Config.TryGetValue(key, out var el)) return null;
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => el.ToString()
        };
    }

    private static int? CfgInt(CapabilityNode node, string key)
    {
        var s = CfgString(node, key);
        return int.TryParse(s, out var n) ? n : null;
    }

    private static bool? CfgBool(CapabilityNode node, string key)
    {
        if (!node.Config.TryGetValue(key, out var el)) return null;
        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(el.GetString(), out var b) => b,
            _ => null
        };
    }

    private static object? JsonElementToObject(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => el.GetRawText()
    };
}

public sealed class AesSecretStore(AgenticFactoryDbContext db, IConfiguration configuration) : ISecretStore
{
    public async Task<Guid> UpsertAsync(Guid organizationId, string name, string plaintextValue, string? provider, CancellationToken cancellationToken)
    {
        TenantGuard.RequireOrganization(organizationId);
        var existing = await db.OrganizationSecrets
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Name == name, cancellationToken);

        var cipher = Protect(plaintextValue);
        if (existing is null)
        {
            existing = new OrganizationSecret
            {
                OrganizationId = organizationId,
                Name = name,
                Provider = provider,
                CipherText = cipher
            };
            db.OrganizationSecrets.Add(existing);
        }
        else
        {
            existing.CipherText = cipher;
            existing.Provider = provider ?? existing.Provider;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return existing.Id;
    }

    public async Task<string?> GetPlaintextAsync(Guid organizationId, Guid secretId, CancellationToken cancellationToken)
    {
        var secret = await db.OrganizationSecrets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == secretId && x.OrganizationId == organizationId, cancellationToken);
        return secret is null ? null : Unprotect(secret.CipherText);
    }

    public async Task<string?> ResolveRefAsync(Guid organizationId, string? secretRefOrPlain, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(secretRefOrPlain)) return null;
        if (secretRefOrPlain.StartsWith("secret:", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(secretRefOrPlain["secret:".Length..], out var id))
        {
            return await GetPlaintextAsync(organizationId, id, cancellationToken);
        }

        return secretRefOrPlain;
    }

    public async Task<IReadOnlyList<SecretListItem>> ListAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        return await db.OrganizationSecrets.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.Name)
            .Select(x => new SecretListItem(x.Id, x.Name, x.Provider, x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    private byte[] KeyBytes()
    {
        var key = configuration["Secrets:EncryptionKey"]
            ?? configuration["Auth:JwtKey"]
            ?? "dev-local-key-minimum-32-characters-ok";
        return SHA256.HashData(Encoding.UTF8.GetBytes(key));
    }

    private string Protect(string plaintext)
    {
        var key = KeyBytes();
        var iv = RandomNumberGenerator.GetBytes(16);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        using var enc = aes.CreateEncryptor();
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = enc.TransformFinalBlock(plain, 0, plain.Length);
        var payload = new byte[iv.Length + cipher.Length];
        Buffer.BlockCopy(iv, 0, payload, 0, iv.Length);
        Buffer.BlockCopy(cipher, 0, payload, iv.Length, cipher.Length);
        return Convert.ToBase64String(payload);
    }

    private string Unprotect(string cipherText)
    {
        var payload = Convert.FromBase64String(cipherText);
        var iv = payload.AsSpan(0, 16).ToArray();
        var cipher = payload.AsSpan(16).ToArray();
        using var aes = Aes.Create();
        aes.Key = KeyBytes();
        aes.IV = iv;
        using var dec = aes.CreateDecryptor();
        var plain = dec.TransformFinalBlock(cipher, 0, cipher.Length);
        return Encoding.UTF8.GetString(plain);
    }
}
