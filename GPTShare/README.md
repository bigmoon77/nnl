# GPT Shared Unity Results

This folder contains automatically captured Unity test results for collaboration with ChatGPT.

Each capture creates a timestamped directory:

- `console.txt`: Unity Console messages captured during the Play Mode session.
- `preview.png`: Game View screenshot captured when Play Mode exits. When captured outside Play Mode, the active Scene View is used.
- `manifest.json`: Unity version, active scene, capture time, and capture metadata.

## Automation

Open **NNL > Result Capture Window** in Unity.

- **Auto capture on Play Stop** is enabled by default and saves a local result bundle.
- **Auto push to GitHub** is disabled by default because the `nnl` repository is public.
- **Capture & Push Now** creates a result bundle, commits it, and pushes it immediately.
- When automatic push is enabled, the tool commits and pushes only this `GPTShare/` folder to the current branch's `origin` remote.
- Other staged or unstaged project changes are not included in the capture commit.

Git for Windows must be installed and the repository's `origin` authentication must already be configured.

Console messages are redacted for common token/password patterns, but review captured logs before sharing if a test prints private data.
