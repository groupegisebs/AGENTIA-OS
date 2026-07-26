using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgenticFactory.Application;

/// <summary>Executable agent capability graph persisted in DefinitionJson / BlueprintJson.</summary>
public sealed class CapabilityGraphDocument
{
    public const string KindValue = "capability-graph";
    public const int CurrentSchemaVersion = 1;

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = KindValue;

    [JsonPropertyName("meta")]
    public CapabilityGraphMeta Meta { get; set; } = new();

    [JsonPropertyName("nodes")]
    public List<CapabilityNode> Nodes { get; set; } = [];

    [JsonPropertyName("edges")]
    public List<CapabilityEdge> Edges { get; set; } = [];

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "graph-runtime";

    public static bool TryParse(string? json, out CapabilityGraphDocument? graph)
    {
        graph = null;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Direct capability-graph document
            if (root.TryGetProperty("kind", out var kind)
                && string.Equals(kind.GetString(), KindValue, StringComparison.OrdinalIgnoreCase))
            {
                graph = JsonSerializer.Deserialize<CapabilityGraphDocument>(json, JsonOptions);
                return graph is not null && graph.Nodes.Count > 0;
            }

            // Designer payload wrapper (schemaVersion 3 + designerWorkflow)
            if (root.TryGetProperty("designerWorkflow", out var dw) && dw.ValueKind == JsonValueKind.Object)
            {
                graph = FromDesignerWorkflow(root, dw);
                return graph.Nodes.Count > 0;
            }

            // Nested designer state already under nodes/edges without kind
            if (root.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array
                && nodes.GetArrayLength() > 0)
            {
                graph = JsonSerializer.Deserialize<CapabilityGraphDocument>(json, JsonOptions)
                        ?? FromLooseNodes(root);
                if (graph.Nodes.Count > 0)
                {
                    graph.Kind = KindValue;
                    graph.SchemaVersion = CurrentSchemaVersion;
                    graph.Provider = "graph-runtime";
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public static CapabilityGraphDocument FromDesignerWorkflow(JsonElement root, JsonElement dw)
    {
        var graph = new CapabilityGraphDocument
        {
            Kind = KindValue,
            SchemaVersion = CurrentSchemaVersion,
            Provider = "graph-runtime",
            Meta = new CapabilityGraphMeta
            {
                Name = ReadString(root, "agentName") ?? ReadNestedString(dw, "meta", "name") ?? "Agent",
                Mission = ReadString(root, "mission") ?? ReadNestedString(dw, "meta", "mission") ?? string.Empty,
                Domain = ReadString(root, "businessDomain") ?? ReadNestedString(dw, "meta", "businessDomain") ?? string.Empty,
                Avatar = ReadString(root, "agentAvatar") ?? ReadNestedString(dw, "meta", "avatar") ?? "🤖"
            }
        };

        if (dw.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array)
        {
            foreach (var n in nodes.EnumerateArray())
            {
                graph.Nodes.Add(new CapabilityNode
                {
                    Id = ReadString(n, "id") ?? Guid.NewGuid().ToString("N"),
                    Type = ReadString(n, "type") ?? "unknown",
                    Label = ReadString(n, "label") ?? "Capacité",
                    Config = ReadConfig(n)
                });
            }
        }

        if (dw.TryGetProperty("edges", out var edges) && edges.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in edges.EnumerateArray())
            {
                graph.Edges.Add(new CapabilityEdge
                {
                    Id = ReadString(e, "id") ?? Guid.NewGuid().ToString("N"),
                    From = ReadString(e, "from") ?? string.Empty,
                    To = ReadString(e, "to") ?? string.Empty,
                    Label = ReadString(e, "label")
                });
            }
        }

        return graph;
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptionsIndented);

    public IReadOnlyList<CapabilityNode> TopologicalOrder()
    {
        var byId = Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var incoming = Nodes.ToDictionary(n => n.Id, _ => 0, StringComparer.Ordinal);
        var outgoing = Nodes.ToDictionary(n => n.Id, _ => new List<string>(), StringComparer.Ordinal);

        foreach (var edge in Edges)
        {
            if (!byId.ContainsKey(edge.From) || !byId.ContainsKey(edge.To)) continue;
            incoming[edge.To] = incoming.GetValueOrDefault(edge.To) + 1;
            outgoing[edge.From].Add(edge.To);
        }

        var queue = new Queue<string>(incoming.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var ordered = new List<CapabilityNode>();
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            ordered.Add(byId[id]);
            foreach (var next in outgoing[id])
            {
                incoming[next]--;
                if (incoming[next] == 0) queue.Enqueue(next);
            }
        }

        // Cycles / orphans: append remaining in original order
        if (ordered.Count < Nodes.Count)
        {
            foreach (var n in Nodes)
            {
                if (ordered.All(o => o.Id != n.Id)) ordered.Add(n);
            }
        }

        return ordered;
    }

    private static CapabilityGraphDocument FromLooseNodes(JsonElement root)
    {
        var graph = new CapabilityGraphDocument();
        if (root.TryGetProperty("meta", out var meta) && meta.ValueKind == JsonValueKind.Object)
        {
            graph.Meta.Name = ReadString(meta, "name") ?? "Agent";
            graph.Meta.Mission = ReadString(meta, "mission") ?? string.Empty;
            graph.Meta.Domain = ReadString(meta, "domain") ?? ReadString(meta, "businessDomain") ?? string.Empty;
        }

        foreach (var n in root.GetProperty("nodes").EnumerateArray())
        {
            graph.Nodes.Add(new CapabilityNode
            {
                Id = ReadString(n, "id") ?? Guid.NewGuid().ToString("N"),
                Type = ReadString(n, "type") ?? "unknown",
                Label = ReadString(n, "label") ?? "Capacité",
                Config = ReadConfig(n)
            });
        }

        if (root.TryGetProperty("edges", out var edges) && edges.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in edges.EnumerateArray())
            {
                graph.Edges.Add(new CapabilityEdge
                {
                    Id = ReadString(e, "id") ?? Guid.NewGuid().ToString("N"),
                    From = ReadString(e, "from") ?? string.Empty,
                    To = ReadString(e, "to") ?? string.Empty,
                    Label = ReadString(e, "label")
                });
            }
        }

        return graph;
    }

    private static Dictionary<string, JsonElement> ReadConfig(JsonElement node)
    {
        var dict = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (node.TryGetProperty("config", out var cfg) && cfg.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in cfg.EnumerateObject())
                dict[p.Name] = p.Value.Clone();
        }
        return dict;
    }

    private static string? ReadString(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static string? ReadNestedString(JsonElement el, string parent, string name)
    {
        if (!el.TryGetProperty(parent, out var p) || p.ValueKind != JsonValueKind.Object) return null;
        return ReadString(p, name);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions JsonOptionsIndented = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}

public sealed class CapabilityGraphMeta
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Agent";

    [JsonPropertyName("mission")]
    public string Mission { get; set; } = string.Empty;

    [JsonPropertyName("domain")]
    public string Domain { get; set; } = string.Empty;

    [JsonPropertyName("avatar")]
    public string Avatar { get; set; } = "🤖";

    [JsonPropertyName("personality")]
    public AgentPersonality? Personality { get; set; }

    [JsonPropertyName("kpis")]
    public List<AgentKpi> Kpis { get; set; } = [];
}

public sealed class CapabilityNode
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("type")]
    public string Type { get; set; } = "unknown";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "Capacité";

    [JsonPropertyName("config")]
    public Dictionary<string, JsonElement> Config { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CapabilityEdge
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string? Label { get; set; }
}

public sealed class AgentPersonality
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.2;

    [JsonPropertyName("style")]
    public string Style { get; set; } = "Professionnel";

    [JsonPropertyName("languages")]
    public string Languages { get; set; } = "fr,en";

    [JsonPropertyName("voice")]
    public string Voice { get; set; } = "Neutre";
}

public sealed class AgentKpi
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;
}

public sealed class GraphStepResult
{
    public required string NodeId { get; init; }
    public required string NodeType { get; init; }
    public required string Label { get; init; }
    public required string Status { get; init; }
    public int DurationMs { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, object?> Output { get; init; } = new();
}

public sealed class GraphExecutionResult
{
    public bool Success { get; init; }
    public List<GraphStepResult> Steps { get; init; } = [];
    public Dictionary<string, object?> FinalOutput { get; init; } = new();
    public string? ErrorMessage { get; init; }
}
