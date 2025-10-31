const MCPPanel = (() => {
    // DOM 
    const $ = (id) => document.getElementById(id);
    const runBtn = $("runBtn");
    const outputPre = $("output");
    const serverRawPre = $("serverRaw");
    const metaDiv = $("meta");
    const scenarioList = $("scenarioList");
    const simBtn = $("simulateBtn");
    const simResult = $("simResult");

    // State
    let connection = null; // SignalR connection
    let running = false;

    // Utils
    function currentSource() {
        return [...document.querySelectorAll('input[name="source"]')]
            .find(r => r.checked)?.value;
    }

    function safeJson(txt) { try { return JSON.parse(txt); } catch { return {}; } }
    function ms(t0) { return Math.round(performance.now() - t0); }

    function setRunning(v) {
        running = v;
        runBtn.disabled = v;
        simBtn.disabled = v;
        runBtn.textContent = v ? "Running…" : "Run";
    }

    function setOutputText(t) { outputPre.textContent = t; }
    function appendOutputChunk(chunk) { outputPre.textContent += chunk; }

    function setRaw(obj, bodyStr) {
        const transport = obj?.headers?.transport || (obj?.headers ? "http" : "-");
        metaDiv.textContent = `status: ${obj?.status ?? "-"} • ${obj?.ms ?? "-"} ms • transport: ${transport}`;
        serverRawPre.textContent = bodyStr ?? "";
    }

    // SignalR 
    async function getConnection() {
        if (connection && connection.state === "Connected") return connection;

        const url = `${API_BASE}/LlamaHub`;
        console.log("SignalR: connecting to", url);

        connection = new signalR.HubConnectionBuilder()
            .withUrl(url)
            .withAutomaticReconnect()
            .build();

        await connection.start();
        console.log("SignalR connected:", connection.state);
        return connection;
    }

    async function streamLocal(req, onChunk) {
        const start = performance.now();
        const connection = await getConnection();
        const stream = connection.stream("StreamResponse", req.prompt);

        await new Promise((resolve, reject) => {
            stream.subscribe({
                next: (x) => onChunk(String(x)),
                error: (e) => reject(e),
                complete: () => resolve()
            });
        });

        return { status: 200, headers: { transport: "signalr" }, body: "(stream complete)", ms: ms(start) };
    }

    // MCP Call
    const API_BASE = "http://localhost:5000";

    async function callMcp(req, onChunk) {
        const start = performance.now();
        const res = await fetch(`${API_BASE}/api/mcp/send`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(req)
        });

        const headers = {};
        res.headers.forEach((v, k) => headers[k] = v);

        const bodyText = await res.text();

        try {
            const parsed = JSON.parse(bodyText);
            if (Array.isArray(parsed?.chunks)) parsed.chunks.forEach(t => onChunk(String(t)));
            else if (typeof parsed?.text === "string") onChunk(parsed.text);
            else onChunk(bodyText);
        } catch {
            onChunk(bodyText);
        }

        return { status: res.status, headers, body: bodyText, ms: ms(start) };
    }

    // Core Logic
    async function runOnce(overrides = {}) {
        if (running) return;
        setRunning(true);
        setOutputText("");
        setRaw({ status: "-", ms: "-" }, "");

        const req = {
            userId: overrides.userId ?? $("userId").value,
            prompt: overrides.prompt ?? $("prompt").value,
            model: overrides.model ?? $("model").value,
            params: overrides.params ? overrides.params : safeJson($("params").value)
        };

        try {
            const source = currentSource();
            const raw = source === "local"
                ? await streamLocal(req, appendOutputChunk)
                : await callMcp(req, appendOutputChunk);

            setRaw(raw, typeof raw.body === "string" ? raw.body : JSON.stringify(raw.body, null, 2));
        } catch (e) {
            setRaw({ status: 500, ms: 0 }, `Error: ${e?.message || e}`);
        } finally {
            setRunning(false);
        }
    }

    // Scenarios 
    const SC_PREFIX = "scenario:";

    function refreshScenarioList() {
        scenarioList.innerHTML = "";
        Object.keys(localStorage)
            .filter(k => k.startsWith(SC_PREFIX))
            .sort()
            .forEach(k => {
                const opt = document.createElement("option");
                opt.value = k;
                opt.textContent = k.replace(SC_PREFIX, "");
                scenarioList.appendChild(opt);
            });
    }

    $("saveScenarioBtn").addEventListener("click", () => {
        const name = $("scenarioName").value.trim();
        if (!name) return;
        const data = {
            userId: $("userId").value,
            prompt: $("prompt").value,
            model: $("model").value,
            params: $("params").value
        };
        localStorage.setItem(`${SC_PREFIX}${name}`, JSON.stringify(data));
        refreshScenarioList();
    });

    $("loadScenarioBtn").addEventListener("click", () => {
        const key = scenarioList.value || Object.keys(localStorage).find(k => k.startsWith(SC_PREFIX));
        if (!key) return;
        const s = localStorage.getItem(key);
        if (!s) return;
        const data = JSON.parse(s);
        $("userId").value = data.userId ?? "user-1";
        $("prompt").value = data.prompt ?? "";
        $("model").value = data.model ?? "";
        $("params").value = data.params ?? "{}";
    });

    $("deleteScenarioBtn").addEventListener("click", () => {
        const key = scenarioList.value;
        if (!key) return;
        localStorage.removeItem(key);
        refreshScenarioList();
    });

    // Simulator
    async function simulate() {
        if (running) return;
        const n = Math.max(1, parseInt($("userCount").value || "1", 10));
        simResult.textContent = "Running…";
        setRunning(true);

        const times = [];

        try {
            await Promise.all([...Array(n)].map(async (_, i) => {
                const t0 = performance.now();

                // Directly call logic similar to runOnce, but skip the global running flag
                const req = {
                    userId: `user-${i + 1}`,
                    prompt: $("prompt").value,
                    model: $("model").value,
                    params: safeJson($("params").value)
                };

                const source = currentSource();
                const raw = source === "local"
                    ? await streamLocal(req, () => {}) 
                    : await callMcp(req, () => {});

                times.push(ms(t0));
                return raw;
            }));

            // Sort and compute metrics
            times.sort((a, b) => a - b);
            const avg = Math.round(times.reduce((a, b) => a + b, 0) / times.length);
            const p95 = times[Math.max(0, Math.floor(times.length * 0.95) - 1)];
            simResult.textContent = `N=${n} • avg ${avg} ms • p95 ${p95} ms`;
        } catch (e) {
            simResult.textContent = `Error: ${e?.message || e}`;
        } finally {
            setRunning(false);
        }
    }

    // Event Wiring
    runBtn.addEventListener("click", runOnce);
    simBtn.addEventListener("click", simulate);
    refreshScenarioList();

   
    return { runOnce, simulate, refreshScenarioList };
})();
