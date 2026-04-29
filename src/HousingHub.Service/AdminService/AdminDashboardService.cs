using HousingHub.Model.Enums;
using HousingHub.Service.CustomerService.Interfaces;
using HousingHub.Service.Dtos.Admin;
using HousingHub.Service.InspectionService.Interfaces;
using HousingHub.Service.PropertyService.Interfaces;

namespace HousingHub.Service.AdminService;

public class AdminDashboardService(
    ICustomerQueryService customerQueryService,
    IPropertyQueryService propertyQueryService,
    IInspectionQueryService inspectionQueryService) : IAdminDashboardService
{
    public async Task<AdminDashboardStatsDto> GetStatsAsync()
    {
        var customersTask = customerQueryService.GetAllCustomersAsync();
        var propertiesTask = propertyQueryService.GetAllPropertiesAsync();
        var inspectionsTask = inspectionQueryService.GetAllInspectionsPaginatedAsync(
            new AdminInspectionFilterDto(1, int.MaxValue));

        await Task.WhenAll(customersTask, propertiesTask, inspectionsTask);

        var customers = customersTask.Result.Data ?? [];
        var properties = propertiesTask.Result.Data ?? [];
        var inspections = inspectionsTask.Result.Data?.Items ?? [];

        var today = DateTime.UtcNow.Date;

        return new AdminDashboardStatsDto(
            TotalCustomers: customers.Count(c => (CustomerType)c.CustomerType == CustomerType.Customer),
            TotalOwners: customers.Count(c => ((CustomerType)c.CustomerType).HasFlag(CustomerType.HouseOwner)),
            TotalAgents: customers.Count(c => ((CustomerType)c.CustomerType).HasFlag(CustomerType.Agent)),
            PendingKyc: customers.Count(c => !((CustomerType)c.CustomerType).HasFlag(CustomerType.Admin)
                                          && c.DateModified != default),
            ActiveListings: properties.Count(p => p.IsPublished && p.Availability == PropertyAvailability.Available),
            TotalProperties: properties.Count,
            PendingInspections: inspections.Count(i => i.Status == InspectionStatus.Pending),
            TodaysInspections: inspections.Count(i => i.ScheduledDate.Date == today));
    }
}
