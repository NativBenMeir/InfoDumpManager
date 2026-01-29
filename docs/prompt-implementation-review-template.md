# Implementation Completeness Review & Test Coverage Analysis

**Objective:** Review the `{DOCUMENT_NAME}` implementation plan against the actual codebase to verify completeness, identify gaps, and assess test coverage.

**Input Parameters:**
- `{DOCUMENT_NAME}`: Path to the planning document (e.g., "Phase1-Completion-Checklist.md", "implementation-plan-1_phase1.md")

---

## Review Process

### 1. Implementation Completeness Check
- Parse the planning document to extract all deliverable items, features, and requirements
- For each item, search the codebase to verify implementation
- Categorize findings as:
  - ✅ **Fully Implemented**: Code exists and matches requirements
  - ⚠️ **Partially Implemented**: Code exists but missing features/requirements
  - ❌ **Not Implemented**: No corresponding code found
- For partial/missing implementations, specify:
  - What exists (with file references and line numbers)
  - What's missing or incomplete
  - Dependencies or blockers (if apparent)

### 2. Code Quality & Patterns Review
- Verify adherence to architectural patterns mentioned in the plan
- Check for proper error handling, validation, and edge cases
- Note any deviations from planned design patterns

### 3. Test Coverage Analysis

**Identify Required Tests from Plan:**
- List all test types mentioned (unit, integration, E2E)
- Note specific test scenarios called out in the plan

**Analyze Existing Tests:**
- Review all test files in the workspace
- Map tests to implementation items
- Identify coverage gaps

**Recommend Additional Tests (Beyond Original Plan):**
- Domain entity validation tests
- Value object behavior tests
- Repository/persistence tests
- API endpoint tests
- Error handling/negative scenarios
- Edge cases and boundary conditions
- Concurrency/race condition tests
- Security/authorization tests

### 4. Report Structure

Generate `implementation-review-YYYY-MM-DD-HHmm.md` with:

```markdown
# Implementation Review Report
**Document Reviewed:** {DOCUMENT_NAME}  
**Review Date:** {ISO_TIMESTAMP}  
**Reviewer:** GitHub Copilot

---

## Executive Summary
- Total Items in Plan: X
- Fully Implemented: X (X%)
- Partially Implemented: X (X%)
- Not Implemented: X (X%)
- Test Coverage: X%

---

## Detailed Findings

### ✅ Fully Implemented Items
| Item | Description | Implementation | Files |
|------|-------------|----------------|-------|
| ... | ... | ... | [file.cs](path/file.cs#LX) |

### ⚠️ Partially Implemented Items
| Item | What Exists | What's Missing | Files |
|------|-------------|----------------|-------|
| ... | ... | ... | [file.cs](path/file.cs#LX) |

### ❌ Not Implemented Items
| Item | Description | Reason/Notes |
|------|-------------|--------------|
| ... | ... | ... |

---

## Test Coverage Analysis

### Existing Tests
| Test File | Test Count | Coverage Area | Status |
|-----------|------------|---------------|--------|
| ... | ... | ... | ✅/⚠️ |

### Test Gaps (From Plan)
- [ ] Missing test description
- [ ] Another missing test

### Recommended Additional Tests
*Tests not in original plan but recommended for robustness:*

#### High Priority
- [ ] Test name - Rationale
- [ ] Test name - Rationale

#### Medium Priority
- [ ] Test name - Rationale

#### Low Priority (Nice to Have)
- [ ] Test name - Rationale

---

## Recommendations
1. Priority actions
2. Next steps
3. Technical debt items

---

## Appendix
- Configuration files reviewed
- Dependencies analyzed
- Notes and observations
```

### 5. User Confirmation for New Tests

After generating the report, present:

```
📋 **Recommended Additional Tests Found:**

I've identified X additional tests that would improve coverage beyond the original plan:

**High Priority (X tests):**
1. Test name - Why it's important
2. ...

**Medium Priority (X tests):**
1. Test name - Why it's useful
2. ...

Would you like me to:
A) Implement all recommended tests
B) Implement only high priority tests
C) Let me choose specific tests to implement
D) Skip new tests for now
```

---

## Quality Guidelines

- Be specific with file paths and line numbers
- Use proper markdown linking for all code references
- Provide context for why items are incomplete
- Be objective and factual in assessments
- Include code snippets where helpful (< 10 lines)
- Flag breaking changes or risks if found

---

## Usage Instructions

1. Replace `{DOCUMENT_NAME}` with the actual document path
2. Review will automatically generate timestamped report
3. All file references will be clickable links
4. Test recommendations will be categorized by priority
5. User will be prompted to confirm new test implementations

---

## Example Usage

```
Review the Phase1-Completion-Checklist.md implementation plan against the actual codebase 
to verify completeness, identify gaps, and assess test coverage.
```

This will:
- Parse Phase1-Completion-Checklist.md
- Search codebase for all mentioned items
- Generate detailed implementation-review-2026-01-29-1430.md
- Present test recommendations for user confirmation
- Optionally implement approved tests
