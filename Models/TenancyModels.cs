using NetTopologySuite.Geometries;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentControlSystem.Tenancy.API.Models
{
    public enum TenancyStatus
    {
        Draft,
        Active,
        Expired,
        Terminated,
        Renewed,
        Suspended
    }

    public enum PaymentFrequency
    {
        Monthly,
        Quarterly,
        SemiAnnually,
        Annually,
        Weekly
    }

    public enum PropertyType
    {
        Residential,
        Commercial,
        Industrial,
        MixedUse
    }

    public enum PropertyStatus
    {
        Available,
        Occupied,
        UnderMaintenance,
        Unavailable
    }

    public enum PaymentMethod
    {
        MobileMoney,
        BankTransfer,
        Cash,
        Cheque,
        OnlinePayment
    }

    public enum PaymentStatus
    {
        Pending,
        Completed,
        Failed,
        Refunded,
        PartiallyPaid
    }

    public enum DocumentType
    {
        Agreement,
        ID_Card,
        UtilityBill,
        InspectionReport,
        PaymentReceipt,
        Notice,
        Other
    }

    public class TenancyAgreement
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string AgreementNumber { get; set; } = string.Empty;
        public Guid PropertyId { get; set; }
        public string LandlordId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthlyRent { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SecurityDeposit { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public TenancyStatus Status { get; set; } = TenancyStatus.Draft;
        public PaymentFrequency PaymentFrequency { get; set; } = PaymentFrequency.Monthly;

        // Store NextPaymentDate in database as nullable column
        public DateTime? NextPaymentDate { get; set; }

        // Calculated property for immediate use (not stored in database)
        [NotMapped]
        public DateTime? CalculatedNextPaymentDate
        {
            get
            {
                if (Status != TenancyStatus.Active)
                    return null;

                var currentDate = DateTime.UtcNow;

                // Calculate the number of months since start date
                var monthsSinceStart = ((currentDate.Year - StartDate.Year) * 12)
                    + currentDate.Month - StartDate.Month;

                // If payment hasn't started yet (agreement starts in the future)
                if (monthsSinceStart < 0)
                    return StartDate;

                // Calculate next payment date
                return StartDate.AddMonths(monthsSinceStart + 1);
            }
        }

        public DateTime? ActualVacateDate { get; set; }
        public string? TerminationReason { get; set; }
        public bool IsActive { get; set; } = true;

        // Foreign keys
        public Guid? LeaseTermId { get; set; }
        public Guid? NoticePeriodId { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }

        // Navigation properties
        public virtual Property? Property { get; set; }
        public virtual LeaseTerm? LeaseTerm { get; set; }
        public virtual NoticePeriod? NoticePeriod { get; set; }
        public virtual ICollection<RentPayment> RentPayments { get; set; } = new List<RentPayment>();
        public virtual ICollection<AgreementDocument> Documents { get; set; } = new List<AgreementDocument>();
        public virtual ICollection<TenancyHistory> History { get; set; } = new List<TenancyHistory>();
    }

    public class Property
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string PropertyCode { get; set; } = string.Empty;
        public string LandlordId { get; set; } = string.Empty;
        public PropertyType PropertyType { get; set; }
        public string Address { get; set; } = string.Empty;
        public string? GhanaPostGpsAddress { get; set; }
        public string City { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public Point? Location { get; set; } // Changed from System.Drawing.Point to NetTopologySuite.Geometries.Point

        [Column(TypeName = "decimal(10,2)")]
        public decimal? SizeInSqMeters { get; set; }

        public int? NumberOfBedrooms { get; set; }
        public int? NumberOfBathrooms { get; set; }
        public int? NumberOfLivingRooms { get; set; }
        public int? NumberOfKitchens { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthlyRent { get; set; }

        public PropertyStatus PropertyStatus { get; set; } = PropertyStatus.Available;
        public bool IsFurnished { get; set; }
        public bool HasParking { get; set; }
        public bool HasSecurity { get; set; }
        public bool HasWater { get; set; }
        public bool HasElectricity { get; set; }
        public string? Description { get; set; }
        public string? AdditionalNotes { get; set; }
        public bool IsActive { get; set; } = true;

        // Timestamps - Added UpdatedBy property
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; } // Added this property

        // Navigation properties
        public virtual ICollection<TenancyAgreement> TenancyAgreements { get; set; } = new List<TenancyAgreement>();
        public virtual ICollection<PropertyAmenity> Amenities { get; set; } = new List<PropertyAmenity>();
        public virtual ICollection<PropertyImage> Images { get; set; } = new List<PropertyImage>();
        public virtual ICollection<RentAdjustment> RentAdjustments { get; set; } = new List<RentAdjustment>();
        public virtual ICollection<Occupancy> Occupancies { get; set; } = new List<Occupancy>();
    }

    public class RentPayment
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenancyAgreementId { get; set; }
        public string TenantId { get; set; } = string.Empty;
        public string LandlordId { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public PaymentMethod PaymentMethod { get; set; }
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
        public string? TransactionId { get; set; }
        public string? ReferenceNumber { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
        public DateTime PeriodStartDate { get; set; }
        public DateTime PeriodEndDate { get; set; }
        public bool IsAdvancePayment { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual TenancyAgreement? TenancyAgreement { get; set; }
    }

    public class AgreementDocument
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenancyAgreementId { get; set; }
        public DocumentType DocumentType { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FileSize { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public string UploadedBy { get; set; } = string.Empty;
        public bool IsVerified { get; set; } = false;
        public string? VerificationNotes { get; set; }
        public DateTime? VerifiedAt { get; set; }

        // Navigation properties
        public virtual TenancyAgreement? TenancyAgreement { get; set; }
    }

    public class PropertyAmenity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PropertyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsAvailable { get; set; } = true;
        public string? AdditionalInfo { get; set; }

        // Navigation properties
        public virtual Property? Property { get; set; }
    }

    public class PropertyImage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PropertyId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsPrimary { get; set; } = false;
        public int DisplayOrder { get; set; } = 0;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Property? Property { get; set; }
    }

    public class LeaseTerm
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public int DurationInMonths { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class NoticePeriod
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public int DurationInDays { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class RentAdjustment
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PropertyId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PreviousRent { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NewRent { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal PercentageChange { get; set; }

        public DateTime EffectiveDate { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? ApprovalReference { get; set; }
        public DateTime ApprovedAt { get; set; }
        public string ApprovedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Property? Property { get; set; }
    }

    public class TenancyHistory
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenancyAgreementId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ChangedBy { get; set; }
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public string? IpAddress { get; set; }

        // Navigation properties
        public virtual TenancyAgreement? TenancyAgreement { get; set; }
    }

    public class Occupancy
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PropertyId { get; set; }
        public string TenantId { get; set; } = string.Empty;
        public DateTime OccupancyStartDate { get; set; }
        public DateTime? OccupancyEndDate { get; set; }
        public bool IsCurrent { get; set; } = true;
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Property? Property { get; set; }
    }
}