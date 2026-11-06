---
name: maui-current-apis
description: Prevents usage of obsolete, deprecated, or removed Xamarin.Forms APIs in .NET 10 MAUI.
---

# .NET MAUI Current APIs Guardrail

Derived from `davidortinau/maui-skills/maui-current-apis`. Prevents AI models from conflating legacy Xamarin.Forms code with modern .NET 10 MAUI patterns, avoiding non-compilable output.

## 1. API Mapping Table: Obsolete (BANNED) vs Modern (.NET 10 MAUI)

| OBSOLETE (STRICTLY BANNED) | MODERN REPLACEMENT (.NET 10 MAUI) |
| :--- | :--- |
| `MessagingCenter.Subscribe / Send` | `WeakReferenceMessenger.Default.Register / Send` |
| `RelativeLayout` | `Grid` or `AbsoluteLayout` |
| `MasterDetailPage` | `AppShell` (FlyoutItem or TabBar) |
| `Device.BeginInvokeOnMainThread(...)` | `MainThread.BeginInvokeOnMainThread(...)` |
| `Device.OpenUri(...)` | `Launcher.Default.OpenAsync(...)` |
| `DependencyService.Get<T>()` | Constructor Dependency Injection registered in `MauiProgram.cs` |
| `Xamarin.Essentials.*` | `Microsoft.Maui.Devices.*`, `Microsoft.Maui.Storage.*` |
| Boilerplate manual `INotifyPropertyChanged` | `[ObservableProperty]` from `CommunityToolkit.Mvvm` |

## 2. Code-Behind Rules
- `*.xaml.cs` files must contain only view-centric logic (e.g., triggering custom animations, focusing inputs).
- Never embed API calls, database access, or state machines into view code-behind.
