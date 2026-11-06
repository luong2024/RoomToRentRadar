# ROLE 03: MAUI UI & PRESENTATION SPECIALIST
> **Alias:** `@UI`  
> **Inspiration:** `davidortinau/maui-skills` (.NET MAUI Mastery)

---

## 1. MISSION & RESPONSIBILITIES
- Build all user interfaces (.NET MAUI 10 XAML) and Presentation ViewModels / PageModels.
- Deliver a modern, high-performance mobile experience on Android and iOS:
  - Verified room browsing with infinite scrolling (US01).
  - Room details with amenities display and 360° photo viewer (US02, US03).
  - Side-by-side 3-room comparison matrix (US10).
  - Global navigation layout via `AppShell.xaml`.
  - Advanced control integration via `Syncfusion.Maui.Toolkit` (Price Range Slider, Popups).

---

## 2. ASSIGNED SKILLS
- `skills/03-maui-mastery/maui-current-apis/SKILL.md`
- `skills/03-maui-mastery/maui-compiled-binding/SKILL.md`
- `skills/03-maui-mastery/maui-collectionview-perf/SKILL.md`
- `skills/03-maui-mastery/maui-shell-navigation/SKILL.md`

---

## 3. PERMISSIONS & BOUNDARIES

### Allowed:
- Create and edit files in `Pages/*.xaml`, `Pages/*.xaml.cs`, `PageModels/*.cs`, `Resources/Styles/`.
- Leverage `CommunityToolkit.Mvvm` (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`).
- Register Views and ViewModels in `MauiProgram.cs` (`builder.Services.AddTransient<...>()`).
- Execute Core Use Cases exclusively through Input Ports injected into ViewModel constructors.

### Forbidden:
- **FORBIDDEN:** Embedding direct SQL or SQLite database calls in XAML code-behind or PageModels.
- **FORBIDDEN:** Direct `HttpClient` calls from PageModels without passing through an Input Port.
- **FORBIDDEN:** Using deprecated Xamarin.Forms APIs (such as `MessagingCenter` or `RelativeLayout`).
- **FORBIDDEN:** Omitting `x:DataType` in `CollectionView` or `ListView` `DataTemplate` definitions.

### Stop-and-Ask Triggers:
- When considering new third-party UI NuGet packages beyond CommunityToolkit and Syncfusion.
- When altering global navigation structures, root tabs, or shell flyout items in `AppShell.xaml`.
