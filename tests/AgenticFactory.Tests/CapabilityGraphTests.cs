using AgenticFactory.Application;
using Xunit;

namespace AgenticFactory.Tests;

public class CapabilityGraphTests
{
    [Fact]
    public void TryParse_DesignerPayload_YieldsExecutableGraph()
    {
        var json = """
        {
          "schemaVersion": 3,
          "creationMode": "designer",
          "agentName": "DocExtract",
          "mission": "Extraire passeports",
          "businessDomain": "documents",
          "designerWorkflow": {
            "meta": { "name": "DocExtract", "mission": "Extraire passeports" },
            "nodes": [
              { "id": "a", "type": "trigger-email", "label": "Email", "config": { "account": "a@b.com" } },
              { "id": "b", "type": "skill-summary", "label": "Résumé", "config": { "model": "gpt-4o-mini" } }
            ],
            "edges": [ { "id": "e1", "from": "a", "to": "b" } ]
          }
        }
        """;

        Assert.True(CapabilityGraphDocument.TryParse(json, out var graph));
        Assert.NotNull(graph);
        Assert.Equal(2, graph!.Nodes.Count);
        Assert.Equal("DocExtract", graph.Meta.Name);
        var order = graph.TopologicalOrder();
        Assert.Equal("a", order[0].Id);
        Assert.Equal("b", order[1].Id);
    }

    [Fact]
    public void TryParse_MockCapabilityGraph_Works()
    {
        var graph = new CapabilityGraphDocument
        {
            Meta = { Name = "X", Mission = "Y" },
            Nodes =
            [
                new CapabilityNode { Id = "n1", Type = "trigger-webhook", Label = "Hook" },
                new CapabilityNode { Id = "n2", Type = "action-api", Label = "API" }
            ],
            Edges = [new CapabilityEdge { Id = "e1", From = "n1", To = "n2" }]
        };
        var json = graph.ToJson();
        Assert.True(CapabilityGraphDocument.TryParse(json, out var parsed));
        Assert.Equal("capability-graph", parsed!.Kind);
        Assert.Equal(2, parsed.Nodes.Count);
    }
}
