namespace FlightPlanAgent.Models;

// One entry per tool call the agent makes, in order — the UI walks through these.
public record AgentStep(
    int StepNumber,
    string ToolName,
    string ToolInputJson,
    string ToolResultJson
);

// Full result returned to the browser after the agent loop finishes.
public class AgentRunResult
{
    public List<AgentStep> Steps { get; set; } = new();
    public string FinalBriefing { get; set; } = string.Empty;
    public string? Error { get; set; }
}

public class AgentRunRequest
{
    public string RouteRequest { get; set; } = string.Empty;
}
