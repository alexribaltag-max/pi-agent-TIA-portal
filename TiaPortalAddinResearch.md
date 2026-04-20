# Research: Integrating TIA Portal Add-ins with Pi Agent

## Objective
To research and design an architecture where a **TIA Portal Add-in** can send contextual commands and selections directly to the **Pi Coding Agent**. This allows a user working within the TIA Portal UI to select objects (like a PLC block, tag table, or device), right-click, and trigger an AI action (e.g., "Review Code", "Generate Tags", "Explain Block") which is immediately picked up and executed by the Pi Agent in the terminal.

---

## 1. TIA Portal Add-in Capabilities
TIA Portal Add-ins are .NET assemblies (DLLs) built against the `Siemens.Engineering.AddIn` API. They are loaded directly into the TIA Portal framework and can interact with the TIA UI.

**Key capabilities relevant to this integration:**
- **Context Menus:** Add-ins can inject custom menu items into the context menus (right-click menus) of the TIA Portal project navigation tree, devices, and networks view.
- **Context Awareness:** When a user clicks an Add-in menu item, the Add-in receives a collection of the currently selected objects (e.g., `PlcBlock`, `PlcTagTable`, `DeviceItem`).
- **Network Access:** Because Add-ins run in a standard .NET environment, they can use `HttpClient` to make network requests to local or external servers.
- **Permissions:** Add-ins run with the permissions of the TIA Portal instance and can be configured to have file system and network access.

---

## 2. Proposed Architecture

Since the Pi Agent runs as a separate Node.js process (often in a CLI terminal), and TIA Portal runs its own desktop UI, they need a communication channel. The best approach is an **HTTP Server running inside a Pi Extension**.

### Architecture Components:

1. **Extending the Existing `tiabridge.ts` Extension**
   - Instead of creating a new extension (`tia-server.ts`), we will extend the existing `C:\Users\masias\Documents\TiaPiAgent\.pi\extensions\tiabridge.ts`.
   - The extension starts a lightweight HTTP server (e.g., using Node's built-in `http` module or `express`) listening on a local port (e.g., `localhost:31415`).
   - **Important Rule:** The Add-in part on the TIA Portal side is strictly **optional**. The `tiabridge.ts` extension and its tools must work perfectly with or without the Add-in installed in TIA Portal.
   - It exposes an endpoint like `POST /api/tia-action`.
   - When a payload is received, the extension uses `pi.sendUserMessage(prompt)` to inject a message into the active Pi session, forcing the LLM to start a turn with the provided context.

2. **TIA Portal Add-in (C#, Optional)**
   - Implements the `ContextMenuAddIn` class.
   - Registers menu items such as "Ask Pi to Explain", "Ask Pi to Refactor", "Send Context to Pi".
   - When clicked, it extracts the TIA metadata:
     - Project Name
     - Device Reference Name
     - Object Type and Path (e.g., `02_Global/Data`)
   - Sends a JSON payload via HTTP POST to the Pi Agent's server.

3. **TiaLocalBridge (Existing)**
   - The Pi Agent continues to use the existing `tiabridge` tool to actually execute read/write operations (like exporting the block XML or injecting new code). The Add-in just acts as a **trigger** mechanism.

4. **TUI (Terminal User Interface) Integration**
   - We will add real-time status information to the Pi TUI, similar to how the token count is displayed.
   - The custom UI component will show:
     - **TIA Bridge Status** (e.g., HTTP server listening, bridge connected)
     - **Actual Project** (The currently active project in TIA Portal)
     - **TIA Portal Instance** (Information about the running TIA instance)

### Flow of Data
1. **User Action:** User right-clicks `Fb_MotorControl` in TIA Portal and selects "Pi: Refactor Block".
2. **Add-in Execution:** The Add-in extracts the block path and device name.
3. **HTTP POST:** Add-in sends `{ action: "refactor", device: "Station_1", block: "Fb_MotorControl" }` to `localhost:31415`.
4. **Pi Extension Receives:** The `tiabridge.ts` extension intercepts the POST request.
5. **Prompt Injection:** Extension calls `pi.sendUserMessage("The user selected block 'Fb_MotorControl' in device 'Station_1' for refactoring. Please use the tiabridge to fetch the block, analyze it, and propose a refactor.")`.
6. **Agent Execution:** The Pi Agent starts generating, calls the `tiabridge` tool to export the block, reads it, and responds to the user.

---

## 3. Implementation Plan (Phased)

### Phase 1: Extend `tiabridge.ts` & TUI Integration
- Modify the existing `.pi/extensions/tiabridge.ts` to spin up an HTTP server on startup.
- Ensure the HTTP server and Add-in dependency remain **optional** so the extension operates standalone.
- Implement a custom TUI element to display: `TIA Bridge Status`, `Actual Project`, and `TIA Portal Instance`.
- Define a generic JSON schema for incoming TIA actions.
- Map incoming actions to `pi.sendUserMessage()`.

### Phase 2: Basic TIA Portal Add-in
- Create a new .NET Class Library project targeting the required .NET Framework (e.g., 4.8) for the TIA version.
- Reference `Siemens.Engineering.AddIn.dll`.
- Create a `ContextMenuProvider` for `PlcBlock` and `PlcTagTable`.
- Implement a basic `HttpClient` POST request to the Pi extension.
- Build and package as a `.addin` file, then deploy to TIA Portal's `AddIns` folder.

### Phase 3: Advanced Context & Feedback
- Instead of just sending paths, the Add-in could potentially export the block (to a temp directory) and send the file path to Pi to save the agent a round-trip step.
- Add desktop notifications from the Pi extension so the user knows the agent has received the command and is working on it.

---

## 4. Technical Details & APIs

### Pi Agent Extension API Usage
```typescript
pi.on("session_start", async (_event, ctx) => {
  // Start HTTP server here
});

// Inside HTTP request handler:
const prompt = `Action requested from TIA Portal: ${payload.action}\nDevice: ${payload.device}\nTarget: ${payload.target}`;

// Inject message into the current session and trigger the LLM
pi.sendUserMessage(prompt);
```

### TIA Portal Add-in API Usage (C# Pseudo-code)
```csharp
public class PiIntegrationAddIn : ContextMenuAddIn
{
    protected override void BuildContextMenuItems(ContextMenuAddInRoot addInRootSubmenu)
    {
        addInRootSubmenu.Items.AddActionItem<PlcBlock>("Review with Pi Agent", OnReviewBlockClick);
    }

    private async void OnReviewBlockClick(MenuSelectionProvider<PlcBlock> menuSelectionProvider)
    {
        foreach (var block in menuSelectionProvider.GetSelection())
        {
            var payload = new {
                action = "review",
                blockName = block.Name,
                // ... logic to walk up the tree to get the device name
            };
            
            using (var client = new HttpClient())
            {
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                await client.PostAsync("http://localhost:31415/api/tia", content);
            }
        }
    }
}
```

## 5. Security & Practical Considerations
1. **Port Conflicts:** Ensure the port used by the Pi extension is configurable in case of conflicts.
2. **CORS / Local-Only Binding:** The HTTP server should ideally bind to `127.0.0.1` to prevent external network triggers.
3. **TIA Portal Lockups:** Add-in code runs on the UI thread. Network requests `PostAsync` should be properly awaited without blocking the UI thread to prevent TIA Portal from freezing.
4. **Synchronization:** The Pi agent might be busy streaming a response when the Add-in sends a request. `pi.sendUserMessage` supports options like `{ deliverAs: "followUp" }` which queues the message until the agent is idle. This is highly recommended to avoid crashing the current conversation turn.