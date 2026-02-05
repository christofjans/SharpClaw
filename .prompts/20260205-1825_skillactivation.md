# Plan

## Summary
Add an LLM-driven skill selection step in `ChatClient` that chooses at most one skill based on the prompt and available skill metadata, activates it (if any), then generates the response.

## Workplan
- [ ] Add a small model-only selector (JSON schema) that, given the user prompt and available skills, returns a single skill name or `null`/"none".
- [ ] Integrate selection into `PromptAsync`, `PromptAsync<T>`, and `StreamPromptAsync` so activation happens before adding the user message to `chatHistory`.
- [ ] Ensure selection is skipped when skills are unavailable or the model returns no skill; keep error handling explicit.
- [ ] Run `dotnet build` to validate changes.

## Notes
- Use a dedicated system prompt for the selector that includes the skills metadata list (via `SkillCatalog.BuildMetadataPromptSection`).
- Keep selection independent from keyword heuristics; rely on the model to decide relevance.
