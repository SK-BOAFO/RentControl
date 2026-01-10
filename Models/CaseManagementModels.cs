using RentControlSystem.Tenancy.API.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentControlSystem.CaseManagement.API.Models
{
    public enum CaseStatus
    {
        Draft,
        Submitted,
        UnderReview,
        Investigation,
        ScheduledForHearing,
        HearingInProgress,
        DecisionPending,
        Resolved,
        Closed,
        Reopened,
        Withdrawn,
        Dismissed
    }

    public enum CasePriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum CaseType
    {
        RentArrears,
        PropertyMaintenance,
        IllegalEviction,
        RentIncreaseDispute,
        SecurityDepositDispute,
        Harassment,
        UtilityDispute,
        RepairNeglect,
        Overcrowding,
        HealthAndSafety,
        NoiseComplaint,
        LeaseViolation,
        Other
    }

    public enum HearingStatus
    {
        Scheduled,
        InProgress,
        Adjourned,
        Completed,
        Cancelled,
        Rescheduled
    }

    public enum ParticipantType
    {
        Complainant,
        Respondent,
        Witness,
        LegalRepresentative,
        ExpertWitness,
        Interpreter,
        Observer
    }

    public enum DocumentEvidenceType
    {
        ComplaintLetter,
        LeaseAgreement,
        PaymentReceipts,
        Photographs,
        MedicalReports,
        WitnessStatements,
        PoliceReport,
        ExpertReport,
        Correspondence,
        CourtDocuments,
        Other
    }

    public enum ResolutionType
    {
        Settlement,
        MediationAgreement,
        ArbitrationAward,
        Ruling,
        ConsentOrder,
        Dismissal,
        Withdrawal
    }

    public enum MediationStatus
    {
        Requested,
        Scheduled,
        InProgress,
        Adjourned,
        Completed,
        Cancelled,
        Failed,
        Successful
    }

    public enum MediationParticipantRole
    {
        Party,
        Representative,
        Witness,
        Observer,
        Mediator,
        SupportPerson
    }

    public class Case
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string CaseNumber { get; set; } = string.Empty;
        public CaseType CaseType { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // Parties involved
        public string ComplainantId { get; set; } = string.Empty;
        public string ComplainantName { get; set; } = string.Empty;
        public string ComplainantPhone { get; set; } = string.Empty;
        public string ComplainantEmail { get; set; } = string.Empty;

        public string RespondentId { get; set; } = string.Empty;
        public string RespondentName { get; set; } = string.Empty;
        public string RespondentPhone { get; set; } = string.Empty;
        public string RespondentEmail { get; set; } = string.Empty;

        // Related entities
        public Guid? PropertyId { get; set; }
        public Guid? TenancyAgreementId { get; set; }
        public string? PropertyAddress { get; set; }

        // Case details
        public CaseStatus Status { get; set; } = CaseStatus.Draft;
        public CasePriority Priority { get; set; } = CasePriority.Medium;
        public DateTime IncidentDate { get; set; }
        public DateTime? ResolutionDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ClaimAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? AwardedAmount { get; set; }

        public ResolutionType? Resolution { get; set; }
        public string? ResolutionDetails { get; set; }
        public bool IsActive { get; set; } = true;

        // Assigned personnel - FIXED: Changed from string? to Guid? to match Mediator.Id and RCDOfficer.Id
        public Guid? AssignedOfficerId { get; set; }
        public string? AssignedOfficerName { get; set; }
        public Guid? AssignedMediatorId { get; set; } // FIXED: Changed from string? to Guid?
        public string? AssignedMediatorName { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SubmittedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string? UpdatedBy { get; set; }

        // Navigation properties
        public virtual Property? Property { get; set; }
        public virtual TenancyAgreement? TenancyAgreement { get; set; }

        // Added navigation properties for assigned personnel
        [ForeignKey("AssignedMediatorId")]
        public virtual Mediator? Mediator { get; set; }

        [ForeignKey("AssignedOfficerId")]
        public virtual RCDOfficer? Officer { get; set; }

        public virtual ICollection<CaseDocument> Documents { get; set; } = new List<CaseDocument>();
        public virtual ICollection<Hearing> Hearings { get; set; } = new List<Hearing>();
        public virtual ICollection<CaseNote> Notes { get; set; } = new List<CaseNote>();
        public virtual ICollection<CaseParticipant> Participants { get; set; } = new List<CaseParticipant>();
        public virtual ICollection<CaseUpdate> Updates { get; set; } = new List<CaseUpdate>();
        public virtual ICollection<CaseCommunication> Communications { get; set; } = new List<CaseCommunication>();
        public virtual ICollection<MediationSession> MediationSessions { get; set; } = new List<MediationSession>();
    }

    public class CaseDocument
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CaseId { get; set; }
        public DocumentEvidenceType DocumentType { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileSize { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public string UploadedBy { get; set; } = string.Empty;
        public bool IsVerified { get; set; } = false;
        public string? VerificationNotes { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public string? VerifiedBy { get; set; }

        // Navigation properties
        [ForeignKey("CaseId")]
        public virtual Case? Case { get; set; }
    }

    public class Hearing
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CaseId { get; set; }
        public string HearingNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime HearingDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Location { get; set; } = string.Empty;
        public string? VirtualMeetingLink { get; set; }
        public HearingStatus Status { get; set; } = HearingStatus.Scheduled;

        // FIXED: Changed from string? to Guid? to match RCDOfficer.Id
        public Guid? PresidingOfficerId { get; set; }
        public string? PresidingOfficerName { get; set; }
        public Guid? ClerkId { get; set; }
        public string? ClerkName { get; set; }

        public string? Outcome { get; set; }
        public string? Minutes { get; set; }
        public string? MinutesFilePath { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string? UpdatedBy { get; set; }

        // Navigation properties
        [ForeignKey("CaseId")]
        public virtual Case? Case { get; set; }

        [ForeignKey("PresidingOfficerId")]
        public virtual RCDOfficer? PresidingOfficer { get; set; }

        public virtual ICollection<HearingParticipant> Participants { get; set; } = new List<HearingParticipant>();
    }

    public class HearingParticipant
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid HearingId { get; set; }
        public string ParticipantId { get; set; } = string.Empty;
        public string ParticipantName { get; set; } = string.Empty;
        public string? ParticipantEmail { get; set; }
        public string? ParticipantPhone { get; set; }
        public ParticipantType ParticipantType { get; set; }
        public string? Role { get; set; }
        public string? Organization { get; set; }
        public bool IsRequired { get; set; } = true;
        public bool HasConfirmedAttendance { get; set; } = false;
        public DateTime? ConfirmedAt { get; set; }
        public bool Attended { get; set; } = false;
        public DateTime? CheckedInAt { get; set; }
        public DateTime? CheckedOutAt { get; set; }
        public string? Notes { get; set; }

        // Navigation properties
        [ForeignKey("HearingId")]
        public virtual Hearing? Hearing { get; set; }
    }

    public class CaseNote
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CaseId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public string? CreatedByName { get; set; }
        public bool IsInternal { get; set; } = false; // Internal notes not visible to parties
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        // Navigation properties
        [ForeignKey("CaseId")]
        public virtual Case? Case { get; set; }
    }

    public class CaseParticipant
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CaseId { get; set; }
        public string ParticipantId { get; set; } = string.Empty;
        public string ParticipantName { get; set; } = string.Empty;
        public string? ParticipantEmail { get; set; }
        public string? ParticipantPhone { get; set; }
        public ParticipantType ParticipantType { get; set; }
        public string? Role { get; set; }
        public string? Organization { get; set; }
        public bool IsPrimaryContact { get; set; } = false;
        public string? Address { get; set; }
        public string? Notes { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
        public string AddedBy { get; set; } = string.Empty;

        // Navigation properties
        [ForeignKey("CaseId")]
        public virtual Case? Case { get; set; }
    }

    public class CaseUpdate
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CaseId { get; set; }
        public string UpdateType { get; set; } = string.Empty; // StatusChange, DocumentAdded, HearingScheduled, etc.
        public string Description { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("CaseId")]
        public virtual Case? Case { get; set; }
    }

    public class CaseCommunication
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CaseId { get; set; }
        public string CommunicationType { get; set; } = string.Empty; // Email, SMS, Letter, PhoneCall, Meeting
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
        public bool IsSent { get; set; } = false;
        public DateTime? SentAt { get; set; }
        public bool IsDelivered { get; set; } = false;
        public DateTime? DeliveredAt { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }
        public string? StatusMessage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("CaseId")]
        public virtual Case? Case { get; set; }
    }

    public class Mediator
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? LicenseNumber { get; set; }
        public string? Specialization { get; set; }
        public string? Qualifications { get; set; }
        public int YearsOfExperience { get; set; }
        public decimal SuccessRate { get; set; }
        public bool IsActive { get; set; } = true;
        public int MaxActiveCases { get; set; } = 10;
        public int CurrentActiveCases { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual ICollection<Case> Cases { get; set; } = new List<Case>();
        public virtual ICollection<MediationSession> MediationSessions { get; set; } = new List<MediationSession>();
    }

    public class RCDOfficer
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? EmployeeNumber { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Department { get; set; }
        public string? Designation { get; set; }
        public string? Region { get; set; }
        public string? District { get; set; }
        public bool CanPresideHearings { get; set; } = false;
        public bool CanAssignCases { get; set; } = false;
        public bool CanCloseCases { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual ICollection<Case> Cases { get; set; } = new List<Case>();
        public virtual ICollection<Hearing> HearingsPresided { get; set; } = new List<Hearing>();
    }

    public class MediationSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CaseId { get; set; }
        public string SessionNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        // Mediation details
        public MediationStatus Status { get; set; } = MediationStatus.Requested;
        public DateTime? RequestedDate { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public string? Location { get; set; }
        public string? VirtualMeetingLink { get; set; }

        // Assigned mediator
        public Guid? AssignedMediatorId { get; set; }
        public string? AssignedMediatorName { get; set; }

        // Session outcomes
        public bool? AgreementReached { get; set; }
        public string? AgreementSummary { get; set; }
        public string? AgreementDocumentPath { get; set; }
        public string? MediatorNotes { get; set; }
        public string? OutcomeSummary { get; set; }

        // Party feedback
        public bool? ComplainantSatisfied { get; set; }
        public bool? RespondentSatisfied { get; set; }
        public string? ComplainantFeedback { get; set; }
        public string? RespondentFeedback { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string? UpdatedBy { get; set; }

        // Navigation properties
        [ForeignKey("CaseId")]
        public virtual Case? Case { get; set; }

        [ForeignKey("AssignedMediatorId")]
        public virtual Mediator? Mediator { get; set; }

        public virtual ICollection<MediationParticipant> Participants { get; set; } = new List<MediationParticipant>();
        public virtual ICollection<MediationDocument> Documents { get; set; } = new List<MediationDocument>();
    }

    public class MediationParticipant
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid MediationSessionId { get; set; }

        // Participant information
        public string ParticipantId { get; set; } = string.Empty;
        public string ParticipantName { get; set; } = string.Empty;
        public string? ParticipantEmail { get; set; }
        public string? ParticipantPhone { get; set; }
        public MediationParticipantRole Role { get; set; }

        // For parties involved in the case
        public string? CasePartyType { get; set; } // "Complainant" or "Respondent"
        public bool IsPrimaryContact { get; set; } = false;

        // Organization/Representation details
        public string? Organization { get; set; }
        public string? Position { get; set; }
        public string? RepresentationAuthority { get; set; }

        // Attendance and participation
        public bool IsRequired { get; set; } = true;
        public bool HasConfirmedAttendance { get; set; } = false;
        public DateTime? ConfirmedAt { get; set; }
        public bool Attended { get; set; } = false;
        public DateTime? CheckedInAt { get; set; }
        public DateTime? CheckedOutAt { get; set; }

        // Additional information
        public string? SpecialRequirements { get; set; }
        public string? Notes { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
        public string AddedBy { get; set; } = string.Empty;

        // Navigation properties
        [ForeignKey("MediationSessionId")]
        public virtual MediationSession? MediationSession { get; set; }
    }

    public class MediationDocument
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid MediationSessionId { get; set; }

        // Document information
        public string DocumentType { get; set; } = string.Empty; // Agreement, Notes, Evidence, Correspondence, etc.
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileSize { get; set; } = string.Empty;
        public string? Description { get; set; }

        // Confidentiality
        public bool IsConfidential { get; set; } = false;
        public string? ConfidentialityLevel { get; set; } // "Public", "PartiesOnly", "MediatorOnly"

        // Timestamps
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public string UploadedBy { get; set; } = string.Empty;

        // Navigation properties
        [ForeignKey("MediationSessionId")]
        public virtual MediationSession? MediationSession { get; set; }
    }
}