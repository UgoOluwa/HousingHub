using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.PropertyReport;
using HousingHub.Service.PropertyReportService.Interfaces;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.PropertyReportService;

public class PropertyReportCommandService : IPropertyReportCommandService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly ILogger<PropertyReportCommandService> _logger;
    private const string ClassName = "property report";

    public PropertyReportCommandService(ILogger<PropertyReportCommandService> logger, IUnitOfWOrk unitOfWOrk)
    {
        _logger = logger;
        _unitOfWOrk = unitOfWOrk;
    }

    public async Task<BaseResponse<bool>> CreateReportAsync(CreatePropertyReportDto request)
    {
        try
        {
            var reporter = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == request.ReporterId);
            if (reporter == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage("customer"));

            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(x => x.Id == request.PropertyId);
            if (property == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage("property"));

            if (property.OwnerId == request.ReporterId)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.CannotReportOwnProperty);

            // One report per person per listing. Without this a single user could file
            // unlimited reports against the same property and bury the moderation queue,
            // or make a legitimate listing look widely-reported.
            bool alreadyReported = await _unitOfWOrk.PropertyReportQueries.AnyAsync(
                x => x.PropertyId == request.PropertyId && x.ReporterId == request.ReporterId);

            if (alreadyReported)
            {
                return new BaseResponse<bool>(
                    true, true, string.Empty, ResponseMessages.PropertyReportSubmitted);
            }

            var report = new PropertyReport(request.PropertyId, request.ReporterId, request.Reason, request.Note);

            bool isSuccessful = await _unitOfWOrk.PropertyReportCommands.InsertAsync(report);
            if (!isSuccessful)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetCreationFailureMessage(ClassName));

            await _unitOfWOrk.SaveAsync();

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.PropertyReportSubmitted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in CreateReportAsync: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ex.Message);
        }
    }
}
