BDD – GEM creation/assignments

Description: Verify the GEM.Create factory trims whitespace, validates title, stores the passed GEMSource, and enforces unique category assignments even when the same Guid is added twice.
Suggested file: GemTests.cs:9-66 (extend existing class).
Expected assertions: title equals trimmed value, source URI preserved, ArgumentException when title is empty/blank, CategoryIds contains only one entry even after duplicate AssignCategory calls.
BDD – GEM title/snapshot lifecycle

Description: Ensure UpdateTitle, AttachSnapshot, and SetSummary throw when given invalid input (empty title/null snapshot/summary) and update the relevant properties when valid data is supplied.
Suggested file: GemTests.cs:9-66.
Expected assertions: ArgumentException for invalid title, GEM.Snapshot and GEM.Summary reflect provided value objects, Title equals trimmed string after UpdateTitle.
BDD – GEMSource validation

Description: Confirm GEMSource.Create rejects empty/invalid URLs and returns the normalized string when a valid absolute URL is provided.
Suggested file: GemTests.cs:45-66.
Expected assertions: ArgumentException for empty string, for malformed URL (missing scheme), and equality of Url property for valid input.
BDD – Category behavior

Description: Test that Category.Create trims the name/description, Rename/UpdateDescription reject invalid names while preserving existing ones, and AssignGem/RemoveGem manage GemIds without duplicates.
Suggested file: CategoryTests.cs:8-39.
Expected assertions: trimmed name/description, ArgumentException on blank rename, GemIds list contains single entry after duplicate assignments, removing returns empty list when existing.
BDD – CreateGemCommandValidator rules

Description: Validate that CreateGemCommandValidator accepts a well-formed command and rejects commands with invalid URLs or missing titles.
Suggested file: CreateGemCommandValidatorTests.cs:7-43.
Expected assertions: result.IsValid true for good input; contains errors for Url when malformed; contains errors for Title when empty.
BDD – Controller contract (integration scaffolding)

Description: (Unit-style) Assert that CreateGemCommand handler or controller returns expected DTO shape when dependencies, like repository or AutoMapper, are mocked.
Suggested file: [tests/InfoDumpManager.Tests.Unit/Application/Handlers/CreateGemCommandHandlerTests.cs](tests/InfoDumpManager.Tests.Unit/Application/Handlers/CreateGemCommandHandlerTests.cs) (new file adjacent to existing validator tests).
Expected assertions: handler returns GemDto with correct Title/Source, repository AddAsync invoked once, and validation errors propagated when repository throws (mocked via Moq).