using RentControlSystem.Tenancy.API.Models;
using System.ComponentModel.DataAnnotations;

namespace RentControlSystem.Tenancy.API.DTOs
{
    // Property DTOs
    public class CreatePropertyDto
    {
        [Required]
        public string LandlordId { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string PropertyCode { get; set; } = string.Empty;

        [Required]
        public PropertyType PropertyType { get; set; }

        [Required]
        [MaxLength(500)]
        public string Address { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? GhanaPostGpsAddress { get; set; }

        [Required]
        [MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Region { get; set; } = string.Empty;

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        public decimal? SizeInSqMeters { get; set; }
        public int? NumberOfBedrooms { get; set; }
        public int? NumberOfBathrooms { get; set; }
        public int? NumberOfLivingRooms { get; set; }
        public int? NumberOfKitchens { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal MonthlyRent { get; set; }

        public bool IsFurnished { get; set; }
        public bool HasParking { get; set; }
        public bool HasSecurity { get; set; }
        public bool HasWater { get; set; }
        public bool HasElectricity { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(1000)]
        public string? AdditionalNotes { get; set; }
    }

    public class UpdatePropertyDto
    {
        [MaxLength(500)]
        public string? Address { get; set; }

        [MaxLength(100)]
        public string? GhanaPostGpsAddress { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(100)]
        public string? Region { get; set; }

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        public decimal? SizeInSqMeters { get; set; }
        public int? NumberOfBedrooms { get; set; }
        public int? NumberOfBathrooms { get; set; }
        public int? NumberOfLivingRooms { get; set; }
        public int? NumberOfKitchens { get; set; }

        [Range(0.01, double.MaxValue)]
        public decimal? MonthlyRent { get; set; }

        public bool? IsFurnished { get; set; }
        public bool? HasParking { get; set; }
        public bool? HasSecurity { get; set; }
        public bool? HasWater { get; set; }
        public bool? HasElectricity { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(1000)]
        public string? AdditionalNotes { get; set; }

        public PropertyStatus? PropertyStatus { get; set; }
    }

    public class PropertyResponseDto
    {
        public Guid Id { get; set; }
        public string PropertyCode { get; set; } = string.Empty;
        public string LandlordId { get; set; } = string.Empty;
        public PropertyType PropertyType { get; set; }
        public string Address { get; set; } = string.Empty;
        public string? GhanaPostGpsAddress { get; set; }
        public string City { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public decimal? SizeInSqMeters { get; set; }
        public int? NumberOfBedrooms { get; set; }
        public int? NumberOfBathrooms { get; set; }
        public int? NumberOfLivingRooms { get; set; }
        public int? NumberOfKitchens { get; set; }
        public decimal MonthlyRent { get; set; }
        public PropertyStatus PropertyStatus { get; set; }
        public bool IsFurnished { get; set; }
        public bool HasParking { get; set; }
        public bool HasSecurity { get; set; }
        public bool HasWater { get; set; }
        public bool HasElectricity { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<PropertyAmenityDto> Amenities { get; set; } = new();
        public List<PropertyImageDto> Images { get; set; } = new();
        public List<RentAdjustmentDto> RentAdjustments { get; set; } = new();
        public int? CurrentOccupancyCount { get; set; }
    }

    public class PropertyAmenityDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsAvailable { get; set; }
        public string? AdditionalInfo { get; set; }
    }

    public class PropertyImageDto
    {
        public Guid Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsPrimary { get; set; }
        public int DisplayOrder { get; set; }
        public DateTime UploadedAt { get; set; }
    }

    // Tenancy Agreement DTOs
    public class CreateTenancyDto
    {
        [Required]
        public Guid PropertyId { get; set; }

        [Required]
        public string LandlordId { get; set; } = string.Empty;

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal MonthlyRent { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? SecurityDeposit { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        public PaymentFrequency PaymentFrequency { get; set; } = PaymentFrequency.Monthly;

        public Guid? LeaseTermId { get; set; }
        public Guid? NoticePeriodId { get; set; }
        public string? SpecialTerms { get; set; }
    }

    public class UpdateTenancyDto
    {
        [Range(0.01, double.MaxValue)]
        public decimal? MonthlyRent { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? SecurityDeposit { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public PaymentFrequency? PaymentFrequency { get; set; }
        public TenancyStatus? Status { get; set; }
        public Guid? LeaseTermId { get; set; }
        public Guid? NoticePeriodId { get; set; }
        public string? TerminationReason { get; set; }
        public DateTime? ActualVacateDate { get; set; }
        public string? SpecialTerms { get; set; }
    }

    public class TenancyAgreementResponseDto
    {
        public Guid Id { get; set; }
        public string AgreementNumber { get; set; } = string.Empty;
        public Guid PropertyId { get; set; }
        public string LandlordId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public decimal MonthlyRent { get; set; }
        public decimal SecurityDeposit { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public TenancyStatus Status { get; set; }
        public PaymentFrequency PaymentFrequency { get; set; }
        public DateTime? NextPaymentDate { get; set; }
        public DateTime? ActualVacateDate { get; set; }
        public string? TerminationReason { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public PropertyResponseDto? Property { get; set; }
        public LeaseTermDto? LeaseTerm { get; set; }
        public NoticePeriodDto? NoticePeriod { get; set; }
   //     public List<RentPaymentDto> RecentPayments { get; set; } = new();
      //  public List<AgreementDocumentDto> Documents { get; set; } = new();
        public decimal? TotalPaid { get; set; }
        public decimal? BalanceDue { get; set; }
    }

    public class LeaseTermDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int DurationInMonths { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
    }

    public class NoticePeriodDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int DurationInDays { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
    }

    // Rent Payment DTOs
    public class CreateRentPaymentDto
    {
        [Required]
        public Guid TenancyAgreementId { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }

        [Required]
        public PaymentMethod PaymentMethod { get; set; }

        [Required]
        public DateTime PeriodStartDate { get; set; }

        [Required]
        public DateTime PeriodEndDate { get; set; }

        public bool IsAdvancePayment { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? Notes { get; set; }
    }

    public class VerifyPaymentDto
    {
        [Required]
        public string TransactionId { get; set; } = string.Empty;

        [Required]
        public PaymentStatus PaymentStatus { get; set; }

        public string? VerificationNotes { get; set; }
    }

    public class RentPaymentResponseDto
    {
        public Guid Id { get; set; }
        public Guid TenancyAgreementId { get; set; }
        public string TenantId { get; set; } = string.Empty;
        public string LandlordId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public string? TransactionId { get; set; }
        public string? ReferenceNumber { get; set; }
        public DateTime PaymentDate { get; set; }
        public DateTime PeriodStartDate { get; set; }
        public DateTime PeriodEndDate { get; set; }
        public bool IsAdvancePayment { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public TenancyAgreementSimpleDto? TenancyAgreement { get; set; }
    }

    public class TenancyAgreementSimpleDto
    {
        public Guid Id { get; set; }
        public string AgreementNumber { get; set; } = string.Empty;
        public decimal MonthlyRent { get; set; }
        public string? PropertyAddress { get; set; }
    }

    // Document DTOs
    public class UploadDocumentDto
    {
        [Required]
        public Guid TenancyAgreementId { get; set; }

        [Required]
        public DocumentType DocumentType { get; set; }

        [Required]
        public IFormFile File { get; set; } = null!;

        [MaxLength(500)]
        public string? Description { get; set; }
    }

    public class AgreementDocumentResponseDto
    {
        public Guid Id { get; set; }
        public Guid TenancyAgreementId { get; set; }
        public DocumentType DocumentType { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FileSize { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime UploadedAt { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
        public string? VerificationNotes { get; set; }
        public DateTime? VerifiedAt { get; set; }
    }

    // Rent Adjustment DTOs
    public class CreateRentAdjustmentDto
    {
        [Required]
        public Guid PropertyId { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal NewRent { get; set; }

        [Required]
        public DateTime EffectiveDate { get; set; }

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        public string? ApprovalReference { get; set; }
    }

    public class RentAdjustmentDto
    {
        public Guid Id { get; set; }
        public Guid PropertyId { get; set; }
        public decimal PreviousRent { get; set; }
        public decimal NewRent { get; set; }
        public decimal PercentageChange { get; set; }
        public DateTime EffectiveDate { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? ApprovalReference { get; set; }
        public DateTime ApprovedAt { get; set; }
        public string ApprovedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    // Search and Filter DTOs
    public class TenancySearchDto
    {
        public string? LandlordId { get; set; }
        public string? TenantId { get; set; }
        public Guid? PropertyId { get; set; }
        public TenancyStatus? Status { get; set; }
        public DateTime? StartDateFrom { get; set; }
        public DateTime? StartDateTo { get; set; }
        public DateTime? EndDateFrom { get; set; }
        public DateTime? EndDateTo { get; set; }
        public string? AgreementNumber { get; set; }
        public bool? IsActive { get; set; }
    }

    public class PropertySearchDto
    {
        public string? LandlordId { get; set; }
        public string? City { get; set; }
        public string? Region { get; set; }
        public PropertyType? PropertyType { get; set; }
        public PropertyStatus? PropertyStatus { get; set; }
        public decimal? MinRent { get; set; }
        public decimal? MaxRent { get; set; }
        public int? MinBedrooms { get; set; }
        public int? MaxBedrooms { get; set; }
        public bool? HasParking { get; set; }
        public bool? HasSecurity { get; set; }
        public bool? IsFurnished { get; set; }
        public string? SearchTerm { get; set; }
    }

    // Statistics DTOs
    public class TenancyStatisticsDto
    {
        public int TotalAgreements { get; set; }
        public int ActiveAgreements { get; set; }
        public int ExpiredAgreements { get; set; }
        public int DraftAgreements { get; set; }
        public decimal TotalMonthlyRent { get; set; }
        public decimal TotalSecurityDeposits { get; set; }
        public Dictionary<string, int> AgreementsByStatus { get; set; } = new();
        public Dictionary<string, int> AgreementsByRegion { get; set; } = new();
        public Dictionary<string, decimal> RentCollectionByMonth { get; set; } = new();
    }

    public class PropertyStatisticsDto
    {
        public int TotalProperties { get; set; }
        public int AvailableProperties { get; set; }
        public int OccupiedProperties { get; set; }
        public decimal AverageRent { get; set; }
        public decimal HighestRent { get; set; }
        public decimal LowestRent { get; set; }
        public Dictionary<string, int> PropertiesByType { get; set; } = new();
        public Dictionary<string, int> PropertiesByRegion { get; set; } = new();
    }

    // Renewal DTOs
    public class RenewTenancyDto
    {
        [Required]
        public DateTime NewEndDate { get; set; }

        [Range(0.01, double.MaxValue)]
        public decimal? NewMonthlyRent { get; set; }

        public string? RenewalNotes { get; set; }
    }

    // Notice DTOs
    public class IssueNoticeDto
    {
        [Required]
        public NoticeType NoticeType { get; set; } // RentIncrease, Termination, Renewal, etc.

        [Required]
        public DateTime EffectiveDate { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? AdditionalNotes { get; set; }

        public List<string>? AttachmentUrls { get; set; }
    }

    public enum NoticeType
    {
        RentIncrease,
        TerminationByLandlord,
        TerminationByTenant,
        Renewal,
        Maintenance,
        Other
    }
}