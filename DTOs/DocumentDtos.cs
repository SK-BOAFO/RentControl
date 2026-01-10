using RentControlSystem.CaseManagement.API.Models;
using System.ComponentModel.DataAnnotations;

namespace RentControlSystem.CaseManagement.API.DTOs
{
    public class UploadCaseDocumentDto
    {
        [Required(ErrorMessage = "Case ID is required")]
        public Guid CaseId { get; set; }

        [Required(ErrorMessage = "Document type is required")]
        public DocumentEvidenceType DocumentType { get; set; }

        [Required(ErrorMessage = "File name is required")]
        public string FileName { get; set; } = string.Empty;

        [Required(ErrorMessage = "File content is required")]
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

    public class VerifyDocumentDto
    {
        public bool IsVerified { get; set; }
        public string? VerificationNotes { get; set; }
    }
}