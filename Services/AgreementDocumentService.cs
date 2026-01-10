using Microsoft.EntityFrameworkCore;
using RentControlSystem.API.Data;
using RentControlSystem.Auth.API.Helpers;
using RentControlSystem.Tenancy.API.DTOs;
using RentControlSystem.Tenancy.API.Models;
using System.Security.Claims;

namespace RentControlSystem.Tenancy.API.Services
{
    public interface IAgreementDocumentService
    {
        Task<ServiceResponse<AgreementDocumentResponseDto>> UploadDocumentAsync(
            UploadDocumentDto dto, IFormFile file, string userId);
        Task<ServiceResponse<AgreementDocumentResponseDto>> GetDocumentByIdAsync(Guid id, string userId);
        Task<ServiceResponse<List<AgreementDocumentResponseDto>>> GetDocumentsByTenancyAsync(
            Guid tenancyId, string userId);
        Task<ServiceResponse<AgreementDocumentResponseDto>> VerifyDocumentAsync(
            Guid id, string verificationNotes, string userId);
        Task<ServiceResponse<bool>> DeleteDocumentAsync(Guid id, string userId);
        Task<ServiceResponse<string>> GenerateTenancyAgreementPdfAsync(Guid tenancyId, string userId);
    }

    public class AgreementDocumentService : IAgreementDocumentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AgreementDocumentService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AgreementDocumentService(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            ILogger<AgreementDocumentService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ServiceResponse<AgreementDocumentResponseDto>> UploadDocumentAsync(
            UploadDocumentDto dto, IFormFile file, string userId)
        {
            try
            {
                // Validate tenancy agreement
                var tenancy = await _context.TenancyAgreements
                    .FirstOrDefaultAsync(ta => ta.Id == dto.TenancyAgreementId && ta.IsActive);

                if (tenancy == null)
                    return ServiceResponse<AgreementDocumentResponseDto>.CreateError("Tenancy agreement not found");

                // Check authorization
                var userRole = GetUserRole();
                if (tenancy.TenantId != userId &&
                    tenancy.LandlordId != userId &&
                    userRole != "Admin" && userRole != "RCD_Officer")
                    return ServiceResponse<AgreementDocumentResponseDto>.CreateError("Unauthorized to upload documents");

                // Validate file
                if (file == null || file.Length == 0)
                    return ServiceResponse<AgreementDocumentResponseDto>.CreateError("No file provided");

                if (file.Length > 10 * 1024 * 1024) // 10MB limit
                    return ServiceResponse<AgreementDocumentResponseDto>.CreateError("File size exceeds 10MB limit");

                var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
                var fileExtension = Path.GetExtension(file.FileName).ToLower();

                if (!allowedExtensions.Contains(fileExtension))
                    return ServiceResponse<AgreementDocumentResponseDto>.CreateError(
                        "Invalid file type. Allowed: PDF, JPG, PNG, DOC, DOCX");

                // Create upload directory if it doesn't exist
                var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "documents", tenancy.Id.ToString());
                if (!Directory.Exists(uploadsPath))
                    Directory.CreateDirectory(uploadsPath);

                // Generate unique filename
                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsPath, fileName);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Create document record
                var document = new AgreementDocument
                {
                    TenancyAgreementId = dto.TenancyAgreementId,
                    DocumentType = dto.DocumentType,
                    FileName = file.FileName,
                    FilePath = $"/uploads/documents/{tenancy.Id}/{fileName}",
                    FileSize = FormatFileSize(file.Length),
                    Description = dto.Description,
                    UploadedBy = userId,
                    UploadedAt = DateTime.UtcNow,
                    IsVerified = (dto.DocumentType == DocumentType.PaymentReceipt) // Auto-verify receipts
                };

                _context.AgreementDocuments.Add(document);
                await _context.SaveChangesAsync();

                var responseDto = new AgreementDocumentResponseDto
                {
                    Id = document.Id,
                    TenancyAgreementId = document.TenancyAgreementId,
                    DocumentType = document.DocumentType,
                    FilePath = document.FilePath,
                    FileName = document.FileName,
                    FileSize = document.FileSize,
                    Description = document.Description,
                    UploadedAt = document.UploadedAt,
                    UploadedBy = document.UploadedBy,
                    IsVerified = document.IsVerified
                };

                _logger.LogInformation("Document uploaded: {FileName} by user {UserId}",
                    file.FileName, userId);

                return ServiceResponse<AgreementDocumentResponseDto>.CreateSuccess(
                    "Document uploaded successfully", responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document");
                return ServiceResponse<AgreementDocumentResponseDto>.CreateError("An error occurred while uploading document");
            }
        }

        public async Task<ServiceResponse<AgreementDocumentResponseDto>> GetDocumentByIdAsync(Guid id, string userId)
        {
            try
            {
                var document = await _context.AgreementDocuments
                    .Include(d => d.TenancyAgreement)
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (document == null)
                    return ServiceResponse<AgreementDocumentResponseDto>.CreateError("Document not found");

                // Check authorization
                var userRole = GetUserRole();
                var tenancy = document.TenancyAgreement;
                if (tenancy == null)
                    return ServiceResponse<AgreementDocumentResponseDto>.CreateError("Associated tenancy not found");

                if (tenancy.TenantId != userId &&
                    tenancy.LandlordId != userId &&
                    userRole != "Admin" && userRole != "RCD_Officer")
                    return ServiceResponse<AgreementDocumentResponseDto>.CreateError("Unauthorized access");

                var responseDto = new AgreementDocumentResponseDto
                {
                    Id = document.Id,
                    TenancyAgreementId = document.TenancyAgreementId,
                    DocumentType = document.DocumentType,
                    FilePath = document.FilePath,
                    FileName = document.FileName,
                    FileSize = document.FileSize,
                    Description = document.Description,
                    UploadedAt = document.UploadedAt,
                    UploadedBy = document.UploadedBy,
                    IsVerified = document.IsVerified,
                    VerificationNotes = document.VerificationNotes,
                    VerifiedAt = document.VerifiedAt
                };

                return ServiceResponse<AgreementDocumentResponseDto>.CreateSuccess("Document retrieved", responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving document {DocumentId}", id);
                return ServiceResponse<AgreementDocumentResponseDto>.CreateError("An error occurred");
            }
        }

        public async Task<ServiceResponse<List<AgreementDocumentResponseDto>>> GetDocumentsByTenancyAsync(
            Guid tenancyId, string userId)
        {
            try
            {
                var tenancy = await _context.TenancyAgreements
                    .FirstOrDefaultAsync(ta => ta.Id == tenancyId && ta.IsActive);

                if (tenancy == null)
                    return ServiceResponse<List<AgreementDocumentResponseDto>>.CreateError("Tenancy not found");

                // Check authorization
                var userRole = GetUserRole();
                if (tenancy.TenantId != userId &&
                    tenancy.LandlordId != userId &&
                    userRole != "Admin" && userRole != "RCD_Officer")
                    return ServiceResponse<List<AgreementDocumentResponseDto>>.CreateError("Unauthorized access");

                var documents = await _context.AgreementDocuments
                    .Where(d => d.TenancyAgreementId == tenancyId)
                    .OrderByDescending(d => d.UploadedAt)
                    .ToListAsync();

                var responseDtos = documents.Select(d => new AgreementDocumentResponseDto
                {
                    Id = d.Id,
                    TenancyAgreementId = d.TenancyAgreementId,
                    DocumentType = d.DocumentType,
                    FilePath = d.FilePath,
                    FileName = d.FileName,
                    FileSize = d.FileSize,
                    Description = d.Description,
                    UploadedAt = d.UploadedAt,
                    UploadedBy = d.UploadedBy,
                    IsVerified = d.IsVerified,
                    VerificationNotes = d.VerificationNotes,
                    VerifiedAt = d.VerifiedAt
                }).ToList();

                return ServiceResponse<List<AgreementDocumentResponseDto>>.CreateSuccess(
                    "Documents retrieved", responseDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving documents for tenancy {TenancyId}", tenancyId);
                return ServiceResponse<List<AgreementDocumentResponseDto>>.CreateError("An error occurred");
            }
        }

        public async Task<ServiceResponse<AgreementDocumentResponseDto>> VerifyDocumentAsync(
            Guid id, string verificationNotes, string userId)
        {
            try
            {
                var document = await _context.AgreementDocuments
                    .Include(d => d.TenancyAgreement)
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (document == null)
                    return ServiceResponse<AgreementDocumentResponseDto>.CreateError("Document not found");

                // Only RCD officers and admins can verify documents
                var userRole = GetUserRole();
                if (userRole != "Admin" && userRole != "RCD_Officer")
                    return ServiceResponse<AgreementDocumentResponseDto>.CreateError("Unauthorized to verify documents");

                document.IsVerified = true;
                document.VerificationNotes = verificationNotes;
                document.VerifiedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var responseDto = new AgreementDocumentResponseDto
                {
                    Id = document.Id,
                    TenancyAgreementId = document.TenancyAgreementId,
                    DocumentType = document.DocumentType,
                    FilePath = document.FilePath,
                    FileName = document.FileName,
                    FileSize = document.FileSize,
                    Description = document.Description,
                    UploadedAt = document.UploadedAt,
                    UploadedBy = document.UploadedBy,
                    IsVerified = document.IsVerified,
                    VerificationNotes = document.VerificationNotes,
                    VerifiedAt = document.VerifiedAt
                };                      

                _logger.LogInformation("Document verified: {DocumentId} by user {UserId}", id, userId);

                return ServiceResponse<AgreementDocumentResponseDto>.CreateSuccess("Document verified", responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying document {DocumentId}", id);
                return ServiceResponse<AgreementDocumentResponseDto>.CreateError("An error occurred");
            }
        }

        public async Task<ServiceResponse<bool>> DeleteDocumentAsync(Guid id, string userId)
        {
            try
            {
                var document = await _context.AgreementDocuments
                    .Include(d => d.TenancyAgreement)
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (document == null)
                    return ServiceResponse<bool>.CreateError("Document not found");

                // Check authorization
                var userRole = GetUserRole();
                var tenancy = document.TenancyAgreement;
                if (tenancy == null)
                    return ServiceResponse<bool>.CreateError("Associated tenancy not found");

                if (tenancy.TenantId != userId &&
                    tenancy.LandlordId != userId &&
                    userRole != "Admin" && userRole != "RCD_Officer")
                    return ServiceResponse<bool>.CreateError("Unauthorized to delete document");

                // Delete physical file
                var filePath = Path.Combine(_environment.WebRootPath, document.FilePath.TrimStart('/'));
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                // Delete database record
                _context.AgreementDocuments.Remove(document);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Document deleted: {DocumentId} by user {UserId}", id, userId);

                return ServiceResponse<bool>.CreateSuccess("Document deleted", true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document {DocumentId}", id);
                return ServiceResponse<bool>.CreateError("An error occurred");
            }
        }

        public async Task<ServiceResponse<string>> GenerateTenancyAgreementPdfAsync(Guid tenancyId, string userId)
        {
            try
            {
                var tenancy = await _context.TenancyAgreements
                    .Include(ta => ta.Property)
                    .Include(ta => ta.LeaseTerm)
                    .Include(ta => ta.NoticePeriod)
                    .FirstOrDefaultAsync(ta => ta.Id == tenancyId && ta.IsActive);

                if (tenancy == null)
                    return ServiceResponse<string>.CreateError("Tenancy agreement not found");

                // Check authorization
                var userRole = GetUserRole();
                if (tenancy.TenantId != userId &&
                    tenancy.LandlordId != userId &&
                    userRole != "Admin" && userRole != "RCD_Officer")
                    return ServiceResponse<string>.CreateError("Unauthorized");

                // Generate PDF using a template (simplified example)
                var pdfContent = GenerateAgreementPdfContent(tenancy);

                // Save PDF to file
                var pdfsPath = Path.Combine(_environment.WebRootPath, "pdfs", "agreements");
                if (!Directory.Exists(pdfsPath))
                    Directory.CreateDirectory(pdfsPath);

                var fileName = $"{tenancy.AgreementNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
                var filePath = Path.Combine(pdfsPath, fileName);

                // In real implementation, use a PDF generation library like iTextSharp
                // File.WriteAllBytes(filePath, pdfContent);

                var pdfUrl = $"/pdfs/agreements/{fileName}";

                // Create document record for the generated PDF
                var document = new AgreementDocument
                {
                    TenancyAgreementId = tenancyId,
                    DocumentType = DocumentType.Agreement,
                    FileName = $"{tenancy.AgreementNumber}_Official_Agreement.pdf",
                    FilePath = pdfUrl,
                    FileSize = "0 KB", // Would be actual size
                    Description = "Official tenancy agreement PDF",
                    UploadedBy = "System",
                    UploadedAt = DateTime.UtcNow,
                    IsVerified = true
                };

                _context.AgreementDocuments.Add(document);
                await _context.SaveChangesAsync();

                return ServiceResponse<string>.CreateSuccess("PDF generated", pdfUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating tenancy agreement PDF");
                return ServiceResponse<string>.CreateError("An error occurred");
            }
        }

        private string GenerateAgreementPdfContent(TenancyAgreement tenancy)
        {
            // Simplified PDF content generation
            // In real implementation, use a proper PDF generation library
            return $@"
                GHANA RENT CONTROL DEPARTMENT
                OFFICIAL TENANCY AGREEMENT
                
                Agreement Number: {tenancy.AgreementNumber}
                Date: {DateTime.UtcNow:dd/MM/yyyy}
                
                PARTIES:
                Landlord: [Landlord Details]
                Tenant: [Tenant Details]
                
                PROPERTY DETAILS:
                Address: {tenancy.Property?.Address}
                Ghana Post GPS: {tenancy.Property?.GhanaPostGpsAddress}
                
                TERMS:
                Monthly Rent: GHS {tenancy.MonthlyRent:N2}
                Security Deposit: GHS {tenancy.SecurityDeposit:N2}
                Start Date: {tenancy.StartDate:dd/MM/yyyy}
                End Date: {tenancy.EndDate:dd/MM/yyyy}
                Payment Frequency: {tenancy.PaymentFrequency}
                
                This agreement is governed by the Rent Act, 1963 (Act 220)
                and subsequent amendments of Ghana.
                
                SIGNATURES:
                ___________________________
                Landlord
                
                ___________________________
                Tenant
                
                ___________________________
                RCD Officer
            ";
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double len = bytes;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private string GetUserRole()
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value ?? "Tenant";
        }
    }
}