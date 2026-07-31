using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.PropertyReport;

namespace HousingHub.Service.PropertyReportService.Interfaces;

public interface IPropertyReportCommandService
{
    Task<BaseResponse<bool>> CreateReportAsync(CreatePropertyReportDto request);
}
