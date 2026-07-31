using HousingHub.Application.Commons.Bases;
using MediatR;

namespace HousingHub.Application.Property.Commands.CreateReport;

public record CreatePropertyReportCommand(Guid PropertyId, string Reason, string? Note, Guid ReporterId) : IRequest<BaseResponse<bool>>;
