document.addEventListener("DOMContentLoaded", function () {
    const runBtn = document.getElementById("runAgentBtn");
    const runStatus = document.getElementById("runStatus");
    const routeInput = document.getElementById("routeRequest");
    const stepsPanel = document.getElementById("stepsPanel");
    const stepsList = document.getElementById("stepsList");
    const stepsMeta = document.getElementById("stepsMeta");
    const briefingPanel = document.getElementById("briefingPanel");
    const briefingText = document.getElementById("briefingText");
    const errorPanel = document.getElementById("errorPanel");
    const copyBtn = document.getElementById("copyBriefingBtn");
    const clock = document.getElementById("utcClock");

    const TOOL_LABELS = {
        get_airfield: "Airfield lookup",
        get_weather: "Live weather",
        calculate_leg: "Nav log"
    };

    // UTC clock — Zulu time is what a pilot expects on a briefing desk.
    function tickClock() {
        const d = new Date();
        const hh = String(d.getUTCHours()).padStart(2, "0");
        const mm = String(d.getUTCMinutes()).padStart(2, "0");
        clock.textContent = `${hh}:${mm}Z`;
    }
    tickClock();
    setInterval(tickClock, 15000);

    document.querySelectorAll(".chip[data-example]").forEach(chip => {
        chip.addEventListener("click", () => {
            routeInput.value = chip.dataset.example;
            routeInput.focus();
        });
    });

    copyBtn.addEventListener("click", async () => {
        try {
            await navigator.clipboard.writeText(briefingText.innerText);
            copyBtn.textContent = "Copied";
            setTimeout(() => (copyBtn.textContent = "Copy"), 1500);
        } catch {
            copyBtn.textContent = "Copy failed";
        }
    });

    let statusTimer = null;

    runBtn.addEventListener("click", async function () {
        resetPanels();
        runBtn.disabled = true;
        runBtn.textContent = "Working…";
        const started = Date.now();
        statusTimer = setInterval(() => {
            const s = Math.round((Date.now() - started) / 1000);
            runStatus.textContent = `Agent working — ${s}s`;
        }, 1000);
        runStatus.textContent = "Agent working — 0s";

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
                clearInterval(statusTimer);
                const secs = Math.round((Date.now() - started) / 1000);
                runStatus.textContent = `Completed in ${secs}s`;
                await revealSteps(result.steps || []);
                showBriefing(result.finalBriefing);
            }
        } catch (err) {
            showError("Request failed: " + err.message);
        } finally {
            clearInterval(statusTimer);
            runBtn.disabled = false;
            runBtn.textContent = "Prepare briefing";
        }
    });

    function resetPanels() {
        stepsList.innerHTML = "";
        stepsMeta.textContent = "";
        stepsPanel.hidden = true;
        briefingPanel.hidden = true;
        briefingText.textContent = "";
        errorPanel.hidden = true;
        errorPanel.textContent = "";
        runStatus.textContent = "";
    }

    async function revealSteps(steps) {
        if (steps.length === 0) return;
        stepsPanel.hidden = false;
        stepsMeta.textContent = `${steps.length} tool call${steps.length === 1 ? "" : "s"}`;

        for (const step of steps) {
            const li = document.createElement("li");
            li.className = "strip";

            const head = document.createElement("div");
            head.className = "strip-head";

            const num = document.createElement("span");
            num.className = "strip-num";
            num.textContent = String(step.stepNumber).padStart(2, "0");

            const tool = document.createElement("span");
            tool.className = "strip-tool";
            tool.textContent = TOOL_LABELS[step.toolName] || step.toolName;

            const tick = document.createElement("span");
            tick.className = "strip-tick";
            tick.setAttribute("aria-hidden", "true");
            tick.textContent = "✓";

            head.appendChild(num);
            head.appendChild(tool);
            head.appendChild(tick);

            const summary = document.createElement("div");
            summary.className = "strip-summary";
            summary.textContent = summariseStep(step);

            const details = document.createElement("details");
            details.className = "strip-details";
            const summaryTag = document.createElement("summary");
            summaryTag.textContent = "Tool input & result";
            const input = document.createElement("pre");
            input.className = "strip-io";
            input.textContent = "input:  " + prettyJson(step.toolInputJson);
            const output = document.createElement("pre");
            output.className = "strip-io";
            output.textContent = "result: " + prettyJson(step.toolResultJson);
            details.appendChild(summaryTag);
            details.appendChild(input);
            details.appendChild(output);

            li.appendChild(head);
            li.appendChild(summary);
            li.appendChild(details);
            stepsList.appendChild(li);

            requestAnimationFrame(() => li.classList.add("is-in"));
            await sleep(420);
        }
    }

    function summariseStep(step) {
        let input = {}, result = {};
        try { input = JSON.parse(step.toolInputJson); } catch {}
        try { result = JSON.parse(step.toolResultJson); } catch {}

        if (result.error) return "⚠ " + result.error;

        switch (step.toolName) {
            case "get_airfield":
                return `${result.Icao || input.icao} — ${result.Name || "?"}, elev ${result.ElevationFt ?? "?"} ft, ${result.RunwayInfo || ""}`;
            case "get_weather": {
                const parts = [];
                if (result.Metar) parts.push("METAR");
                if (result.Taf) parts.push("TAF");
                const got = parts.length ? parts.join(" + ") + " received" : "no data";
                return `${result.Icao || input.icao} — ${got}${result.Error ? " · " + result.Error : ""}`;
            }
            case "calculate_leg":
                return `${result.from} → ${result.to} — ${result.DistanceNm} nm, ${result.TrueHeadingDeg}°T, ${result.EstTimeEnrouteMinutes} min, ${result.FuelBurnUsg} USG (${result.FuelRemainingUsg} USG remaining)`;
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
