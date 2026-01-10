using RentControlSystem.CaseManagement.API.Models;
using System.ComponentModel.DataAnnotations;

namespace RentControlSystem.CaseManagement.API.DTOs
{
    public class ScheduleMediationDto
    {
        [Required(ErrorMessage = "Case ID is required")]
        public Guid CaseId { get; set; }

        [Required(ErrorMessage = "Mediator ID is required")]
        public string MediatorId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mediator name is required")]
        public string MediatorName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mediation date is required")]
        public DateTime MediationDate { get; set; }

        [Required(ErrorMessage = "Start time is required")]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "End time is required")]
        public TimeSpan EndTime { get; set; }

        [Required(ErrorMessage = "Location is required")]
        public string Location { get; set; } = string.Empty;

        public string? VirtualMeetingLink { get; set; }

        [StringLength(500, ErrorMessage = "Agenda cannot exceed 500 characters")]
        public string? Agenda { get; set; }
    }

    public class RecordMediationOutcomeDto
    {
        [Required(ErrorMessage = "Outcome is required")]
        public MediationOutcome Outcome { get; set; }

        [Required(ErrorMessage = "Details are required")]
        [StringLength(2000, ErrorMessage = "Details cannot exceed 2000 characters")]
        public string Details { get; set; } = string.Empty;

        public decimal? SettlementAmount { get; set; }
        public DateTime? SettlementDate { get; set; }
        public string? AgreementFilePath { get; set; }
        public bool IsBinding { get; set; } = false;
    }

    public class MediationSessionDto
    {
        public Guid Id { get; set; }
        public Guid CaseId { get; set; }
        public string MediatorId { get; set; } = string.Empty;
        public string MediatorName { get; set; } = string.Empty;
        public DateTime MediationDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Location { get; set; } = string.Empty;
        public string? VirtualMeetingLink { get; set; }
        public string? Agenda { get; set; }
        public MediationStatus Status { get; set; }
        public MediationOutcome? Outcome { get; set; }
        public string? OutcomeDetails { get; set; }
        public decimal? SettlementAmount { get; set; }
        public bool IsBinding { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public CaseDto? Case { get; set; }
        public List<MediationParticipantDto> Participants { get; set; } = new();
    }

    public class MediatorDto
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? LicenseNumber { get; set; }
        public string? Specialization { get; set; }
        public string? Qualifications { get; set; }
        public int YearsOfExperience { get; set; }
        public decimal SuccessRate { get; set; }
        public bool IsActive { get; set; }
        public int MaxActiveCases { get; set; }
        public int CurrentActiveCases { get; set; }
        public bool IsAvailable { get; set; }
        public DateTime? NextAvailableDate { get; set; }
    }

    public class MediationParticipantDto
    {
        public Guid Id { get; set; }
        public string ParticipantId { get; set; } = string.Empty;
        public string ParticipantName { get; set; } = string.Empty;
        public string? ParticipantEmail { get; set; }
        public string? ParticipantPhone { get; set; }
        public ParticipantType ParticipantType { get; set; }
        public string? Role { get; set; }
        public bool HasConfirmed { get; set; }
        public bool Attended { get; set; }
    }

    // Additional enums for mediation
    public enum MediationStatus
    {
        Scheduled,
        InProgress,
        Completed,
        Cancelled,
        Rescheduled
    }

    public enum MediationOutcome
    {
        Settled,
        PartiallySettled,
        NotSettled,
        Withdrawn,
        ReferredToHearing
    }
}