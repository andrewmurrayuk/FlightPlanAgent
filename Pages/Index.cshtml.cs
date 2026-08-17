using FlightPlanAgent.Models;
using FlightPlanAgent.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlightPlanAgent.Pages;

[IgnoreAntiforgeryToken]
public class IndexModel : PageModel
{
    private readonly ClaudeAgentService _agentService;

    public IndexModel(ClaudeAgentService agentService)
    {
        _agentService = agentService;
    }

    public void OnGet()
    {
    }

    public async Task<JsonResult> OnPostRunAgentAsync([FromBody] AgentRunRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RouteRequest))
        {
            return new JsonResult(new AgentRunResult { Error = "Please describe a route." });
        }

        var result = await _agentService.RunAsync(request.RouteRequest);
        return new JsonResult(result);
    }
}
