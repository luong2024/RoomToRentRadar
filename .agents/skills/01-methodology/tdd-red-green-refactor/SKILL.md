---
name: tdd-red-green-refactor
description: Mandatory Test-Driven Development cycle enforcing failing tests before production code.
---

# Test-Driven Development (TDD) Protocol

Derived from `obra/superpowers/test-driven-development`. Guarantees automated test coverage for all features and eliminates superfluous code.

## 1. Mandatory 3-Phase Cycle

```mermaid
flowchart LR
    Red["Phase 1: RED<br/>(Write Failing Test)"] --> Green["Phase 2: GREEN<br/>(Minimal Code to Pass)"]
    Green --> Refactor["Phase 3: REFACTOR<br/>(Clean Code, Tests Stay Green)"]
    Refactor --> Red
```

### Phase 1: RED (Write a Failing Test)
- QA Agent authors the test case within `RoomToRentRadarUnitTests`.
- Execute:
  ```powershell
  dotnet test RoomToRentRadarUnitTests/RoomToRentRadarUnitTests.csproj
  ```
- **Prerequisite:** The test **MUST FAIL** due to missing feature behavior, NOT due to compilation errors.

### Phase 2: GREEN (Make the Test Pass)
- The developer agent (@Core or @Infra) writes the **minimal** production code required to satisfy the test.
- No speculative coding beyond what is verified by the test case.
- Re-run test command -> Must output `Passed: 100%`.

### Phase 3: REFACTOR (Safely Improve Code)
- Clean up implementation, adopt idiomatic .NET 10 / C# conventions, eliminate duplication (DRY).
- Re-run the full test suite to guarantee zero regressions.

## 2. Test Framework & Core Mocking Rules
- **Standard Test Framework:** **NUnit 4** (`[TestFixture]`, `[SetUp]`, `[Test]`, `[TestCase]`, and constraint-based assertions via `Assert.That(...)`). Do **NOT** use xUnit (`[Fact]`, `[Theory]`) or MSTest.
- **Standard Mocking Library:** **NSubstitute** (`Substitute.For<IPort>()`). Do **NOT** mix `Moq` and `NSubstitute` in the same test suite.
- Never connect to live databases or live remote networks within Unit Tests; always mock Output Ports (`IRoomApiClient`, `ILocalCachePort`, `IMapRadarPort`, `IUserSessionPort`).
