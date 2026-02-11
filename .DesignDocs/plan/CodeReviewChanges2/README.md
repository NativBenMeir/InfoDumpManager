# Code Review Remediation Plan — Opus46 Review (Feb 10, 2026)

This plan addresses all **Not-Fixed / Partially-Fixed** items from the Opus46 architecture review.
It is split into **6 phases**, ordered by dependency (later phases may depend on earlier ones).

Each phase is small enough to implement and verify independently.

---

## Phase Index

| Phase | Title | Files Changed | Estimated Effort |
|-------|-------|--------------|-----------------|
| 1 | [Validation Deduplication](phase-1-validation-deduplication.md) | ~6 files | Small |
| 2 | [ActivityLog Demotion from AggregateRoot](phase-2-activitylog-demotion.md) | ~5 files | Small |
| 3 | [Orchestrator Decomposition](phase-3-orchestrator-decomposition.md) | ~8 new/changed files | Large |
| 4 | [Agent Repository Removal (SummarizationAgent)](phase-4-agent-repository-removal.md) | ~2 files | Small |
| 5 | [Durable Job Queue (Redis-backed)](phase-5-durable-job-queue.md) | ~4 files | Medium |
| 6 | [Polly Centralization & Semantic Kernel Config](phase-6-polly-and-semantic-kernel.md) | ~5 files | Medium |

After all phases, run:
```bash
dotnet build
dotnet test
```
