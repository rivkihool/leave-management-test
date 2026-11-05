using LeaveManagement.Api.Data;
using LeaveManagement.Api.Models;
using LeaveManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagement.Api.Controllers;

[ApiController]
[Route("api/leave-requests")]
public class LeaveRequestsController : ControllerBase
{
    private readonly LeaveDbContext _db;
    private readonly ILeaveRequestService _leaveService;

    public LeaveRequestsController(LeaveDbContext db, ILeaveRequestService leaveService)
    {
        _db = db;
        _leaveService = leaveService;
    }

    // GET /api/leave-requests
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var all = await _leaveService.GetAllAsync();
        return Ok(all);
    }

    // POST /api/leave-requests
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLeaveRequestDto dto)
    {
        var employee = _db.Employees.FirstOrDefault(e => e.Id == dto.EmployeeId);
        if (employee == null)
            return NotFound(new { error = "Employee not found" });

        try
        {
            var request = await _leaveService.CreateAsync(dto, employee);
            return Ok(request);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST /api/leave-requests/{id}/approve
    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(int id)
    {
        try
        {
            var request = await _leaveService.ApproveAsync(id);
            return Ok(new
            {
                message = "Leave request approved successfully",
                data = request
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Check if it's a concurrency exception message
            if (ex.Message.Contains("modified by another user"))
            {
                return Conflict(new { error = ex.Message });
            }
            // Otherwise it's a business logic error (status or quota issue)
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET /api/leave-requests/search?name=Dana
    // Search leave requests by employee name
    [HttpGet("search")]
    public IActionResult Search([FromQuery] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { error = "Search name cannot be empty" });

        var results = _db.LeaveRequests
            .Include(lr => lr.Employee)
            .Where(lr => lr.Employee != null && lr.Employee.Name.Contains(name))
            .ToList();

        return Ok(results);
    }
}

