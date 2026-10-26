# Meshy Setup

ForgeCore v0.13 supports supervised Meshy Text-to-3D generation. It never downloads a model or creates an account automatically. ForgeCore launches normally when Meshy is disabled or no key is installed.

## Account and key

1. Confirm that the Meshy account has API access and sufficient credits.
2. Create a dedicated API key at `https://www.meshy.ai/settings/api`.
3. Store it as the current Windows user's `MESHY_API_KEY` environment variable. Do not paste the value into ForgeCore chat, configuration, screenshots, source control, or documentation.
4. Close and restart ForgeCore so the process receives the updated user environment.

The production workstation configuration enables Meshy and selects it as the preferred deterministic 3D provider. The distributable example configuration keeps Meshy disabled.

## Supervised workflow

For an exact named request such as `Generate an original realistic 3D watchtower as a self-contained GLB. Name it frontier-watchtower.`, ForgeCore proposes one typed `Generate3DAsset` action. The approval window shows the full outbound prompt, Meshy 6 model, project, cloud boundary, and credit warning. Meshy briefs are limited to 300 characters so ForgeCore's fixed safety prefix remains within Meshy's 600-character provider limit.

After approval ForgeCore:

1. submits a fixed Meshy 6 preview task;
2. waits for that exact task with cancellation and a hard deadline;
3. submits a PBR refine linked to the preview task, requesting 2K textures and GLB output;
4. downloads only the refined passive GLB;
5. removes signed URL parameters from durable provenance;
6. hashes and structurally inspects the file in `<project>/.forgecore/staging`;
7. asks separately before importing the exact inspected hash into Unity.

Preview and refinement consume Meshy credits even if a later stage fails or the resulting art is rejected. Do not approve a request unless that cost is acceptable. Generated geometry, textures, rights, scale, collision, performance, and visual quality still require human review.

## Troubleshooting

- `MESHY 3D KEY NEEDED`: verify the user environment variable name and restart ForgeCore.
- Denied as disabled: set `EnableMeshyGeneration` to `true` only in the private runtime configuration.
- API plan error: API access may require an eligible Meshy plan in addition to credits.
- Generation timeout: ForgeCore stops polling after the configured limit; the remote task may still need review in the Meshy account.
- No quarantined GLB: inspect ForgeCore's visible task result. It does not silently import or retry a credit-consuming request.
