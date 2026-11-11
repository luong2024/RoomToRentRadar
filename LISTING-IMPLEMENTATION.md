# Room listing screen

The application now contains only the room discovery flow. The unrelated
Projects/Tasks template, SQLite seed data, Syncfusion controls and placeholder
tests have been removed. Build outputs and Visual Studio caches are ignored.
The warm white palette uses a restrained sage accent and always opens in light
mode, independently of the system theme. The listing grid uses
one, two or three columns according to window width. Room and campus SVG
illustrations are bundled locally and clearly marked as illustrative.

The initial Shell destination is `rooms`. Select a campus to sort available,
verified rooms by great-circle distance, with ID as a stable tie breaker.
Each page contains 20 rooms; scrolling within five items of the end loads more.
Pull to refresh resets pagination. Errors preserve loaded rooms and retry the
failed offset. Campus changes cancel and invalidate older requests.

## Data

The app currently uses 500 deterministic demo listings and two demo campuses,
clearly labelled in the UI. Replace the `IRoomListingService` DI registration
with an API implementation when a backend is available. The API must filter
Available AND Verified and sort by distance then ID before pagination.
Coordinates and campus selections must come from the real data source.

## Validation

Run `dotnet test RoomToRentRadarUnitTests/RoomToRentRadarUnitTests.csproj`.
Tests compile the platform-independent models, service and page model directly
to avoid requiring MAUI workloads in the unit test runner.

AC3 is interpreted as time to the first usable page with a source of up to
500 listings. The automated timing test covers service processing only.
Before accepting AC3, measure a Release build on the target device from page
entry/campus change until the first cards are visible and scrollable, including
network time when an API is connected. Target <= 3 seconds. Record device,
network conditions, and cold/warm launch measurements; no device UI timing has
been established by the service test.
