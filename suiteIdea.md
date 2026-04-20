# Pi Agent Suite: Ideas & Architecture

This document captures the architecture and ideas for expanding the local Pi Agent into a multi-purpose, TIA-aware automation suite.

---

## 1. TIA Portal Add-in Integration & HTTP Bridge

Instead of creating a standalone `tia-server.ts` extension, we will augment our existing local bridge integration.

*   **Extending `tiabridge.ts`:** The existing extension will spin up a lightweight HTTP server (e.g., `localhost:31415`) upon session start.
*   **Optional TIA Add-in:** The C# TIA Portal Add-in is strictly an optional trigger mechanism. It registers context menus (e.g., right-click "Ask Pi to Refactor") and sends JSON payloads containing the project, device, and object path to the local HTTP server.
*   **Action Injection:** When the HTTP server receives a payload, it calls `pi.sendUserMessage()` to automatically inject a prompt into the active Pi session and trigger the LLM.
*   **TUI Dashboard:** The Pi Terminal UI (TUI) will be enhanced to display custom footer elements showing:
    *   **TIA Bridge Status** (Connected / Listening)
    *   **Actual Project**
    *   **TIA Portal Instance**

---

## 2. Extension Loading and Tool Toggling

Pi automatically evaluates and loads any `.ts` file placed in the auto-discovery folder (`.pi/extensions/`). To manage tools effectively without overwhelming the LLM context:

*   **Automatic Loading, Manual Activation:** Extensions load immediately, but their tools do not have to be active by default.
*   **Command Toggles:** By using `pi.registerCommand()`, we can create commands (like `/enable-tools`) that call `pi.setActiveTools(...)` to inject specialized tools into the LLM's active context only when explicitly requested by the user.

---

## 3. Persona / Agent Manager Architecture

To support widely different domains (e.g., pure software coding vs. TIA Portal automation vs. hardware schematics) within the same environment, we will implement a multi-agent "Persona Manager".

### Core Capabilities:
1.  **Command-Driven TUI Menu:** A custom `/agent` command opens a native TUI selection menu allowing the user to pick their current persona.
2.  **Dynamic Toolsets:** Switching an agent dynamically alters the active tools (e.g., granting `tiabridge` access only to the TIA Portal persona).
3.  **System Prompt Overrides:** Using the `before_agent_start` event, the extension intercepts the chat loop and completely replaces the default system prompt (`agent.md`) with a highly specialized prompt for the active persona.
4.  **Session Persistence:** The active persona choice is saved to the chat session history using `pi.appendEntry("active_persona", ...)`. If Pi is closed and reopened, the last used agent automatically loads.
5.  **Status Indication:** The TUI status bar continuously displays the currently active agent.

### Reference Implementation snippet (`persona-manager.ts`):

```typescript
import type { ExtensionAPI } from "@mariozechner/pi-coding-agent";

const PERSONAS = {
  normal: {
    name: "Normal Pi Agent",
    tools: ["read", "bash", "edit", "write"],
    systemPrompt: "You are an expert coding assistant..."
  },
  tiaportal: {
    name: "TIA Portal Expert",
    tools: ["read", "bash", "edit", "write", "tiabridge"],
    systemPrompt: "You are a Siemens TIA Portal automation expert..."
  },
  hardware: {
    name: "Hardware Expert",
    tools: ["read", "bash", "edit"], 
    systemPrompt: "You are an embedded hardware expert..."
  }
};

export default function (pi: ExtensionAPI) {
  let activePersona = "normal";

  pi.on("session_start", async (_event, ctx) => {
    const entries = ctx.sessionManager.getEntries();
    const saved = [...entries].reverse().find(e => e.type === "custom" && e.customType === "active_persona");
    if (saved?.data?.persona) activePersona = saved.data.persona as string;
    
    pi.setActiveTools(PERSONAS[activePersona].tools);
    ctx.ui.setStatus("persona", `Agent: ${PERSONAS[activePersona].name}`);
  });

  pi.registerCommand("agent", {
    description: "Switch active Persona",
    handler: async (args, ctx) => {
      const items = Object.entries(PERSONAS).map(([k, p]) => ({ value: k, label: p.name }));
      const selected = await ctx.ui.select("Select Agent Persona", items, activePersona);

      if (selected) {
        activePersona = selected;
        pi.setActiveTools(PERSONAS[activePersona].tools);
        pi.appendEntry("active_persona", { persona: activePersona });
        ctx.ui.setStatus("persona", `Agent: ${PERSONAS[activePersona].name}`);
      }
    },
  });

  pi.on("before_agent_start", async () => {
    if (activePersona === "normal") return {}; 
    return { systemPrompt: PERSONAS[activePersona].systemPrompt };
  });
}
```
