using RentControlSystem.API.DTOs;
using RentControlSystem.Auth.API.Helpers;
using RentControlSystem.CaseManagement.API.DTOs;
using RentControlSystem.CaseManagement.API.Models;
using CaseDocumentDto = RentControlSystem.API.DTOs.CaseDocumentDto;
using UploadCaseDocumentDto = RentControlSystem.API.DTOs.UploadCaseDocumentDto;

namespace RentControlSystem.CaseManagement.API.Services
{
    public interface ICaseManagementService
    {
        // Case Operations
        Task<ServiceResponse<CaseResponseDto>> CreateCaseAsync(CreateCaseDto dto, string userId);
        Task<ServiceResponse<CaseResponseDto>> GetCaseByIdAsync(Guid caseId, string userId);
        Task<PaginatedServiceResponse<List<CaseResponseDto>>> SearchCasesAsync(
            CaseSearchDto searchDto, int page, int pageSize, string userId);
        Task<ServiceResponse<CaseResponseDto>> UpdateCaseAsync(
            Guid caseId, UpdateCaseDto dto, string userId);
        Task<ServiceResponse<bool>> SubmitCaseAsync(Guid caseId, string userId);
        Task<ServiceResponse<bool>> AssignCaseAsync(Guid caseId, AssignCaseDto dto, string userId);
        Task<ServiceResponse<bool>> UpdateCaseStatusAsync(
            Guid caseId, CaseStatus newStatus, string reason, string userId);
        Task<ServiceResponse<bool>> ResolveCaseAsync(Guid caseId, ResolveCaseDto dto, string userId);
        Task<ServiceResponse<bool>> ReopenCaseAsync(Guid caseId, string reason, string userId);

        // Hearing Operations
        Task<ServiceResponse<HearingResponseDto>> ScheduleHearingAsync(ScheduleHearingDto dto, string userId);
        Task<ServiceResponse<HearingResponseDto>> UpdateHearingAsync(
            Guid hearingId, UpdateHearingDto dto, string userId);
        Task<ServiceResponse<bool>> CancelHearingAsync(Guid hearingId, string reason, string userId);
        Task<ServiceResponse<bool>> AddHearingParticipantAsync(
            Guid hearingId, AddParticipantDto dto, string userId);
        Task<ServiceResponse<bool>> RecordHearingOutcomeAsync(
            Guid hearingId, RecordHearingOutcomeDto dto, string userId);

        // Document Operations
        Task<ServiceResponse<CaseDocumentDto>> UploadCaseDocumentAsync(
            UploadCaseDocumentDto dto, string userId);
        Task<ServiceResponse<bool>> VerifyDocumentAsync(
            Guid documentId, bool isVerified, string notes, string userId);

        // Communication Operations
        Task<ServiceResponse<CaseCommunicationDto>> SendCommunicationAsync(
            SendCommunicationDto dto, string userId);

        // Reporting
        Task<ServiceResponse<CaseStatisticsDto>> GetCaseStatisticsAsync(string userId);
        Task<ServiceResponse<HearingCalendarDto>> GetHearingCalendarAsync(
            DateTime fromDate, DateTime toDate, string userId);
        Task<ServiceResponse<List<CaseDashboardDto>>> GetDashboardCasesAsync(string userId);
    }

    public interface IMediationService
    {
        Task<ServiceResponse<MediationSessionDto>> ScheduleMediationAsync(
            ScheduleMediationDto dto, string userId);
        Task<ServiceResponse<bool>> RecordMediationOutcomeAsync(
            Guid sessionId, RecordMediationOutcomeDto dto, string userId);
        Task<ServiceResponse<List<MediatorDto>>> GetAvailableMediatorsAsync(DateTime date);
    }

    public interface ICaseAssignmentService
    {
        Task<ServiceResponse<bool>> AutoAssignCaseAsync(Guid caseId);
        Task<ServiceResponse<List<RCDOfficerDto>>> GetAvailableOfficersAsync(
            string region, CaseType caseType);
        Task<ServiceResponse<bool>> ReassignCaseAsync(
            Guid caseId, string newOfficerId, string reason, string userId);
    }
}