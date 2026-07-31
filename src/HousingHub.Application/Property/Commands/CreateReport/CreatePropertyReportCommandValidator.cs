using FluentValidation;

namespace HousingHub.Application.Property.Commands.CreateReport;

public class CreatePropertyReportCommandValidator : AbstractValidator<CreatePropertyReportCommand>
{
    public CreatePropertyReportCommandValidator()
    {
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.ReporterId).NotEmpty();
    }
}
