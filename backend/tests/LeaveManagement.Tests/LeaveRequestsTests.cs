using LeaveManagement.Api.Controllers;
using LeaveManagement.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace LeaveManagement.Tests;

public class LeaveRequestsTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _database;

    public LeaveRequestsTests(TestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public void Create_WithinQuota_Succeeds()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Test Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        var controller = new LeaveRequestsController(db);

        // Act: request 3 days, well within the quota.
        var result = controller.Create(new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 3)
        });

        // Assert
        Assert.IsType<OkObjectResult>(result);
        Assert.Single(db.LeaveRequests);
    }

    // TODO (candidate): add a test that proves the balance bug is fixed —
    // an employee who has already used most of the quota should NOT be able
    // to create a request that pushes them over the annual quota.

    [Fact]
    public void Create_ExceedsQuota_ReturnsBadRequest()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Test Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        // Add an existing approved leave request for 18 days
        var existingRequest = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 18),
            Days = 18,
            Status = LeaveStatus.Approved
        };
        db.LeaveRequests.Add(existingRequest);
        db.SaveChanges();

        var controller = new LeaveRequestsController(db);

        // Act: try to request 4 additional days (18 + 4 = 22, which exceeds the 20-day quota)
        var result = controller.Create(new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 4)
        });

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(1, db.LeaveRequests.Count()); // Should still only have the original request
    }

    // ============ APPROVE ENDPOINT TESTS ============

    [Fact]
    public void Approve_PendingRequest_Succeeds()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Test Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        var leaveRequest = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 5),
            Days = 5,
            Status = LeaveStatus.Pending
        };
        db.LeaveRequests.Add(leaveRequest);
        db.SaveChanges();

        var controller = new LeaveRequestsController(db);

        // Act
        var result = controller.Approve(leaveRequest.Id);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        var updatedRequest = db.LeaveRequests.First(r => r.Id == leaveRequest.Id);
        Assert.Equal(LeaveStatus.Approved, updatedRequest.Status);
    }

    [Fact]
    public void Approve_NonExistentRequest_ReturnsNotFound()
    {
        // Arrange
        using var db = _database.NewDb();
        var controller = new LeaveRequestsController(db);

        // Act
        var result = controller.Approve(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void Approve_AlreadyApprovedRequest_ReturnsBadRequest()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Test Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        var leaveRequest = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 5),
            Days = 5,
            Status = LeaveStatus.Approved
        };
        db.LeaveRequests.Add(leaveRequest);
        db.SaveChanges();

        var controller = new LeaveRequestsController(db);

        // Act
        var result = controller.Approve(leaveRequest.Id);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        var badRequest = result as BadRequestObjectResult;
        Assert.NotNull(badRequest);
    }

    [Fact]
    public void Approve_RejectedRequest_ReturnsBadRequest()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Test Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        var leaveRequest = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 5),
            Days = 5,
            Status = LeaveStatus.Rejected
        };
        db.LeaveRequests.Add(leaveRequest);
        db.SaveChanges();

        var controller = new LeaveRequestsController(db);

        // Act
        var result = controller.Approve(leaveRequest.Id);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void Approve_VacationRequestExceedsQuota_ReturnsBadRequest()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Test Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        // Add an existing approved vacation request for 18 days
        var approvedRequest = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 18),
            Days = 18,
            Status = LeaveStatus.Approved
        };
        db.LeaveRequests.Add(approvedRequest);

        // Add a pending request for 4 additional days (would exceed quota)
        var pendingRequest = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 4),
            Days = 4,
            Status = LeaveStatus.Pending
        };
        db.LeaveRequests.Add(pendingRequest);
        db.SaveChanges();

        var controller = new LeaveRequestsController(db);

        // Act
        var result = controller.Approve(pendingRequest.Id);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        var badRequest = result as BadRequestObjectResult;
        Assert.NotNull(badRequest);
        // Request should still be Pending
        var unchangedRequest = db.LeaveRequests.First(r => r.Id == pendingRequest.Id);
        Assert.Equal(LeaveStatus.Pending, unchangedRequest.Status);
    }

    [Fact]
    public void Approve_VacationRequestWithinQuota_Succeeds()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Test Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        // Add an existing approved vacation request for 15 days
        var approvedRequest = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 15),
            Days = 15,
            Status = LeaveStatus.Approved
        };
        db.LeaveRequests.Add(approvedRequest);

        // Add a pending request for 5 days (within quota)
        var pendingRequest = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 5),
            Days = 5,
            Status = LeaveStatus.Pending
        };
        db.LeaveRequests.Add(pendingRequest);
        db.SaveChanges();

        var controller = new LeaveRequestsController(db);

        // Act
        var result = controller.Approve(pendingRequest.Id);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        var approvedPendingRequest = db.LeaveRequests.First(r => r.Id == pendingRequest.Id);
        Assert.Equal(LeaveStatus.Approved, approvedPendingRequest.Status);
    }

    [Fact]
    public void Approve_SickLeaveRequest_IgnoresQuota()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Test Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        // Add an existing approved vacation request for 20 days (full quota)
        var approvedRequest = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 20),
            Days = 20,
            Status = LeaveStatus.Approved
        };
        db.LeaveRequests.Add(approvedRequest);

        // Add a pending sick leave request (should not count against vacation quota)
        var sickLeaveRequest = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Sick,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 5),
            Days = 5,
            Status = LeaveStatus.Pending
        };
        db.LeaveRequests.Add(sickLeaveRequest);
        db.SaveChanges();

        var controller = new LeaveRequestsController(db);

        // Act
        var result = controller.Approve(sickLeaveRequest.Id);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        var approvedSickLeave = db.LeaveRequests.First(r => r.Id == sickLeaveRequest.Id);
        Assert.Equal(LeaveStatus.Approved, approvedSickLeave.Status);
    }
}

