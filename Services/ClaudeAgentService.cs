using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlightPlanAgent.Data;
using FlightPlanAgent.Models;

namespace FlightPlanAgent.Services;

// Talks to the Anthropic Messages API, exposing three local tools (airfield lookup,
// live weather, nav-log calculation) and running the tool-use loop until Claude
// produces a final answer. Every tool call/result is captured as an AgentStep so
// the front end can walk through the agent's reasoning step by step.
public class ClaudeAgentService
{
    private const string Model = "claude-sonnet-5";
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const int MaxIterations = 8;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WeatherService _weatherService;
    private readonly NavCalculator _navCalculator;
    private readonly IConfiguration _configuration;

    public ClaudeAgentService(
        IHttpClientFactory httpClientFactory,
        WeatherService weatherService,
        NavCalculator navCalculator,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _weatherService = weatherService;
        _navCalculator = navCalculator;
        _configuration = configuration;
    }

    public async Task<AgentRunResult> RunAsync(string routeRequest)
    {
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new AgentRunResult { Error = "ANTHROPIC_API_KEY environment variable is not set." };
        }

        var result = new AgentRunResult();
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var systemPrompt =
            "You are a flight planning assistant for a private pilot who flies a Piper PA28-161 Warrior " +
            "based in Scotland. Use the available tools to look up airfield details, fetch live weather " +
            "(METAR/TAF), and calculate nav-log figures (distance, heading, time, fuel) for each leg of the " +
            "route the pilot asks about. Work through the request step by step, calling tools as needed. " +
            "Airfields must be identified by their ICAO code — if the pilot gives a place name, work out the " +
            "most likely Scottish airfield ICAO code yourself. " +
            "Finish with a short, clear go/no-go style briefing: route summary, weather picture, nav-log " +
            "figures per leg, and anything that stands out (marginal weather, low fuel margin, etc). " +
            "This is a demo tool for illustration only — always make clear it is not a substitute for a " +
            "proper flight plan, NOTAMs, and a real weather briefing.";

        var messages = new JsonArray
        {
            new JsonObject
            {
                ["role"] = "user",
                ["content"] = routeRequest
            }
        };

        var tools = BuildToolDefinitions();
        var stepNumber = 0;

        for (var i = 0; i < MaxIterations; i++)
        {
            var requestBody = new JsonObject
            {
                ["model"] = Model,
                ["max_tokens"] = 1500,
                ["system"] = systemPrompt,
                ["tools"] = tools.DeepClone(),
                ["messages"] = messages.DeepClone()
            };

            using var httpResponse = await client.PostAsync(
                ApiUrl,
                new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json"));

            var responseText = await httpResponse.Content.ReadAsStringAsync();

            if (!httpResponse.IsSuccessStatusCode)
            {
                result.Error = $"Anthropic API error ({(int)httpResponse.StatusCode}): {responseText}";
                return result;
            }

            var responseJson = JsonNode.Parse(responseText)!.AsObject();
            var contentBlocks = responseJson["content"]!.AsArray();
            var stopReason = responseJson["stop_reason"]?.GetValue<string>();

            // Record the assistant's turn (text + any tool_use blocks) in the conversation.
            messages.Add(new JsonObject
            {
                ["role"] = "assistant",
                ["content"] = contentBlocks.DeepClone()
            });

            var toolUseBlocks = contentBlocks
                .Where(b => b!["type"]!.GetValue<string>() == "tool_use")
                .ToList();

            if (toolUseBlocks.Count == 0)
            {
                // No more tools requested — collect final text and stop.
                var finalText = string.Join("\n\n", contentBlocks
                    .Where(b => b!["type"]!.GetValue<string>() == "text")
                    .Select(b => b!["text"]!.GetValue<string>()));

                result.FinalBriefing = finalText;
                return result;
            }

            var toolResultBlocks = new JsonArray();

            foreach (var block in toolUseBlocks)
            {
                var toolName = block!["name"]!.GetValue<string>();
                var toolUseId = block["id"]!.GetValue<string>();
                var toolInput = block["input"]!.AsObject();

                var toolOutput = await ExecuteToolAsync(toolName, toolInput);
                stepNumber++;

                result.Steps.Add(new AgentStep(
                    stepNumber,
                    toolName,
                    toolInput.ToJsonString(),
                    toolOutput));

                toolResultBlocks.Add(new JsonObject
                {
                    ["type"] = "tool_result",
                    ["tool_use_id"] = toolUseId,
                    ["content"] = toolOutput
                });
            }

            messages.Add(new JsonObject
            {
                ["role"] = "user",
                ["content"] = toolResultBlocks
            });

            if (stopReason == "end_turn")
            {
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(result.FinalBriefing))
        {
            result.Error = "Agent stopped without a final answer (hit the iteration limit). Try a simpler route.";
        }

        return result;
    }

    private async Task<string> ExecuteToolAsync(string toolName, JsonObject input)
    {
        try
        {
            return toolName switch
            {
                "get_airfield" => GetAirfield(input),
                "get_weather" => await GetWeather(input),
                "calculate_leg" => CalculateLeg(input),
                _ => JsonSerializer.Serialize(new { error = $"Unknown tool '{toolName}'" })
            };
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private static string GetAirfield(JsonObject input)
    {
        var icao = input["icao"]?.GetValue<string>() ?? "";
        var airfield = ScottishAirfields.Find(icao);

        if (airfield is null)
        {
            var known = string.Join(", ", ScottishAirfields.All.Select(a => a.Icao));
            return JsonSerializer.Serialize(new
            {
                error = $"Unknown ICAO '{icao}'. This demo only knows: {known}"
            });
        }

        return JsonSerializer.Serialize(airfield);
    }

    private async Task<string> GetWeather(JsonObject input)
    {
        var icao = input["icao"]?.GetValue<string>() ?? "";
        var weather = await _weatherService.GetWeatherAsync(icao);
        return JsonSerializer.Serialize(weather);
    }

    private string CalculateLeg(JsonObject input)
    {
        var fromIcao = input["from_icao"]?.GetValue<string>() ?? "";
        var toIcao = input["to_icao"]?.GetValue<string>() ?? "";
        var windDir = input["wind_dir_deg"]?.GetValue<double>() ?? 0;
        var windSpeed = input["wind_speed_kt"]?.GetValue<double>() ?? 0;

        var from = ScottishAirfields.Find(fromIcao);
        var to = ScottishAirfields.Find(toIcao);

        if (from is null || to is null)
        {
            return JsonSerializer.Serialize(new { error = "Both from_icao and to_icao must be known Scottish airfield codes." });
        }

        var leg = _navCalculator.CalculateLeg(
            from.LatDeg, from.LonDeg, to.LatDeg, to.LonDeg,
            windDirDeg: windDir, windSpeedKt: windSpeed);

        return JsonSerializer.Serialize(new
        {
            from = from.Icao,
            to = to.Icao,
            leg.DistanceNm,
            leg.TrueHeadingDeg,
            leg.EstTimeEnrouteMinutes,
            leg.FuelBurnUsg,
            leg.FuelRemainingUsg,
            leg.Notes
        });
    }

    private static JsonArray BuildToolDefinitions()
    {
        return new JsonArray
        {
            new JsonObject
            {
                ["name"] = "get_airfield",
                ["description"] = "Look up details for a Scottish GA airfield by ICAO code (location, elevation, runway).",
                ["input_schema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["icao"] = new JsonObject { ["type"] = "string", ["description"] = "4-letter ICAO code, e.g. EGPT" }
                    },
                    ["required"] = new JsonArray { "icao" }
                }
            },
            new JsonObject
            {
                ["name"] = "get_weather",
                ["description"] = "Fetch the current live METAR and TAF for an airfield by ICAO code.",
                ["input_schema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["icao"] = new JsonObject { ["type"] = "string", ["description"] = "4-letter ICAO code, e.g. EGPT" }
                    },
                    ["required"] = new JsonArray { "icao" }
                }
            },
            new JsonObject
            {
                ["name"] = "calculate_leg",
                ["description"] = "Calculate distance, true heading, estimated time enroute and fuel burn for a PA28 Warrior flying direct between two Scottish airfields.",
                ["input_schema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["from_icao"] = new JsonObject { ["type"] = "string", ["description"] = "Departure ICAO code" },
                        ["to_icao"] = new JsonObject { ["type"] = "string", ["description"] = "Destination ICAO code" },
                        ["wind_dir_deg"] = new JsonObject { ["type"] = "number", ["description"] = "Wind direction in degrees true (optional, default 0)" },
                        ["wind_speed_kt"] = new JsonObject { ["type"] = "number", ["description"] = "Wind speed in knots (optional, default 0)" }
                    },
                    ["required"] = new JsonArray { "from_icao", "to_icao" }
                }
            }
        };
    }
}
