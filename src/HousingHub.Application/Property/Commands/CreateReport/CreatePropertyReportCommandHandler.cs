using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.PropertyReport;
using HousingHub.Service.PropertyReportService.Interfaces;
using MediatR;

namespace HousingHub.Application.Property.Commands.CreateReport;

public class CreatePropertyReportCommandHandler : IRequestHandler<CreatePropertyReportCommand, BaseResponse<bool>>
{
    private readonly IPropertyReportCommandService _propertyReportCommandService;

    public CreatePropertyReportCommandHandler(IPropertyReportCommandService propertyReportCommandService)
    {
        _propertyReportCommandService = propertyReportCommandService;
    }

    public async Task<BaseResponse<bool>> Handle(CreatePropertyReportCommand request, CancellationToken cancellationToken)
    {
        var dto = new CreatePropertyReportDto(request.PropertyId, request.ReporterId, request.Reason, request.Note);
        var response = await _propertyReportCommandService.CreateReportAsync(dto);
        return new BaseResponse<bool>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
