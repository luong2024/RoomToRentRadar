# Sprint 1 Task Matrix & 4-Sprint Roadmap (RoomToRentRadar)

## 1. 4-Sprint Release Plan (200 Story Points Total, Velocity = 50 SP/Sprint)

| Sprint | Weeks | Target Increment & Core Features | Planned SP | Remaining SP |
| :--- | :--- | :--- | :---: | :---: |
| **Sprint 1** | Week 3 - 4 (23/11 - 04/12/2026) | **Increment 1**: Verified rental listings, room details, photo/360° gallery, price/area/distance filter search, map polygon search, favourites, saved search alerts, landlord publishing, travel time to campus, room comparison, house rules, listing reports (US01-US12) | 50 | 150 |
| **Sprint 2** | Week 5 - 6 | **Increment 2**: Nearby amenities (markets, gyms, eateries, bus stops), area security information & safety scores, enhanced routing & travel time | 50 | 100 |
| **Sprint 3** | Week 7 - 8 | **Increment 3**: Roommate sharing & matching profile, online deposit payment (sandbox gateway integration) | 50 | 50 |
| **Sprint 4** | Week 9 - 10 | **Increment 4**: Rideshare matching, odd-job marketplace for students, unified notifications, system-wide integration | 50 | 0 |
| **Release / UAT** | Week 11 - 12 | Integration testing, bug fixing, optimization, pilot rollout (3 faculties, 50 users, 100 verified listings), handover to ICTU | - | 0 |

## 2. Sprint 1 Detailed Task Assignment (165 Hours Estimate / 27 Tasks)

| Task ID | User Story | Task Description | Layer | Assignee | Est (h) | Priority | Dates |
| :--- | :--- | :--- | :--- | :--- | :---: | :--- | :--- |
| **T01.1** | US01 (5 SP) | Build listing API with pagination and sorting | Backend API | Nguyễn Minh Hiếu | 8 | High | 23/11 |
| **T01.2** | US01 (5 SP) | Build listing screen, room cards, and infinite scroll | MAUI Mobile | Bế Văn Duy | 8 | High | 23/11 - 24/11 |
| **T02.1** | US02 (4 SP) | Build room detail API (description, amenities, rules) | Backend API | Phạm Anh Dũng | 6 | High | 23/11 |
| **T02.2** | US02 (4 SP) | Build room detail screen and unavailable-listing handling | MAUI Mobile | Đặng Quang Anh | 7 | High | 26/11 - 30/11 |
| **T03.1** | US03 (3 SP) | Build photo upload pipeline and gallery API | Backend API | Nguyễn Minh Hiếu | 6 | Medium | 23/11 |
| **T03.2** | US03 (3 SP) | Build photo gallery UI and 360° viewer | MAUI Mobile | Bế Văn Duy | 6 | Medium | 26/11 - 30/11 |
| **T04.1** | US04 (6 SP) | Build search API with price, area, and distance filters | Backend API | Phạm Anh Dũng | 8 | High | 24/11 |
| **T04.2** | US04 (6 SP) | Build filter panel, active filters, and result count | MAUI Mobile | Đặng Quang Anh | 7 | High | 25/11 - 26/11 |
| **T04.3** | US04 (6 SP) | Test filter combinations and search performance | QA / Integration | Nguyễn Minh Hiếu | 4 | High | 27/11 - 01/12 |
| **T05.1** | US05 (6 SP) | Integrate map SDK and polygon drawing | MAUI Mobile | Bế Văn Duy | 8 | High | 24/11 |
| **T05.2** | US05 (6 SP) | Build spatial search API for a drawn area | Backend API | Phạm Anh Dũng | 8 | High | 26/11 - 01/12 |
| **T05.3** | US05 (6 SP) | Build map pins, preview cards, and saved areas | MAUI Mobile | Đặng Quang Anh | 7 | High | 30/11 - 01/12 |
| **T06.1** | US06 (4 SP) | Build favourites model, API, and account sync | Backend API | Nguyễn Minh Hiếu | 6 | Medium | 25/11 |
| **T06.2** | US06 (4 SP) | Build favourite button, favourites screen, and undo | MAUI Mobile | Bế Văn Duy | 6 | Medium | 27/11 - 30/11 |
| **T07.1** | US07 (5 SP) | Build saved-search model and alert rules | Backend API | Phạm Anh Dũng | 7 | High | 25/11 |
| **T07.2** | US07 (5 SP) | Build push-notification service and alert settings | Full-Stack / Push | Đặng Quang Anh | 8 | High | 30/11 - 01/12 |
| **T08.1** | US08 (6 SP) | Build listing creation form and validation | MAUI Mobile | Nguyễn Minh Hiếu | 8 | High | 25/11 |
| **T08.2** | US08 (6 SP) | Build listing API and image upload | Backend API | Bế Văn Duy | 8 | High | 26/11 |
| **T08.3** | US08 (6 SP) | Build moderation status, listing management, and tests | Full-Stack / QA | Phạm Anh Dũng | 5 | High | 27/11 - 30/11 |
| **T09.1** | US09 (3 SP) | Build routing and travel-time service | Backend Service | Đặng Quang Anh | 6 | Medium | 26/11 |
| **T09.2** | US09 (3 SP) | Display travel time and fallback distance | MAUI Mobile | Nguyễn Minh Hiếu | 4 | Medium | 30/11 |
| **T10.1** | US10 (3 SP) | Build room comparison model and screen | MAUI Mobile | Bế Văn Duy | 6 | Low | 27/11 |
| **T10.2** | US10 (3 SP) | Test comparison and share-as-image | QA / Mobile | Phạm Anh Dũng | 3 | Low | 01/12 |
| **T11.1** | US11 (3 SP) | Build house-rules model and API | Backend API | Đặng Quang Anh | 4 | Low | 01/12 |
| **T11.2** | US11 (3 SP) | Build house-rules UI with versioning | MAUI Mobile | Nguyễn Minh Hiếu | 4 | Low | 02/12 |
| **T12.1** | US12 (2 SP) | Build report API and moderation queue | Backend API | Bế Văn Duy | 4 | Medium | 02/12 |
| **T12.2** | US12 (2 SP) | Build report UI and confirmation | MAUI Mobile | Phạm Anh Dũng | 3 | Medium | 03/12 |

## 3. Workload Summary per Developer in Sprint 1

- **Nguyễn Minh Hiếu (40h)**: T01.1 (8h), T03.1 (6h), T04.3 (4h), T06.1 (6h), T08.1 (8h), T09.2 (4h), T11.2 (4h)
- **Bế Văn Duy (46h)**: T01.2 (8h), T03.2 (6h), T05.1 (8h), T06.2 (6h), T08.2 (8h), T10.1 (6h), T12.1 (4h)
- **Phạm Anh Dũng (40h)**: T02.1 (6h), T04.1 (8h), T05.2 (8h), T07.1 (7h), T08.3 (5h), T10.2 (3h), T12.2 (3h)
- **Đặng Quang Anh (39h)**: T02.2 (7h), T04.2 (7h), T05.3 (7h), T07.2 (8h), T09.1 (6h), T11.1 (4h)
