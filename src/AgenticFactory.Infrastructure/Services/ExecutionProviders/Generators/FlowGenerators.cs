using System.Text.Json;
using AgenticFactory.Application;

namespace AgenticFactory.Infrastructure.Services.ExecutionProviders.Generators;

public sealed class PowerAutomateGenerator : IPowerAutomateGenerator
{
    public GeneratedFlowResult GenerateFlow(GenerateFlowRequest request) =>
        Build("flow", new
        {
            definition = new
            {
                schemaVersion = "1.0.0.0",
                triggers = new { manual = new { type = "Request", kind = "Http" } },
                actions = new Dictionary<string, object>
                {
                    ["Execute_Action"] = new
                    {
                        type = "ApiConnection",
                        inputs = new
                        {
                            host = new { connection = new { name = "@parameters('$connections')['shared_agentia']['connectionId']" } },
                            method = "post",
                            path = $"/actions/{request.ActuatorType}",
                            body = request.Parameters
                        }
                    }
                }
            },
            metadata = new { actionLabel = request.ActionLabel, actuatorType = request.ActuatorType }
        });

    public GeneratedFlowResult GenerateTrigger(GenerateFlowRequest request) =>
        Build("trigger", new { type = "Recurrence", recurrence = new { frequency = "Hour", interval = 1 } });

    public GeneratedFlowResult GenerateActions(GenerateFlowRequest request) =>
        Build("actions", new { steps = new[] { new { name = request.ActionLabel, type = request.ActuatorType } } });

    public GeneratedFlowResult GenerateExpressions(GenerateFlowRequest request) =>
        Build("expressions", new { expressions = new { actionId = "@triggerBody()?['actionId']", payload = "@body('Execute_Action')" } });

    public GeneratedFlowResult GenerateVariables(GenerateFlowRequest request) =>
        Build("variables", new { variables = new { correlationId = "@guid()", environment = "production" } });

    private static GeneratedFlowResult Build(string section, object payload) =>
        new("PowerAutomate", section, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
}

public sealed class LogicAppGenerator : ILogicAppGenerator
{
    public GeneratedFlowResult GenerateWorkflow(GenerateFlowRequest request)
    {
        var workflow = new
        {
            definition = new
            {
                schema = "https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#",
                contentVersion = "1.0.0.0",
                triggers = new { manual = new { type = "Request", kind = "Http", inputs = new { schema = new { } } } },
                actions = new Dictionary<string, object>
                {
                    ["Run_Agent_Action"] = new
                    {
                        type = "Http",
                        inputs = new
                        {
                            method = "POST",
                            uri = "@parameters('agentiaEndpoint')",
                            body = new { request.ActuatorType, request.ActionLabel, request.Parameters }
                        }
                    }
                }
            }
        };

        return new GeneratedFlowResult(
            "LogicApps",
            "workflow.json",
            JsonSerializer.Serialize(workflow, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed class N8nWorkflowGenerator : IN8nWorkflowGenerator
{
    public GeneratedFlowResult GenerateWorkflow(GenerateFlowRequest request)
    {
        var workflow = new
        {
            name = $"Agentia — {request.ActionLabel}",
            nodes = new object[]
            {
                new { id = "trigger", name = "Webhook", type = "n8n-nodes-base.webhook", typeVersion = 1, position = new[] { 0, 0 } },
                new
                {
                    id = "action",
                    name = request.ActionLabel,
                    type = "n8n-nodes-base.httpRequest",
                    typeVersion = 1,
                    position = new[] { 300, 0 },
                    parameters = new { url = "={{$env.AGENTIA_API}}", method = "POST", body = request.Parameters }
                }
            },
            connections = new { Webhook = new { main = new[] { new object[] { new { node = "action", type = "main", index = 0 } } } } },
            active = false,
            settings = new { executionOrder = "v1" },
            meta = new { actuatorType = request.ActuatorType }
        };

        return new GeneratedFlowResult(
            "N8n",
            "n8n-workflow.json",
            JsonSerializer.Serialize(workflow, new JsonSerializerOptions { WriteIndented = true }));
    }
}
