using LeaveManagement.Api.Data;
using LeaveManagement.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagement.Api.Controllers;

// NOTE: This controller was written quickly for a POC.
// It does data access, business logic and validation all in one place.
[ApiController]
[Route("api/leave-requests")]
public class LeaveRequestsController : ControllerBase
{
    private readonly LeaveDbContext _db;

    public LeaveRequestsController(LeaveDbContext db)
    {
        _db = db;
    }

    // GET /api/leave-requests
    [HttpGet]
    public IActionResult GetAll()
    {
        var all = _db.LeaveRequests
            .Include(r => r.Employee)
            .OrderByDescending(r => r.StartDate)
            .ToList();
        return Ok(all);
    }

    // GET /api/leave-requests/search?name=Dana
    // Lets the UI quickly find requests by employee name.
    [HttpGet("search")]
    public IActionResult Search([FromQuery] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Ok(new List<LeaveRequest>());

        var results = _db.LeaveRequests
            .Include(r => r.Employee)
            .Where(r => r.Employee!.Name.Contains(name))
            .ToList();

        return Ok(results);
    }

    // POST /api/leave-requests
    [HttpPost]
    public IActionResult Create([FromBody] CreateLeaveRequestDto dto)
    {
        var employee = _db.Employees.FirstOrDefault(e => e.Id == dto.EmployeeId);
        if (employee == null)
            return NotFound("Employee not found");

        var days = (dto.EndDate - dto.StartDate).Days + 1;

        // How many vacation days has the employee already used this year?
        var used = _db.LeaveRequests
            .Where(r => r.EmployeeId == dto.EmployeeId
                        && r.Type == LeaveType.Vacation
                        && r.Status == LeaveStatus.Approved)
            .Sum(r => r.Days);

        // Make sure the request does not exceed the quota.
        if (dto.Type == LeaveType.Vacation && (used + days) > employee.AnnualQuota)
        {
            return BadRequest($"Not enough vacation balance. Used: {used} days, Requested: {days} days, Annual Quota: {employee.AnnualQuota} days.");
        }

        var request = new LeaveRequest
        {
            EmployeeId = dto.EmployeeId,
            Type = dto.Type,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Days = days,
            Status = LeaveStatus.Pending
        };

        _db.LeaveRequests.Add(request);
        _db.SaveChanges();

        return Ok(request);
    }

    // POST /api/leave-requests/{id}/approve
    [HttpPost("{id}/approve")]
    public IActionResult Approve(int id)
    {
        // Step 1: Find the leave request
        var request = _db.LeaveRequests
            .Include(r => r.Employee)
            .FirstOrDefault(r => r.Id == id);

        if (request == null)
            return NotFound(new { error = "Leave request not found" });

        // Step 2: Validate the request is in Pending status
        if (request.Status != LeaveStatus.Pending)
        {
            return BadRequest(new
            {
                error = $"Cannot approve a leave request with status '{request.Status}'",
                currentStatus = request.Status
            });
        }

        // Step 3: For vacation requests, validate the employee has sufficient balance
        if (request.Type == LeaveType.Vacation && request.Employee != null)
        {
            var used = _db.LeaveRequests
                .Where(r => r.EmployeeId == request.EmployeeId
                            && r.Type == LeaveType.Vacation
                            && r.Status == LeaveStatus.Approved)
                .Sum(r => r.Days);

            if ((used + request.Days) > request.Employee.AnnualQuota)
            {
                return BadRequest(new
                {
                    error = "Insufficient vacation balance",
                    used = used,
                    requested = request.Days,
                    available = request.Employee.AnnualQuota - used,
                    quota = request.Employee.AnnualQuota
                });
            }
        }

        // Step 4: Update status and attempt to save with concurrency handling
        request.Status = LeaveStatus.Approved;

        try
        {
            _db.SaveChanges();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Concurrency conflict: another process modified the request
            _db.Entry(request).Reload();
            return Conflict(new
            {
                error = "The leave request was modified by another user. Please refresh and try again.",
                conflictDetails = ex.Message
            });
        }
        catch (DbUpdateException ex)
        {
            // Database error (e.g., constraint violation)
            return StatusCode(500, new
            {
                error = "An error occurred while updating the leave request",
                details = ex.InnerException?.Message
            });
        }

        return Ok(new
        {
            message = "Leave request approved successfully",
            data = request
        });
    }
}

