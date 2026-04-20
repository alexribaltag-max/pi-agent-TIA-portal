import { spawn, ChildProcess } from "node:child_process";
import * as path from "node:path";
import * as fs from "node:fs";
import * as os from "node:os";
import * as crypto from "node:crypto";
import * as readline from "node:readline";
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
  
  // Pending promises waiting for the next response from the bridge
  const pendingRequests: Array<{
    resolve: (value: any) => void;
    reject: (reason: any) => void;
  }> = [];

  // Buffer to capture unhandled lines/events if no pending request
  const eventBuffer: any[] = [];

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
          } else if (parsed.type === "response" || parsed.type === "fatal") {
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

  async function sendCommand(command: string, ctx: any): Promise<any> {
    await ensureProcess(ctx);
    
    return new Promise((resolve, reject) => {
      pendingRequests.push({ resolve, reject });
      bridgeProcess!.stdin!.write(command + "\n");
    });
  }

  pi.on("session_shutdown", async () => {
    if (bridgeProcess && !bridgeProcess.killed) {
      bridgeProcess.stdin?.write("EXIT\n");
      // Wait a moment for graceful exit, then kill
      setTimeout(() => {
        if (bridgeProcess && !bridgeProcess.killed) {
          bridgeProcess.kill();
        }
      }, 500);
    }
  });

  pi.registerCommand("tiaportal", {
    description: "Start or check the TIA Portal Bridge process.",
    handler: async (args, ctx) => {
      ctx.ui.notify("Starting TIA Portal Bridge...", "info");
      try {
        await ensureProcess(ctx);
        ctx.ui.notify("TIA Portal Bridge is running and ready to accept tool calls.", "success");
      } catch (err: any) {
        ctx.ui.notify(`Failed to start bridge: ${err.message}`, "error");
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
