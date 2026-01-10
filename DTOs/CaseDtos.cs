using RentControlSystem.CaseManagement.API.DTOs;
using RentControlSystem.CaseManagement.API.Models;
using System.ComponentModel.DataAnnotations;

namespace RentControlSystem.API.DTOs
{
    public class CreateCaseDto
    {
        [Required(ErrorMessage = "Case type is required")]
        public CaseType CaseType { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required")]
        [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
        public string Description { get; set; } = string.Empty;

        // Complainant Information
        [Required(ErrorMessage = "Complainant ID is required")]
        public string ComplainantId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Complainant name is required")]
        public string ComplainantName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Complainant phone is required")]
        [Phone(ErrorMessage = "Invalid phone number format")]
        public string ComplainantPhone { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string ComplainantEmail { get; set; } = string.Empty;

        // Respondent Information
        [Required(ErrorMessage = "Respondent ID is required")]
        public string RespondentId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Respondent name is required")]
        public string RespondentName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Respondent phone is required")]
        [Phone(ErrorMessage = "Invalid phone number format")]
        public string RespondentPhone { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string RespondentEmail { get; set; } = string.Empty;

        // Related Entities
        public Guid? PropertyId { get; set; }
        public Guid? TenancyAgreementId { get; set; }
        public string? PropertyAddress { get; set; }

        // Case Details
        [Required(ErrorMessage = "Incident date is required")]
        public DateTime IncidentDate { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Claim amount must be positive")]
        public decimal? ClaimAmount { get; set; }

        public string? InitialNote { get; set; }
    }

    public class UpdateCaseDto
    {
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        public string? Title { get; set; }

        [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
        public string? Description { get; set; }

        public CaseStatus? Status { get; set; }
        public CasePriority? Priority { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Claim amount must be positive")]
        public decimal? ClaimAmount { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Awarded amount must be positive")]
        public decimal? AwardedAmount { get; set; }

        public ResolutionType? Resolution { get; set; }
        public string? ResolutionDetails { get; set; }

        // Note: TerminationReason and ActualVacateDate have been removed as they don't exist on Case model
    }

    public class CaseResponseDto
    {
        public Guid Id { get; set; }
        public string CaseNumber { get; set; } = string.Empty;
        public CaseType CaseType { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // Parties
        public string ComplainantId { get; set; } = string.Empty;
        public string ComplainantName { get; set; } = string.Empty;
        public string ComplainantPhone { get; set; } = string.Empty;
        public string ComplainantEmail { get; set; } = string.Empty;

        public string RespondentId { get; set; } = string.Empty;
        public string RespondentName { get; set; } = string.Empty;
        public string RespondentPhone { get; set; } = string.Empty;
        public string RespondentEmail { get; set; } = string.Empty;

        // Related Entities
        public Guid? PropertyId { get; set; }
        public Guid? TenancyAgreementId { get; set; }
        public string? PropertyAddress { get; set; }

        // Case Details
        public CaseStatus Status { get; set; }
        public CasePriority Priority { get; set; }
        public DateTime IncidentDate { get; set; }
        public DateTime? ResolutionDate { get; set; }
        public decimal? ClaimAmount { get; set; }
        public decimal? AwardedAmount { get; set; }
        public ResolutionType? Resolution { get; set; }
        public string? ResolutionDetails { get; set; }
        public bool IsActive { get; set; }

        // Assigned Personnel
        public string? AssignedOfficerId { get; set; }
        public string? AssignedOfficerName { get; set; }
        public string? AssignedMediatorId { get; set; }
        public string? AssignedMediatorName { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }

        // Statistics
        public int DocumentCount { get; set; }
        public int HearingCount { get; set; }
        public int NoteCount { get; set; }
        public int CommunicationCount { get; set; }

        // Navigation Properties (simplified)
        public PropertyDto? Property { get; set; }
        public TenancyAgreementDto? TenancyAgreement { get; set; }
        public List<CaseDocumentDto> Documents { get; set; } = new();
        public List<UpdateHearingDto> Hearings { get; set; } = new();
        public List<CaseNoteDto> Notes { get; set; } = new();
        public List<CaseParticipantDto> Participants { get; set; } = new();
        public List<CaseUpdateDto> Updates { get; set; } = new();
    }

    public class CaseSearchDto
    {
        public string? CaseNumber { get; set; }
        public string? Title { get; set; }
        public CaseType? CaseType { get; set; }
        public CaseStatus? Status { get; set; }
        public CasePriority? Priority { get; set; }
        public string? ComplainantId { get; set; }
        public string? RespondentId { get; set; }
        public string? ComplainantName { get; set; }
        public string? RespondentName { get; set; }
        public Guid? PropertyId { get; set; }
        public Guid? TenancyAgreementId { get; set; }
        public string? AssignedOfficerId { get; set; }
        public string? AssignedMediatorId { get; set; }
        public DateTime? CreatedFrom { get; set; }
        public DateTime? CreatedTo { get; set; }
        public DateTime? IncidentDateFrom { get; set; }
        public DateTime? IncidentDateTo { get; set; }
        public bool? IsActive { get; set; }
    }

    public class AssignCaseDto
    {
        [Required(ErrorMessage = "Officer ID is required")]
        public string OfficerId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Officer name is required")]
        public string OfficerName { get; set; } = string.Empty;

        public string? MediatorId { get; set; }
        public string? MediatorName { get; set; }
    }

    public class ResolveCaseDto
    {
        [Required(ErrorMessage = "Resolution type is required")]
        public ResolutionType Resolution { get; set; }

        [Required(ErrorMessage = "Resolution details are required")]
        [StringLength(1000, ErrorMessage = "Details cannot exceed 1000 characters")]
        public string ResolutionDetails { get; set; } = string.Empty;

        [Range(0, double.MaxValue, ErrorMessage = "Awarded amount must be positive")]
        public decimal? AwardedAmount { get; set; }

        public string? Notes { get; set; }
    }

    public class CaseStatisticsDto
    {
        public int TotalCases { get; set; }
        public int DraftCases { get; set; }
        public int SubmittedCases { get; set; }
        public int UnderReviewCases { get; set; }
        public int InvestigationCases { get; set; }
        public int HearingScheduledCases { get; set; }
        public int ResolvedCases { get; set; }
        public int ClosedCases { get; set; }
        public int ReopenedCases { get; set; }
        public int WithdrawnCases { get; set; }
        public int DismissedCases { get; set; }

        public List<CaseTypeCountDto> CasesByType { get; set; } = new();
        public List<PriorityCountDto> CasesByPriority { get; set; } = new();
        public List<MonthCountDto> CasesByMonth { get; set; } = new();

        public double AverageResolutionTime { get; set; } // In days
        public decimal ResolutionRate { get; set; } // Percentage

        public int CasesRequiringAttention { get; set; }
        public int OverdueCases { get; set; }
        public int UpcomingHearings { get; set; }
    }

    public class CaseDashboardDto
    {
        public Guid Id { get; set; }
        public string CaseNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public CaseType CaseType { get; set; }
        public CaseStatus Status { get; set; }
        public CasePriority Priority { get; set; }
        public string ComplainantName { get; set; } = string.Empty;
        public string RespondentName { get; set; } = string.Empty;
        public string? AssignedOfficerName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? NextHearingDate { get; set; }
        public int DaysSinceCreation { get; set; }
        public bool RequiresAttention { get; set; }
    }

    public class CaseTypeCountDto
    {
        public CaseType CaseType { get; set; }
        public int Count { get; set; }
        public decimal Percentage { get; set; }
    }

    public class PriorityCountDto
    {
        public CasePriority Priority { get; set; }
        public int Count { get; set; }
        public decimal Percentage { get; set; }
    }

    public class MonthCountDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int Count { get; set; }
    }

    // Case Document DTOs
    public class UploadCaseDocumentDto
    {
        [Required(ErrorMessage = "Case ID is required")]
        public Guid CaseId { get; set; }

        [Required(ErrorMessage = "Document type is required")]
        public DocumentEvidenceType DocumentType { get; set; }

        [Required(ErrorMessage = "File name is required")]
        public string FileName { get; set; } = string.Empty;

        [Required(ErrorMessage = "File is required")]
        public IFormFile File { get; set; } = null!;

        public string? Description { get; set; }
    }

    public class CaseDocumentDto
    {
        public Guid Id { get; set; }
        public Guid CaseId { get; set; }
        public DocumentEvidenceType DocumentType { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileSize { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime UploadedAt { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
        public string? VerificationNotes { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public string? VerifiedBy { get; set; }
    }

    // Case Note DTOs
    public class CaseNoteDto
    {
        public Guid Id { get; set; }
        public Guid CaseId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public string? CreatedByName { get; set; }
        public bool IsInternal { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }

    // Case Participant DTOs
    public class CaseParticipantDto
    {
        public Guid Id { get; set; }
        public Guid CaseId { get; set; }
        public string ParticipantId { get; set; } = string.Empty;
        public string ParticipantName { get; set; } = string.Empty;
        public string? ParticipantEmail { get; set; }
        public string? ParticipantPhone { get; set; }
        public ParticipantType ParticipantType { get; set; }
        public string? Role { get; set; }
        public string? Organization { get; set; }
        public bool IsPrimaryContact { get; set; }
        public string? Address { get; set; }
        public string? Notes { get; set; }
        public DateTime AddedAt { get; set; }
        public string AddedBy { get; set; } = string.Empty;
    }

    // Case Update DTOs
    public class CaseUpdateDto
    {
        public Guid Id { get; set; }
        public Guid CaseId { get; set; }
        public string UpdateType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Case Communication DTOs
    public class SendCommunicationDto
    {
        [Required(ErrorMessage = "Case ID is required")]
        public Guid CaseId { get; set; }

        [Required(ErrorMessage = "Communication type is required")]
        public string CommunicationType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Subject is required")]
        [StringLength(200, ErrorMessage = "Subject cannot exceed 200 characters")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Content is required")]
        public string Content { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Invalid sender email")]
        public string? SenderEmail { get; set; }

        [Phone(ErrorMessage = "Invalid sender phone")]
        public string? SenderPhone { get; set; }

        [Required(ErrorMessage = "Recipient ID is required")]
        public string RecipientId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Recipient name is required")]
        public string RecipientName { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Invalid recipient email")]
        public string? RecipientEmail { get; set; }

        [Phone(ErrorMessage = "Invalid recipient phone")]
        public string? RecipientPhone { get; set; }

        public string? AttachmentPath { get; set; }
    }

    public class CaseCommunicationDto
    {
        public Guid Id { get; set; }
        public Guid CaseId { get; set; }
        public string CommunicationType { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string? SenderEmail { get; set; }
        public string? SenderPhone { get; set; }
        public string RecipientId { get; set; } = string.Empty;
        public string RecipientName { get; set; } = string.Empty;
        public string? RecipientEmail { get; set; }
        public string? RecipientPhone { get; set; }
        public string? AttachmentPath { get; set; }
        public bool IsSent { get; set; }
        public DateTime? SentAt { get; set; }
        public bool IsDelivered { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public string? StatusMessage { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Simplified DTOs for navigation properties
    public class PropertyDto
    {
        public Guid Id { get; set; }
        public string PropertyCode { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
    }

    public class TenancyAgreementDto
    {
        public Guid Id { get; set; }
        public string AgreementNumber { get; set; } = string.Empty;
        public decimal MonthlyRent { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    // RCD Officer DTOs
    public class RCDOfficerDto
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? EmployeeNumber { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Department { get; set; }
        public string? Designation { get; set; }
        public string? Region { get; set; }
        public string? District { get; set; }
        public bool CanPresideHearings { get; set; }
        public bool CanAssignCases { get; set; }
        public bool CanCloseCases { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}