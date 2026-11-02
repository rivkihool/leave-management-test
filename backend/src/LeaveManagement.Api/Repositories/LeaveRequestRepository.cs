using LeaveManagement.Api.Data;
using LeaveManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagement.Api.Repositories;

public class LeaveRequestRepository : ILeaveRequestRepository
{
    private readonly LeaveDbContext _dbContext;

    public LeaveRequestRepository(LeaveDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<LeaveRequest?> GetByIdAsync(int id)
    {
        return await _dbContext.LeaveRequests
            .Include(r => r.Employee)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<List<LeaveRequest>> GetAllAsync()
    {
        return await _dbContext.LeaveRequests
            .Include(r => r.Employee)
            .OrderByDescending(r => r.StartDate)
            .ToListAsync();
    }

    public async Task<int> GetUsedVacationDaysAsync(int employeeId)
    {
        return await _dbContext.LeaveRequests
            .Where(r => r.EmployeeId == employeeId
                        && r.Type == LeaveType.Vacation
                        && r.Status == LeaveStatus.Approved)
            .SumAsync(r => r.Days);
    }

    public async Task AddAsync(LeaveRequest request)
    {
        await _dbContext.LeaveRequests.AddAsync(request);
    }

    public async Task SaveAsync()
    {
        await _dbContext.SaveChangesAsync();
    }
}
