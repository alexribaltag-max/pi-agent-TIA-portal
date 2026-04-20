import { spawn, ChildProcess } from "node:child_process";
import * as path from "node:path";
import * as fs from "node:fs";
import * as os from "node:os";
import * as crypto from "node:crypto";
import * as readline from "node:readline";
import * as http from "node:http";
import type { ExtensionAPI } from "@mariozechner/pi-coding-agent";
import { Type } from "@sinclair/typebox";
import {
  truncateHead,
  formatSize,
  DEFAULT_MAX_BYTES,
  DEFAULT_MAX_LINES,
} from "@mariozechner/pi-coding-agent";

export default function (pi: ExtensionAPI) {
  let bridgeProcess: ChildProcess | null = null;
  let rl: readline.Interface | null = null;
  let isStarting = false;
  
  let httpServer: http.Server | null = null;
  let statusPollTimer: NodeJS.Timeout | null = null;
  let isStatusPolling = false;
  const HTTP_PORT = 31415;

  
  // Pending promises waiting for the next response from the bridge
  const pendingRequests: Array<{
    resolve: (value: any) => void;
    reject: (reason: any) => void;
  }> = [];

  // Buffer to capture unhandled lines/events if no pending request
  const eventBuffer: any[] = [];

  let connectedProject = "Unknown";
  let portalStatus = "Disconnected";

  function updateTuiStatus(ctx: any) {
    if (httpServer && httpServer.listening) {
      ctx.ui.setStatus("tiabridge-http", ctx.ui.theme.fg("accent", `[TIA Bridge HTTP: :${HTTP_PORT} | Portal: ${portalStatus} | Project: ${connectedProject}]`));
    } else {
      ctx.ui.setStatus("tiabridge-http", ctx.ui.theme.fg("dim", `[TIA Bridge: HTTP Offline | Portal: ${portalStatus} | Project: ${connectedProject}]`));
    }
  }

  function updateProjectFromResponse(parsed: any) {
    if (parsed?.status === "success" && typeof parsed?.result === "string") {
      const resultText = parsed.result.trim();
      const match = resultText.match(/Project '([^']+)'/);
      if (match) {
        connectedProject = match[1];
        return;
      }

      if (parsed?.command === "LIST" && resultText) {
        connectedProject = resultText;
        return;
      }

      if (/no open project/i.test(resultText) || /no project open/i.test(resultText)) {
        connectedProject = "None";
      }
    }

    if (parsed?.status === "success" && typeof parsed?.command === "string" && parsed.command.startsWith("OPEN|")) {
      const projectPath = parsed.command.substring(5).trim();
      if (projectPath) {
        const baseName = path.basename(projectPath, path.extname(projectPath));
        if (baseName) {
          connectedProject = baseName;
        }
      }
    }
  }

  function formatBridgeResult(result: any): string {
    return JSON.stringify(result, null, 2);
  }

  function appendBridgeMessage(ctx: any, title: string, result: any) {
    const text = `${title}\n\n${formatBridgeResult(result)}`;
    ctx.sessionManager.appendCustomMessageEntry("tiabridge", text, true, { title, result });
  }

  async function ensureProcess(ctx: any): Promise<void> {
    if (bridgeProcess && !bridgeProcess.killed) return;
    if (isStarting) {
      // wait until started
      while (isStarting) {
        await new Promise(r => setTimeout(r, 100));
      }
      return;
    }

    isStarting = true;
    try {
      const exePath = path.join(ctx.cwd, "TiaLocalBridge", "bin", "Debug", "TiaLocalBridge.exe");
      
      bridgeProcess = spawn(exePath, [], {
        cwd: ctx.cwd,
        stdio: ["pipe", "pipe", "pipe"]
      });

      bridgeProcess.on("error", (err) => {
        if (pendingRequests.length > 0) {
          const req = pendingRequests.shift();
          req?.reject(err);
        }
      });

      bridgeProcess.on("exit", () => {
        bridgeProcess = null;
        rl = null;
        portalStatus = "Disconnected";
        updateTuiStatus(ctx);
        for (const req of pendingRequests) {
          req.reject(new Error("Bridge process exited unexpectedly"));
        }
        pendingRequests.length = 0;
      });

      rl = readline.createInterface({
        input: bridgeProcess.stdout!,
        terminal: false
      });

      rl.on("line", (line) => {
        if (!line.trim()) return;
        try {
          const parsed = JSON.parse(line);
          if (parsed.type === "event") {
            // Collect events to include in the final response
            eventBuffer.push(parsed);
            
            if (parsed.event === "FOUND_EXISTING_TIA_PORTAL") {
              portalStatus = "Connected";
              updateTuiStatus(ctx);
            }
          } else if (parsed.type === "response" || parsed.type === "fatal") {
            if (parsed.portalConnected !== undefined) {
               portalStatus = parsed.portalConnected ? "Connected" : "Disconnected";
            }
            updateProjectFromResponse(parsed);
            updateTuiStatus(ctx);

            if (pendingRequests.length > 0) {
              const req = pendingRequests.shift();
              // Include buffered events in the result
              const finalResult = { ...parsed, events: [...eventBuffer] };
              eventBuffer.length = 0; // clear buffer
              req?.resolve(finalResult);
            }
          }
        } catch (e) {
          // Unparseable line, maybe a crash trace
          if (pendingRequests.length > 0) {
             const req = pendingRequests.shift();
             req?.reject(new Error("Unparseable output from bridge: " + line));
          }
        }
      });
      
      // Wait for the bridge to be ready (it prints INITIALIZING then READY)
      // We'll give it a few seconds or consume the ready events
      await new Promise(r => setTimeout(r, 1000));
      
    } finally {
      isStarting = false;
    }
  }

  function isDisposedPortalError(value: any): boolean {
    const message = typeof value === "string"
      ? value
      : (value?.error || value?.message || "");

    return message.includes("Access to a disposed object of type 'Siemens.Engineering.TiaPortal'")
      || message.includes("TIA Portal has either been disposed or stopped running");
  }

  async function resetBridgeProcess(ctx: any, reason?: string): Promise<void> {
    const processToStop = bridgeProcess;
    const rlToClose = rl;

    bridgeProcess = null;
    rl = null;
    portalStatus = "Disconnected";
    connectedProject = "Unknown";
    eventBuffer.length = 0;
    updateTuiStatus(ctx);

    try {
      rlToClose?.close();
    } catch {
      // ignore
    }

    if (reason) {
      ctx.ui.notify(`TIA Bridge reset: ${reason}`, "warning");
    }

    if (!processToStop || processToStop.killed) {
      return;
    }

    await new Promise<void>((resolve) => {
      let finished = false;
      const finish = () => {
        if (finished) return;
        finished = true;
        resolve();
      };

      const timeout = setTimeout(() => {
        try {
          processToStop.kill();
        } catch {
          // ignore
        }
        finish();
      }, 1000);

      processToStop.once("exit", () => {
        clearTimeout(timeout);
        finish();
      });

      try {
        processToStop.stdin?.write("EXIT\n");
      } catch {
        try {
          processToStop.kill();
        } catch {
          // ignore
        }
      }
    });
  }

  async function sendCommandOnce(command: string, ctx: any): Promise<any> {
    await ensureProcess(ctx);

    return new Promise((resolve, reject) => {
      pendingRequests.push({ resolve, reject });
      bridgeProcess!.stdin!.write(command + "\n");
    });
  }

  async function sendCommand(command: string, ctx: any): Promise<any> {
    try {
      const result = await sendCommandOnce(command, ctx);
      if (isDisposedPortalError(result)) {
        await resetBridgeProcess(ctx, "TIA Portal instance was disposed. Reconnecting.");
        return await sendCommandOnce(command, ctx);
      }
      return result;
    } catch (error: any) {
      if (isDisposedPortalError(error)) {
        await resetBridgeProcess(ctx, "TIA Portal instance was disposed. Reconnecting.");
        return await sendCommandOnce(command, ctx);
      }
      throw error;
    }
  }

  async function pollBridgeStatus(ctx: any): Promise<void> {
    if (isStatusPolling || isStarting || pendingRequests.length > 0 || !bridgeProcess || bridgeProcess.killed) {
      return;
    }

    isStatusPolling = true;
    try {
      await sendCommand("LIST", ctx);
    } catch {
      // ignore background polling failures
    } finally {
      isStatusPolling = false;
    }
  }

  pi.on("session_start", async (_event, ctx) => {
    if (!httpServer) {
      httpServer = http.createServer((req, res) => {
        // Only allow localhost
        if (req.socket.remoteAddress !== "127.0.0.1" && req.socket.remoteAddress !== "::1") {
          res.writeHead(403);
          res.end();
          return;
        }

        if (req.method === "POST" && req.url === "/api/tia-action") {
          let body = "";
          req.on("data", chunk => { body += chunk.toString(); });
          req.on("end", () => {
            try {
              const payload = JSON.parse(body);
              
              const targetStr = payload.block ? `block '${payload.block}'` : (payload.target ? `'${payload.target}'` : "the selected object");
              const deviceStr = payload.device ? ` in device '${payload.device}'` : "";
              const actionStr = payload.action || "review";
              
              const prompt = `The user selected ${targetStr}${deviceStr} in TIA Portal for: ${actionStr}. Please use the tiabridge to fetch the relevant item, analyze it, and perform the requested action.`;
              
              pi.sendUserMessage(prompt, { deliverAs: "followUp" });
              
              res.writeHead(200, { "Content-Type": "application/json" });
              res.end(JSON.stringify({ status: "ok" }));
            } catch (e: any) {
              res.writeHead(400, { "Content-Type": "application/json" });
              res.end(JSON.stringify({ error: e.message }));
            }
          });
        } else {
          res.writeHead(404);
          res.end();
        }
      });
      
      httpServer.listen(HTTP_PORT, "127.0.0.1", () => {
        updateTuiStatus(ctx);
      });
      
      httpServer.on("error", (e: any) => {
        if (e.code === "EADDRINUSE") {
          ctx.ui.notify(`TIA Bridge: Port ${HTTP_PORT} in use, Add-in triggers disabled.`, "warning");
        } else {
          ctx.ui.notify(`TIA Bridge HTTP Error: ${e.message}`, "error");
        }
      });
    } else if (httpServer.listening) {
      updateTuiStatus(ctx);
    }

    void (async () => {
      try {
        await ensureProcess(ctx);
        await pollBridgeStatus(ctx);
      } catch {
        // ignore startup refresh failures
      }
    })();

    if (!statusPollTimer) {
      statusPollTimer = setInterval(() => {
        void pollBridgeStatus(ctx);
      }, 10000);
    }
  });

  pi.on("session_shutdown", async () => {
    if (statusPollTimer) {
      clearInterval(statusPollTimer);
      statusPollTimer = null;
    }

    if (bridgeProcess && !bridgeProcess.killed) {
      bridgeProcess.stdin?.write("EXIT\n");
      // Wait a moment for graceful exit, then kill
      setTimeout(() => {
        if (bridgeProcess && !bridgeProcess.killed) {
          bridgeProcess.kill();
        }
      }, 500);
    }
    
    if (httpServer) {
      httpServer.close();
      httpServer = null;
    }
  });

  pi.registerCommand("tiaportal", {
    description: "Start or check the TIA Portal Bridge process.",
    handler: async (_args, ctx) => {
      ctx.ui.notify("Starting TIA Portal Bridge...", "info");
      try {
        await ensureProcess(ctx);
        const result = await sendCommand("LIST", ctx);
        appendBridgeMessage(ctx, "TIA Portal status", result);
        ctx.ui.notify("TIA Portal Bridge is running and status was refreshed.", "success");
      } catch (err: any) {
        ctx.ui.notify(`Failed to start bridge: ${err.message}`, "error");
      }
    }
  });

  pi.registerCommand("tiastatus", {
    description: "Query the current TIA Portal / project status.",
    handler: async (_args, ctx) => {
      try {
        const result = await sendCommand("LIST", ctx);
        appendBridgeMessage(ctx, "TIA Portal status", result);
        ctx.ui.notify("TIA Portal status fetched.", "success");
      } catch (err: any) {
        ctx.ui.notify(`Failed to fetch TIA status: ${err.message}`, "error");
      }
    }
  });

  pi.registerCommand("tiaopen", {
    description: "Open a TIA Portal project via the bridge. Usage: /tiaopen C:\\Path\\Project.ap20",
    handler: async (args, ctx) => {
      const projectPath = args.trim();
      if (!projectPath) {
        ctx.ui.notify("Usage: /tiaopen C:\\Path\\Project.apXX", "warning");
        return;
      }

      try {
        const result = await sendCommand(`OPEN|${projectPath}`, ctx);
        appendBridgeMessage(ctx, `TIA open project: ${projectPath}`, result);
        ctx.ui.notify("TIA project open command sent.", "success");
      } catch (err: any) {
        ctx.ui.notify(`Failed to open TIA project: ${err.message}`, "error");
      }
    }
  });

  pi.registerCommand("tiacmd", {
    description: "Send a raw TiaLocalBridge command directly. Usage: /tiacmd GETDEVICES|ProjectName",
    handler: async (args, ctx) => {
      const command = args.trim();
      if (!command) {
        ctx.ui.notify("Usage: /tiacmd COMMAND|arg1|arg2", "warning");
        return;
      }

      try {
        const result = await sendCommand(command, ctx);
        appendBridgeMessage(ctx, `TIA command: ${command}`, result);
        ctx.ui.notify("TIA bridge command executed.", "success");
      } catch (err: any) {
        ctx.ui.notify(`TIA bridge command failed: ${err.message}`, "error");
      }
    }
  });

  pi.registerTool({
    name: "tiabridge",
    label: "TIA Portal Bridge",
    description: "Send commands to TIA Portal via the TiaLocalBridge.",
    promptSnippet: "Automate TIA portal via bridge. Provide exactly the command string (e.g. GETDEVICES|PackagingMachine).",
    parameters: Type.Object({
      command: Type.String({ description: "Pipe-separated command string (e.g. GETDEVICES|PackagingMachine)" }),
    }),
    async execute(toolCallId, params, signal, onUpdate, ctx) {
      try {
        const result = await sendCommand(params.command, ctx);
        
        let outputText = JSON.stringify(result, null, 2);
        
        const truncation = truncateHead(outputText, {
          maxLines: DEFAULT_MAX_LINES,
          maxBytes: DEFAULT_MAX_BYTES,
        });

        let finalContent = truncation.content;

        if (truncation.truncated) {
          const tmpName = `tiabridge_${crypto.randomBytes(4).toString("hex")}.json`;
          const tmpPath = path.join(os.tmpdir(), tmpName);
          fs.writeFileSync(tmpPath, outputText, "utf8");

          finalContent += `\n\n[Output truncated: ${truncation.outputLines} of ${truncation.totalLines} lines`;
          finalContent += ` (${formatSize(truncation.outputBytes)} of ${formatSize(truncation.totalBytes)}).`;
          finalContent += ` Full output saved to: ${tmpPath}]`;
        }
        
        return {
          content: [{ type: "text", text: finalContent }],
          details: { result },
        };
      } catch (error: any) {
        throw new Error(`Bridge error: ${error.message}`);
      }
    },
  });
}
