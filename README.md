# Pi TIA Agent: a Pi Agent extension

<img width="800" height="450" alt="gigh_submodule-ezgif com-optimize" src="https://github.com/user-attachments/assets/57b1f4b9-fb1e-4349-be7f-0e96cc29de04" />


**The ultimate bridge between AI and industrial automation.**

This project transforms your TIA Portal engineering experience by extending the **Pi Coding Agent** into the world of Siemens automation. It allows an AI agent to "see," "interact," and "modify" your TIA Portal projects, PLC code, HMI screens, and hardware configurations in real time using the Siemens TIA Portal Openness API.

![Pi TIA Agent overview](images/image.png)

## 🎥 Demo Videos

See the repo in action:

- [Repository walkthrough and usage demo](https://youtu.be/bEAtLDp9gfo?si=p9nHuzOftbYUNBI6)
- [TIA Portal agent usage demonstration](https://youtu.be/8Cj0zDgfZiM?si=ZTuvSMCU1jwlry5o)

---

## 🤖 What is the Pi Coding Agent?

The [Pi Coding Agent](https://shittycodingagent.ai/) is an advanced AI-powered developer harness designed to operate directly within your workspace. Unlike static chat interfaces, Pi can:

- **Read and Write Files**: It understands your entire codebase context.
- **Execute Commands**: It runs builds, scripts, and automation tools.
- **Self-Correct**: It observes the results of its actions and iterates until the task is complete.
- **YOLO Mode**: By default, the agent operates with a "get things done" philosophy, executing complex workflows with speed and autonomy.

Pi can be extended with other useful skills and extensions for related tasks. Check: https://pi.dev/packages

We have extended this philosophy to TIA Portal, creating a seamless link between high-level AI reasoning and low-level industrial engineering.
Check it out: https://github.com/badlogic/pi-mono/tree/main/packages/coding-agent

---

## 🌟 Key Features

- **🤖 AI-Driven PLC Programming**: Ask the agent to generate SCL blocks, refactor logic, or explain complex networks.
- **🔌 Seamless UI Integration**: Right-click any block or tag table directly in TIA Portal to trigger AI reviews, explanations, or custom prompts via the **Pi Agent Add-in**.
- **🛠️ Automated Hardware Management**: Programmatically discover, add, and configure PLC modules, ET200 stations, and drives.
- **📊 HMI & Tag Synchronization**: Automatically manage HMI tags, connections, and screen items.
- **⚡ Smart Import/Export**: Intelligent handling of block formats (XML vs. Document) based on language and protection status.
- **🧪 Testing & Validation**: Create disposable test blocks, compile them, and verify logic without manual clicks.

<img width="800" height="450" alt="hihghmi-ezgif com-optimize" src="https://github.com/user-attachments/assets/31582776-afda-4c33-8ffd-dd5a9160a002" />


---

## 🧠 Agent Intelligence (.pi folder)

This repository is "agent-ready" out of the box. The `.pi` folder contains the configuration that turns a standard Pi Coding Agent into a TIA Portal expert.

Drop the `.pi` folder into your working project and run `pi`.

- **Extensions (`.pi/extensions`)**: Includes `tiabridge.ts`, which registers the `tiabridge` tool. This allows the agent to send pipe-separated commands directly to `TiaLocalBridge.exe`.
- **Skills (`.pi/skills`)**: The `tia-portal-agent` skill provides the agent with structured workflows for discovery, export, import, and testing. It also loads task-specific project context such as programming guidelines, tag naming rules, and hardware/address policies.
- **Prompts (`.pi/prompts`)**: Specialized prompt files (for example `tia-plc-blocks.md`) that teach the agent how to interpret TIA Openness XML and document formats, as well as how to handle device references.

When you start the Pi Coding Agent in this directory, it automatically loads these capabilities, making it aware of your TIA environment without any additional setup.

### Customize Engineering Standards

One of the most useful customization points in this repo is the `tia-portal-agent` skill context. You can teach the agent how **your company**, **your machine platform**, or **your plant** works by editing three Markdown files:

- `.pi/skills/tia-portal-agent/references/programming-guidelines.md`
- `.pi/skills/tia-portal-agent/references/tag-naming-guidelines.md`
- `.pi/skills/tia-portal-agent/references/hardware-address-guidelines.md`

Folder layout:

```text
.pi/
└── skills/
    └── tia-portal-agent/
        ├── SKILL.md
        └── references/
            ├── programming-guidelines.md
            ├── tag-naming-guidelines.md
            └── hardware-address-guidelines.md
```

The skill loads them depending on the task:

- **Programming guidelines**: loaded for most PLC engineering tasks
- **Tag naming guidelines**: loaded for PLC/HMI tag work and naming reviews
- **Hardware/address guidelines**: loaded for hardware configuration, IO addresses, networks, and drive telegrams

This lets you keep the global system prompt small while still injecting the right project standards at the right time.

#### 1. Programming guidelines

Use this file to define how the agent should write and review PLC logic.

Typical things to customize:
- preferred block naming (`FbMotorControl`, `FC_MotorControl`, etc.)
- whether new logic should prefer **SCL**, **LAD**, or preserve the existing block language
- interface design rules
- alarm handling rules
- state-machine style
- code comment style

Short example:

```md
## Company standards
- New reusable logic shall be implemented as SCL FB blocks.
- Public FB interfaces must remain stable unless explicitly approved.
- Alarm handling must be separated from actuator command logic.
- Sequence logic shall use an explicit state variable and CASE structure.
```

#### 2. Tag naming guidelines

Use this file to define how PLC tags, HMI tags, DB members, and interface signals should be named.

Typical things to customize:
- prefixes or suffixes
- machine area naming
- approved abbreviations
- HMI-only tag naming
- alarm, command, feedback, and status naming
- tag table organization

Short example:

```md
## Plant tag naming standard
- PLC tags shall follow `<Area>_<Unit>_<Signal><Suffix>`.
- Commands use `Cmd`, feedbacks use `Fb`, statuses use `Sts`, alarms use `Alm`.
- HMI internal tags must start with `Hmi_`.
- Global machine tags belong in table `01_Global`.
```

#### 3. Hardware and address guidelines

Use this file to define how the agent should work with racks, ET200 stations, module slots, IO ranges, subnet naming, and drive telegram addressing.

Typical things to customize:
- reserved IO ranges by station
- address alignment rules
- slot/module ordering conventions
- PROFINET subnet naming
- IP addressing scheme
- drive telegram numbering/address allocation rules

Short example:

```md
## Hardware allocation standard
- Main PLC local IO starts at `%I0.0` / `%Q0.0`.
- Remote ET200 stations are allocated in 32-byte input/output blocks.
- Station `Line01_ET200_01` uses inputs starting at 128 and outputs starting at 128.
- PROFINET subnets shall be named `PN_<Line>_<Area>`.
- Main drive telegrams must be grouped in contiguous address ranges.
```

#### Recommended customization workflow

1. Copy this repo or `.pi` folder into your working project.
2. Edit the three guideline files with your real engineering standards.
3. Keep examples short and explicit.
4. When possible, describe both the **preferred pattern** and the **things to avoid**.
5. If your existing project already has a different convention, say whether the agent should **preserve legacy naming** or **migrate toward the new standard**.

#### Why this is better than putting everything in the system prompt

- easier to maintain
- less clutter in the always-loaded prompt
- more relevant context per task
- safer for mixed workflows such as blocks vs tags vs hardware
- easier to version with the project

---

The system follows the Pi Coding Agent's extension model and consists of two main components that connect TIA Portal to Pi:

### 1. [TiaLocalBridge](./TiaLocalBridge)

A high-performance C# command bridge that uses the **Siemens Openness API**. It acts as the "hands" of the AI agent.

The complete source code is available. By default, the TIA agent will try to work through the bridge. If needed, the bridge can be extended with additional functionality that is not yet implemented.

### 2. [TiaPiAddin](./TiaPiAddin)

A native TIA Portal add-in that provides a direct "hotline" from the engineering UI to the Pi Agent.

---

## 🚀 Getting Started

### Model support

TIA Agent has been tested with frontier models such as OpenAI 5.4 and Gemini 5.1 Pro, and it should also work with other top-tier models.

### Prerequisites

- **TIA Portal V20** (currently tested version)
- **Siemens Openness** installed and configured
- **.NET Framework 4.8** (for the bridge and add-in)
- **Pi Coding Agent** installed
- `npm`
- **Python** for the agent to use for various tasks like document manipulation

NOTE: Install Pi Coding Agent, clone the repo, and ask it to guide you during installation.

### Installation

1. **Build the Bridge**: Compile the `TiaLocalBridge` solution.
2. **Install the Add-in**: Copy the `.addin` file from `TiaPiAddin/bin/Debug/net48/` to your TIA Portal `AddIns` folder and activate it.
3. **Start the Agent**: Run the Pi Coding Agent in your workspace. It will automatically detect the extension and wait for commands or Add-in triggers.

### TIA Openness Configuration

To allow the bridge to interact with TIA Portal, you must ensure Openness is configured correctly:

- **Enable Openness**: Ensure "TIA Portal Openness" was selected during the TIA Portal installation (under Options).
- **User Group Membership**: You must add your Windows user account to the local **"Siemens TIA Openness"** user group.
  1. Open **Computer Management** (`compmgmt.msc`).
  2. Navigate to **Local Users and Groups** > **Groups**.
  3. Double-click the **Siemens TIA Openness** group.
  4. Add your current user account to the list.
  5. **Log off and log back on** for the changes to take effect.
- **Firewall/Permissions**: The first time the bridge runs, TIA Portal may show a security prompt. You should select "Yes to all" to allow the connection.

---

## ⚠️ Disclaimer & Status

- **Experimental**: This project is currently in **active development and testing** and is a proof of concept of what can be achieved using coding agents and LLMs to develop custom software.
- **Open Source**: We believe in open collaboration for the future of industrial automation.
- **YOLO Default**: Following the Pi Coding Agent philosophy, this tool defaults to high-autonomy operation not only in TIA Portal but also on your system as a whole, so please use a VM when possible and don't use "dumb" models:

  ![alt text](yolo.png)

- **Safety First**: **NEVER** use this agent directly on a live production PLC. Always validate AI-generated code in a simulated environment (PLCSIM) and perform thorough manual reviews before deployment.
- **Mostly AI-generated code**: A large portion of this repository was generated with AI and Pi Coding Agent under human supervision.

---

## 📂 Repository Structure

- `TiaLocalBridge/`: The core Openness automation engine.
- `TiaPiAddin/`: The TIA Portal UI extension.
- `examples/`: Practical tutorials and example workflows.
- `TiaLocalBridge/scripts/`: Useful PowerShell utilities for testing and inspection.

---

## 🙌 Credits

Built for the **Awesome Pi Coding Agent**. Elevate your automation engineering to the next level.
https://github.com/badlogic/pi-mono/tree/main/packages/coding-agent
