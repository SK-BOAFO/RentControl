using RentControlSystem.CaseManagement.API.Models;
using System.ComponentModel.DataAnnotations;

namespace RentControlSystem.CaseManagement.API.DTOs
{
    public class ScheduleHearingDto
    {
        [Required(ErrorMessage = "Case ID is required")]
        public Guid CaseId { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required(ErrorMessage = "Hearing date is required")]
        public DateTime HearingDate { get; set; }

        [Required(ErrorMessage = "Start time is required")]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "End time is required")]
        public TimeSpan EndTime { get; set; }

        [Required(ErrorMessage = "Location is required")]
        public string Location { get; set; } = string.Empty;

        public string? VirtualMeetingLink { get; set; }

        [Required(ErrorMessage = "Presiding officer ID is required")]
        public string PresidingOfficerId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Presiding officer name is required")]
        public string PresidingOfficerName { get; set; } = string.Empty;

        public string? ClerkId { get; set; }
        public string? ClerkName { get; set; }
    }

    public class UpdateHearingDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public DateTime? HearingDate { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public string? Location { get; set; }
        public string? VirtualMeetingLink { get; set; }
        public HearingStatus? Status { get; set; }
        public string? PresidingOfficerId { get; set; }
        public string? PresidingOfficerName { get; set; }
        public string? ClerkId { get; set; }
        public string? ClerkName { get; set; }
        public string? Outcome { get; set; }
        public string? Minutes { get; set; }
    }

    public class HearingResponseDto
    {
        public Guid Id { get; set; }
        public Guid CaseId { get; set; }
        public string HearingNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime HearingDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Location { get; set; } = string.Empty;
        public string? VirtualMeetingLink { get; set; }
        public HearingStatus Status { get; set; }
        public string? PresidingOfficerId { get; set; }
        public string? PresidingOfficerName { get; set; }
        public string? ClerkId { get; set; }
        public string? ClerkName { get; set; }
        public string? Outcome { get; set; }
        public string? Minutes { get; set; }
        public string? MinutesFilePath { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation
        public CaseDto? Case { get; set; }
        public List<HearingParticipantDto> Participants { get; set; } = new();
    }

    public class RecordHearingOutcomeDto
    {
        [Required(ErrorMessage = "Outcome is required")]
        [StringLength(1000, ErrorMessage = "Outcome cannot exceed 1000 characters")]
        public string Outcome { get; set; } = string.Empty;

        [StringLength(5000, ErrorMessage = "Minutes cannot exceed 5000 characters")]
        public string? Minutes { get; set; }

        public HearingStatus Status { get; set; } = HearingStatus.Completed;
    }

    public class AddParticipantDto
    {
        [Required(ErrorMessage = "Participant ID is required")]
        public string ParticipantId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Participant name is required")]
        public string ParticipantName { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string? ParticipantEmail { get; set; }

        [Phone(ErrorMessage = "Invalid phone number")]
        public string? ParticipantPhone { get; set; }

        [Required(ErrorMessage = "Participant type is required")]
        public ParticipantType ParticipantType { get; set; }

        public string? Role { get; set; }
        public string? Organization { get; set; }
        public bool IsRequired { get; set; } = true;
    }

    public class HearingCalendarDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<HearingCalendarItemDto> Hearings { get; set; } = new();
    }

    public class HearingCalendarItemDto
    {
        public Guid HearingId { get; set; }
        public string HearingNumber { get; set; } = string.Empty;
        public Guid CaseId { get; set; }
        public string CaseNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime HearingDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Location { get; set; } = string.Empty;
        public string? VirtualMeetingLink { get; set; }
        public HearingStatus Status { get; set; }
        public string PresidingOfficerName { get; set; } = string.Empty;
        public string ComplainantName { get; set; } = string.Empty;
        public string RespondentName { get; set; } = string.Empty;
        public bool IsToday { get; set; }
        public bool IsPast { get; set; }
        public bool IsUpcoming { get; set; }
    }

    // Simplified DTO for navigation
    public class CaseDto
    {
        public Guid Id { get; set; }
        public string CaseNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public CaseType CaseType { get; set; }
        public CaseStatus Status { get; set; }
    }

    public class HearingParticipantDto
    {
        public Guid Id { get; set; }
        public string ParticipantId { get; set; } = string.Empty;
        public string ParticipantName { get; set; } = string.Empty;
        public string? ParticipantEmail { get; set; }
        public string? ParticipantPhone { get; set; }
        public ParticipantType ParticipantType { get; set; }
        public string? Role { get; set; }
        public string? Organization { get; set; }
        public bool IsRequired { get; set; }
        public bool HasConfirmedAttendance { get; set; }
        public bool Attended { get; set; }
        public DateTime? CheckedInAt { get; set; }
        public DateTime? CheckedOutAt { get; set; }
        public string? Notes { get; set; }
    }
}