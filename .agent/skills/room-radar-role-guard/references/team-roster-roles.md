# Team Roster & Sprint 1 Pure-Role Task Matrix (RoomToRentRadar - Group 3)

## 1. Pure-Role Team Roster & Priority Batch 1 (15h Target Before Sunday)

| Member | Position / Role | Tech Stack Scope (.NET 10 LTS) | Priority Batch 1 (~14-16h before Sunday) | Full Sprint 1 Assigned Tasks (Total Hours) |
| :--- | :--- | :--- | :--- | :--- |
| **Vũ Huy Lượng** | Product Owner (PO) | Product Management | Review & accept US01, US02, US03, US04 | Acceptance of US01-US12 at Sprint Review (4h/wk) |
| **Nguyễn Trung Thành** | Scrum Master (SM) | Agile / Scrum & Git Guardrails | Init `.sln` on `Group-3`, coordinate API DTOs, track Burndown | Scrum facilitation, Burndown tracking, DoD verification (10h/wk) |
| **Nguyễn Minh Hiếu (02/11)** | Developer (Backend Lead) | ASP.NET Core MVC (`net10.0`), Cosmos DB, Push | **T01.1** (8h) + **T03.1** (6h) = **14h** *(+2h T06.1 = 16h)* | **T01.1** (8h), **T03.1** (6h), **T04.3** (4h), **T06.1** (6h), **T07.2** (8h), **T08.3** (5h), **T11.1** (4h) = **41h** |
| **Bế Văn Duy** | Developer (Backend) | ASP.NET Core MVC (`net10.0`), Cosmos DB, GeoJSON | **T02.1** (6h) + **T04.1** (8h) = **14h** *(+2h T08.2 = 16h)* | **T02.1** (6h), **T04.1** (8h), **T05.2** (8h), **T07.1** (7h), **T08.2** (8h), **T09.1** (6h), **T12.1** (4h) = **47h** |
| **Phạm Anh Dũng** | Developer (Frontend) | .NET MAUI (`net10.0-android` API 35), XAML, Appium | **T01.2** (8h) + **T05.1** (8h) = **16h** *(or T03.2 6h = 14h)* | **T01.2** (8h), **T03.2** (6h), **T05.1** (8h), **T06.2** (6h), **T09.2** (4h), **T10.2** (3h), **T12.2** (3h) = **38h** |
| **Đặng Quang Anh** | Developer (Frontend) | .NET MAUI (`net10.0-android` API 35), XAML, Appium | **T02.2** (7h) + **T04.2** (7h) = **14h** *(+2h T08.1 = 16h)* | **T02.2** (7h), **T04.2** (7h), **T05.3** (7h), **T08.1** (8h), **T10.1** (6h), **T11.2** (4h) = **39h** |

## 2. Complete 27-Task Sprint 1 Table (Pure Backend vs. Frontend Assignment)

| Task ID | User Story | Task Description | Role Layer | Assignee | Est (h) | Priority |
| :--- | :--- | :--- | :--- | :--- | :---: | :--- |
| **T01.1** | US01 (5 SP) | Build listing API with pagination and sorting | **Backend** | Nguyễn Minh Hiếu (02/11) | 8 | High |
| **T01.2** | US01 (5 SP) | Build listing screen, room cards, and infinite scroll | **Frontend** | Phạm Anh Dũng | 8 | High |
| **T02.1** | US02 (4 SP) | Build room detail API (description, amenities, rules) | **Backend** | Bế Văn Duy | 6 | High |
| **T02.2** | US02 (4 SP) | Build room detail screen and unavailable-listing handling | **Frontend** | Đặng Quang Anh | 7 | High |
| **T03.1** | US03 (3 SP) | Build photo upload pipeline and gallery API | **Backend** | Nguyễn Minh Hiếu (02/11) | 6 | Medium |
| **T03.2** | US03 (3 SP) | Build photo gallery UI and 360° viewer | **Frontend** | Phạm Anh Dũng | 6 | Medium |
| **T04.1** | US04 (6 SP) | Build search API with price, area, and distance filters | **Backend** | Bế Văn Duy | 8 | High |
| **T04.2** | US04 (6 SP) | Build filter panel, active filters, and result count | **Frontend** | Đặng Quang Anh | 7 | High |
| **T04.3** | US04 (6 SP) | Test filter combinations and search performance | **Backend/QA** | Nguyễn Minh Hiếu (02/11) | 4 | High |
| **T05.1** | US05 (6 SP) | Integrate map SDK and polygon drawing | **Frontend** | Phạm Anh Dũng | 8 | High |
| **T05.2** | US05 (6 SP) | Build spatial search API for a drawn area | **Backend** | Bế Văn Duy | 8 | High |
| **T05.3** | US05 (6 SP) | Build map pins, preview cards, and saved areas | **Frontend** | Đặng Quang Anh | 7 | High |
| **T06.1** | US06 (4 SP) | Build favourites model, API, and account sync | **Backend** | Nguyễn Minh Hiếu (02/11) | 6 | Medium |
| **T06.2** | US06 (4 SP) | Build favourite button, favourites screen, and undo | **Frontend** | Phạm Anh Dũng | 6 | Medium |
| **T07.1** | US07 (5 SP) | Build saved-search model and alert rules | **Backend** | Bế Văn Duy | 7 | High |
| **T07.2** | US07 (5 SP) | Build push-notification service and alert settings | **Backend** | Nguyễn Minh Hiếu (02/11) | 8 | High |
| **T08.1** | US08 (6 SP) | Build listing creation form and validation | **Frontend** | Đặng Quang Anh | 8 | High |
| **T08.2** | US08 (6 SP) | Build listing API and image upload | **Backend** | Bế Văn Duy | 8 | High |
| **T08.3** | US08 (6 SP) | Build moderation status, listing management, and tests | **Backend/QA** | Nguyễn Minh Hiếu (02/11) | 5 | High |
| **T09.1** | US09 (3 SP) | Build routing and travel-time service | **Backend** | Bế Văn Duy | 6 | Medium |
| **T09.2** | US09 (3 SP) | Display travel time and fallback distance | **Frontend** | Phạm Anh Dũng | 4 | Medium |
| **T10.1** | US10 (3 SP) | Build room comparison model and screen | **Frontend** | Đặng Quang Anh | 6 | Low |
| **T10.2** | US10 (3 SP) | Test comparison and share-as-image | **Frontend/QA** | Phạm Anh Dũng | 3 | Low |
| **T11.1** | US11 (3 SP) | Build house-rules model and API | **Backend** | Nguyễn Minh Hiếu (02/11) | 4 | Low |
| **T11.2** | US11 (3 SP) | Build house-rules UI with versioning | **Frontend** | Đặng Quang Anh | 4 | Low |
| **T12.1** | US12 (2 SP) | Build report API and moderation queue | **Backend** | Bế Văn Duy | 4 | Medium |
| **T12.2** | US12 (2 SP) | Build report UI and confirmation | **Frontend** | Phạm Anh Dũng | 3 | Medium |
