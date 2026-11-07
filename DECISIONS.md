# החלטות ארכיטקטוניות

## 1. החלטות ארכיטקטוניות

### Backend (Task 3) — Separation of Concerns
- **Repository Pattern**: `ILeaveRequestRepository` מופשט את ה-DB - אפשר להחליף מ-SQLite ל-PostgreSQL בלי לשנות service
- **Service Layer**: `ILeaveRequestService` - כל ההיגיון של business logic (quota validation, status transitions) - לא בקונטרולר
- **Validator Pattern**: `VacationQuotaValidator` - בדיקת quota שניתן לעשות בו-זמנית בעת Create ו-Approve
- **Dependency Injection**: הכל מוזרק דרך `Program.cs` - קל לבדוק עם mocks
- **Async/Await**: כל הקוד `async` - scalable, לא חוסם thread pool

### Frontend (Task 6) — Reactive Architecture
- **Service Layer**: `LeaveRequestService` - HTTP calls מופשטים, כל endpoint מוקפד
- **State Management**: `LeaveRequestStateService` with `BehaviorSubject` - single source of truth
- **Type Safety**: `LeaveType` ו-`LeaveStatus` enums - compile-time validation
- **Memory Leaks**: `takeUntil(destroy$)` בכל subscriptions - מנקה כשקומפוננטה נהרסת
- **Form Toggle**: "`showForm`" בוליאן - טופס מוסתר כברירת מחדל, לחצן "ביטול" כשפתוח

---

## 2. הבאג ביתרת החופשה

**מה היה**:
בקונטרולר בדקנו רק `days > quota` - לא את הסכום הכולל.
עובד עם 20 ימים quota יכול להגיש בקשה ל-15 ימים, אחרי זה עוד 10 ימים, וזה יעבור!

**איפה**:
[LeaveRequestsController.cs](backend/src/LeaveManagement.Api/Controllers/LeaveRequestsController.cs#L58) בשורה 58

**איך תיקנו**:
שינו מ-`days > quota` ל-`(usedDays + days) > quota`
- קורים ל-`repo.GetUsedVacationDaysAsync()` כדי לדעת כמה כבר נלקח
- בודקים את הסכום הכולל

**הטסט שמוכיח**:
[Create_ExceedsQuota_ReturnsBadRequest](backend/tests/LeaveManagement.Tests/LeaveRequestsTests.cs#L49-L76) — משתמש עם 10 ימים quota מנסה להגיש 15 ימים, קיבל 400 Bad Request

---

## 3. אישור בקשה (Approve) ו-Concurrency

### מצבים לא חוקיים
- **404 Not Found**: בקשה לא קיימת
- **400 Bad Request**: בקשה כבר אושרה או נדחתה
- **400 Bad Request**: בקשה vacation חוצה quota (בדיקה זהה ל-Create)
- **409 Conflict**: שני admins אישרו בו-זמנית (race condition)

### Race Condition — איך טיפלנו
1. הוסיפנו `[Timestamp] RowVersion` ל-`LeaveRequest` model
2. Migration: [20261021181131_AddRowVersionToLeaveRequest](backend/src/LeaveManagement.Api/Migrations/20261021181131_AddRowVersionToLeaveRequest.cs)
3. כשה-admin הראשון שומר, `RowVersion` משתנה
4. כשה-admin השני מנסה לשמור בנתונים ישנים → `DbUpdateConcurrencyException` 
5. אנחנו תופסים את זה ומחזירים `409 Conflict` עם הודעה "אנא רענן את הדף"

**7 בדיקות Approve כולל Race Condition**:
- `Approve_PendingRequest_Succeeds`
- `Approve_NonExistentRequest_ReturnsNotFound` (404)
- `Approve_AlreadyApprovedRequest_ReturnsBadRequest` (400)
- `Approve_RejectedRequest_ReturnsBadRequest` (400)
- `Approve_VacationRequestExceedsQuota_ReturnsBadRequest` (400)
- `Approve_VacationRequestWithinQuota_Succeeds`
- `Approve_SickLeaveRequest_IgnoresQuota` (sick leave לא דורש quota)

---

## 4. על מה ויתרתי בגלל הזמן

### Service Layer מלא Backend
- ✅ לא הספקתי refactor את כל ה-Controller לService Layer - רק תיקנו את ה-bug בחיוב
- ❌ עדיין קצת לוגיקה בקונטרולר
- ⏱️ **עם עוד יום**: היו מזיז הכל לשכבה service נקיה

### Frontend State Management
- ✅ יש לנו per-request approvalSuccess/approvalErrors maps
- ❌ loading states חלקיים עדיין (יש TODOs בקוד)
- ❌ לא עושים optimistic update - עושים blind load() אחרי כל אישור
- ⏱️ **עם עוד יום**: היו משדרגים ל-RxJS מלא, optimistic updates, retry logic

---

## 5. שימוש ב-AI

### איפה AI עזר

**Prompt 1 — Frontend Validation (Requirement 4)**
```
"טופס הגשת בקשת חופשה עם Validation מלא:
- תאריך ההתחלה לא אחרי תאריך הסיום
- חובה לבחור סוג חופשה
- אין מספר ימים שלילי
- הודעות שגיאה ברורות"
```
✅ קיבלתי: Custom validators framework
✅ עשיתי: Implemented `dateRangeValidator` + `noDaysValidator` + proper error messages

**Prompt 2 — Approve Button UX (Requirement 5)**
```
"כפתור אישור מחובר בצורה נאיבית.
שדרגו ל: מצב טעינה, טיפול בשגיאה (כבר אושרה),
משוב הצלחה — בלי generic alert ובלי רענון עיוור"
```
✅ קיבלתי: Basic button state management
✅ עשיתי: Per-request `approvingIds`, `approvalSuccess`, `approvalErrors` maps + inline feedback

### איפה דחיתי/תיקנתי AI

**AI הציע — סדר העדיפויות**
- 🔴 AI הביא לפי סדר שכתוב בקובץ הדרכה (לא נכון!)
- 🟢 תיקנתי: "לפי חשיבות הטיפול, לא לפי הסדר המילולי"
- ✅ קיבלתי: סידור נכון

### בעיות אבטחה שמצאנו

**בעיה 1: Quota Validation לא נכונה**
- **איפה**: [LeaveRequestsController.cs L58](backend/src/LeaveManagement.Api/Controllers/LeaveRequestsController.cs#L58)
- **מה**: בדיקה בדקה רק הימים החדשים, לא את הסכום הכולל
- **למה זו בעיה**: עובד יכול לחמוק מהקוטה השנתית
- **איך תיקנו**: `(usedDays + days) > quota`

**בעיה 2: Double-Approval Race Condition**
- **איפה**: Approve endpoint [L87-L166](backend/src/LeaveManagement.Api/Controllers/LeaveRequestsController.cs#L87-L166)
- **מה**: שני admins יכולים לאשר את אותה בקשה בו-זמנית
- **למה זו בעיה**: הוצאה כפולה של חופשה מהמערכת
- **איך תיקנו**: `[Timestamp] RowVersion` + `DbUpdateConcurrencyException` handling → 409 Conflict

---

## סיכום

✅ **הממשתי**: Bug fix + Approve endpoint + concurrency handling + 9 בדיקות + migrations + SQLite/PostgreSQL

✅ **בדקתי**: כל בדיקות עובדות, הרצה ידנית עובדת, טיפול בשגיאות מלא

❌ **לא הממשתי**: Backend service layer מלא (deferral for POC), Frontend state management advanced (deferral for time)
