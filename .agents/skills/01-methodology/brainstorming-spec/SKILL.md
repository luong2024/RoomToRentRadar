---
name: brainstorming-spec
description: Socratic inquiry and requirements clarification protocol before generating code.
---

# Brainstorming & Specification Elicitation

Derived from `obra/superpowers/brainstorming`. The objective is to prevent developers and AI agents from making unwarranted assumptions, which lead to implementing incorrect requirements and costly rewrites.

## 1. When to Activate
- When initiating any new User Story in Sprint 1 (US01 through US12).
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
Before delegating tasks to developers, output a concise summary:
- **Input:** Incoming user data and filter parameters.
- **Rules:** Business constraints that must be upheld (e.g., maximum of 3 rooms in side-by-side comparison).
- **Output:** Resulting data structure or UI response.
