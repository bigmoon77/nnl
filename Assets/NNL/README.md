# NNL - First Shader Prototype

This prototype targets the VRChat PC avatar environment using Unity 2022.3.22f1 and the Built-in Render Pipeline.

## Included

- `Shaders/NNL_UnlitMLP.shader`: a fragment-shader MLP with a 4-8-3 topology.
- `Editor/NNLTestAvatarBuilder.cs`: generates a primitive humanoid test avatar, its weight texture/material, and a test scene.
- `Generated/NNL_TestWeights.asset`: generated deterministic example weights (created by the editor tool).

## Build the test scene

1. Open the project with the configured Unity editor.
2. Wait for the shader and editor script to finish importing.
3. Select **NNL > Build Test Avatar** from the menu.
4. Open `Assets/NNL/Scenes/NNL_TestAvatar.unity` if it is not opened automatically.

The generated avatar is a collection of primitive meshes. Each body part uses the same NNL material.

## MLP prototype

The network takes four values:

- World-space normal X, Y, Z.
- Signed dot product between the world normal and the main directional-light direction.

The network uses eight ReLU hidden units and three output units (RGB). The current example weights are hand-authored to produce a simple normal/light-dependent color response. They are a data-layout and inference smoke test, not trained weights.

### Weight texture layout

The editor tool creates a linear, point-filtered RGBAFloat texture of size 3 x 11.

| Rows | X | Contents |
| --- | --- | --- |
| 0-7 | 0 | Four input weights for each hidden neuron |
| 0-7 | 1.r | Hidden bias |
| 8-10 | 0 | Output weights for hidden units 0-3 |
| 8-10 | 1 | Output weights for hidden units 4-7 |
| 8-10 | 2.r | Output bias |

The shader samples texel centers with explicit LOD 0. The texture uses point filtering and clamp addressing.

## Current limitations

- The network is a fixed-size prototype; topology is currently 4-8-3.
- The sample weights are not trained.
- The shader uses the main directional light as its lighting input.
- There is no low-resolution inference, upscaling, temporal reuse, or frame interpolation yet.
- The generated humanoid is a shader test fixture, not a production avatar or a humanoid-rigged VRChat avatar.

## Next steps

1. Verify shader compilation and visual output in Unity.
2. Add an external weight exporter/training format.
3. Benchmark fragment cost at multiple avatar screen sizes.
4. Investigate feasible temporal reuse and upscaling within VRChat avatar shader constraints.
5. Document the shader data contract for a future engine port.

## GPT result sharing

The Editor tool at `Editor/NNLResultCapture.cs` automatically captures Console output and a preview image when Play Mode stops. It writes timestamped bundles to the repository-root `GPTShare/` folder.

Open **NNL > Result Capture Window** to configure capture and push behavior. Automatic local capture is enabled by default; automatic GitHub push is disabled by default because this repository is public. **Capture & Push Now** explicitly commits and pushes the shared folder. See `../../GPTShare/README.md` for details.
