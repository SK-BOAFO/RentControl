using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using RentControlSystem.API.Data;
using RentControlSystem.API.DTOs;
using RentControlSystem.Auth.API.Helpers;
using RentControlSystem.CaseManagement.API.DTOs;
using RentControlSystem.CaseManagement.API.Models;
using RentControlSystem.Tenancy.API.Services;
using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using CaseDocumentDto = RentControlSystem.API.DTOs.CaseDocumentDto;
using UploadCaseDocumentDto = RentControlSystem.API.DTOs.UploadCaseDocumentDto;

namespace RentControlSystem.CaseManagement.API.Services
{
    public class CaseManagementService : ICaseManagementService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IDistributedCache _cache;
        private readonly ILogger<CaseManagementService> _logger;
        private readonly INotificationService _notificationService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CaseManagementService(
            ApplicationDbContext context,
            IMapper mapper,
            IDistributedCache cache,
            ILogger<CaseManagementService> logger,
            INotificationService notificationService,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _mapper = mapper;
            _cache = cache;
            _logger = logger;
            _notificationService = notificationService;
            _httpContextAccessor = httpContextAccessor;
        }

        #region Case Operations

        public async Task<ServiceResponse<CaseResponseDto>> CreateCaseAsync(
            CreateCaseDto dto, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Validate related entities if provided
                if (dto.PropertyId.HasValue)
                {
                    var property = await _context.Properties
                        .FirstOrDefaultAsync(p => p.Id == dto.PropertyId.Value);
                    if (property == null)
                        return ServiceResponse<CaseResponseDto>.CreateError("Property not found");
                }

                if (dto.TenancyAgreementId.HasValue)
                {
                    var tenancy = await _context.TenancyAgreements
                        .FirstOrDefaultAsync(t => t.Id == dto.TenancyAgreementId.Value);
                    if (tenancy == null)
                        return ServiceResponse<CaseResponseDto>.CreateError("Tenancy agreement not found");
                }

                // Generate case number
                var caseNumber = await GenerateCaseNumberAsync(dto.CaseType);

                // Create case
                var newCase = new Case
                {
                    CaseNumber = caseNumber,
                    CaseType = dto.CaseType,
                    Title = dto.Title,
                    Description = dto.Description,
                    ComplainantId = dto.ComplainantId,
                    ComplainantName = dto.ComplainantName,
                    ComplainantPhone = dto.ComplainantPhone,
                    ComplainantEmail = dto.ComplainantEmail,
                    RespondentId = dto.RespondentId,
                    RespondentName = dto.RespondentName,
                    RespondentPhone = dto.RespondentPhone,
                    RespondentEmail = dto.RespondentEmail,
                    PropertyId = dto.PropertyId,
                    TenancyAgreementId = dto.TenancyAgreementId,
                    PropertyAddress = dto.PropertyAddress,
                    Status = CaseStatus.Draft,
                    Priority = DeterminePriority(dto.CaseType, dto.ClaimAmount),
                    IncidentDate = dto.IncidentDate,
                    ClaimAmount = dto.ClaimAmount,
                    IsActive = true,
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Cases.Add(newCase);

                // Add participants
                var complainantParticipant = new CaseParticipant
                {
                    CaseId = newCase.Id,
                    ParticipantId = dto.ComplainantId,
                    ParticipantName = dto.ComplainantName,
                    ParticipantEmail = dto.ComplainantEmail,
                    ParticipantPhone = dto.ComplainantPhone,
                    ParticipantType = ParticipantType.Complainant,
                    IsPrimaryContact = true,
                    AddedBy = userId,
                    AddedAt = DateTime.UtcNow
                };

                var respondentParticipant = new CaseParticipant
                {
                    CaseId = newCase.Id,
                    ParticipantId = dto.RespondentId,
                    ParticipantName = dto.RespondentName,
                    ParticipantEmail = dto.RespondentEmail,
                    ParticipantPhone = dto.RespondentPhone,
                    ParticipantType = ParticipantType.Respondent,
                    IsPrimaryContact = true,
                    AddedBy = userId,
                    AddedAt = DateTime.UtcNow
                };

                _context.CaseParticipants.Add(complainantParticipant);
                _context.CaseParticipants.Add(respondentParticipant);

                // Add initial note if provided
                if (!string.IsNullOrEmpty(dto.InitialNote))
                {
                    var note = new CaseNote
                    {
                        CaseId = newCase.Id,
                        Title = "Initial Case Details",
                        Content = dto.InitialNote,
                        CreatedBy = userId,
                        CreatedByName = await GetUserNameAsync(userId),
                        IsInternal = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.CaseNotes.Add(note);
                }

                // Add case update
                var update = new CaseUpdate
                {
                    CaseId = newCase.Id,
                    UpdateType = "CaseCreated",
                    Description = "Case created in draft status",
                    CreatedBy = userId,
                    CreatedByName = await GetUserNameAsync(userId),
                    CreatedAt = DateTime.UtcNow
                };
                _context.CaseUpdates.Add(update);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Clear cache
                await ClearCaseCache(userId, dto.ComplainantId, dto.RespondentId);

                // Get created case with details
                var createdCase = await GetCaseWithDetailsAsync(newCase.Id);
                var responseDto = _mapper.Map<CaseResponseDto>(createdCase);

                _logger.LogInformation("Case created: {CaseNumber} by user {UserId}",
                    caseNumber, userId);

                return ServiceResponse<CaseResponseDto>.CreateSuccess(
                    "Case created successfully", responseDto);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating case");
                return ServiceResponse<CaseResponseDto>.CreateError(
                    "An error occurred while creating case");
            }
        }

        public async Task<ServiceResponse<CaseResponseDto>> GetCaseByIdAsync(
            Guid caseId, string userId)
        {
            try
            {
                var cacheKey = $"case_{caseId}";
                var cachedData = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    var cachedResponse = JsonSerializer.Deserialize<CaseResponseDto>(cachedData);
                    if (cachedResponse != null)
                        return ServiceResponse<CaseResponseDto>.CreateSuccess(
                            "Case retrieved from cache", cachedResponse);
                }

                var caseEntity = await GetCaseWithDetailsAsync(caseId);
                if (caseEntity == null)
                    return ServiceResponse<CaseResponseDto>.CreateError("Case not found");

                // Check authorization
                if (!await HasAccessToCaseAsync(caseId, userId))
                    return ServiceResponse<CaseResponseDto>.CreateError("Unauthorized access");

                var responseDto = _mapper.Map<CaseResponseDto>(caseEntity);

                // Add statistics
                responseDto.DocumentCount = await _context.CaseDocuments
                    .CountAsync(d => d.CaseId == caseId);
                responseDto.HearingCount = await _context.Hearings
                    .CountAsync(h => h.CaseId == caseId && h.IsActive);
                responseDto.NoteCount = await _context.CaseNotes
                    .CountAsync(n => n.CaseId == caseId);
                responseDto.CommunicationCount = await _context.CaseCommunications
                    .CountAsync(c => c.CaseId == caseId);

                // Cache the response
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                };
                await _cache.SetStringAsync(cacheKey,
                    JsonSerializer.Serialize(responseDto), cacheOptions);

                return ServiceResponse<CaseResponseDto>.CreateSuccess(
                    "Case retrieved", responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving case {CaseId}", caseId);
                return ServiceResponse<CaseResponseDto>.CreateError("An error occurred");
            }
        }

        public async Task<PaginatedServiceResponse<List<CaseResponseDto>>> SearchCasesAsync(
            CaseSearchDto searchDto, int page, int pageSize, string userId)
        {
            try
            {
                var query = _context.Cases
                    .Include(c => c.Property)
                    .Include(c => c.TenancyAgreement)
                    .Where(c => c.IsActive)
                    .AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(searchDto.CaseNumber))
                    query = query.Where(c => c.CaseNumber.Contains(searchDto.CaseNumber));

                if (!string.IsNullOrEmpty(searchDto.Title))
                    query = query.Where(c => c.Title.Contains(searchDto.Title));

                if (searchDto.CaseType.HasValue)
                    query = query.Where(c => c.CaseType == searchDto.CaseType.Value);

                if (searchDto.Status.HasValue)
                    query = query.Where(c => c.Status == searchDto.Status.Value);

                if (searchDto.Priority.HasValue)
                    query = query.Where(c => c.Priority == searchDto.Priority.Value);

                if (!string.IsNullOrEmpty(searchDto.ComplainantId))
                    query = query.Where(c => c.ComplainantId == searchDto.ComplainantId);

                if (!string.IsNullOrEmpty(searchDto.RespondentId))
                    query = query.Where(c => c.RespondentId == searchDto.RespondentId);

                if (!string.IsNullOrEmpty(searchDto.ComplainantName))
                    query = query.Where(c => c.ComplainantName.Contains(searchDto.ComplainantName));

                if (!string.IsNullOrEmpty(searchDto.RespondentName))
                    query = query.Where(c => c.RespondentName.Contains(searchDto.RespondentName));

                if (searchDto.PropertyId.HasValue)
                    query = query.Where(c => c.PropertyId == searchDto.PropertyId.Value);

                if (searchDto.TenancyAgreementId.HasValue)
                    query = query.Where(c => c.TenancyAgreementId == searchDto.TenancyAgreementId.Value);

                // FIXED: Convert string to Guid for comparisons
                if (!string.IsNullOrEmpty(searchDto.AssignedOfficerId))
                {
                    if (Guid.TryParse(searchDto.AssignedOfficerId, out var officerId))
                    {
                        query = query.Where(c => c.AssignedOfficerId == officerId);
                    }
                }

                if (!string.IsNullOrEmpty(searchDto.AssignedMediatorId))
                {
                    if (Guid.TryParse(searchDto.AssignedMediatorId, out var mediatorId))
                    {
                        query = query.Where(c => c.AssignedMediatorId == mediatorId);
                    }
                }

                if (searchDto.CreatedFrom.HasValue)
                    query = query.Where(c => c.CreatedAt >= searchDto.CreatedFrom.Value);

                if (searchDto.CreatedTo.HasValue)
                    query = query.Where(c => c.CreatedAt <= searchDto.CreatedTo.Value);

                if (searchDto.IncidentDateFrom.HasValue)
                    query = query.Where(c => c.IncidentDate >= searchDto.IncidentDateFrom.Value);

                if (searchDto.IncidentDateTo.HasValue)
                    query = query.Where(c => c.IncidentDate <= searchDto.IncidentDateTo.Value);

                if (searchDto.IsActive.HasValue)
                    query = query.Where(c => c.IsActive == searchDto.IsActive.Value);

                // Authorization
                var userRole = GetUserRole();
                if (userRole == "Tenant")
                {
                    query = query.Where(c => c.ComplainantId == userId || c.RespondentId == userId);
                }
                else if (userRole == "Landlord")
                {
                    query = query.Where(c => c.ComplainantId == userId || c.RespondentId == userId);
                }
                else if (userRole == "RCD_Officer")
                {
                    // Get the officer's Guid Id from their UserId
                    var officer = await _context.RCDOfficers
                        .FirstOrDefaultAsync(o => o.UserId == userId);

                    if (officer != null)
                    {
                        query = query.Where(c => c.AssignedOfficerId == officer.Id);
                    }
                    else if (userRole != "Admin")
                    {
                        query = query.Where(c => false); // No access if not an officer and not admin
                    }
                }
                else if (userRole == "Mediator")
                {
                    // Get the mediator's Guid Id from their UserId
                    var mediator = await _context.Mediators
                        .FirstOrDefaultAsync(m => m.UserId == userId);

                    if (mediator != null)
                    {
                        query = query.Where(c => c.AssignedMediatorId == mediator.Id);
                    }
                    else
                    {
                        query = query.Where(c => false); // No access if not a mediator
                    }
                }

                // Get total count
                var totalCount = await query.CountAsync();

                // Apply ordering and pagination
                var cases = await query
                    .OrderByDescending(c => c.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var responseDtos = _mapper.Map<List<CaseResponseDto>>(cases);

                return PaginatedServiceResponse<List<CaseResponseDto>>.CreateSuccess(
                    "Cases retrieved successfully",
                    responseDtos,
                    totalCount,
                    page,
                    pageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching cases");
                return PaginatedServiceResponse<List<CaseResponseDto>>.CreateError("An error occurred");
            }
        }

        public async Task<ServiceResponse<CaseResponseDto>> UpdateCaseAsync(
            Guid caseId, UpdateCaseDto dto, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var caseEntity = await _context.Cases
                    .Include(c => c.Property)
                    .FirstOrDefaultAsync(c => c.Id == caseId && c.IsActive);

                if (caseEntity == null)
                    return ServiceResponse<CaseResponseDto>.CreateError("Case not found");

                // Check authorization
                if (caseEntity.ComplainantId != userId && !IsOfficer(userId))
                    return ServiceResponse<CaseResponseDto>.CreateError("Unauthorized to update this case");

                // Store old values for history
                var oldValues = new
                {
                    Title = caseEntity.Title,
                    Description = caseEntity.Description,
                    Status = caseEntity.Status,
                    Priority = caseEntity.Priority,
                    ClaimAmount = caseEntity.ClaimAmount
                };

                // Update fields if provided
                if (!string.IsNullOrEmpty(dto.Title))
                    caseEntity.Title = dto.Title;

                if (!string.IsNullOrEmpty(dto.Description))
                    caseEntity.Description = dto.Description;

                if (dto.Status.HasValue)
                    caseEntity.Status = dto.Status.Value;

                if (dto.Priority.HasValue)
                    caseEntity.Priority = dto.Priority.Value;

                if (dto.ClaimAmount.HasValue)
                    caseEntity.ClaimAmount = dto.ClaimAmount.Value;

                if (dto.AwardedAmount.HasValue)
                    caseEntity.AwardedAmount = dto.AwardedAmount.Value;

                if (dto.Resolution.HasValue)
                    caseEntity.Resolution = dto.Resolution.Value;

                if (!string.IsNullOrEmpty(dto.ResolutionDetails))
                    caseEntity.ResolutionDetails = dto.ResolutionDetails;

                caseEntity.UpdatedAt = DateTime.UtcNow;
                caseEntity.UpdatedBy = userId;

                // If status changed to resolved or closed, update dates
                if (dto.Status.HasValue &&
                    (dto.Status.Value == CaseStatus.Resolved || dto.Status.Value == CaseStatus.Closed))
                {
                    caseEntity.ResolutionDate = DateTime.UtcNow;
                    caseEntity.ClosedAt = DateTime.UtcNow;
                }

                // Add case update
                var update = new CaseUpdate
                {
                    CaseId = caseId,
                    UpdateType = "CaseUpdated",
                    Description = "Case information updated",
                    OldValue = JsonSerializer.Serialize(oldValues),
                    NewValue = JsonSerializer.Serialize(new
                    {
                        caseEntity.Title,
                        caseEntity.Description,
                        caseEntity.Status,
                        caseEntity.Priority,
                        caseEntity.ClaimAmount
                    }),
                    CreatedBy = userId,
                    CreatedByName = await GetUserNameAsync(userId),
                    CreatedAt = DateTime.UtcNow
                };
                _context.CaseUpdates.Add(update);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Clear cache
                await ClearCaseCache(userId, caseEntity.ComplainantId, caseEntity.RespondentId);
                await _cache.RemoveAsync($"case_{caseId}");

                // Get updated case
                var updatedCase = await GetCaseWithDetailsAsync(caseId);
                var responseDto = _mapper.Map<CaseResponseDto>(updatedCase);

                // Send notification if status changed
                if (dto.Status.HasValue && dto.Status.Value != oldValues.Status)
                {
                    await SendCaseStatusNotificationAsync(caseId, oldValues.Status, dto.Status.Value);
                }

                _logger.LogInformation("Case updated: {CaseNumber} by user {UserId}",
                    caseEntity.CaseNumber, userId);

                return ServiceResponse<CaseResponseDto>.CreateSuccess("Case updated successfully", responseDto);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating case {CaseId}", caseId);
                return ServiceResponse<CaseResponseDto>.CreateError("An error occurred while updating case");
            }
        }

        public async Task<ServiceResponse<bool>> SubmitCaseAsync(Guid caseId, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var caseEntity = await _context.Cases
                    .FirstOrDefaultAsync(c => c.Id == caseId && c.IsActive);

                if (caseEntity == null)
                    return ServiceResponse<bool>.CreateError("Case not found");

                if (caseEntity.Status != CaseStatus.Draft)
                    return ServiceResponse<bool>.CreateError("Only draft cases can be submitted");

                // Check if complainant is the one submitting
                if (caseEntity.ComplainantId != userId && !IsOfficer(userId))
                    return ServiceResponse<bool>.CreateError("Only complainant or officer can submit case");

                // Validate required information
                if (string.IsNullOrEmpty(caseEntity.Description) ||
                    string.IsNullOrEmpty(caseEntity.Title))
                    return ServiceResponse<bool>.CreateError("Case title and description are required");

                // Update case
                var oldStatus = caseEntity.Status;
                caseEntity.Status = CaseStatus.Submitted;
                caseEntity.SubmittedAt = DateTime.UtcNow;
                caseEntity.UpdatedAt = DateTime.UtcNow;
                caseEntity.UpdatedBy = userId;

                // Add case update
                var update = new CaseUpdate
                {
                    CaseId = caseId,
                    UpdateType = "CaseSubmitted",
                    Description = "Case submitted for review",
                    OldValue = oldStatus.ToString(),
                    NewValue = CaseStatus.Submitted.ToString(),
                    CreatedBy = userId,
                    CreatedByName = await GetUserNameAsync(userId),
                    CreatedAt = DateTime.UtcNow
                };
                _context.CaseUpdates.Add(update);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Clear cache
                await ClearCaseCache(userId, caseEntity.ComplainantId, caseEntity.RespondentId);

                // Send notifications
                await SendCaseStatusNotificationAsync(caseId, oldStatus, CaseStatus.Submitted);

                _logger.LogInformation("Case submitted: {CaseNumber} by user {UserId}",
                    caseEntity.CaseNumber, userId);

                return ServiceResponse<bool>.CreateSuccess("Case submitted successfully", true);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error submitting case {CaseId}", caseId);
                return ServiceResponse<bool>.CreateError("An error occurred while submitting case");
            }
        }

        public async Task<ServiceResponse<bool>> AssignCaseAsync(
            Guid caseId, AssignCaseDto dto, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var caseEntity = await _context.Cases
                    .FirstOrDefaultAsync(c => c.Id == caseId && c.IsActive);

                if (caseEntity == null)
                    return ServiceResponse<bool>.CreateError("Case not found");

                // Check authorization - only officers can assign cases
                if (!IsOfficer(userId))
                    return ServiceResponse<bool>.CreateError("Unauthorized to assign cases");

                // FIXED: Convert UserId strings to Guid Ids for assignment
                Guid? officerId = null;
                string? officerName = null;
                Guid? mediatorId = null;
                string? mediatorName = null;

                // Validate officer exists
                if (!string.IsNullOrEmpty(dto.OfficerId))
                {
                    var officer = await _context.RCDOfficers
                        .FirstOrDefaultAsync(o => o.UserId == dto.OfficerId && o.IsActive);
                    if (officer == null)
                        return ServiceResponse<bool>.CreateError("Officer not found or inactive");

                    officerId = officer.Id;
                    officerName = dto.OfficerName;
                }

                // Validate mediator exists
                if (!string.IsNullOrEmpty(dto.MediatorId))
                {
                    var mediator = await _context.Mediators
                        .FirstOrDefaultAsync(m => m.UserId == dto.MediatorId && m.IsActive);
                    if (mediator == null)
                        return ServiceResponse<bool>.CreateError("Mediator not found or inactive");

                    mediatorId = mediator.Id;
                    mediatorName = dto.MediatorName;
                }

                // Update assignment with Guid Ids
                caseEntity.AssignedOfficerId = officerId;
                caseEntity.AssignedOfficerName = officerName;
                caseEntity.AssignedMediatorId = mediatorId;
                caseEntity.AssignedMediatorName = mediatorName;
                caseEntity.Status = CaseStatus.UnderReview;
                caseEntity.UpdatedAt = DateTime.UtcNow;
                caseEntity.UpdatedBy = userId;

                // Add case update
                var update = new CaseUpdate
                {
                    CaseId = caseId,
                    UpdateType = "CaseAssigned",
                    Description = $"Case assigned to officer: {officerName}" +
                                 (string.IsNullOrEmpty(mediatorName) ? "" : $", mediator: {mediatorName}"),
                    CreatedBy = userId,
                    CreatedByName = await GetUserNameAsync(userId),
                    CreatedAt = DateTime.UtcNow
                };
                _context.CaseUpdates.Add(update);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Clear cache
                await ClearCaseCache(userId, caseEntity.ComplainantId, caseEntity.RespondentId);

                // Send notifications
                await SendAssignmentNotificationAsync(caseId, dto.OfficerId, dto.MediatorId);

                _logger.LogInformation("Case assigned: {CaseNumber} to officer {OfficerId}",
                    caseEntity.CaseNumber, dto.OfficerId);

                return ServiceResponse<bool>.CreateSuccess("Case assigned successfully", true);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error assigning case {CaseId}", caseId);
                return ServiceResponse<bool>.CreateError("An error occurred while assigning case");
            }
        }

        public async Task<ServiceResponse<bool>> UpdateCaseStatusAsync(
            Guid caseId, CaseStatus newStatus, string reason, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var caseEntity = await _context.Cases
                    .FirstOrDefaultAsync(c => c.Id == caseId && c.IsActive);

                if (caseEntity == null)
                    return ServiceResponse<bool>.CreateError("Case not found");

                // Check authorization
                if (!IsOfficer(userId))
                    return ServiceResponse<bool>.CreateError("Unauthorized to update case status");

                var oldStatus = caseEntity.Status;
                caseEntity.Status = newStatus;
                caseEntity.UpdatedAt = DateTime.UtcNow;
                caseEntity.UpdatedBy = userId;

                // Update relevant dates based on status
                switch (newStatus)
                {
                    case CaseStatus.Resolved:
                        caseEntity.ResolutionDate = DateTime.UtcNow;
                        break;
                    case CaseStatus.Closed:
                        caseEntity.ClosedAt = DateTime.UtcNow;
                        break;
                }

                // Add case update
                var update = new CaseUpdate
                {
                    CaseId = caseId,
                    UpdateType = "StatusChanged",
                    Description = $"Case status changed: {reason}",
                    OldValue = oldStatus.ToString(),
                    NewValue = newStatus.ToString(),
                    CreatedBy = userId,
                    CreatedByName = await GetUserNameAsync(userId),
                    CreatedAt = DateTime.UtcNow
                };
                _context.CaseUpdates.Add(update);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Clear cache
                await ClearCaseCache(userId, caseEntity.ComplainantId, caseEntity.RespondentId);
                await _cache.RemoveAsync($"case_{caseId}");

                // Send notification
                await SendCaseStatusNotificationAsync(caseId, oldStatus, newStatus);

                _logger.LogInformation("Case status updated: {CaseNumber} from {OldStatus} to {NewStatus} by user {UserId}",
                    caseEntity.CaseNumber, oldStatus, newStatus, userId);

                return ServiceResponse<bool>.CreateSuccess("Case status updated successfully", true);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating case status for case {CaseId}", caseId);
                return ServiceResponse<bool>.CreateError("An error occurred while updating case status");
            }
        }

        public async Task<ServiceResponse<bool>> ResolveCaseAsync(Guid caseId, ResolveCaseDto dto, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var caseEntity = await _context.Cases
                    .FirstOrDefaultAsync(c => c.Id == caseId && c.IsActive);

                if (caseEntity == null)
                    return ServiceResponse<bool>.CreateError("Case not found");

                // Check authorization
                if (!IsOfficer(userId))
                    return ServiceResponse<bool>.CreateError("Unauthorized to resolve case");

                if (caseEntity.Status == CaseStatus.Resolved || caseEntity.Status == CaseStatus.Closed)
                    return ServiceResponse<bool>.CreateError("Case is already resolved or closed");

                var oldStatus = caseEntity.Status;
                caseEntity.Status = CaseStatus.Resolved;
                caseEntity.Resolution = dto.Resolution;
                caseEntity.ResolutionDetails = dto.ResolutionDetails;
                caseEntity.AwardedAmount = dto.AwardedAmount;
                caseEntity.ResolutionDate = DateTime.UtcNow;
                caseEntity.UpdatedAt = DateTime.UtcNow;
                caseEntity.UpdatedBy = userId;

                // Add case note with resolution details
                var note = new CaseNote
                {
                    CaseId = caseId,
                    Title = "Case Resolution",
                    Content = $"Case resolved via {dto.Resolution}. Details: {dto.ResolutionDetails}" +
                             (dto.AwardedAmount.HasValue ? $"\nAwarded Amount: {dto.AwardedAmount.Value:C}" : ""),
                    CreatedBy = userId,
                    CreatedByName = await GetUserNameAsync(userId),
                    IsInternal = false,
                    CreatedAt = DateTime.UtcNow
                };
                _context.CaseNotes.Add(note);

                // Add case update
                var update = new CaseUpdate
                {
                    CaseId = caseId,
                    UpdateType = "CaseResolved",
                    Description = $"Case resolved via {dto.Resolution}",
                    OldValue = oldStatus.ToString(),
                    NewValue = CaseStatus.Resolved.ToString(),
                    CreatedBy = userId,
                    CreatedByName = await GetUserNameAsync(userId),
                    CreatedAt = DateTime.UtcNow
                };
                _context.CaseUpdates.Add(update);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Clear cache
                await ClearCaseCache(userId, caseEntity.ComplainantId, caseEntity.RespondentId);
                await _cache.RemoveAsync($"case_{caseId}");

                // Send notification
                await SendCaseStatusNotificationAsync(caseId, oldStatus, CaseStatus.Resolved);

                _logger.LogInformation("Case resolved: {CaseNumber} via {Resolution} by user {UserId}",
                    caseEntity.CaseNumber, dto.Resolution, userId);

                return ServiceResponse<bool>.CreateSuccess("Case resolved successfully", true);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error resolving case {CaseId}", caseId);
                return ServiceResponse<bool>.CreateError("An error occurred while resolving case");
            }
        }

        public async Task<ServiceResponse<bool>> ReopenCaseAsync(Guid caseId, string reason, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var caseEntity = await _context.Cases
                    .FirstOrDefaultAsync(c => c.Id == caseId && c.IsActive);

                if (caseEntity == null)
                    return ServiceResponse<bool>.CreateError("Case not found");

                // Check authorization
                if (!IsOfficer(userId))
                    return ServiceResponse<bool>.CreateError("Unauthorized to reopen case");

                if (caseEntity.Status != CaseStatus.Resolved && caseEntity.Status != CaseStatus.Closed)
                    return ServiceResponse<bool>.CreateError("Only resolved or closed cases can be reopened");

                var oldStatus = caseEntity.Status;
                caseEntity.Status = CaseStatus.Reopened;
                caseEntity.UpdatedAt = DateTime.UtcNow;
                caseEntity.UpdatedBy = userId;

                // Add case note with reopening reason
                var note = new CaseNote
                {
                    CaseId = caseId,
                    Title = "Case Reopened",
                    Content = $"Case reopened. Reason: {reason}",
                    CreatedBy = userId,
                    CreatedByName = await GetUserNameAsync(userId),
                    IsInternal = true,
                    CreatedAt = DateTime.UtcNow
                };
                _context.CaseNotes.Add(note);

                // Add case update
                var update = new CaseUpdate
                {
                    CaseId = caseId,
                    UpdateType = "CaseReopened",
                    Description = $"Case reopened: {reason}",
                    OldValue = oldStatus.ToString(),
                    NewValue = CaseStatus.Reopened.ToString(),
                    CreatedBy = userId,
                    CreatedByName = await GetUserNameAsync(userId),
                    CreatedAt = DateTime.UtcNow
                };
                _context.CaseUpdates.Add(update);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Clear cache
                await ClearCaseCache(userId, caseEntity.ComplainantId, caseEntity.RespondentId);
                await _cache.RemoveAsync($"case_{caseId}");

                // Send notification
                await SendCaseStatusNotificationAsync(caseId, oldStatus, CaseStatus.Reopened);

                _logger.LogInformation("Case reopened: {CaseNumber} by user {UserId}. Reason: {Reason}",
                    caseEntity.CaseNumber, userId, reason);

                return ServiceResponse<bool>.CreateSuccess("Case reopened successfully", true);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error reopening case {CaseId}", caseId);
                return ServiceResponse<bool>.CreateError("An error occurred while reopening case");
            }
        }

        #endregion

        #region Hearing Operations

        public async Task<ServiceResponse<HearingResponseDto>> ScheduleHearingAsync(
            ScheduleHearingDto dto, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var caseEntity = await _context.Cases
                    .FirstOrDefaultAsync(c => c.Id == dto.CaseId && c.IsActive);

                if (caseEntity == null)
                    return ServiceResponse<HearingResponseDto>.CreateError("Case not found");

                // Check authorization - only officers can schedule hearings
                if (!IsOfficer(userId))
                    return ServiceResponse<HearingResponseDto>.CreateError("Unauthorized to schedule hearings");

                // FIXED: Get presiding officer's Guid Id from UserId
                var presidingOfficer = await _context.RCDOfficers
                    .FirstOrDefaultAsync(o => o.UserId == dto.PresidingOfficerId);

                if (presidingOfficer == null)
                    return ServiceResponse<HearingResponseDto>.CreateError("Presiding officer not found");

                // Check for scheduling conflicts
                var conflictingHearings = await _context.Hearings
                    .Where(h => h.PresidingOfficerId == presidingOfficer.Id &&
                               h.HearingDate == dto.HearingDate.Date &&
                               ((h.StartTime <= dto.StartTime && h.EndTime > dto.StartTime) ||
                                (h.StartTime < dto.EndTime && h.EndTime >= dto.EndTime)) &&
                               h.Status != HearingStatus.Cancelled)
                    .ToListAsync();

                if (conflictingHearings.Any())
                    return ServiceResponse<HearingResponseDto>.CreateError(
                        "Hearing time conflicts with existing hearings for the presiding officer");

                // Generate hearing number
                var hearingNumber = await GenerateHearingNumberAsync();

                // Create hearing
                var hearing = new Hearing
                {
                    CaseId = dto.CaseId,
                    HearingNumber = hearingNumber,
                    Title = dto.Title,
                    Description = dto.Description,
                    HearingDate = dto.HearingDate.Date,
                    StartTime = dto.StartTime,
                    EndTime = dto.EndTime,
                    Location = dto.Location,
                    VirtualMeetingLink = dto.VirtualMeetingLink,
                    Status = HearingStatus.Scheduled,
                    PresidingOfficerId = presidingOfficer.Id,
                    PresidingOfficerName = presidingOfficer.FullName,
                    ClerkId = null, // Will need to get clerk from dto if provided
                    ClerkName = dto.ClerkName,
                    IsActive = true,
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Hearings.Add(hearing);

                // Update case status
                var oldStatus = caseEntity.Status;
                caseEntity.Status = CaseStatus.ScheduledForHearing;
                caseEntity.UpdatedAt = DateTime.UtcNow;
                caseEntity.UpdatedBy = userId;

                // Add case update
                var update = new CaseUpdate
                {
                    CaseId = dto.CaseId,
                    UpdateType = "HearingScheduled",
                    Description = $"Hearing scheduled for {dto.HearingDate:yyyy-MM-dd} at {dto.Location}",
                    OldValue = oldStatus.ToString(),
                    NewValue = CaseStatus.ScheduledForHearing.ToString(),
                    CreatedBy = userId,
                    CreatedByName = await GetUserNameAsync(userId),
                    CreatedAt = DateTime.UtcNow
                };
                _context.CaseUpdates.Add(update);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Clear cache
                await ClearCaseCache(userId, caseEntity.ComplainantId, caseEntity.RespondentId);

                // Send notifications to participants
                await SendHearingScheduledNotificationAsync(hearing.Id);

                // Get created hearing
                var createdHearing = await _context.Hearings
                    .Include(h => h.Case)
                    .Include(h => h.Participants)
                    .FirstOrDefaultAsync(h => h.Id == hearing.Id);

                var responseDto = _mapper.Map<HearingResponseDto>(createdHearing);

                _logger.LogInformation("Hearing scheduled: {HearingNumber} for case {CaseNumber}",
                    hearingNumber, caseEntity.CaseNumber);

                return ServiceResponse<HearingResponseDto>.CreateSuccess(
                    "Hearing scheduled successfully", responseDto);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error scheduling hearing for case {CaseId}", dto.CaseId);
                return ServiceResponse<HearingResponseDto>.CreateError(
                    "An error occurred while scheduling hearing");
            }
        }

        public async Task<ServiceResponse<HearingResponseDto>> UpdateHearingAsync(
            Guid hearingId, UpdateHearingDto dto, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var hearing = await _context.Hearings
                    .Include(h => h.Case)
                    .FirstOrDefaultAsync(h => h.Id == hearingId && h.IsActive);

                if (hearing == null)
                    return ServiceResponse<HearingResponseDto>.CreateError("Hearing not found");

                // Check authorization
                if (!IsOfficer(userId))
                    return ServiceResponse<HearingResponseDto>.CreateError("Unauthorized to update hearing");

                // Update fields
                if (!string.IsNullOrEmpty(dto.Title))
                    hearing.Title = dto.Title;

                if (!string.IsNullOrEmpty(dto.Description))
                    hearing.Description = dto.Description;

                if (dto.HearingDate.HasValue)
                    hearing.HearingDate = dto.HearingDate.Value.Date;

                if (dto.StartTime.HasValue)
                    hearing.StartTime = dto.StartTime.Value;

                if (dto.EndTime.HasValue)
                    hearing.EndTime = dto.EndTime.Value;

                if (!string.IsNullOrEmpty(dto.Location))
                    hearing.Location = dto.Location;

                if (!string.IsNullOrEmpty(dto.VirtualMeetingLink))
                    hearing.VirtualMeetingLink = dto.VirtualMeetingLink;

                if (dto.Status.HasValue)
                    hearing.Status = dto.Status.Value;

                // FIXED: Handle presiding officer update with Guid conversion
                if (!string.IsNullOrEmpty(dto.PresidingOfficerId))
                {
                    var presidingOfficer = await _context.RCDOfficers
                        .FirstOrDefaultAsync(o => o.UserId == dto.PresidingOfficerId);

                    if (presidingOfficer == null)
                        return ServiceResponse<HearingResponseDto>.CreateError("Presiding officer not found");

                    hearing.PresidingOfficerId = presidingOfficer.Id;
                }

                if (!string.IsNullOrEmpty(dto.PresidingOfficerName))
                    hearing.PresidingOfficerName = dto.PresidingOfficerName;

                // FIXED: Handle clerk update with Guid conversion
                if (!string.IsNullOrEmpty(dto.ClerkId))
                {
                    var clerk = await _context.RCDOfficers
                        .FirstOrDefaultAsync(o => o.UserId == dto.ClerkId);

                    if (clerk != null)
                    {
                        hearing.ClerkId = clerk.Id;
                    }
                }

                if (!string.IsNullOrEmpty(dto.ClerkName))
                    hearing.ClerkName = dto.ClerkName;

                if (!string.IsNullOrEmpty(dto.Outcome))
                    hearing.Outcome = dto.Outcome;

                if (!string.IsNullOrEmpty(dto.Minutes))
                    hearing.Minutes = dto.Minutes;

                hearing.UpdatedAt = DateTime.UtcNow;
                hearing.UpdatedBy = userId;

                // Add case update
                if (hearing.Case != null)
                {
                    var update = new CaseUpdate
                    {
                        CaseId = hearing.Case.Id,
                        UpdateType = "HearingUpdated",
                        Description = $"Hearing updated: {hearing.Title}",
                        CreatedBy = userId,
                        CreatedByName = await GetUserNameAsync(userId),
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.CaseUpdates.Add(update);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Clear cache
                if (hearing.Case != null)
                {
                    await ClearCaseCache(userId, hearing.Case.ComplainantId, hearing.Case.RespondentId);
                }
                await _cache.RemoveAsync($"hearing_{hearingId}");

                // Get updated hearing
                var updatedHearing = await _context.Hearings
                    .Include(h => h.Case)
                    .Include(h => h.Participants)
                    .FirstOrDefaultAsync(h => h.Id == hearingId);

                var responseDto = _mapper.Map<HearingResponseDto>(updatedHearing);

                _logger.LogInformation("Hearing updated: {HearingNumber} by user {UserId}",
                    hearing.HearingNumber, userId);

                return ServiceResponse<HearingResponseDto>.CreateSuccess("Hearing updated successfully", responseDto);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating hearing {HearingId}", hearingId);
                return ServiceResponse<HearingResponseDto>.CreateError("An error occurred while updating hearing");
            }
        }

        public async Task<ServiceResponse<bool>> CancelHearingAsync(Guid hearingId, string reason, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var hearing = await _context.Hearings
                    .Include(h => h.Case)
                    .FirstOrDefaultAsync(h => h.Id == hearingId && h.IsActive);

                if (hearing == null)
                    return ServiceResponse<bool>.CreateError("Hearing not found");

                // Check authorization
                if (!IsOfficer(userId))
                    return ServiceResponse<bool>.CreateError("Unauthorized to cancel hearing");

                if (hearing.Status == HearingStatus.Cancelled)
                    return ServiceResponse<bool>.CreateError("Hearing is already cancelled");

                var oldStatus = hearing.Status;
                hearing.Status = HearingStatus.Cancelled;
                hearing.UpdatedAt = DateTime.UtcNow;
                hearing.UpdatedBy = userId;

                // Update case status if needed
                if (hearing.Case != null && hearing.Case.Status == CaseStatus.ScheduledForHearing)
                {
                    hearing.Case.Status = CaseStatus.UnderReview;
                    hearing.Case.UpdatedAt = DateTime.UtcNow;
                    hearing.Case.UpdatedBy = userId;
                }

                // Add case update
                if (hearing.Case != null)
                {
                    var update = new CaseUpdate
                    {
                        CaseId = hearing.Case.Id,
                        UpdateType = "HearingCancelled",
                        Description = $"Hearing cancelled: {reason}",
                        OldValue = oldStatus.ToString(),
                        NewValue = HearingStatus.Cancelled.ToString(),
                        CreatedBy = userId,
                        CreatedByName = await GetUserNameAsync(userId),
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.CaseUpdates.Add(update);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Clear cache
                if (hearing.Case != null)
                {
                    await ClearCaseCache(userId, hearing.Case.ComplainantId, hearing.Case.RespondentId);
                }

                // Send notifications
                await SendHearingCancelledNotificationAsync(hearingId, reason);

                _logger.LogInformation("Hearing cancelled: {HearingNumber} by user {UserId}. Reason: {Reason}",
                    hearing.HearingNumber, userId, reason);

                return ServiceResponse<bool>.CreateSuccess("Hearing cancelled successfully", true);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error cancelling hearing {HearingId}", hearingId);
                return ServiceResponse<bool>.CreateError("An error occurred while cancelling hearing");
            }
        }

        public async Task<ServiceResponse<bool>> AddHearingParticipantAsync(
            Guid hearingId, AddParticipantDto dto, string userId)
        {
            try
            {
                var hearing = await _context.Hearings
                    .FirstOrDefaultAsync(h => h.Id == hearingId && h.IsActive);

                if (hearing == null)
                    return ServiceResponse<bool>.CreateError("Hearing not found");

                // Check authorization
                if (!IsOfficer(userId))
                    return ServiceResponse<bool>.CreateError("Unauthorized to add participants");

                // Check if participant already exists
                var existingParticipant = await _context.HearingParticipants
                    .FirstOrDefaultAsync(p => p.HearingId == hearingId &&
                                             p.ParticipantId == dto.ParticipantId);

                if (existingParticipant != null)
                    return ServiceResponse<bool>.CreateError("Participant already added to this hearing");

                var participant = new HearingParticipant
                {
                    HearingId = hearingId,
                    ParticipantId = dto.ParticipantId,
                    ParticipantName = dto.ParticipantName,
                    ParticipantEmail = dto.ParticipantEmail,
                    ParticipantPhone = dto.ParticipantPhone,
                    ParticipantType = dto.ParticipantType,
                    Role = dto.Role,
                    Organization = dto.Organization,
                    IsRequired = dto.IsRequired,
                    //CreatedAt = DateTime.UtcNow
                };

                _context.HearingParticipants.Add(participant);

                // Add case update if hearing has a case
                var hearingWithCase = await _context.Hearings
                    .Include(h => h.Case)
                    .FirstOrDefaultAsync(h => h.Id == hearingId);

                if (hearingWithCase?.Case != null)
                {
                    var update = new CaseUpdate
                    {
                        CaseId = hearingWithCase.Case.Id,
                        UpdateType = "HearingParticipantAdded",
                        Description = $"Added participant to hearing: {dto.ParticipantName} ({dto.ParticipantType})",
                        CreatedBy = userId,
                        CreatedByName = await GetUserNameAsync(userId),
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.CaseUpdates.Add(update);
                }

                await _context.SaveChangesAsync();

                // Clear cache
                await _cache.RemoveAsync($"hearing_{hearingId}");

                _logger.LogInformation("Participant added to hearing {HearingId}: {ParticipantName}",
                    hearingId, dto.ParticipantName);

                return ServiceResponse<bool>.CreateSuccess("Participant added successfully", true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding participant to hearing {HearingId}", hearingId);
                return ServiceResponse<bool>.CreateError("An error occurred while adding participant");
            }
        }

        public async Task<ServiceResponse<bool>> RecordHearingOutcomeAsync(
            Guid hearingId, RecordHearingOutcomeDto dto, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var hearing = await _context.Hearings
                    .Include(h => h.Case)
                    .FirstOrDefaultAsync(h => h.Id == hearingId && h.IsActive);

                if (hearing == null)
                    return ServiceResponse<bool>.CreateError("Hearing not found");

                // Check authorization
                if (!IsOfficer(userId))
                    return ServiceResponse<bool>.CreateError("Unauthorized to record hearing outcome");

                if (hearing.Status != HearingStatus.Completed)
                    return ServiceResponse<bool>.CreateError("Only completed hearings can have outcomes recorded");

                var oldStatus = hearing.Status;
                hearing.Outcome = dto.Outcome;
                hearing.Minutes = dto.Minutes;
                hearing.Status = dto.Status;
                hearing.UpdatedAt = DateTime.UtcNow;
                hearing.UpdatedBy = userId;

                // Add case update
                if (hearing.Case != null)
                {
                    var update = new CaseUpdate
                    {
                        CaseId = hearing.Case.Id,
                        UpdateType = "HearingOutcomeRecorded",
                        Description = $"Hearing outcome recorded: {dto.Outcome}",
                        CreatedBy = userId,
                        CreatedByName = await GetUserNameAsync(userId),
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.CaseUpdates.Add(update);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Clear cache
                if (hearing.Case != null)
                {
                    await ClearCaseCache(userId, hearing.Case.ComplainantId, hearing.Case.RespondentId);
                }

                _logger.LogInformation("Hearing outcome recorded: {HearingNumber} - {Outcome}",
                    hearing.HearingNumber, dto.Outcome);

                return ServiceResponse<bool>.CreateSuccess("Hearing outcome recorded successfully", true);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error recording hearing outcome for hearing {HearingId}", hearingId);
                return ServiceResponse<bool>.CreateError("An error occurred while recording hearing outcome");
            }
        }

        #endregion

        #region Document Operations

        public async Task<ServiceResponse<CaseDocumentDto>> UploadCaseDocumentAsync(
            UploadCaseDocumentDto dto, string userId)
        {
            try
            {
                var caseEntity = await _context.Cases
                    .FirstOrDefaultAsync(c => c.Id == dto.CaseId && c.IsActive);

                if (caseEntity == null)
                    return ServiceResponse<CaseDocumentDto>.CreateError("Case not found");

                // Check authorization
                if (!await HasAccessToCaseAsync(dto.CaseId, userId))
                    return ServiceResponse<CaseDocumentDto>.CreateError("Unauthorized to upload documents");

                // Validate file
                if (dto.File == null || dto.File.Length == 0)
                    return ServiceResponse<CaseDocumentDto>.CreateError("File is required");

                var maxFileSize = 10 * 1024 * 1024; // 10MB
                if (dto.File.Length > maxFileSize)
                    return ServiceResponse<CaseDocumentDto>.CreateError("File size exceeds 10MB limit");

                var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };
                var fileExtension = Path.GetExtension(dto.File.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                    return ServiceResponse<CaseDocumentDto>.CreateError("Invalid file type. Allowed: PDF, DOC, DOCX, JPG, PNG");

                // Generate unique filename
                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine("uploads", "cases", dto.CaseId.ToString(), fileName);

                // In production, save to cloud storage or file system
                // For now, we'll just store the path
                var document = new CaseDocument
                {
                    CaseId = dto.CaseId,
                    DocumentType = dto.DocumentType,
                    FileName = dto.FileName,
                    FilePath = filePath,
                    FileSize = FormatFileSize(dto.File.Length),
                    Description = dto.Description,
                    UploadedBy = userId,
                    UploadedAt = DateTime.UtcNow,
                    IsVerified = false
                };

                _context.CaseDocuments.Add(document);

                // Add case update
                var update = new CaseUpdate
                {
                    CaseId = dto.CaseId,
                    UpdateType = "DocumentUploaded",
                    Description = $"Document uploaded: {dto.FileName}",
                    CreatedBy = userId,
                    CreatedByName = await GetUserNameAsync(userId),
                    CreatedAt = DateTime.UtcNow
                };
                _context.CaseUpdates.Add(update);

                await _context.SaveChangesAsync();

                // Clear cache
                await _cache.RemoveAsync($"case_{dto.CaseId}");

                var responseDto = _mapper.Map<CaseDocumentDto>(document);

                _logger.LogInformation("Document uploaded for case {CaseId}: {FileName}",
                    dto.CaseId, dto.FileName);

                return ServiceResponse<CaseDocumentDto>.CreateSuccess("Document uploaded successfully", responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document for case {CaseId}", dto.CaseId);
                return ServiceResponse<RentControlSystem.API.DTOs.CaseDocumentDto>.CreateError("An error occurred while uploading document");
            }
        }

        public async Task<ServiceResponse<bool>> VerifyDocumentAsync(
            Guid documentId, bool isVerified, string notes, string userId)
        {
            try
            {
                var document = await _context.CaseDocuments
                    .Include(d => d.Case)
                    .FirstOrDefaultAsync(d => d.Id == documentId);

                if (document == null)
                    return ServiceResponse<bool>.CreateError("Document not found");

                // Check authorization - only officers can verify documents
                if (!IsOfficer(userId))
                    return ServiceResponse<bool>.CreateError("Unauthorized to verify documents");

                document.IsVerified = isVerified;
                document.VerificationNotes = notes;
                document.VerifiedAt = DateTime.UtcNow;
                document.VerifiedBy = userId;

                // Add case update
                if (document.Case != null)
                {
                    var update = new CaseUpdate
                    {
                        CaseId = document.Case.Id,
                        UpdateType = "DocumentVerified",
                        Description = $"Document verified: {document.FileName} - {(isVerified ? "Approved" : "Rejected")}",
                        CreatedBy = userId,
                        CreatedByName = await GetUserNameAsync(userId),
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.CaseUpdates.Add(update);
                }

                await _context.SaveChangesAsync();

                // Clear cache
                if (document.Case != null)
                {
                    await _cache.RemoveAsync($"case_{document.Case.Id}");
                }

                _logger.LogInformation("Document verified: {DocumentId} - {Status} by user {UserId}",
                    documentId, isVerified ? "Verified" : "Rejected", userId);

                return ServiceResponse<bool>.CreateSuccess("Document verification updated successfully", true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying document {DocumentId}", documentId);
                return ServiceResponse<bool>.CreateError("An error occurred while verifying document");
            }
        }

        #endregion

        #region Communication Operations

        public async Task<ServiceResponse<CaseCommunicationDto>> SendCommunicationAsync(
            SendCommunicationDto dto, string userId)
        {
            try
            {
                var caseEntity = await _context.Cases
                    .FirstOrDefaultAsync(c => c.Id == dto.CaseId && c.IsActive);

                if (caseEntity == null)
                    return ServiceResponse<CaseCommunicationDto>.CreateError("Case not found");

                // Check authorization
                if (!await HasAccessToCaseAsync(dto.CaseId, userId))
                    return ServiceResponse<CaseCommunicationDto>.CreateError("Unauthorized to send communication");

                var communication = new CaseCommunication
                {
                    CaseId = dto.CaseId,
                    CommunicationType = dto.CommunicationType,
                    Subject = dto.Subject,
                    Content = dto.Content,
                    SenderId = userId,
                    SenderName = await GetUserNameAsync(userId),
                    SenderEmail = dto.SenderEmail,
                    SenderPhone = dto.SenderPhone,
                    RecipientId = dto.RecipientId,
                    RecipientName = dto.RecipientName,
                    RecipientEmail = dto.RecipientEmail,
                    RecipientPhone = dto.RecipientPhone,
                    AttachmentPath = dto.AttachmentPath,
                    IsSent = true,
                    SentAt = DateTime.UtcNow,
                    IsDelivered = false, // Will be updated when delivered
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.CaseCommunications.Add(communication);

                // Add case update
                var update = new CaseUpdate
                {
                    CaseId = dto.CaseId,
                    UpdateType = "CommunicationSent",
                    Description = $"Communication sent: {dto.Subject} to {dto.RecipientName}",
                    CreatedBy = userId,
                    CreatedByName = await GetUserNameAsync(userId),
                    CreatedAt = DateTime.UtcNow
                };
                _context.CaseUpdates.Add(update);

                await _context.SaveChangesAsync();

                // Clear cache
                await _cache.RemoveAsync($"case_{dto.CaseId}");

                var responseDto = _mapper.Map<CaseCommunicationDto>(communication);

                // Send actual communication (email/SMS)
                await SendActualCommunicationAsync(communication);

                _logger.LogInformation("Communication sent for case {CaseId}: {Subject}",
                    dto.CaseId, dto.Subject);

                return ServiceResponse<CaseCommunicationDto>.CreateSuccess("Communication sent successfully", responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending communication for case {CaseId}", dto.CaseId);
                return ServiceResponse<CaseCommunicationDto>.CreateError("An error occurred while sending communication");
            }
        }

        #endregion

        #region Reporting

        public async Task<ServiceResponse<CaseStatisticsDto>> GetCaseStatisticsAsync(string userId)
        {
            try
            {
                var cacheKey = $"case_stats_{userId}";
                var cachedData = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    var cachedStats = JsonSerializer.Deserialize<CaseStatisticsDto>(cachedData);
                    if (cachedStats != null)
                        return ServiceResponse<CaseStatisticsDto>.CreateSuccess(
                            "Statistics retrieved from cache", cachedStats);
                }

                var userRole = GetUserRole();
                IQueryable<Case> query = _context.Cases.Where(c => c.IsActive);

                // Apply filters based on user role
                if (userRole == "Tenant")
                {
                    query = query.Where(c => c.ComplainantId == userId || c.RespondentId == userId);
                }
                else if (userRole == "Landlord")
                {
                    query = query.Where(c => c.ComplainantId == userId || c.RespondentId == userId);
                }
                else if (userRole == "RCD_Officer")
                {
                    // FIXED: Get officer's Guid Id from UserId
                    var officer = await _context.RCDOfficers
                        .FirstOrDefaultAsync(o => o.UserId == userId);

                    if (officer != null)
                    {
                        query = query.Where(c => c.AssignedOfficerId == officer.Id);
                    }
                    else if (userRole != "Admin")
                    {
                        query = query.Where(c => false);
                    }
                }
                else if (userRole == "Mediator")
                {
                    // FIXED: Get mediator's Guid Id from UserId
                    var mediator = await _context.Mediators
                        .FirstOrDefaultAsync(m => m.UserId == userId);

                    if (mediator != null)
                    {
                        query = query.Where(c => c.AssignedMediatorId == mediator.Id);
                    }
                    else
                    {
                        query = query.Where(c => false);
                    }
                }

                var statistics = new CaseStatisticsDto
                {
                    TotalCases = await query.CountAsync(),
                    DraftCases = await query.CountAsync(c => c.Status == CaseStatus.Draft),
                    SubmittedCases = await query.CountAsync(c => c.Status == CaseStatus.Submitted),
                    UnderReviewCases = await query.CountAsync(c => c.Status == CaseStatus.UnderReview),
                    InvestigationCases = await query.CountAsync(c => c.Status == CaseStatus.Investigation),
                    HearingScheduledCases = await query.CountAsync(c => c.Status == CaseStatus.ScheduledForHearing),
                    ResolvedCases = await query.CountAsync(c => c.Status == CaseStatus.Resolved),
                    ClosedCases = await query.CountAsync(c => c.Status == CaseStatus.Closed),
                    ReopenedCases = await query.CountAsync(c => c.Status == CaseStatus.Reopened),
                    WithdrawnCases = await query.CountAsync(c => c.Status == CaseStatus.Withdrawn),
                    DismissedCases = await query.CountAsync(c => c.Status == CaseStatus.Dismissed),

                    CasesByType = await query
                        .GroupBy(c => c.CaseType)
                        .Select(g => new CaseTypeCountDto
                        {
                            CaseType = g.Key,
                            Count = g.Count()
                        })
                        .ToListAsync(),

                    CasesByPriority = await query
                        .GroupBy(c => c.Priority)
                        .Select(g => new PriorityCountDto
                        {
                            Priority = g.Key,
                            Count = g.Count()
                        })
                        .ToListAsync(),

                    CasesByMonth = await query
                        .Where(c => c.CreatedAt >= DateTime.UtcNow.AddMonths(-6))
                        .GroupBy(c => new { Year = c.CreatedAt.Year, Month = c.CreatedAt.Month })
                        .Select(g => new MonthCountDto
                        {
                            Year = g.Key.Year,
                            Month = g.Key.Month,
                            Count = g.Count()
                        })
                        .ToListAsync(),

                    AverageResolutionTime = await CalculateAverageResolutionTimeAsync(query),
                    ResolutionRate = await CalculateResolutionRateAsync(query),

                    CasesRequiringAttention = await query
                        .CountAsync(c => (c.Priority == CasePriority.Critical || c.Priority == CasePriority.High) &&
                                        (c.Status == CaseStatus.Submitted || c.Status == CaseStatus.UnderReview) &&
                                        c.CreatedAt <= DateTime.UtcNow.AddDays(-14)),

                    OverdueCases = await query
                        .CountAsync(c => c.Status == CaseStatus.Submitted &&
                                        c.CreatedAt <= DateTime.UtcNow.AddDays(-30)),

                    UpcomingHearings = await _context.Hearings
                        .CountAsync(h => h.HearingDate >= DateTime.UtcNow.Date &&
                                       h.HearingDate <= DateTime.UtcNow.AddDays(7).Date &&
                                       h.Status == HearingStatus.Scheduled)
                };

                // Cache for 15 minutes
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
                };
                await _cache.SetStringAsync(cacheKey,
                    JsonSerializer.Serialize(statistics), cacheOptions);

                return ServiceResponse<CaseStatisticsDto>.CreateSuccess(
                    "Case statistics retrieved", statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting case statistics for user {UserId}", userId);
                return ServiceResponse<CaseStatisticsDto>.CreateError("An error occurred");
            }
        }

        public async Task<ServiceResponse<HearingCalendarDto>> GetHearingCalendarAsync(
            DateTime fromDate, DateTime toDate, string userId)
        {
            try
            {
                var cacheKey = $"hearing_calendar_{userId}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}";
                var cachedData = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    var cachedCalendar = JsonSerializer.Deserialize<HearingCalendarDto>(cachedData);
                    if (cachedCalendar != null)
                        return ServiceResponse<HearingCalendarDto>.CreateSuccess(
                            "Calendar retrieved from cache", cachedCalendar);
                }

                IQueryable<Hearing> query = _context.Hearings
                    .Include(h => h.Case)
                    .Where(h => h.IsActive &&
                               h.HearingDate >= fromDate.Date &&
                               h.HearingDate <= toDate.Date &&
                               h.Status == HearingStatus.Scheduled);

                // Apply authorization filters
                var userRole = GetUserRole();
                if (userRole == "Tenant" || userRole == "Landlord")
                {
                    query = query.Where(h => h.Case != null &&
                                           (h.Case.ComplainantId == userId || h.Case.RespondentId == userId));
                }
                else if (userRole == "RCD_Officer")
                {
                    // FIXED: Get officer's Guid Id from UserId
                    var officer = await _context.RCDOfficers
                        .FirstOrDefaultAsync(o => o.UserId == userId);

                    if (officer != null)
                    {
                        query = query.Where(h => h.PresidingOfficerId == officer.Id ||
                                               (h.Case != null && h.Case.AssignedOfficerId == officer.Id));
                    }
                    else if (userRole != "Admin")
                    {
                        query = query.Where(h => false);
                    }
                }

                var hearings = await query
                    .OrderBy(h => h.HearingDate)
                    .ThenBy(h => h.StartTime)
                    .ToListAsync();

                var calendarItems = hearings.Select(h => new HearingCalendarItemDto
                {
                    HearingId = h.Id,
                    HearingNumber = h.HearingNumber,
                    CaseId = h.Case?.Id ?? Guid.Empty,
                    CaseNumber = h.Case?.CaseNumber ?? string.Empty,
                    Title = h.Title,
                    HearingDate = h.HearingDate,
                    StartTime = h.StartTime,
                    EndTime = h.EndTime,
                    Location = h.Location,
                    VirtualMeetingLink = h.VirtualMeetingLink,
                    Status = h.Status,
                    PresidingOfficerName = h.PresidingOfficerName,
                    ComplainantName = h.Case?.ComplainantName ?? string.Empty,
                    RespondentName = h.Case?.RespondentName ?? string.Empty,
                    IsToday = h.HearingDate.Date == DateTime.UtcNow.Date,
                    IsPast = h.HearingDate.Date < DateTime.UtcNow.Date,
                    IsUpcoming = h.HearingDate.Date > DateTime.UtcNow.Date
                }).ToList();

                var calendarDto = new HearingCalendarDto
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    Hearings = calendarItems
                };

                // Cache for 5 minutes
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                };
                await _cache.SetStringAsync(cacheKey,
                    JsonSerializer.Serialize(calendarDto), cacheOptions);

                return ServiceResponse<HearingCalendarDto>.CreateSuccess(
                    "Hearing calendar retrieved", calendarDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting hearing calendar for user {UserId}", userId);
                return ServiceResponse<HearingCalendarDto>.CreateError("An error occurred");
            }
        }

        public async Task<ServiceResponse<List<CaseDashboardDto>>> GetDashboardCasesAsync(string userId)
        {
            try
            {
                var cacheKey = $"dashboard_cases_{userId}";
                var cachedData = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    var cachedDashboard = JsonSerializer.Deserialize<List<CaseDashboardDto>>(cachedData);
                    if (cachedDashboard != null)
                        return ServiceResponse<List<CaseDashboardDto>>.CreateSuccess(
                            "Dashboard cases retrieved from cache", cachedDashboard);
                }

                var userRole = GetUserRole();
                IQueryable<Case> query = _context.Cases.Where(c => c.IsActive);

                // Apply filters based on user role
                if (userRole == "Tenant")
                {
                    query = query.Where(c => c.ComplainantId == userId || c.RespondentId == userId);
                }
                else if (userRole == "Landlord")
                {
                    query = query.Where(c => c.ComplainantId == userId || c.RespondentId == userId);
                }
                else if (userRole == "RCD_Officer")
                {
                    // FIXED: Get officer's Guid Id from UserId
                    var officer = await _context.RCDOfficers
                        .FirstOrDefaultAsync(o => o.UserId == userId);

                    if (officer != null)
                    {
                        query = query.Where(c => c.AssignedOfficerId == officer.Id);
                    }
                    else if (userRole != "Admin")
                    {
                        query = query.Where(c => false);
                    }
                }
                else if (userRole == "Mediator")
                {
                    // FIXED: Get mediator's Guid Id from UserId
                    var mediator = await _context.Mediators
                        .FirstOrDefaultAsync(m => m.UserId == userId);

                    if (mediator != null)
                    {
                        query = query.Where(c => c.AssignedMediatorId == mediator.Id);
                    }
                    else
                    {
                        query = query.Where(c => false);
                    }
                }

                // Get cases requiring attention
                var attentionCases = await query
                    .Where(c => c.Priority == CasePriority.Critical ||
                               c.Status == CaseStatus.Submitted ||
                               c.Status == CaseStatus.UnderReview ||
                               c.Status == CaseStatus.ScheduledForHearing)
                    .OrderByDescending(c => c.Priority)
                    .ThenByDescending(c => c.CreatedAt)
                    .Take(10)
                    .Select(c => new CaseDashboardDto
                    {
                        Id = c.Id,
                        CaseNumber = c.CaseNumber,
                        Title = c.Title,
                        CaseType = c.CaseType,
                        Status = c.Status,
                        Priority = c.Priority,
                        ComplainantName = c.ComplainantName,
                        RespondentName = c.RespondentName,
                        AssignedOfficerName = c.AssignedOfficerName,
                        CreatedAt = c.CreatedAt,
                        NextHearingDate = c.Hearings
                            .Where(h => h.IsActive && h.Status == HearingStatus.Scheduled)
                            .OrderBy(h => h.HearingDate)
                            .Select(h => h.HearingDate)
                            .FirstOrDefault(),
                        DaysSinceCreation = (int)(DateTime.UtcNow - c.CreatedAt).TotalDays,
                        RequiresAttention = c.Priority == CasePriority.Critical ||
                                          (c.Status == CaseStatus.Submitted &&
                                           (DateTime.UtcNow - c.CreatedAt).TotalDays > 7) ||
                                          (c.Status == CaseStatus.UnderReview &&
                                           (DateTime.UtcNow - c.CreatedAt).TotalDays > 14)
                    })
                    .ToListAsync();

                // Cache for 5 minutes
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                };
                await _cache.SetStringAsync(cacheKey,
                    JsonSerializer.Serialize(attentionCases), cacheOptions);

                return ServiceResponse<List<CaseDashboardDto>>.CreateSuccess(
                    "Dashboard cases retrieved", attentionCases);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard cases for user {UserId}", userId);
                return ServiceResponse<List<CaseDashboardDto>>.CreateError("An error occurred");
            }
        }

        #endregion

        #region Helper Methods

        private async Task<string> GenerateCaseNumberAsync(CaseType caseType)
        {
            var prefix = caseType switch
            {
                CaseType.RentArrears => "RA",
                CaseType.PropertyMaintenance => "PM",
                CaseType.IllegalEviction => "IE",
                CaseType.RentIncreaseDispute => "RI",
                CaseType.SecurityDepositDispute => "SD",
                CaseType.Harassment => "HR",
                CaseType.UtilityDispute => "UD",
                CaseType.RepairNeglect => "RN",
                CaseType.Overcrowding => "OC",
                CaseType.HealthAndSafety => "HS",
                CaseType.NoiseComplaint => "NC",
                CaseType.LeaseViolation => "LV",
                _ => "OT"
            };

            var year = DateTime.UtcNow.Year;
            var month = DateTime.UtcNow.Month.ToString("D2");

            var sequence = await _context.Cases
                .CountAsync(c => c.CaseNumber.StartsWith($"{prefix}/{year}/{month}/")) + 1;

            return $"{prefix}/{year}/{month}/{sequence:D4}";
        }

        private async Task<string> GenerateHearingNumberAsync()
        {
            var year = DateTime.UtcNow.Year;
            var month = DateTime.UtcNow.Month.ToString("D2");

            var sequence = await _context.Hearings
                .CountAsync(h => h.HearingNumber.StartsWith($"HE/{year}/{month}/")) + 1;

            return $"HE/{year}/{month}/{sequence:D4}";
        }

        private CasePriority DeterminePriority(CaseType caseType, decimal? claimAmount)
        {
            // Critical cases
            if (caseType == CaseType.IllegalEviction ||
                caseType == CaseType.Harassment ||
                caseType == CaseType.HealthAndSafety)
                return CasePriority.Critical;

            // High priority cases
            if (caseType == CaseType.RentArrears && claimAmount > 5000)
                return CasePriority.High;

            if (caseType == CaseType.RepairNeglect)
                return CasePriority.High;

            // Medium priority for others
            return CasePriority.Medium;
        }

        private async Task<Case?> GetCaseWithDetailsAsync(Guid caseId)
        {
            return await _context.Cases
                .Include(c => c.Property)
                .Include(c => c.TenancyAgreement)
                .Include(c => c.Documents.OrderByDescending(d => d.UploadedAt))
                .Include(c => c.Hearings.Where(h => h.IsActive).OrderBy(h => h.HearingDate))
                .Include(c => c.Notes.OrderByDescending(n => n.CreatedAt))
                .Include(c => c.Participants)
                .Include(c => c.Updates.OrderByDescending(u => u.CreatedAt))
                .Include(c => c.Communications.OrderByDescending(c => c.CreatedAt))
                .FirstOrDefaultAsync(c => c.Id == caseId && c.IsActive);
        }

        private async Task<bool> HasAccessToCaseAsync(Guid caseId, string userId)
        {
            var userRole = GetUserRole();
            var caseEntity = await _context.Cases
                .FirstOrDefaultAsync(c => c.Id == caseId);

            if (caseEntity == null)
                return false;

            // Admin has access to all cases
            if (userRole == "Admin")
                return true;

            // RCD Officers have access to assigned cases
            if (userRole == "RCD_Officer")
            {
                var officer = await _context.RCDOfficers
                    .FirstOrDefaultAsync(o => o.UserId == userId);

                if (officer != null && caseEntity.AssignedOfficerId == officer.Id)
                    return true;
            }

            // Mediator has access to assigned cases
            if (userRole == "Mediator")
            {
                var mediator = await _context.Mediators
                    .FirstOrDefaultAsync(m => m.UserId == userId);

                if (mediator != null && caseEntity.AssignedMediatorId == mediator.Id)
                    return true;
            }

            // Complainant and Respondent have access to their cases
            return caseEntity.ComplainantId == userId || caseEntity.RespondentId == userId;
        }

        private bool IsOfficer(string userId)
        {
            var userRole = GetUserRole();
            return userRole == "RCD_Officer" || userRole == "Admin";
        }

        private async Task<string> GetUserNameAsync(string userId)
        {
            // This would typically come from your User service
            // For now, return a placeholder
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            return user?.FirstName ?? "Unknown User";
        }

        private string GetUserRole()
        {
            return _httpContextAccessor.HttpContext?.User
                ?.FindFirst(ClaimTypes.Role)?.Value ?? "Tenant";
        }

        private async Task ClearCaseCache(string userId, string complainantId, string respondentId)
        {
            await _cache.RemoveAsync($"case_stats_{userId}");
            await _cache.RemoveAsync($"case_stats_{complainantId}");
            await _cache.RemoveAsync($"case_stats_{respondentId}");
            await _cache.RemoveAsync($"dashboard_cases_{userId}");
            await _cache.RemoveAsync($"dashboard_cases_{complainantId}");
            await _cache.RemoveAsync($"dashboard_cases_{respondentId}");
        }

        private async Task SendCaseStatusNotificationAsync(
            Guid caseId, CaseStatus oldStatus, CaseStatus newStatus)
        {
            // Implementation would send notifications to involved parties
            await Task.CompletedTask;
        }

        private async Task SendAssignmentNotificationAsync(
            Guid caseId, string officerId, string mediatorId)
        {
            // Implementation would send notifications to assigned personnel
            await Task.CompletedTask;
        }

        private async Task SendHearingScheduledNotificationAsync(Guid hearingId)
        {
            // Implementation would send notifications to hearing participants
            await Task.CompletedTask;
        }

        private async Task SendHearingCancelledNotificationAsync(Guid hearingId, string reason)
        {
            // Implementation would send notifications about cancelled hearings
            await Task.CompletedTask;
        }

        private async Task SendActualCommunicationAsync(CaseCommunication communication)
        {
            // Implementation would send actual email/SMS
            await Task.CompletedTask;
        }

        private async Task<double> CalculateAverageResolutionTimeAsync(IQueryable<Case> query)
        {
            var resolvedCases = await query
                .Where(c => c.Status == CaseStatus.Resolved &&
                           c.ResolutionDate.HasValue &&
                           c.SubmittedAt.HasValue)
                .ToListAsync();

            if (!resolvedCases.Any())
                return 0;

            var totalDays = resolvedCases
                .Sum(c => (c.ResolutionDate.Value - c.SubmittedAt.Value).TotalDays);

            return totalDays / resolvedCases.Count;
        }

        private async Task<decimal> CalculateResolutionRateAsync(IQueryable<Case> query)
        {
            var totalCases = await query
                .Where(c => c.Status != CaseStatus.Draft)
                .CountAsync();

            var resolvedCases = await query
                .CountAsync(c => c.Status == CaseStatus.Resolved);

            if (totalCases == 0)
                return 0;

            return (decimal)resolvedCases / totalCases * 100;
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double len = bytes;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        #endregion
    }
}