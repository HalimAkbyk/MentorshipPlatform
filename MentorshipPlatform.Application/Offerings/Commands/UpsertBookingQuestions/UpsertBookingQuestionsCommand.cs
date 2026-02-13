using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Offerings.Commands.UpsertBookingQuestions;

public record BookingQuestionDto(string QuestionText, bool IsRequired);

public record UpsertBookingQuestionsCommand(
    Guid OfferingId,
    List<BookingQuestionDto> Questions) : IRequest<Result<bool>>;

public class UpsertBookingQuestionsCommandValidator : AbstractValidator<UpsertBookingQuestionsCommand>
{
    public UpsertBookingQuestionsCommandValidator()
    {
        RuleFor(x => x.OfferingId).NotEmpty();

        RuleFor(x => x.Questions)
            .Must(q => q.Count <= 4).WithMessage("En fazla 4 soru eklenebilir");

        RuleForEach(x => x.Questions)
            .ChildRules(q =>
            {
                q.RuleFor(x => x.QuestionText)
                    .NotEmpty().WithMessage("Soru metni boş olamaz")
                    .MaximumLength(200).WithMessage("Soru metni en fazla 200 karakter olabilir");
            });
    }
}

public class UpsertBookingQuestionsCommandHandler : IRequestHandler<UpsertBookingQuestionsCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpsertBookingQuestionsCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(UpsertBookingQuestionsCommand request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<bool>.Failure("User not authenticated");

        var offering = await _context.Offerings
            .FirstOrDefaultAsync(o => o.Id == request.OfferingId && o.MentorUserId == _currentUser.UserId.Value, ct);

        if (offering == null)
            return Result<bool>.Failure("Offering not found");

        // Mevcut soruları sil
        var existingQuestions = await _context.BookingQuestions
            .Where(q => q.OfferingId == request.OfferingId)
            .ToListAsync(ct);
        _context.BookingQuestions.RemoveRange(existingQuestions);

        // Yeni soruları ekle
        for (int i = 0; i < request.Questions.Count; i++)
        {
            var dto = request.Questions[i];
            var question = BookingQuestion.Create(
                request.OfferingId,
                dto.QuestionText,
                dto.IsRequired,
                i);
            _context.BookingQuestions.Add(question);
        }

        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
