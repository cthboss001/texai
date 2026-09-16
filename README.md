# texAi

A background Windows utility. Select text anywhere, press a hotkey, the
selection is rewritten in place by a local Ollama model. No window, no
tray icon, no settings screen.

Just want to install and use it? See the [user guide](USER_GUIDE.md).

## Why

Cloud rewriting tools mean sending whatever you highlighted to someone
else's server. This runs entirely against a local Ollama instance
(`127.0.0.1:11434`) and never touches the network otherwise, so it works
offline and nothing you select ever leaves the machine.

## Hotkeys

| Hotkey             | Action              |
|---------------------|---------------------|
| Ctrl+Shift+G        | Fix grammar         |
| Ctrl+Shift+T        | Translate to English|
| Ctrl+Shift+R        | Rewrite             |
| Ctrl+Shift+F        | Change tone         |

Select text in any app, press the hotkey, the selection is replaced with
the model's output a moment later. If Ollama isn't running, the model
doesn't exist, the request times out, or the response is empty, nothing
happens to your text.

## Install

Requires [Ollama](https://ollama.com) running locally with the model
pulled:

```powershell
ollama pull qwen2.5:7b
```

The model name, endpoint, and default tone are constants in
[Config.cs](Config.cs) if you want to change them.

## Build

```powershell
dotnet build -c Release
```

## Run

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
.\bin\Release\net8.0-windows\win-x64\publish\texAi.exe
```

It exits immediately if another instance is already running. To stop it
during development, kill it from Task Manager.

## How it works

```text
Global hotkey
  -> save current clipboard
  -> Ctrl+C (synthetic, via SendInput)
  -> read selection from clipboard
  -> POST /api/generate to Ollama
  -> Ctrl+V the result back in
  -> restore original clipboard
```

Only one transformation runs at a time; a hotkey pressed while one is in
flight is dropped. Nothing processed is ever written to disk or logged.
