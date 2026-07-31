namespace HousingHub.Service.Dtos.PropertyReport;

public record CreatePropertyReportDto(Guid PropertyId, Guid ReporterId, string Reason, string? Note);
