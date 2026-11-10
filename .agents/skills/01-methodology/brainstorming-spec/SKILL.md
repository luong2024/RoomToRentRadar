---
name: brainstorming-spec
description: Socratic inquiry and requirements clarification protocol before generating code.
---

# Brainstorming & Specification Elicitation

Derived from `obra/superpowers/brainstorming`. The objective is to prevent developers and AI agents from making unwarranted assumptions, which lead to implementing incorrect requirements and costly rewrites.

## 1. When to Activate
- When initiating any new User Story in the Product Backlog (e.g., US01, US13, or any subsequent story).
- Whenever user requirements leave ambiguity or edge cases unspecified.

## 2. Mandatory 3-Step Procedure

### Step 1: Acceptance Criteria (AC) Review
Compare the request against the Product Backlog's ACs:
- *Example from US02:* "If the listing is unavailable, the app shows a message and suggests similar rooms." -> Probing question: What defines a "similar room" (matching price range, room type, or geographic radius)?

### Step 2: Formulate Targeted Questions (Maximum 3)
Ask only critical questions that impact architecture or core user experience:
1. When offline, should the app display cached data or an explicit network warning?
2. What are the default values when no filters are applied?
3. If GPS permissions are denied by the user, what fallback coordinates should be used (e.g., default ICTU coordinates)?

### Step 3: Produce an Agreed Specification (Mini-Spec)
Before delegating tasks to developers, output a concise summary that locks down exact type names (following project naming conventions):
- **Input:** Incoming user data, filter parameters, and request DTO names (`*Request`).
- **Contracts & Ports:** Exact `I*UseCase`, `I*Repository`, `I*Port`, `*Repository`, `*Adapter`, `*Controller`, and `*PageModel` names to be used by `@QA`, `@Core`, `@Infra`, and `@UI`.
- **Rules:** Business constraints that must be upheld (e.g., maximum of 3 rooms in side-by-side comparison).
- **Output:** Resulting response DTO (`*Dto` / `PagedResult<T>`) or UI state.
