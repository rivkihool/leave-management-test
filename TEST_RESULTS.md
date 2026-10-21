# ✅ Approve Endpoint - Testing Summary

## Tests Status: ✅ ALL PASSED (9/9)

```
Test summary: total: 9, failed: 0, succeeded: 9, skipped: 0, duration: 1.5s
```

### Tests Executed:
1. ✅ Create_WithinQuota_Succeeds
2. ✅ Create_ExceedsQuota_ReturnsBadRequest
3. ✅ **Approve_PendingRequest_Succeeds** ⭐
4. ✅ **Approve_NonExistentRequest_ReturnsNotFound** ⭐
5. ✅ **Approve_AlreadyApprovedRequest_ReturnsBadRequest** ⭐
6. ✅ **Approve_RejectedRequest_ReturnsBadRequest** ⭐
7. ✅ **Approve_VacationRequestExceedsQuota_ReturnsBadRequest** ⭐
8. ✅ **Approve_VacationRequestWithinQuota_Succeeds** ⭐
9. ✅ **Approve_SickLeaveRequest_IgnoresQuota** ⭐

**(⭐ = New Approve endpoint tests)**

---

## 🔧 How to Run Tests

### With SQLite (No Docker Needed):
```powershell
cd backend
$env:Database__Provider="Sqlite"
dotnet test LeaveManagement.sln
```

### With PostgreSQL (Requires Docker):
```powershell
cd backend
docker compose up -d
dotnet test LeaveManagement.sln
```

---

## 🚀 How to Run the API

### With SQLite:
```powershell
cd backend/src/LeaveManagement.Api
$env:Database__Provider="Sqlite"
dotnet run
```

The API will start at: **http://localhost:5080**

### With PostgreSQL:
```powershell
docker compose up -d
dotnet run
```

---

## 📝 Testing the Approve Endpoint

### Using PowerShell:
```powershell
# Get all leave requests
$requests = Invoke-RestMethod -Uri "http://localhost:5080/api/leave-requests" -Method Get
$requests | ConvertTo-Json

# Approve a specific request (ID = 1)
$response = Invoke-RestMethod -Uri "http://localhost:5080/api/leave-requests/1/approve" -Method Post
$response | ConvertTo-Json
```

### Using curl:
```bash
# Get all leave requests
curl http://localhost:5080/api/leave-requests

# Approve request (ID = 1)
curl -X POST http://localhost:5080/api/leave-requests/1/approve
```

### Using Postman:
1. **GET** `http://localhost:5080/api/leave-requests` - View all requests
2. **POST** `http://localhost:5080/api/leave-requests/{id}/approve` - Approve a request

---

## ✨ Implementation Highlights

### ✅ Error Handling
- **404 Not Found** - Request doesn't exist
- **400 Bad Request** - Invalid status or quota exceeded
- **409 Conflict** - Concurrency issue (someone else modified it)
- **500 Server Error** - Database error

### ✅ Concurrency Control
- Added `RowVersion` property with `[Timestamp]` attribute to `LeaveRequest` model
- EF Core tracks changes and prevents race conditions
- Returns 409 Conflict if another user modified the request

### ✅ Business Logic
- Only Pending requests can be approved
- Vacation quota validation
- Sick leave bypass (doesn't count against quota)
- Detailed error responses with balance info

### ✅ Code Quality
- Proper HTTP status codes
- JSON error responses with context
- Comments explaining each validation step
- Comprehensive test coverage

---

## 📂 Files Modified

1. **LeaveRequest.cs** - Added RowVersion for concurrency
2. **LeaveDbContext.cs** - Configured RowVersion as row version token
3. **LeaveRequestsController.cs** - Enhanced Approve endpoint with validation
4. **LeaveRequestsTests.cs** - Added 7 new tests for Approve endpoint

---

## ⚠️ Important Note

A database migration is needed to add the RowVersion column:

```powershell
cd backend/src/LeaveManagement.Api
dotnet ef migrations add AddRowVersionToLeaveRequest
dotnet ef database update
```

For SQLite, it will add a BLOB column. For PostgreSQL, it will use the native xmin system column.

