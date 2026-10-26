# Local Model Setup

ForgeCore never downloads a model. Obtain a GGUF model whose licence and source you trust, then configure its absolute local path.

## Suggested starting point for 16 GB RAM and an RTX 3070

- Instruction-tuned 7B or 8B model
- `Q4_K_M` quantization
- 4096-token context
- 33 GPU layers as the initial setting
- Current Vulkan-capable NVIDIA graphics driver

The development machine reported an RTX 3070 with 8192 MiB VRAM and NVIDIA driver 610.62. ForgeCore uses LLamaSharp's Vulkan backend so no separate CUDA Toolkit installation is required. Actual model loading still depends on the GGUF architecture and memory requirements.

## Installed development model

- Model: official `Qwen/Qwen3-8B-GGUF`, `Q4_K_M`
- Local path: `C:\Users\jacob\Documents\Codex\ForgeCoreModels\Qwen3-8B-Q4_K_M.gguf`
- File size: 5,027,783,488 bytes
- SHA-256: `d98cdcbd03e17ce47681435b5150e34c1417f50b5c0019dd560e4882c5745785`
- Checksum status: matches the publisher value

The end-to-end development check successfully loaded this file through `ggml-vulkan.dll`, offloaded the configured layers to the RTX 3070, generated a grammar-constrained ForgeCore response, and persisted a completed task. Peak observed GPU allocation was approximately 6,001 MiB.

A typical 7B/8B `Q4_K_M` file is small enough for this class of machine, while leaving some memory for Windows and ForgeCore. Exact memory use varies by architecture and context size. If loading fails with an out-of-memory error, reduce `GpuLayerCount` to 24, then 16; use `0` for CPU-only diagnosis. Reduce `ContextSize` to 2048 if system RAM remains tight.

## Configuration

Edit `src\ForgeCore.Desktop\appsettings.json` for development, or the copied `appsettings.json` beside a built executable:

```json
"LocalModel": {
  "Provider": "LLamaSharp",
  "ModelPath": "D:\\Models\\your-instruct-model.Q4_K_M.gguf",
  "EnableLlamaSharp": true,
  "ContextSize": 4096,
  "GpuLayerCount": 33,
  "MaxTokens": 1024,
  "MaxRequestCharacters": 16000,
  "MaxResponseCharacters": 32000,
  "MaxPlanActions": 12,
  "MaxHistoryMessages": 12,
  "MaxHistoryCharacters": 12000,
  "EnableJsonGrammar": true
}
```

Restart ForgeCore after changing model settings. The model is not loaded at application startup; the first request triggers loading. The status bar reports readiness, successful loading, or a load error.

## Safety and troubleshooting

- Do not run ForgeCore as administrator.
- Keep `EnableJsonGrammar` enabled. The security layer still validates every parsed call.
- A Vulkan/backend error usually indicates an outdated graphics driver or incompatible model. Update the driver, confirm the model is current GGUF, and retry with fewer GPU layers.
- A malformed-plan error means the model could not satisfy the strict ForgeCore schema. Try a stronger instruction-tuned model; do not weaken parsing or security checks.
- No API key, cloud account, or model downloader is required.
