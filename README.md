# PA28 Flight Planning Agent — Demo

A small ASP.NET Core 8 Razor Pages app demonstrating an agentic loop against the
Anthropic Claude API: Claude is given three tools (Scottish airfield lookup, live
METAR/TAF weather, and PA28 Warrior nav-log calculation) and works through a
route request step by step, producing a go/no-go style briefing.

**Demo only — not a substitute for a proper flight plan, NOTAMs, or an official
weather briefing.**

## How it works

- `Services/ClaudeAgentService.cs` runs the tool-use loop against the Anthropic
  Messages API (`/v1/messages`), executing each tool call locally and feeding
  the result back until Claude returns a final answer.
- `Data/ScottishAirfields.cs` is a small hardcoded list of GA airfields — no
  database required for this demo.
- `Services/WeatherService.cs` fetches live METAR/TAF from the free,
  no-key aviationweather.gov API.
- `Services/NavCalculator.cs` does great-circle distance/heading and a simple
  wind-corrected nav-log calculation using approximate PA28-161 Warrior figures
  (edit `Pa28WarriorPerformance` in that file to match your actual aircraft).
- Each tool call is recorded as an `AgentStep` and sent to the browser, where
  `wwwroot/js/agent.js` reveals them one at a time for the step-by-step effect.

## Configuration

Only one environment variable is required:

```
ANTHROPIC_API_KEY=sk-ant-...
```

Model used is `claude-sonnet-5` — change the `Model` constant in
`ClaudeAgentService.cs` if you'd prefer a different one.

## Run locally

```
dotnet restore
set ANTHROPIC_API_KEY=sk-ant-...      (PowerShell: $env:ANTHROPIC_API_KEY="sk-ant-...")
dotnet run
```

Then open the URL shown in the console (typically https://localhost:5001).

## Deploy (Docker → GitHub → Render)

Same pattern as VoiceChat/Groundwork:

1. Push this folder to a new GitHub repo.
2. Create a new Web Service on Render, pointing at the repo, Docker environment.
3. Set the single environment variable `ANTHROPIC_API_KEY` in Render's dashboard.
4. Render will build the Dockerfile and deploy on port 8080 (already set via
   `ASPNETCORE_URLS` in the Dockerfile).

## Known limitations (it's a demo)

- Only ~12 Scottish airfields are known — ask about anything else and the
  agent will say so.
- Nav-log wind correction is a simple headwind/tailwind component, not a full
  triangle-of-velocities calculation.
- No persistence — nothing is saved between runs.
- No auth on the endpoint — add one before sharing this publicly, since it
  spends your Anthropic API credits per run.
