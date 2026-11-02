using LeaveManagement.Api.Models;
using LeaveManagement.Api.Repositories;

namespace LeaveManagement.Api.Validators;

public class VacationQuotaValidator
{
    private readonly ILeaveRequestRepository _repository;

    public VacationQuotaValidator(ILeaveRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<(bool IsValid, string ErrorMessage, int UsedDays, int AvailableDays)> 
        ValidateVacationQuotaAsync(int employeeId, int requestedDays, int annualQuota)
    {
        var usedDays = await _repository.GetUsedVacationDaysAsync(employeeId);
        var totalAfterRequest = usedDays + requestedDays;

        if (totalAfterRequest > annualQuota)
        {
            var availableDays = annualQuota - usedDays;
            var errorMessage = $"Not enough vacation balance. Used: {usedDays} days, Requested: {requestedDays} days, Annual Quota: {annualQuota} days.";
            return (false, errorMessage, usedDays, availableDays);
        }

        return (true, string.Empty, usedDays, annualQuota - usedDays);
    }
}
