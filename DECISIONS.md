# Architectural Decisions

This document explains the key architectural decisions made for Task 3 (Backend Refactoring) and Task 6 (Frontend Improvements) in the Leave Management System.

## Task 3: Backend Refactoring - Separation of Concerns

### Decision 1: Repository Pattern for Data Access Layer

**What:** Implemented `ILeaveRequestRepository` interface and `LeaveRequestRepository` concrete class to abstract all database interactions.

**Why:** 
- Decouples business logic from data access implementation
- Enables dependency injection for testability - tests can mock the repository
- Centralizes database queries in one place for maintainability
- Makes it easy to switch database providers (e.g., from SQLite to PostgreSQL) without changing service code

**Implementation Details:**
- All data access is async (`Task<T>` methods) for scalability
- Methods include: `GetByIdAsync()`, `GetAllAsync()`, `GetUsedVacationDaysAsync()`, `AddAsync()`, `SaveAsync()`
- Uses `Include()` for eager loading to prevent N+1 queries
- Queries sorted by `StartDate` descending for consistent ordering

---

### Decision 2: Service Layer for Business Logic

**What:** Created `ILeaveRequestService` interface and `LeaveRequestService` class to orchestrate business logic and coordinate between repository and validators.

**Why:**
- Keeps business rules (quota validation, status transitions, date calculations) separate from HTTP concerns
- Enables service reuse - could be called from different endpoints (REST, gRPC, background jobs)
- Improves testability - services can be unit tested without HTTP/database infrastructure
- Makes business logic explicit and easier to understand

**Implementation Details:**
- `CreateAsync()`: Validates vacation quota, calculates days, delegates to repository
- `ApproveAsync()`: Validates status is "Pending", re-checks quota before approval, handles concurrency
- `GetAllAsync()`: Returns all leave requests, delegating to repository
- All methods are async (`Task<T>`) for scalability

---

### Decision 3: Validator Pattern for Reusable Validation

**What:** Created `VacationQuotaValidator` class with `ValidateVacationQuotaAsync()` method to encapsulate quota validation logic.

**Why:**
- Keeps validation logic separate and reusable - can be called from multiple places
- Single responsibility principle - one class for one validation concern
- Testable in isolation - can unit test quota calculation without service layer
- Makes error messages consistent across all code paths

**Implementation Details:**
- Calculates: `usedDays + requestedDays ≤ annualQuota`
- Returns tuple: `(isValid, errorMessage, usedDays, remainingQuota)`
- Only validates Vacation requests - Sick leave and Unpaid bypass this check
- Used by both `CreateAsync()` and `ApproveAsync()` in service

---

### Decision 4: Minimal HTTP Responsibility in Controller

**What:** `LeaveRequestsController` now only handles HTTP concerns: routing, request/response mapping, status code selection.

**Why:**
- Controller thickness is a common anti-pattern - business logic should not live in controllers
- Makes the controller a thin "adapter" layer - easy to understand and maintain
- Separates concerns clearly: HTTP handling (controller) vs. business logic (service)
- Improves testability - services are tested independently of HTTP

**Implementation Details:**
- Controller maps DTOs to domain models
- Catches exceptions from service and converts to appropriate HTTP status codes:
  - `InvalidOperationException` → 400 Bad Request (business logic violations)
  - `KeyNotFoundException` → 404 Not Found (resource doesn't exist)
  - Quota/Status exceptions → 400 Bad Request
  - Concurrency exceptions → 409 Conflict
- All validation logic is delegated to service

---

### Decision 5: Concurrency Handling with DbUpdateConcurrencyException

**What:** Added try-catch for `DbUpdateConcurrencyException` in `ApproveAsync()` to handle race conditions.

**Why:**
- Leave requests can be modified concurrently by different users/processes
- Without concurrency control, "lost update" problem can occur
- Entity Framework's optimistic concurrency (via RowVersion) detects these conflicts
- Client receives clear 409 Conflict response to know the request was stale

**Implementation Details:**
- Added `RowVersion` property to `LeaveRequest` model for optimistic concurrency
- Migration: `20261021181131_AddRowVersionToLeaveRequest`
- When concurrency conflict detected: Convert `DbUpdateConcurrencyException` to `InvalidOperationException` with descriptive message
- Response includes error message explaining the modification conflict

---

### Decision 6: Dependency Injection Setup

**What:** Registered Repository, Service, and Validator in `Program.cs` for loose coupling.

**Why:**
- Services don't create their own dependencies - dependencies are injected
- Enables easy testing with mock implementations
- Makes dependencies explicit - clear what each class needs
- Follows dependency inversion principle

**Implementation:**
```csharp
services.AddScoped<ILeaveRequestRepository, LeaveRequestRepository>();
services.AddScoped<ILeaveRequestService, LeaveRequestService>();
services.AddScoped<VacationQuotaValidator>();
```

---

### Decision 7: Async/Await Throughout

**What:** All methods use `async/await` pattern with `Task<T>` return types.

**Why:**
- Scalability - async I/O doesn't block thread pool threads
- Modern .NET best practice - recommended over Task.Result or .Wait()
- Consistent throughout codebase - easier to understand and maintain
- Enables proper error handling with async stack traces

---

## Task 6: Frontend Improvements - Angular Component Architecture

### Decision 1: Service Layer for HTTP Abstraction

**What:** Created `LeaveRequestService` with methods `getAll()`, `create()`, `approve()` that abstract HTTP calls.

**Why:**
- Decouples component from HTTP implementation details
- Centralizes API endpoint URLs in one place
- Enables easy service mocking for unit tests
- Makes it easy to add interceptors, error handling, or caching at service level
- Component tests don't need to mock HTTP - just mock the service

**Implementation Details:**
- Returns `Observable<T>` using RxJS for reactive data flow
- Typed DTOs: `CreateLeaveRequestDto`, `LeaveRequestResponse`
- No side effects - pure data transformation layer

---

### Decision 2: BehaviorSubject-Based State Management

**What:** Created `LeaveRequestStateService` that uses `BehaviorSubject` to manage component state.

**Why:**
- Centralized state in one service - single source of truth
- Reactive updates - component subscribes and gets updates automatically
- Better than NgRx for this app size - simpler, less boilerplate
- Typed state interface - `ComponentState` provides type safety
- Prevents prop drilling - component doesn't pass state through multiple layers

**State Shape:**
```typescript
interface ComponentState {
  requests: LeaveRequest[]           // Array of leave requests
  loading: boolean                   // True while fetching data
  error: string | null              // Error message if request fails
  submitting: boolean               // True while creating new request
  approvingIds: Set<number>         // IDs of requests being approved
  approvalErrors: { [id: number]: string }   // Error messages per request
  approvalSuccess: { [id: number]: boolean } // Success flags per request
}
```

**Implementation Details:**
- Methods: `setLoading()`, `setRequests()`, `setError()`, `setSubmitting()`, `setApprovingId()`, `setApprovalError()`, `setApprovalSuccess()`, `updateRequest()`, `reset()`
- `getState()` returns Observable for async subscriptions
- `getCurrentState()` returns synchronous snapshot for immediate access
- Only service can modify state - component uses service methods

---

### Decision 3: Type-Safe Enums for Leave Types and Status

**What:** Added `LeaveType` and `LeaveStatus` enums to `LeaveRequestModel`.

**Why:**
- Prevents magic numbers and string values scattered throughout code
- Compile-time type checking - can't pass invalid enum value
- Enables autocomplete in IDE - developer discovers available values
- Display methods (`leaveTypeLabel()`, `leaveStatusLabel()`) convert enums to UI text
- Same enums on backend ensure API contract clarity

**Implementation:**
```typescript
export enum LeaveType { Vacation = 0, Sick = 1, Unpaid = 2 }
export enum LeaveStatus { Pending = 0, Approved = 1, Rejected = 2 }
```

---

### Decision 4: Async Pipe with Single Subscription

**What:** Template uses `<ng-container *ngIf="state$ | async as state">` to manage state subscription.

**Why:**
- Single async pipe instead of multiple pipes reduces subscriptions
- `as state` destructuring makes state properties accessible in template
- Automatic unsubscription when component is destroyed
- Better performance than manual subscription + unsubscribe
- Cleaner template code

**Template Pattern:**
```html
<ng-container *ngIf="state$ | async as state">
  {{ state.loading ? 'Loading...' : '' }}
  {{ state.error ? state.error : '' }}
</ng-container>
```

---

### Decision 5: RxJS Memory Leak Prevention

**What:** All subscriptions use `takeUntil(destroy$)` pattern and `timer()` instead of `setTimeout()`.

**Why:**
- Subscriptions are memory leaks if not properly cleaned up
- `destroy$` Subject signals when component is destroyed
- `takeUntil()` automatically completes subscriptions at that point
- `timer()` from RxJS is composable and respects the same subscription pattern
- Prevents "stale closures" where setTimout/setInterval outlive component

**Implementation Details:**
- Create `destroy$ = new Subject<void>()` in component
- All subscriptions pipe through `.pipe(takeUntil(this.destroy$))`
- In `ngOnDestroy()`, call `this.destroy$.complete()`
- Error/success message timers use `timer(ms).pipe(takeUntil(destroy$))`

---

### Decision 6: Form Visibility Toggle

**What:** Added `showForm` boolean property with `toggleForm()` method to show/hide form.

**Why:**
- Better UX - form hidden by default, user clicks button to open
- Cleaner UI - no unnecessary form elements on screen
- Form properly reset when closed - prevents stale data
- Button text changes dynamically: "New Request" (form closed) → "Cancel" (form open)

**Implementation:**
- `showForm: boolean = false` - form hidden initially
- `toggleForm()`: Toggles `showForm`, resets form when closing
- Template: `<form *ngIf="showForm">` - form only renders when visible

---

### Decision 7: Proper Error Handling for Approval

**What:** Added `setApprovingId(id, false)` immediately in error handler to prevent loading state persisting.

**Why:**
- Without this, button stays "Approving..." forever when approval fails
- User has no visual feedback that approval failed (just button stuck)
- Immediately clearing loading state shows button is interactive again
- Error message still displays (in separate error state) so user knows what went wrong

**Implementation:**
- In `approve()` method's error handler: Immediately call `this.stateService.setApprovingId(id, false);`
- This removes ID from `approvingIds` set, making button clickable again
- Separate error state system displays the error message

---

### Decision 8: Component Typing and Initialization

**What:** Strongly typed properties: `state$!: Observable<ComponentState>` and services injected with typed dependencies.

**Why:**
- TypeScript strict mode catches type errors at compile time
- IDE provides accurate autocomplete and error highlighting
- Self-documenting code - types tell reader what data structure to expect
- Prevents runtime errors from wrong data types
- Non-null assertion (`!`) indicates developer has guaranteed initialization

---

## Summary of Benefits

### Backend (Task 3)
- ✅ Clear separation of concerns: Data Access (Repository) → Business Logic (Service) → HTTP (Controller)
- ✅ Testable - services can be unit tested without database
- ✅ Reusable - services can be called from multiple endpoints
- ✅ Scalable - all async/await prevents thread starvation
- ✅ Maintainable - business logic is explicit and centralized

### Frontend (Task 6)
- ✅ Reactive architecture using RxJS observables
- ✅ Centralized state - single source of truth
- ✅ Type-safe - enums and interfaces prevent errors
- ✅ Memory efficient - proper subscription cleanup
- ✅ Testable - services can be mocked, components can be unit tested
- ✅ User experience - proper loading states and error handling

---

## Patterns Used

1. **Repository Pattern** - Abstraction for data access
2. **Service Layer Pattern** - Orchestration and business logic
3. **Validator Pattern** - Reusable validation logic
4. **Dependency Injection** - Loose coupling between components
5. **Observable Pattern** - Reactive data flow with RxJS
6. **Async/Await** - Non-blocking I/O operations
7. **Optimistic Concurrency Control** - Handle race conditions gracefully
8. **Enum Type Safety** - Compile-time validation of values
- 400: כבר אושרה/נדחתה
- 400: vacation חוצה quota (בדיקה זהה ל-Create)
- 500: database error

**אישור בו-זמני (Race Condition)**
- שני משתמשים קוראים את אותה בקשה עם RowVersion=X
- שניהם מנסים לאשר
- הראשון שומר → RowVersion=Y
- השני מנסה לשמור → `DbUpdateConcurrencyException`
- תוצאה: 409 Conflict + "Please refresh"

**7 בדיקות**
- Approve_PendingRequest_Succeeds
- Approve_NonExistentRequest_ReturnsNotFound (404)
- Approve_AlreadyApprovedRequest_ReturnsBadRequest (400)
- Approve_RejectedRequest_ReturnsBadRequest (400)
- Approve_VacationRequestExceedsQuota_ReturnsBadRequest (400)
- Approve_VacationRequestWithinQuota_Succeeds
- Approve_SickLeaveRequest_IgnoresQuota

---

## 4. דרישה 3 & 6 — לא הספקתי

**דרישה 3 — שיפור ארכיטקטוני Backend**
- ❌ Controller עדיין עושה הכל (validation + logic + DB)
- **למה לא**: POC צריך speed, לא abstraction layers
- **עם עוד יום**: Service layer, Repository pattern

**דרישה 6 — שיפור קומפוננטה Frontend**
- ✅ Approve button + validators בטופס
- ❌ loading/error/success states עדיין חלקיים (TODO בקוד)
- ✅ `approvalSuccess`, `approvalErrors` maps — per-request tracking
- ❌ עושה blind `load()` בכל אישור (צריך optimistic update + proper state)
- **למה לא**: עדיפות — backend חשוב יותר, Frontend POC
- **עם עוד יום**: RxJS, ngrx, retry logic

---

## 5. שימוש ב-AI

**איפה AI עזר (כולל prompts אמיתיים)**

**Prompt 1 — דרישה 4 (Frontend Validation)**
```
Prompt: טופס הגשת בקשת חופשה עם Validation מלא

הקשר
פרויקט Angular 17 Standalone Components לניהול בקשות חופשה.

Frontend: frontend/src/app/
קומפוננטה ראשית: leave-requests/leave-requests.component.ts, .html, .css
מודלים: models/leave-request.model.ts

בעיה קיימת
קובץ leave-requests.component.html מכיל TODO:
<!-- TODO: add a form for submitting new leave requests here -->

דרישה
Validation טופס הגשת בקשה:
- תאריך ההתחלה אינו מאוחר מתאריך הסיום
- חובה לבחור סוג חופשה
- אין מספר ימים שלילי
- הודעות שגיאה ברורות
```
- קיבלתי: Custom validators framework
- עשיתי: Implemented [dateRangeValidator](frontend/src/app/leave-requests/validators/date-range.validator.ts) + [noDaysValidator](frontend/src/app/leave-requests/validators/no-days.validator.ts) + proper error messages per-field

**Prompt 2 — דרישה 5 (Approve Button UX)**
```
Prompt: שיפור UX של כפתור האישור (Approve Button)

הקשר
פרויקט Angular 17 Standalone Components לניהול בקשות חופשה.

קומפוננטה: frontend/src/app/leave-requests/leave-requests.component.ts, .html, .css
רשימת בקשות מוצגת בטבלה/רשימה
כל בקשה ב-status Pending יכולה להיות אושרת

בעיה קיימת
קובץ leave-requests.component.html מכיל TODO:
<!-- TODO: wire up the approve button properly (currently naive POST) -->

דרישה
כפתור האישור מחובר כרגע בצורה נאיבית (בלי רענון כל הרשימה, בלי POST)
שדרגו לטיפול נכון: מצב טעינה, טיפול בשגיאה (אישור שנכשל כי בקשה כבר אושרה), 
משוב הצלחה — בלי generic alert ובלי רענון עיוור

תממש לי בצורה מקצועית ונקיה
```
- קיבלתי: Basic button state management code
- עשיתי: Implemented per-request [approvingIds](frontend/src/app/leave-requests/leave-requests.component.ts#L20), [approvalSuccess](frontend/src/app/leave-requests/leave-requests.component.ts#L22), [approvalErrors](frontend/src/app/leave-requests/leave-requests.component.ts#L23) maps + inline per-request feedback (אין generic alert)

**איפה AI טעה וקלטתי:**

**AI הציע — סדר העדיפויות**
- Prompt: "תביא לי את הדרישות לפי סדר העדיפויות"
- קיבלתי: הביא לפי הסדר שכתוב בקובץ ההדרכה (לא נכון!)
- ❌ למה שגוי: AI קלט את הסדר המילולי, לא את חשיבות הטיפול
- תיקנתי: "תביא לפי חשיבות הטיפול, ולא לפי הסדר שקיים בקובץ הדרכה"
- ✅ קיבלתי: סידר בצורה נכונה את סדר המשימות לטיפול

**סה"כ AI usage:** 2 prompts אמיתיים + 1 correction

**אבטחה — בעיות שמצאנו ותיקנו**

**בעיה 1: Quota Validation שגויה**
- **איפה**: [LeaveRequestsController.cs L58](backend/src/LeaveManagement.Api/Controllers/LeaveRequestsController.cs#L58)
- **מה מצאנו**: בדיקת quota בדקה רק הימים החדשים, לא את הסכום הכולל
- **למה זו בעיה**: עובד יכול לחמוק מהקוטה השנתית
- **איך תיקנו**: שינו מ-`days > quota` ל-`(used + days) > quota`
- **בדיקה**: [Create_ExceedsQuota_ReturnsBadRequest](backend/tests/LeaveManagement.Tests/LeaveRequestsTests.cs#L49-L76)

**בעיה 2: Double-Approval (Race Condition)**
- **איפה**: [LeaveRequestsController.cs L87-L166](backend/src/LeaveManagement.Api/Controllers/LeaveRequestsController.cs#L87-L166)
- **מה מצאנו**: שני admins יכולים לאשר את אותה בקשה בו-זמנית
- **למה זו בעיה**: הוצאה כפולה של חופשה מהמערכת
- **איך תיקנו**: הוסיפו `[Timestamp] RowVersion` ב-[migration](backend/src/LeaveManagement.Api/Migrations/20261021181131_AddRowVersionToLeaveRequest.cs), Approve בודק `DbUpdateConcurrencyException`
- **בדיקה**: כל 7 בדיקות Approve (409 Conflict בריצה שנייה)

---

## 6. הוראות הרצה

**SQLite (no Docker)**:
```powershell
cd backend
$env:Database__Provider="Sqlite"
dotnet run --project src/LeaveManagement.Api
dotnet test
```

**PostgreSQL (Docker required)**:
```powershell
docker compose up -d
cd backend
dotnet run --project src/LeaveManagement.Api
dotnet test
```

Swagger: http://localhost:5080/swagger

---

## Summary

✅ **Implemented**: Bug fix + Approve endpoint + concurrency + 9 tests + migrations + SQLite/PostgreSQL

✅ **Verified**: All tests pass, manual testing works, error handling complete

❌ **Not Implemented**: Backend service layer (deferral for POC), Frontend proper state management (deferral for time)

</div>
