using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RentControlSystem.Auth.API.Models;
using RentControlSystem.CaseManagement.API.DTOs;
using RentControlSystem.CaseManagement.API.Models;
using RentControlSystem.Tenancy.API.Models;
using PropertyStatus = RentControlSystem.Tenancy.API.Models.PropertyStatus;

namespace RentControlSystem.API.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Tenancy DbSets
        public DbSet<TenancyAgreement> TenancyAgreements { get; set; }
        public DbSet<Property> Properties { get; set; }
        public DbSet<RentPayment> RentPayments { get; set; }
        public DbSet<AgreementDocument> AgreementDocuments { get; set; }
        public DbSet<PropertyAmenity> PropertyAmenities { get; set; }
        public DbSet<PropertyImage> PropertyImages { get; set; }
        public DbSet<LeaseTerm> LeaseTerms { get; set; }
        public DbSet<NoticePeriod> NoticePeriods { get; set; }
        public DbSet<RentAdjustment> RentAdjustments { get; set; }
        public DbSet<TenancyHistory> TenancyHistories { get; set; }
        public DbSet<Occupancy> Occupancies { get; set; }

        // Auth DbSets
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<EmailQueue> EmailQueue { get; set; }

        // Case Management DbSets
        public DbSet<Case> Cases { get; set; }
        public DbSet<CaseDocument> CaseDocuments { get; set; }
        public DbSet<Hearing> Hearings { get; set; }
        public DbSet<HearingParticipant> HearingParticipants { get; set; }
        public DbSet<CaseNote> CaseNotes { get; set; }
        public DbSet<CaseParticipant> CaseParticipants { get; set; }
        public DbSet<CaseUpdate> CaseUpdates { get; set; }
        public DbSet<CaseCommunication> CaseCommunications { get; set; }
        public DbSet<Mediator> Mediators { get; set; }
        public DbSet<RCDOfficer> RCDOfficers { get; set; }
        public DbSet<MediationSession> MediationSessions { get; set; }
        public DbSet<MediationParticipant> MediationParticipants { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Identity Configuration (from AuthDbContext)
            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.HasIndex(u => u.GhanaCardNumber).IsUnique();
                entity.Property(u => u.GhanaCardNumber).IsRequired().HasMaxLength(20);
                entity.Property(u => u.PhoneNumber).IsRequired().HasMaxLength(15);
                entity.Property(u => u.Role).IsRequired().HasMaxLength(50);
                entity.Property(u => u.FirstName).IsRequired().HasMaxLength(50);
                entity.Property(u => u.LastName).IsRequired().HasMaxLength(50);
            });

            // UserProfile Configuration
            modelBuilder.Entity<UserProfile>(entity =>
            {
                entity.HasKey(up => up.Id);
                entity.HasOne(up => up.User)
                      .WithOne(u => u.Profile)
                      .HasForeignKey<UserProfile>(up => up.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Property(up => up.Address).HasMaxLength(500);
                entity.Property(up => up.EmergencyContact).HasMaxLength(100);
                entity.Property(up => up.EmergencyPhone).HasMaxLength(15);
                entity.Property(up => up.City).HasMaxLength(100);
                entity.Property(up => up.Region).HasMaxLength(100);
                entity.Property(up => up.PostalCode).HasMaxLength(20);
            });

            // RefreshToken Configuration
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(rt => rt.Id);
                entity.HasOne(rt => rt.User)
                      .WithMany()
                      .HasForeignKey(rt => rt.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Property(rt => rt.Token).IsRequired();
                entity.Property(rt => rt.JwtId).IsRequired();

                entity.HasIndex(rt => rt.Token);
                entity.HasIndex(rt => rt.UserId);
                entity.HasIndex(rt => rt.ExpiryDate);
            });

            // AuditLog Configuration
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(al => al.Id);
                entity.Property(al => al.Action).IsRequired().HasMaxLength(100);
                entity.Property(al => al.EntityType).IsRequired().HasMaxLength(100);
                entity.Property(al => al.EntityId).IsRequired().HasMaxLength(100);
                entity.Property(al => al.OldValues).HasMaxLength(2000);
                entity.Property(al => al.NewValues).HasMaxLength(2000);
                entity.Property(al => al.IpAddress).HasMaxLength(50);

                entity.HasIndex(al => al.UserId);
                entity.HasIndex(al => al.EntityType);
                entity.HasIndex(al => al.EntityId);
                entity.HasIndex(al => al.Timestamp);
            });

            // EmailQueue Configuration
            modelBuilder.Entity<EmailQueue>(entity =>
            {
                entity.HasKey(eq => eq.Id);

                entity.Property(eq => eq.ToEmail)
                      .IsRequired()
                      .HasMaxLength(256);

                entity.Property(eq => eq.Subject)
                      .IsRequired()
                      .HasMaxLength(500);

                entity.Property(eq => eq.Body)
                      .IsRequired();

                entity.Property(eq => eq.LastError)
                      .HasMaxLength(1000);

                entity.HasIndex(eq => eq.ToEmail);
                entity.HasIndex(eq => eq.IsSent);
                entity.HasIndex(eq => eq.CreatedAt);
            });

            // TenancyAgreement Configuration
            modelBuilder.Entity<TenancyAgreement>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.AgreementNumber).IsUnique();
                entity.HasIndex(e => new { e.PropertyId, e.IsActive });
                entity.HasIndex(e => new { e.TenantId, e.IsActive });
                entity.HasIndex(e => new { e.LandlordId, e.IsActive });

                entity.Property(e => e.AgreementNumber)
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasDefaultValueSql("'AG' + RIGHT('000000' + CAST(NEXT VALUE FOR AgreementNumberSeq AS VARCHAR(6)), 6)");

                entity.Property(e => e.MonthlyRent)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();

                entity.Property(e => e.SecurityDeposit)
                    .HasColumnType("decimal(18,2)");

                entity.Property(e => e.Status)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.PaymentFrequency)
                    .IsRequired()
                    .HasMaxLength(20);

                // Removed computed column for NextPaymentDate since we're using CalculatedNextPaymentDate property
                entity.Ignore(e => e.CalculatedNextPaymentDate);

                entity.HasOne(e => e.Property)
                    .WithMany(p => p.TenancyAgreements)
                    .HasForeignKey(e => e.PropertyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.LeaseTerm)
                    .WithMany()
                    .HasForeignKey(e => e.LeaseTermId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.NoticePeriod)
                    .WithMany()
                    .HasForeignKey(e => e.NoticePeriodId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Property Configuration
            modelBuilder.Entity<Property>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.PropertyCode).IsUnique();
                entity.HasIndex(e => new { e.LandlordId, e.IsActive });
                entity.HasIndex(e => e.GhanaPostGpsAddress);

                entity.Property(e => e.PropertyCode)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(e => e.PropertyType)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.PropertyStatus)
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasDefaultValue(PropertyStatus.Available);

                // Configure the Location property for NetTopologySuite
                entity.Property(e => e.Location)
                    .HasColumnType("geography");

                entity.Property(e => e.MonthlyRent)
                    .HasColumnType("decimal(18,2)");

                entity.Property(e => e.SizeInSqMeters)
                    .HasColumnType("decimal(10,2)");

                entity.HasMany(e => e.Amenities)
                    .WithOne(a => a.Property)
                    .HasForeignKey(a => a.PropertyId);

                entity.HasMany(e => e.Images)
                    .WithOne(i => i.Property)
                    .HasForeignKey(i => i.PropertyId);

                entity.HasMany(e => e.RentAdjustments)
                    .WithOne(ra => ra.Property)
                    .HasForeignKey(ra => ra.PropertyId);

                entity.HasMany(e => e.Occupancies)
                    .WithOne(o => o.Property)
                    .HasForeignKey(o => o.PropertyId);
            });

            // RentPayment Configuration
            modelBuilder.Entity<RentPayment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TransactionId).IsUnique();
                entity.HasIndex(e => new { e.TenancyAgreementId, e.PaymentDate });
                entity.HasIndex(e => new { e.PaymentStatus, e.PaymentDate });

                entity.Property(e => e.Amount)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();

                entity.Property(e => e.PaymentMethod)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.PaymentStatus)
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasDefaultValue(PaymentStatus.Pending);

                entity.Property(e => e.PaymentDate)
                    .IsRequired();

                entity.Property(e => e.PeriodStartDate)
                    .IsRequired();

                entity.Property(e => e.PeriodEndDate)
                    .IsRequired();

                entity.HasOne(e => e.TenancyAgreement)
                    .WithMany(ta => ta.RentPayments)
                    .HasForeignKey(e => e.TenancyAgreementId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // AgreementDocument Configuration
            modelBuilder.Entity<AgreementDocument>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.TenancyAgreementId, e.DocumentType });

                entity.Property(e => e.DocumentType)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.FilePath)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(e => e.FileName)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.HasOne(e => e.TenancyAgreement)
                    .WithMany(ta => ta.Documents)
                    .HasForeignKey(e => e.TenancyAgreementId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // TenancyHistory Configuration
            modelBuilder.Entity<TenancyHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TenancyAgreementId);
                entity.HasIndex(e => e.ChangedAt);

                entity.Property(e => e.Action)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.Description)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.HasOne(e => e.TenancyAgreement)
                    .WithMany(ta => ta.History)
                    .HasForeignKey(e => e.TenancyAgreementId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Occupancy Configuration
            modelBuilder.Entity<Occupancy>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.PropertyId, e.IsCurrent });
                entity.HasIndex(e => e.TenantId);

                entity.HasOne(e => e.Property)
                    .WithMany(p => p.Occupancies)
                    .HasForeignKey(e => e.PropertyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Case Configuration
            modelBuilder.Entity<Case>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.HasIndex(c => c.CaseNumber).IsUnique();
                entity.HasIndex(c => new { c.ComplainantId, c.Status });
                entity.HasIndex(c => new { c.RespondentId, c.Status });
                entity.HasIndex(c => c.AssignedOfficerId);
                entity.HasIndex(c => c.AssignedMediatorId);
                entity.HasIndex(c => new { c.Status, c.CreatedAt });

                entity.Property(c => c.CaseNumber)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(c => c.Title)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(c => c.Description)
                    .IsRequired()
                    .HasMaxLength(2000);

                entity.Property(c => c.ComplainantName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(c => c.ComplainantPhone)
                    .IsRequired()
                    .HasMaxLength(15);

                entity.Property(c => c.ComplainantEmail)
                    .HasMaxLength(100);

                entity.Property(c => c.RespondentName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(c => c.RespondentPhone)
                    .IsRequired()
                    .HasMaxLength(15);

                entity.Property(c => c.RespondentEmail)
                    .HasMaxLength(100);

                entity.Property(c => c.PropertyAddress)
                    .HasMaxLength(500);

                entity.Property(c => c.ClaimAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(c => c.AwardedAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(c => c.CaseType)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasMaxLength(50);

                entity.Property(c => c.Status)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasMaxLength(50)
                    .HasDefaultValue(CaseStatus.Draft);

                entity.Property(c => c.Priority)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValue(CasePriority.Medium);

                entity.Property(c => c.Resolution)
                    .HasConversion<string>()
                    .HasMaxLength(50);

                entity.Property(c => c.ResolutionDetails)
                    .HasMaxLength(1000);

                entity.HasOne(c => c.Property)
                    .WithMany()
                    .HasForeignKey(c => c.PropertyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.TenancyAgreement)
                    .WithMany()
                    .HasForeignKey(c => c.TenancyAgreementId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(c => c.Documents)
                    .WithOne(d => d.Case)
                    .HasForeignKey(d => d.CaseId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(c => c.Hearings)
                    .WithOne(h => h.Case)
                    .HasForeignKey(h => h.CaseId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(c => c.Notes)
                    .WithOne(n => n.Case)
                    .HasForeignKey(n => n.CaseId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(c => c.Participants)
                    .WithOne(p => p.Case)
                    .HasForeignKey(p => p.CaseId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(c => c.Updates)
                    .WithOne(u => u.Case)
                    .HasForeignKey(u => u.CaseId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(c => c.Communications)
                    .WithOne(c => c.Case)
                    .HasForeignKey(c => c.CaseId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // CaseDocument Configuration
            modelBuilder.Entity<CaseDocument>(entity =>
            {
                entity.HasKey(d => d.Id);
                entity.HasIndex(d => new { d.CaseId, d.DocumentType });

                entity.Property(d => d.DocumentType)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasMaxLength(50);

                entity.Property(d => d.FileName)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(d => d.FilePath)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(d => d.FileSize)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(d => d.Description)
                    .HasMaxLength(500);

                entity.Property(d => d.VerificationNotes)
                    .HasMaxLength(1000);

                entity.HasOne(d => d.Case)
                    .WithMany(c => c.Documents)
                    .HasForeignKey(d => d.CaseId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Hearing Configuration
            modelBuilder.Entity<Hearing>(entity =>
            {
                entity.HasKey(h => h.Id);
                entity.HasIndex(h => h.HearingNumber).IsUnique();
                entity.HasIndex(h => new { h.CaseId, h.HearingDate });
                entity.HasIndex(h => new { h.PresidingOfficerId, h.HearingDate });

                entity.Property(h => h.HearingNumber)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(h => h.Title)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(h => h.Description)
                    .HasMaxLength(1000);

                entity.Property(h => h.Location)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(h => h.VirtualMeetingLink)
                    .HasMaxLength(500);

                entity.Property(h => h.Status)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasMaxLength(50)
                    .HasDefaultValue(HearingStatus.Scheduled);

                entity.Property(h => h.Outcome)
                    .HasMaxLength(1000);

                entity.Property(h => h.Minutes)
                    .HasMaxLength(5000);

                entity.Property(h => h.MinutesFilePath)
                    .HasMaxLength(500);

                entity.HasOne(h => h.Case)
                    .WithMany(c => c.Hearings)
                    .HasForeignKey(h => h.CaseId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(h => h.Participants)
                    .WithOne(p => p.Hearing)
                    .HasForeignKey(p => p.HearingId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // HearingParticipant Configuration
            modelBuilder.Entity<HearingParticipant>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.HasIndex(p => new { p.HearingId, p.ParticipantId });

                entity.Property(p => p.ParticipantName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(p => p.ParticipantEmail)
                    .HasMaxLength(100);

                entity.Property(p => p.ParticipantPhone)
                    .HasMaxLength(15);

                entity.Property(p => p.ParticipantType)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasMaxLength(50);

                entity.Property(p => p.Role)
                    .HasMaxLength(100);

                entity.Property(p => p.Organization)
                    .HasMaxLength(200);

                entity.Property(p => p.Notes)
                    .HasMaxLength(500);

                entity.HasOne(p => p.Hearing)
                    .WithMany(h => h.Participants)
                    .HasForeignKey(p => p.HearingId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // CaseNote Configuration
            modelBuilder.Entity<CaseNote>(entity =>
            {
                entity.HasKey(n => n.Id);
                entity.HasIndex(n => new { n.CaseId, n.CreatedAt });

                entity.Property(n => n.Title)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(n => n.Content)
                    .IsRequired()
                    .HasMaxLength(2000);

                entity.Property(n => n.CreatedBy)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(n => n.CreatedByName)
                    .HasMaxLength(100);

                entity.HasOne(n => n.Case)
                    .WithMany(c => c.Notes)
                    .HasForeignKey(n => n.CaseId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // CaseParticipant Configuration
            modelBuilder.Entity<CaseParticipant>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.HasIndex(p => new { p.CaseId, p.ParticipantId });

                entity.Property(p => p.ParticipantName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(p => p.ParticipantEmail)
                    .HasMaxLength(100);

                entity.Property(p => p.ParticipantPhone)
                    .HasMaxLength(15);

                entity.Property(p => p.ParticipantType)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasMaxLength(50);

                entity.Property(p => p.Role)
                    .HasMaxLength(100);

                entity.Property(p => p.Organization)
                    .HasMaxLength(200);

                entity.Property(p => p.Address)
                    .HasMaxLength(500);

                entity.Property(p => p.Notes)
                    .HasMaxLength(500);

                entity.HasOne(p => p.Case)
                    .WithMany(c => c.Participants)
                    .HasForeignKey(p => p.CaseId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // CaseUpdate Configuration
            modelBuilder.Entity<CaseUpdate>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.HasIndex(u => new { u.CaseId, u.CreatedAt });

                entity.Property(u => u.UpdateType)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(u => u.Description)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(u => u.OldValue)
                    .HasMaxLength(500);

                entity.Property(u => u.NewValue)
                    .HasMaxLength(500);

                entity.Property(u => u.CreatedBy)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(u => u.CreatedByName)
                    .HasMaxLength(100);

                entity.HasOne(u => u.Case)
                    .WithMany(c => c.Updates)
                    .HasForeignKey(u => u.CaseId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // CaseCommunication Configuration
            modelBuilder.Entity<CaseCommunication>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.HasIndex(c => new { c.CaseId, c.CreatedAt });

                entity.Property(c => c.CommunicationType)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(c => c.Subject)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(c => c.Content)
                    .IsRequired()
                    .HasMaxLength(2000);

                entity.Property(c => c.SenderName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(c => c.SenderEmail)
                    .HasMaxLength(100);

                entity.Property(c => c.SenderPhone)
                    .HasMaxLength(15);

                entity.Property(c => c.RecipientName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(c => c.RecipientEmail)
                    .HasMaxLength(100);

                entity.Property(c => c.RecipientPhone)
                    .HasMaxLength(15);

                entity.Property(c => c.AttachmentPath)
                    .HasMaxLength(500);

                entity.Property(c => c.StatusMessage)
                    .HasMaxLength(500);

                entity.HasOne(c => c.Case)
                    .WithMany(cs => cs.Communications)
                    .HasForeignKey(c => c.CaseId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Mediator Configuration
            modelBuilder.Entity<Mediator>(entity =>
            {
                entity.HasKey(m => m.Id);
                entity.HasIndex(m => m.UserId).IsUnique();
                entity.HasIndex(m => m.LicenseNumber).IsUnique();

                entity.Property(m => m.FullName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(m => m.Email)
                    .HasMaxLength(100);

                entity.Property(m => m.Phone)
                    .HasMaxLength(15);

                entity.Property(m => m.LicenseNumber)
                    .HasMaxLength(50);

                entity.Property(m => m.Specialization)
                    .HasMaxLength(200);

                entity.Property(m => m.Qualifications)
                    .HasMaxLength(500);

                entity.Property(m => m.SuccessRate)
                    .HasColumnType("decimal(5,2)");

                entity.HasMany(m => m.Cases)
                    .WithOne()
                    .HasForeignKey(c => c.AssignedMediatorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // RCDOfficer Configuration
            modelBuilder.Entity<RCDOfficer>(entity =>
            {
                entity.HasKey(o => o.Id);
                entity.HasIndex(o => o.UserId).IsUnique();
                entity.HasIndex(o => o.EmployeeNumber).IsUnique();

                entity.Property(o => o.FullName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(o => o.EmployeeNumber)
                    .HasMaxLength(50);

                entity.Property(o => o.Email)
                    .HasMaxLength(100);

                entity.Property(o => o.Phone)
                    .HasMaxLength(15);

                entity.Property(o => o.Department)
                    .HasMaxLength(100);

                entity.Property(o => o.Designation)
                    .HasMaxLength(100);

                entity.Property(o => o.Region)
                    .HasMaxLength(100);

                entity.Property(o => o.District)
                    .HasMaxLength(100);

                entity.HasMany(o => o.Cases)
                    .WithOne()
                    .HasForeignKey(c => c.AssignedOfficerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(o => o.HearingsPresided)
                    .WithOne()
                    .HasForeignKey(h => h.PresidingOfficerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            

            
            // Additional indexes for tenancy
            modelBuilder.Entity<TenancyAgreement>()
                .HasIndex(e => new { e.Status, e.EndDate })
                .HasFilter("[Status] = 'Active'");

            modelBuilder.Entity<RentPayment>()
                .HasIndex(e => new { e.TenantId, e.PaymentDate })
                .IsDescending(false, true);

            // Additional indexes for cases
            modelBuilder.Entity<Case>()
                .HasIndex(c => new { c.CreatedAt, c.Priority })
                .IsDescending(true, false);

            modelBuilder.Entity<Hearing>()
                .HasIndex(h => new { h.HearingDate, h.Status })
                .HasFilter("[Status] = 'Scheduled'");

            modelBuilder.Entity<CaseCommunication>()
                .HasIndex(c => new { c.RecipientId, c.IsRead, c.CreatedAt });

            // Sequence for agreement numbers
            modelBuilder.HasSequence<int>("AgreementNumberSeq")
                .StartsAt(1)
                .IncrementsBy(1);
        }
    }
}