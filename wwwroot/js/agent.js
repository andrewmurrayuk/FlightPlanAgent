document.addEventListener("DOMContentLoaded", function () {
    const runBtn = document.getElementById("runAgentBtn");
    const routeInput = document.getElementById("routeRequest");
    const stepsPanel = document.getElementById("stepsPanel");
    const stepsList = document.getElementById("stepsList");
    const briefingPanel = document.getElementById("briefingPanel");
    const briefingText = document.getElementById("briefingText");
    const errorPanel = document.getElementById("errorPanel");

    const TOOL_LABELS = {
        get_airfield: "Looking up airfield",
        get_weather: "Fetching live weather",
        calculate_leg: "Calculating nav-log leg"
    };

    runBtn.addEventListener("click", async function () {
        resetPanels();
        runBtn.disabled = true;
        runBtn.textContent = "Running…";

        try {
            const response = await fetch(window.AGENT_RUN_URL, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ routeRequest: routeInput.value })
            });

            const rawText = await response.text();

            if (!response.ok) {
                showError(`Server returned ${response.status} ${response.statusText}: ${rawText || "(empty body)"}`);
                return;
            }

            let result;
            try {
                result = JSON.parse(rawText);
            } catch {
                showError("Response was not valid JSON: " + rawText.slice(0, 500));
                return;
            }

            if (result.error) {
                showError(result.error);
            } else {
                await revealSteps(result.steps || []);
                showBriefing(result.finalBriefing);
            }
        } catch (err) {
            showError("Request failed: " + err.message);
        } finally {
            runBtn.disabled = false;
            runBtn.textContent = "Run agent";
        }
    });

    function resetPanels() {
        stepsList.innerHTML = "";
        stepsPanel.hidden = true;
        briefingPanel.hidden = true;
        briefingText.textContent = "";
        errorPanel.hidden = true;
        errorPanel.textContent = "";
    }

    async function revealSteps(steps) {
        if (steps.length === 0) return;
        stepsPanel.hidden = false;

        for (const step of steps) {
            const li = document.createElement("li");
            li.className = "agent-step";

            const label = TOOL_LABELS[step.toolName] || step.toolName;
            const title = document.createElement("div");
            title.className = "step-title";
            title.textContent = `${step.stepNumber}. ${label}`;

            const summary = document.createElement("div");
            summary.className = "step-summary";
            summary.textContent = summariseStep(step);

            const details = document.createElement("details");
            details.className = "step-details";
            const summaryTag = document.createElement("summary");
            summaryTag.textContent = "Show tool input / result";
            const input = document.createElement("pre");
            input.className = "step-io";
            input.textContent = "input:  " + prettyJson(step.toolInputJson);
            const output = document.createElement("pre");
            output.className = "step-io";
            output.textContent = "result: " + prettyJson(step.toolResultJson);
            details.appendChild(summaryTag);
            details.appendChild(input);
            details.appendChild(output);

            li.appendChild(title);
            li.appendChild(summary);
            li.appendChild(details);
            stepsList.appendChild(li);

            await sleep(450);
        }
    }

    function summariseStep(step) {
        let input = {}, result = {};
        try { input = JSON.parse(step.toolInputJson); } catch {}
        try { result = JSON.parse(step.toolResultJson); } catch {}

        if (result.error) return "⚠️ " + result.error;

        switch (step.toolName) {
            case "get_airfield":
                return `${result.Icao || input.icao}: ${result.Name || "?"} — elev ${result.ElevationFt ?? "?"} ft, ${result.RunwayInfo || ""}`;
            case "get_weather": {
                const parts = [];
                if (result.Metar) parts.push("METAR received");
                if (result.Taf) parts.push("TAF received");
                if (result.Error) parts.push(result.Error);
                return `${result.Icao || input.icao}: ${parts.join(", ") || "no data"}`;
            }
            case "calculate_leg":
                return `${result.from} → ${result.to}: ${result.DistanceNm} nm, hdg ${result.TrueHeadingDeg}°T, ${result.EstTimeEnrouteMinutes} min, ${result.FuelBurnUsg} USG burn (${result.FuelRemainingUsg} USG remaining)`;
            default:
                return "";
        }
    }

    function showBriefing(text) {
        briefingPanel.hidden = false;
        const md = text || "(no briefing text returned)";
        briefingText.innerHTML = (window.marked && window.marked.parse) ? window.marked.parse(md) : md;
    }

    function showError(message) {
        errorPanel.hidden = false;
        errorPanel.textContent = message;
    }

    function prettyJson(jsonString) {
        try {
            return JSON.stringify(JSON.parse(jsonString), null, 2);
        } catch {
            return jsonString;
        }
    }

    function sleep(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
    }
});
