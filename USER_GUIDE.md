# texAi — user guide

Select text anywhere on Windows, press a hotkey, get it rewritten in place. No app window ever opens.

## The whole idea, in one picture

```mermaid
flowchart LR
    A[Select text\nin any app] --> B[Press a hotkey]
    B --> C[texAi copies it\nand asks your local Ollama model]
    C --> D[Result is pasted\nback over the selection]
```

Nothing is sent anywhere except your own computer. There is no cloud, no account, no popup.

## Step 1 — Install Ollama

texAi needs a local model server called Ollama to actually do the rewriting.

```mermaid
flowchart TD
    A[Download Ollama] --> B[Install it\ndouble-click the installer]
    B --> C[It starts automatically\nand keeps running quietly]
```

1. Go to [ollama.com/download](https://ollama.com/download) and download the Windows installer.
2. Run it. Ollama installs itself and starts running in the background — you won't see a window.

## Step 2 — Get the model

Open a terminal (PowerShell or Command Prompt) and run:

```powershell
ollama pull qwen2.5:7b
```

This downloads the language model itself (about 4.7 GB), one time only. Wait for it to finish.

## Step 3 — Get texAi

1. Go to the [Releases page](../../releases) of this repo.
2. Download `texAi.exe` from the latest release.
3. Put it anywhere you like, for example `Documents\texAi\texAi.exe`.

No installer, no setup wizard. It's a single file.

## Step 4 — Run it

Double-click `texAi.exe`.

Nothing will appear on screen. That's expected — texAi has no window by design. It's now sitting quietly in the background, listening for hotkeys.

> To check it's actually running: open Task Manager → Details tab → look for `texAi.exe`.

## Step 5 — Use it

```mermaid
flowchart TD
    Start([You're typing in any app]) --> Select[Highlight some text]
    Select --> Key{Which hotkey?}
    Key -->|Ctrl + Shift + G| Grammar[Fix grammar]
    Key -->|Ctrl + Shift + T| Translate[Translate to English]
    Key -->|Ctrl + Shift + R| Rewrite[Rewrite / improve wording]
    Key -->|Ctrl + Shift + F| Tone[Make it more professional]
    Grammar --> Done[Selection is replaced\nwith the result]
    Translate --> Done
    Rewrite --> Done
    Tone --> Done
```

### The four hotkeys

| Press | What happens | Example in | Example out |
|---|---|---|---|
| `Ctrl + Shift + G` | Fixes grammar and spelling | `i has completed this project yesterday` | `I completed this project yesterday.` |
| `Ctrl + Shift + T` | Translates to English | `আমি আগামীকাল অফিসে যাব` | `I will go to the office tomorrow.` |
| `Ctrl + Shift + R` | Rewrites for clarity and flow | `the meeting was very much productive` | `The meeting was very productive.` |
| `Ctrl + Shift + F` | Makes the tone professional | `hey can u send me that file asap` | `Could you please send me the file as soon as possible?` |

It works the same way in Notepad, Chrome, Word, VS Code, Slack, email — anywhere you can select text and use Ctrl+C.

### What it looks like in practice

```mermaid
sequenceDiagram
    participant You
    participant App as Any text app
    participant texAi
    participant Ollama as Local Ollama

    You->>App: Type a sentence
    You->>App: Select it
    You->>texAi: Press Ctrl+Shift+G
    texAi->>App: Copies the selection
    texAi->>Ollama: Sends it with a short instruction
    Ollama->>texAi: Sends back the corrected text
    texAi->>App: Pastes the result over the selection
```

## If nothing happens

```mermaid
flowchart TD
    A[Pressed a hotkey,\nnothing changed] --> B{Is Ollama running?}
    B -->|Not sure| C[Open a terminal, run:\nollama list]
    C --> D{Did it list qwen2.5:7b?}
    D -->|No| E[Run: ollama pull qwen2.5:7b]
    D -->|Yes| F{Is texAi.exe running?\nCheck Task Manager}
    F -->|No| G[Double-click texAi.exe again]
    F -->|Yes| H{Did you actually\nselect text first?}
    H -->|No| I[Select the text,\nthen press the hotkey]
    H -->|Yes| J[Try again — the first request\nafter starting Ollama can take\n30-60s while the model loads]
```

texAi fails silently on purpose: if Ollama isn't running, the model is missing, or the request times out, your text is left exactly as it was. Nothing is ever deleted or replaced with garbage or an empty result.

## Stopping it

texAi has no exit menu on purpose. To stop it: Task Manager → Details tab → find `texAi.exe` → End task.

## Changing the model or tone

Both are constants in the source, not settings screens — see [Config.cs](Config.cs) if you're building from source. Default model is `qwen2.5:7b`, default tone is `Professional`.

## Privacy

texAi never writes your text to disk, never logs it, and never contacts anything except `127.0.0.1:11434` — your own machine. See [README.md](README.md) for the technical details.
