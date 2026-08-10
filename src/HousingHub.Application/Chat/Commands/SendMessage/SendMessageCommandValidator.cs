using FluentValidation;

namespace HousingHub.Application.Chat.Commands.SendMessage;

public class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    /// <summary>
    /// Generous for a chat message, bounded enough that a single request cannot be
    /// used to store megabytes per row. Content was previously unbounded — only the
    /// 100-character email preview was truncated, not the stored message.
    /// </summary>
    private const int MaxContentLength = 4000;

    public SendMessageCommandValidator()
    {
        RuleFor(x => x.RecipientId).NotEmpty();

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Message cannot be empty.")
            .MaximumLength(MaxContentLength)
            .WithMessage($"Message cannot exceed {MaxContentLength} characters.");
    }
}
