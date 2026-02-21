namespace MentorshipPlatform.Application.Common.Constants;

/// <summary>
/// Well-known email template keys matching the NotificationTemplate.Key values in the database.
/// </summary>
public static class EmailTemplateKeys
{
    public const string Welcome = "welcome";
    public const string BookingConfirmed = "booking_confirmed";
    public const string BookingConfirmedMentor = "booking_confirmed_mentor";
    public const string BookingReminder = "booking_reminder";
    public const string BookingCancelledStudent = "booking_cancelled_student";
    public const string BookingCancelledMentor = "booking_cancelled_mentor";
    public const string BookingCompleted = "booking_completed";
    public const string RescheduleRequest = "reschedule_request";
    public const string RescheduleApproved = "reschedule_approved";
    public const string RescheduleRejected = "reschedule_rejected";
    public const string GroupClassEnrolled = "group_class_enrolled";
    public const string GroupClassCancelled = "group_class_cancelled";
    public const string GroupClassCompleted = "group_class_completed";
    public const string CourseEnrolled = "course_enrolled";
    public const string CourseReviewResult = "course_review_result";
    public const string NewReview = "new_review";
    public const string VerificationApproved = "verification_approved";
    public const string VerificationRejected = "verification_rejected";
    public const string MentorPublished = "mentor_published";
    public const string UnreadMessages = "unread_messages";
    public const string PayoutProcessed = "payout_processed";
    public const string DisputeOpened = "dispute_opened";
    public const string DisputeResolved = "dispute_resolved";
}
