using AgenticFactory.Application;

namespace AgenticFactory.Tests;

public class AgentMessageParserTests
{
    [Fact]
    public void Parse_UsesProposedName_WhenPresent()
    {
        var message = """
            Créer un agent IA Runtime Agentic via Agent Factory Studio :
            - Mission : Automatiser le tri des factures fournisseurs
            - Domaine (tag) : comptabilite
            - Nom proposé : Agent Factures Fournisseurs
            """;

        var result = AgentMessageParser.Parse(message);

        Assert.Equal("Agent Factures Fournisseurs", result.Name);
        Assert.Equal("Automatiser le tri des factures fournisseurs", result.Description);
        Assert.Equal("comptabilite", result.DomainId);
    }

    [Fact]
    public void Parse_UsesMission_WhenProposedNameMissing()
    {
        var message = """
            Créer un agent IA Runtime Agentic via Agent Factory Studio :
            - Mission : Qualifier les leads entrants depuis le site web
            - Domaine (tag) : marketing
            """;

        var result = AgentMessageParser.Parse(message);

        Assert.Equal("Qualifier les leads entrants depuis le site web", result.Name);
        Assert.Equal("marketing", result.DomainId);
    }

    [Fact]
    public void Parse_UsesDomainAgent_WhenOnlyDomainPresent()
    {
        var message = """
            Créer un agent IA Runtime Agentic via Agent Factory Studio :
            - Domaine métier : Support client
            """;

        var result = AgentMessageParser.Parse(message);

        Assert.Equal("Support client Agent", result.Name);
        Assert.Contains("Tickets", result.Description);
    }

    [Fact]
    public void Parse_DoesNotUseTimestampStyleName()
    {
        var message = """
            Créer un agent IA Runtime Agentic via Agent Factory Studio :
            - Nom proposé : Agent 20260629003058
            - Domaine (tag) : email
            """;

        var result = AgentMessageParser.Parse(message);

        Assert.Equal("Email Agent", result.Name);
    }
}
