# AGENTS.md

Project-wide rules and skill instructions for AI agents working in this repository.

## Repository Skills

Official .NET and .NET MAUI skills from `dotnet/skills` are located under `.agents/skills/`. Agents should discover and consult the relevant `SKILL.md` before and during work in the respective area:

- **`csharp-expert`** (`.agents/skills/csharp-expert/SKILL.md`): C# and .NET architecture, idioms, coding conventions, and specialist routing.
- **`maui-data-binding`** (`.agents/skills/maui-data-binding/SKILL.md`): XAML compiled bindings (`x:DataType`), `INotifyPropertyChanged`, `ObservableObject`, value converters, and MVVM binding patterns.
- **`maui-dependency-injection`** (`.agents/skills/maui-dependency-injection/SKILL.md`): Dependency injection configuration, `MauiProgram.cs` service registrations, service lifetimes (`Singleton`, `Transient`, `Scoped`), constructor injection, and DI architecture.
- **`maui-shell-navigation`** (`.agents/skills/maui-shell-navigation/SKILL.md`): .NET MAUI Shell navigation, `GoToAsync`, route registration, parameter passing, and navigation lifecycle.
- **`maui-collectionview`** (`.agents/skills/maui-collectionview/SKILL.md`): `CollectionView` layout configuration, item templates, data selection, grouping, empty views, scrolling, and pull-to-refresh.
- **`maui-app-lifecycle`** (`.agents/skills/maui-app-lifecycle/SKILL.md`): Cross-platform Window lifecycle events, platform lifecycle hooks, and state-preservation patterns.
- **`dotnet-maui-doctor`** (`.agents/skills/dotnet-maui-doctor/SKILL.md`): Environment diagnostics and verification for .NET SDK, MAUI workloads, Android SDK, and developer toolchains.

## Agent Guidelines

- Read the relevant `SKILL.md` under `.agents/skills/` before working in an area it covers.
- Only change code required by the current task. Never edit, refactor, reformat, or delete code outside the task scope.
- Never commit or push changes automatically. Always wait for human confirmation.
- Do not add dependencies or modify project structure without explicit instruction.
- When a task is complete, report changed files, verification performed, and follow-up.
