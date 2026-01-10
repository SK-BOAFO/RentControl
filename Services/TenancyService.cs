using System.Text.Json;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using RentControlSystem.Auth.API.Helpers;
using RentControlSystem.Tenancy.API.DTOs;
using RentControlSystem.Tenancy.API.Models;
using System.Security.Claims;
using RentControlSystem.API.Data;

namespace RentControlSystem.Tenancy.API.Services
{
    public interface ITenancyService
    {
        Task<ServiceResponse<TenancyAgreementResponseDto>> CreateTenancyAsync(CreateTenancyDto dto, string userId);
        Task<ServiceResponse<TenancyAgreementResponseDto>> GetTenancyByIdAsync(Guid id, string userId);
        Task<PaginatedServiceResponse<List<TenancyAgreementResponseDto>>> SearchTenanciesAsync(
            TenancySearchDto searchDto, int page, int pageSize, string userId);
        Task<ServiceResponse<TenancyAgreementResponseDto>> UpdateTenancyAsync(
            Guid id, UpdateTenancyDto dto, string userId);
        Task<ServiceResponse<bool>> TerminateTenancyAsync(Guid id, string reason, string userId);
        Task<ServiceResponse<TenancyAgreementResponseDto>> RenewTenancyAsync(
            Guid id, RenewTenancyDto dto, string userId);
        Task<ServiceResponse<TenancyStatisticsDto>> GetTenancyStatisticsAsync(string userId);
        Task<ServiceResponse<List<TenancyAgreementResponseDto>>> GetTenantAgreementsAsync(
            string tenantId, string userId);
        Task<ServiceResponse<List<TenancyAgreementResponseDto>>> GetLandlordAgreementsAsync(
            string landlordId, string userId);
        Task<ServiceResponse<bool>> IssueNoticeAsync(Guid tenancyId, IssueNoticeDto dto, string userId);
    }

    // Add the missing INotificationService interface
    public interface INotificationService
    {
        Task SendTenancyCreatedNotificationAsync(Guid tenancyId, string tenantId, string landlordId);
        Task SendRentChangeNotificationAsync(Guid tenancyId, decimal oldRent, decimal newRent);
        Task SendTenancyTerminatedNotificationAsync(Guid tenancyId, string reason);
        Task SendTenancyRenewedNotificationAsync(Guid oldTenancyId, Guid newTenancyId, decimal newRent);
        Task SendNoticeIssuedNotificationAsync(Guid tenancyId, string noticeType, string reason, DateTime effectiveDate);
        Task SendNoticeIssuedNotificationAsync(Guid tenancyId, NoticeType noticeType, string reason, DateTime effectiveDate);
    }

    public class TenancyService : ITenancyService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IDistributedCache _cache;
        private readonly ILogger<TenancyService> _logger;
        private readonly INotificationService _notificationService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TenancyService(
            ApplicationDbContext context,
            IMapper mapper,
            IDistributedCache cache,
            ILogger<TenancyService> logger,
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

        public async Task<ServiceResponse<TenancyAgreementResponseDto>> CreateTenancyAsync(
            CreateTenancyDto dto, string userId)
        {
            try
            {
                // Check if property exists and is available
                var property = await _context.Properties
                    .FirstOrDefaultAsync(p => p.Id == dto.PropertyId && p.IsActive);

                if (property == null)
                    return ServiceResponse<TenancyAgreementResponseDto>.CreateError("Property not found or inactive");

                if (property.PropertyStatus != PropertyStatus.Available)
                    return ServiceResponse<TenancyAgreementResponseDto>.CreateError("Property is not available for tenancy");

                // Check if property already has active tenancy
                var existingActiveTenancy = await _context.TenancyAgreements
                    .AnyAsync(ta => ta.PropertyId == dto.PropertyId &&
                                   ta.Status == TenancyStatus.Active &&
                                   ta.IsActive);

                if (existingActiveTenancy)
                    return ServiceResponse<TenancyAgreementResponseDto>.CreateError("Property already has an active tenancy");

                // Check dates validity
                if (dto.StartDate >= dto.EndDate)
                    return ServiceResponse<TenancyAgreementResponseDto>.CreateError("Start date must be before end date");

                if (dto.StartDate < DateTime.UtcNow.Date)
                    return ServiceResponse<TenancyAgreementResponseDto>.CreateError("Start date cannot be in the past");

                // Validate rent amount
                if (dto.MonthlyRent <= 0)
                    return ServiceResponse<TenancyAgreementResponseDto>.CreateError("Monthly rent must be greater than zero");

                // Create tenancy agreement
                var tenancyAgreement = new TenancyAgreement
                {
                    PropertyId = dto.PropertyId,
                    LandlordId = dto.LandlordId,
                    TenantId = dto.TenantId,
                    MonthlyRent = dto.MonthlyRent,
                    SecurityDeposit = dto.SecurityDeposit ?? dto.MonthlyRent, // Default to one month's rent
                    StartDate = dto.StartDate.Date,
                    EndDate = dto.EndDate.Date,
                    Status = TenancyStatus.Draft,
                    PaymentFrequency = dto.PaymentFrequency,
                    LeaseTermId = dto.LeaseTermId,
                    NoticePeriodId = dto.NoticePeriodId,
                    CreatedBy = userId,
                    UpdatedBy = userId
                };

                // Set next payment date based on start date
                tenancyAgreement.NextPaymentDate = CalculateNextPaymentDate(
                    tenancyAgreement.StartDate,
                    tenancyAgreement.PaymentFrequency);

                // Add to database
                _context.TenancyAgreements.Add(tenancyAgreement);

                // Update property status
                property.PropertyStatus = PropertyStatus.Occupied;
                property.UpdatedAt = DateTime.UtcNow;

                // Create occupancy record
                var occupancy = new Occupancy
                {
                    PropertyId = dto.PropertyId,
                    TenantId = dto.TenantId,
                    OccupancyStartDate = dto.StartDate.Date,
                    IsCurrent = true
                };
                _context.Occupancies.Add(occupancy);

                // Create history record
                var history = new TenancyHistory
                {
                    TenancyAgreementId = tenancyAgreement.Id,
                    Action = "CREATED",
                    Description = "Tenancy agreement created",
                    ChangedBy = userId,
                    IpAddress = GetIpAddress()
                };
                _context.TenancyHistories.Add(history);

                await _context.SaveChangesAsync();

                // Get created agreement with details
                var createdAgreement = await _context.TenancyAgreements
                    .Include(ta => ta.Property)
                    .Include(ta => ta.LeaseTerm)
                    .Include(ta => ta.NoticePeriod)
                    .FirstOrDefaultAsync(ta => ta.Id == tenancyAgreement.Id);

                var responseDto = _mapper.Map<TenancyAgreementResponseDto>(createdAgreement);

                // Send notifications
                await _notificationService.SendTenancyCreatedNotificationAsync(
                    tenancyAgreement.Id, dto.TenantId, dto.LandlordId);

                // Clear cache
                await ClearTenancyCache(dto.LandlordId, dto.TenantId);

                _logger.LogInformation("Tenancy agreement created: {AgreementNumber} by user {UserId}",
                    responseDto.AgreementNumber, userId);

                return ServiceResponse<TenancyAgreementResponseDto>.CreateSuccess(
                    "Tenancy agreement created successfully", responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating tenancy agreement");
                return ServiceResponse<TenancyAgreementResponseDto>.CreateError("An error occurred while creating tenancy agreement");
            }
        }

        public async Task<ServiceResponse<TenancyAgreementResponseDto>> GetTenancyByIdAsync(
            Guid id, string userId)
        {
            try
            {
                var cacheKey = $"tenancy_{id}";
                var cachedData = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    var cachedResponse = JsonSerializer.Deserialize<TenancyAgreementResponseDto>(cachedData);
                    if (cachedResponse != null)
                        return ServiceResponse<TenancyAgreementResponseDto>.CreateSuccess("Tenancy retrieved from cache", cachedResponse);
                }

                var tenancyAgreement = await _context.TenancyAgreements
                    .Include(ta => ta.Property)
                        .ThenInclude(p => p.Amenities)
                    .Include(ta => ta.Property)
                        .ThenInclude(p => p.Images)
                    .Include(ta => ta.LeaseTerm)
                    .Include(ta => ta.NoticePeriod)
                    .Include(ta => ta.Documents.Where(d => d.IsVerified))
                    .Include(ta => ta.RentPayments
                        .Where(rp => rp.PaymentStatus == PaymentStatus.Completed)
                        .OrderByDescending(rp => rp.PaymentDate)
                        .Take(6))
                    .FirstOrDefaultAsync(ta => ta.Id == id && ta.IsActive);

                if (tenancyAgreement == null)
                    return ServiceResponse<TenancyAgreementResponseDto>.CreateError("Tenancy agreement not found");

                // Check authorization - user must be tenant, landlord, or admin/RCD officer
                var userRole = GetUserRole();
                if (tenancyAgreement.TenantId != userId &&
                    tenancyAgreement.LandlordId != userId &&
                    !(userRole == "Admin" || userRole == "RCD_Officer"))
                    return ServiceResponse<TenancyAgreementResponseDto>.CreateError("Unauthorized access");

                var responseDto = _mapper.Map<TenancyAgreementResponseDto>(tenancyAgreement);

                // Calculate total paid and balance due
                responseDto.TotalPaid = await CalculateTotalPaidAsync(id);
                responseDto.BalanceDue = await CalculateBalanceDueAsync(tenancyAgreement);

                // Cache the response for 5 minutes
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                };
                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(responseDto), cacheOptions);

                return ServiceResponse<TenancyAgreementResponseDto>.CreateSuccess("Tenancy retrieved", responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tenancy agreement {TenancyId}", id);
                return ServiceResponse<TenancyAgreementResponseDto>.CreateError("An error occurred");
            }
        }

        public async Task<PaginatedServiceResponse<List<TenancyAgreementResponseDto>>> SearchTenanciesAsync(
            TenancySearchDto searchDto, int page, int pageSize, string userId)
        {
            try
            {
                var query = _context.TenancyAgreements
                    .Include(ta => ta.Property)
                    .Include(ta => ta.LeaseTerm)
                    .Include(ta => ta.NoticePeriod)
                    .Where(ta => ta.IsActive)
                    .AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(searchDto.LandlordId))
                    query = query.Where(ta => ta.LandlordId == searchDto.LandlordId);

                if (!string.IsNullOrEmpty(searchDto.TenantId))
                    query = query.Where(ta => ta.TenantId == searchDto.TenantId);

                if (searchDto.PropertyId.HasValue)
                    query = query.Where(ta => ta.PropertyId == searchDto.PropertyId.Value);

                if (searchDto.Status.HasValue)
                    query = query.Where(ta => ta.Status == searchDto.Status.Value);

                if (searchDto.StartDateFrom.HasValue)
                    query = query.Where(ta => ta.StartDate >= searchDto.StartDateFrom.Value);

                if (searchDto.StartDateTo.HasValue)
                    query = query.Where(ta => ta.StartDate <= searchDto.StartDateTo.Value);

                if (searchDto.EndDateFrom.HasValue)
                    query = query.Where(ta => ta.EndDate >= searchDto.EndDateFrom.Value);

                if (searchDto.EndDateTo.HasValue)
                    query = query.Where(ta => ta.EndDate <= searchDto.EndDateTo.Value);

                if (!string.IsNullOrEmpty(searchDto.AgreementNumber))
                    query = query.Where(ta => ta.AgreementNumber.Contains(searchDto.AgreementNumber));

                if (searchDto.IsActive.HasValue)
                {
                    if (searchDto.IsActive.Value)
                        query = query.Where(ta => ta.Status == TenancyStatus.Active);
                    else
                        query = query.Where(ta => ta.Status != TenancyStatus.Active);
                }

                // Authorization - users can only see their own tenancies unless admin/RCD officer
                var userRole = GetUserRole();
                if (userRole != "Admin" && userRole != "RCD_Officer")
                {
                    query = query.Where(ta => ta.TenantId == userId || ta.LandlordId == userId);
                }

                // Get total count
                var totalCount = await query.CountAsync();

                // Apply ordering and pagination
                var tenancies = await query
                    .OrderByDescending(ta => ta.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var responseDtos = _mapper.Map<List<TenancyAgreementResponseDto>>(tenancies);

                return PaginatedServiceResponse<List<TenancyAgreementResponseDto>>.CreateSuccess(
                    "Tenancies retrieved successfully",
                    responseDtos,
                    totalCount,
                    page,
                    pageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching tenancies");
                return PaginatedServiceResponse<List<TenancyAgreementResponseDto>>.CreateError("An error occurred");
            }
        }

        public async Task<ServiceResponse<TenancyAgreementResponseDto>> UpdateTenancyAsync(
            Guid id, UpdateTenancyDto dto, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var tenancyAgreement = await _context.TenancyAgreements
                    .Include(ta => ta.Property)
                    .FirstOrDefaultAsync(ta => ta.Id == id && ta.IsActive);

                if (tenancyAgreement == null)
                    return ServiceResponse<TenancyAgreementResponseDto>.CreateError("Tenancy agreement not found");

                // Check authorization
                if (tenancyAgreement.LandlordId != userId && GetUserRole() != "Admin" && GetUserRole() != "RCD_Officer")
                    return ServiceResponse<TenancyAgreementResponseDto>.CreateError("Unauthorized to update this tenancy");

                // Store old values for history
                var oldValues = new
                {
                    MonthlyRent = tenancyAgreement.MonthlyRent,
                    SecurityDeposit = tenancyAgreement.SecurityDeposit,
                    StartDate = tenancyAgreement.StartDate,
                    EndDate = tenancyAgreement.EndDate,
                    Status = tenancyAgreement.Status,
                    PaymentFrequency = tenancyAgreement.PaymentFrequency
                };

                // Update fields if provided
                if (dto.MonthlyRent.HasValue && dto.MonthlyRent.Value > 0)
                {
                    if (dto.MonthlyRent.Value != tenancyAgreement.MonthlyRent)
                    {
                        // Create rent adjustment if rent is changed
                        var adjustment = new RentAdjustment
                        {
                            PropertyId = tenancyAgreement.PropertyId,
                            PreviousRent = tenancyAgreement.MonthlyRent,
                            NewRent = dto.MonthlyRent.Value,
                            PercentageChange = ((dto.MonthlyRent.Value - tenancyAgreement.MonthlyRent) / tenancyAgreement.MonthlyRent) * 100,
                            EffectiveDate = DateTime.UtcNow,
                            Reason = "Tenancy agreement update",
                            ApprovedAt = DateTime.UtcNow,
                            ApprovedBy = userId
                        };
                        _context.RentAdjustments.Add(adjustment);
                    }
                    tenancyAgreement.MonthlyRent = dto.MonthlyRent.Value;
                }

                if (dto.SecurityDeposit.HasValue)
                    tenancyAgreement.SecurityDeposit = dto.SecurityDeposit.Value;

                if (dto.StartDate.HasValue)
                    tenancyAgreement.StartDate = dto.StartDate.Value.Date;

                if (dto.EndDate.HasValue)
                    tenancyAgreement.EndDate = dto.EndDate.Value.Date;

                if (dto.PaymentFrequency.HasValue)
                {
                    tenancyAgreement.PaymentFrequency = dto.PaymentFrequency.Value;
                    tenancyAgreement.NextPaymentDate = CalculateNextPaymentDate(
                        tenancyAgreement.StartDate, tenancyAgreement.PaymentFrequency);
                }

                if (dto.Status.HasValue)
                    tenancyAgreement.Status = dto.Status.Value;

                if (dto.LeaseTermId.HasValue)
                    tenancyAgreement.LeaseTermId = dto.LeaseTermId.Value;

                if (dto.NoticePeriodId.HasValue)
                    tenancyAgreement.NoticePeriodId = dto.NoticePeriodId.Value;

                if (!string.IsNullOrEmpty(dto.TerminationReason))
                    tenancyAgreement.TerminationReason = dto.TerminationReason;

                if (dto.ActualVacateDate.HasValue)
                    tenancyAgreement.ActualVacateDate = dto.ActualVacateDate.Value.Date;

                tenancyAgreement.UpdatedAt = DateTime.UtcNow;
                tenancyAgreement.UpdatedBy = userId;

                // If status changed to terminated or expired, update property status
                if (dto.Status.HasValue &&
                    (dto.Status.Value == TenancyStatus.Terminated || dto.Status.Value == TenancyStatus.Expired))
                {
                    if (tenancyAgreement.Property != null)
                    {
                        tenancyAgreement.Property.PropertyStatus = PropertyStatus.Available;
                        tenancyAgreement.Property.UpdatedAt = DateTime.UtcNow;
                    }

                    // Update occupancy record
                    var occupancy = await _context.Occupancies
                        .FirstOrDefaultAsync(o => o.PropertyId == tenancyAgreement.PropertyId &&
                                                o.TenantId == tenancyAgreement.TenantId &&
                                                o.IsCurrent);

                    if (occupancy != null)
                    {
                        occupancy.OccupancyEndDate = dto.ActualVacateDate ?? DateTime.UtcNow;
                        occupancy.IsCurrent = false;
                    }
                }

                // Create history record
                var history = new TenancyHistory
                {
                    TenancyAgreementId = tenancyAgreement.Id,
                    Action = "UPDATED",
                    Description = "Tenancy agreement updated",
                    ChangedBy = userId,
                    OldValues = JsonSerializer.Serialize(oldValues),
                    NewValues = JsonSerializer.Serialize(new
                    {
                        tenancyAgreement.MonthlyRent,
                        tenancyAgreement.SecurityDeposit,
                        tenancyAgreement.StartDate,
                        tenancyAgreement.EndDate,
                        tenancyAgreement.Status,
                        tenancyAgreement.PaymentFrequency
                    }),
                    IpAddress = GetIpAddress()
                };
                _context.TenancyHistories.Add(history);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Clear cache
                await ClearTenancyCache(tenancyAgreement.LandlordId, tenancyAgreement.TenantId);
                await _cache.RemoveAsync($"tenancy_{id}");

                // Get updated agreement
                var updatedAgreement = await GetTenancyByIdAsync(id, userId);

                // Send notification if rent changed
                if (dto.MonthlyRent.HasValue && dto.MonthlyRent.Value != oldValues.MonthlyRent)
                {
                    await _notificationService.SendRentChangeNotificationAsync(
                        tenancyAgreement.Id, oldValues.MonthlyRent, dto.MonthlyRent.Value);
                }

                _logger.LogInformation("Tenancy agreement updated: {AgreementNumber} by user {UserId}",
                    tenancyAgreement.AgreementNumber, userId);

                return updatedAgreement;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating tenancy agreement {TenancyId}", id);
                return ServiceResponse<TenancyAgreementResponseDto>.CreateError("An error occurred while updating tenancy");
            }
        }

        public async Task<ServiceResponse<bool>> TerminateTenancyAsync(
            Guid id, string reason, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var tenancyAgreement = await _context.TenancyAgreements
                    .Include(ta => ta.Property)
                    .FirstOrDefaultAsync(ta => ta.Id == id && ta.IsActive);

                if (tenancyAgreement == null)
                    return ServiceResponse<bool>.CreateError("Tenancy agreement not found");

                if (tenancyAgreement.Status != TenancyStatus.Active)
                    return ServiceResponse<bool>.CreateError("Only active tenancies can be terminated");

                // Check authorization
                var userRole = GetUserRole();
                if (tenancyAgreement.LandlordId != userId &&
                    tenancyAgreement.TenantId != userId &&
                    userRole != "Admin" && userRole != "RCD_Officer")
                    return ServiceResponse<bool>.CreateError("Unauthorized to terminate this tenancy");

                // Update tenancy
                tenancyAgreement.Status = TenancyStatus.Terminated;
                tenancyAgreement.TerminationReason = reason;
                tenancyAgreement.ActualVacateDate = DateTime.UtcNow.Date;
                tenancyAgreement.UpdatedAt = DateTime.UtcNow;
                tenancyAgreement.UpdatedBy = userId;

                // Update property status
                if (tenancyAgreement.Property != null)
                {
                    tenancyAgreement.Property.PropertyStatus = PropertyStatus.Available;
                    tenancyAgreement.Property.UpdatedAt = DateTime.UtcNow;
                }

                // Update occupancy record
                var occupancy = await _context.Occupancies
                    .FirstOrDefaultAsync(o => o.PropertyId == tenancyAgreement.PropertyId &&
                                            o.TenantId == tenancyAgreement.TenantId &&
                                            o.IsCurrent);

                if (occupancy != null)
                {
                    occupancy.OccupancyEndDate = DateTime.UtcNow;
                    occupancy.IsCurrent = false;
                }

                // Create history record
                var history = new TenancyHistory
                {
                    TenancyAgreementId = tenancyAgreement.Id,
                    Action = "TERMINATED",
                    Description = $"Tenancy terminated: {reason}",
                    ChangedBy = userId,
                    IpAddress = GetIpAddress()
                };
                _context.TenancyHistories.Add(history);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Clear cache
                await ClearTenancyCache(tenancyAgreement.LandlordId, tenancyAgreement.TenantId);

                // Send notification
                await _notificationService.SendTenancyTerminatedNotificationAsync(
                    tenancyAgreement.Id, reason);

                _logger.LogInformation("Tenancy terminated: {AgreementNumber} by user {UserId}",
                    tenancyAgreement.AgreementNumber, userId);

                return ServiceResponse<bool>.CreateSuccess("Tenancy terminated successfully", true);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error terminating tenancy {TenancyId}", id);
                return ServiceResponse<bool>.CreateError("An error occurred while terminating tenancy");
            }
        }

        public async Task<ServiceResponse<TenancyAgreementResponseDto>> RenewTenancyAsync(
            Guid id, RenewTenancyDto dto, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var existingTenancy = await _context.TenancyAgreements
                    .Include(ta => ta.Property)
                    .FirstOrDefaultAsync(ta => ta.Id == id && ta.IsActive);

                if (existingTenancy == null)
                    return ServiceResponse<TenancyAgreementResponseDto>.CreateError("Tenancy agreement not found");

                if (existingTenancy.Status != TenancyStatus.Active && existingTenancy.Status != TenancyStatus.Expired)
                    return ServiceResponse<TenancyAgreementResponseDto>.CreateError("Only active or expired tenancies can be renewed");

                // Check authorization
                if (existingTenancy.LandlordId != userId && GetUserRole() != "Admin" && GetUserRole() != "RCD_Officer")
                    return ServiceResponse<TenancyAgreementResponseDto>.CreateError("Unauthorized to renew this tenancy");

                if (dto.NewEndDate <= existingTenancy.EndDate)
                    return ServiceResponse<TenancyAgreementResponseDto>.CreateError("New end date must be after current end date");

                // Close existing tenancy
                existingTenancy.Status = TenancyStatus.Renewed;
                existingTenancy.UpdatedAt = DateTime.UtcNow;
                existingTenancy.UpdatedBy = userId;

                // Create new tenancy agreement
                var newTenancy = new TenancyAgreement
                {
                    PropertyId = existingTenancy.PropertyId,
                    LandlordId = existingTenancy.LandlordId,
                    TenantId = existingTenancy.TenantId,
                    MonthlyRent = dto.NewMonthlyRent ?? existingTenancy.MonthlyRent,
                    SecurityDeposit = existingTenancy.SecurityDeposit,
                    StartDate = existingTenancy.EndDate.AddDays(1).Date,
                    EndDate = dto.NewEndDate.Date,
                    Status = TenancyStatus.Active,
                    PaymentFrequency = existingTenancy.PaymentFrequency,
                    LeaseTermId = existingTenancy.LeaseTermId,
                    NoticePeriodId = existingTenancy.NoticePeriodId,
                    NextPaymentDate = CalculateNextPaymentDate(
                        existingTenancy.EndDate.AddDays(1).Date, existingTenancy.PaymentFrequency),
                    CreatedBy = userId,
                    UpdatedBy = userId
                };

                _context.TenancyAgreements.Add(newTenancy);

                // Create history records
                var history1 = new TenancyHistory
                {
                    TenancyAgreementId = existingTenancy.Id,
                    Action = "RENEWED",
                    Description = "Tenancy renewed and closed",
                    ChangedBy = userId,
                    IpAddress = GetIpAddress()
                };

                var history2 = new TenancyHistory
                {
                    TenancyAgreementId = newTenancy.Id,
                    Action = "CREATED",
                    Description = "New tenancy created from renewal",
                    ChangedBy = userId,
                    IpAddress = GetIpAddress()
                };

                _context.TenancyHistories.Add(history1);
                _context.TenancyHistories.Add(history2);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Clear cache
                await ClearTenancyCache(existingTenancy.LandlordId, existingTenancy.TenantId);

                // Get new tenancy
                var response = await GetTenancyByIdAsync(newTenancy.Id, userId);

                // Send notification
                await _notificationService.SendTenancyRenewedNotificationAsync(
                    existingTenancy.Id, newTenancy.Id, dto.NewMonthlyRent ?? existingTenancy.MonthlyRent);

                _logger.LogInformation("Tenancy renewed: {OldAgreementNumber} -> {NewAgreementNumber} by user {UserId}",
                    existingTenancy.AgreementNumber, newTenancy.AgreementNumber, userId);

                return response;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error renewing tenancy {TenancyId}", id);
                return ServiceResponse<TenancyAgreementResponseDto>.CreateError("An error occurred while renewing tenancy");
            }
        }

        public async Task<ServiceResponse<TenancyStatisticsDto>> GetTenancyStatisticsAsync(string userId)
        {
            try
            {
                var cacheKey = $"tenancy_stats_{userId}";
                var cachedData = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    var cachedStats = JsonSerializer.Deserialize<TenancyStatisticsDto>(cachedData);
                    if (cachedStats != null)
                        return ServiceResponse<TenancyStatisticsDto>.CreateSuccess("Statistics retrieved from cache", cachedStats);
                }

                var userRole = GetUserRole();
                IQueryable<TenancyAgreement> query = _context.TenancyAgreements
                    .Include(ta => ta.Property)
                    .Where(ta => ta.IsActive);

                // Filter by user role
                if (userRole == "Landlord")
                    query = query.Where(ta => ta.LandlordId == userId);
                else if (userRole == "Tenant")
                    query = query.Where(ta => ta.TenantId == userId);

                var totalAgreements = await query.CountAsync();
                var activeAgreements = await query.CountAsync(ta => ta.Status == TenancyStatus.Active);
                var expiredAgreements = await query.CountAsync(ta => ta.Status == TenancyStatus.Expired);
                var draftAgreements = await query.CountAsync(ta => ta.Status == TenancyStatus.Draft);

                var totalMonthlyRent = await query
                    .Where(ta => ta.Status == TenancyStatus.Active)
                    .SumAsync(ta => ta.MonthlyRent);

                var totalSecurityDeposits = await query
                    .Where(ta => ta.Status == TenancyStatus.Active)
                    .SumAsync(ta => ta.SecurityDeposit);

                // Agreements by status
                var agreementsByStatus = await query
                    .GroupBy(ta => ta.Status)
                    .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
                    .ToDictionaryAsync(x => x.Status, x => x.Count);

                // Agreements by region
                var agreementsByRegion = await query
                    .Include(ta => ta.Property)
                    .Where(ta => ta.Property != null)
                    .GroupBy(ta => ta.Property.Region)
                    .Select(g => new { Region = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Region, x => x.Count);

                // Rent collection by month (last 6 months)
                var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
                var rentCollectionByMonth = await _context.RentPayments
                    .Where(rp => rp.PaymentStatus == PaymentStatus.Completed &&
                                rp.PaymentDate >= sixMonthsAgo)
                    .GroupBy(rp => new { Year = rp.PaymentDate.Year, Month = rp.PaymentDate.Month })
                    .Select(g => new
                    {
                        Key = $"{g.Key.Year}-{g.Key.Month:D2}",
                        Total = g.Sum(rp => rp.Amount)
                    })
                    .ToDictionaryAsync(x => x.Key, x => x.Total);

                var statistics = new TenancyStatisticsDto
                {
                    TotalAgreements = totalAgreements,
                    ActiveAgreements = activeAgreements,
                    ExpiredAgreements = expiredAgreements,
                    DraftAgreements = draftAgreements,
                    TotalMonthlyRent = totalMonthlyRent,
                    TotalSecurityDeposits = totalSecurityDeposits,
                    AgreementsByStatus = agreementsByStatus,
                    AgreementsByRegion = agreementsByRegion,
                    RentCollectionByMonth = rentCollectionByMonth
                };

                // Cache for 10 minutes
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                };
                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(statistics), cacheOptions);

                return ServiceResponse<TenancyStatisticsDto>.CreateSuccess("Statistics retrieved", statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tenancy statistics for user {UserId}", userId);
                return ServiceResponse<TenancyStatisticsDto>.CreateError("An error occurred while retrieving statistics");
            }
        }

        public async Task<ServiceResponse<List<TenancyAgreementResponseDto>>> GetTenantAgreementsAsync(
            string tenantId, string userId)
        {
            try
            {
                // Check authorization
                if (tenantId != userId && GetUserRole() != "Admin" && GetUserRole() != "RCD_Officer")
                    return ServiceResponse<List<TenancyAgreementResponseDto>>.CreateError("Unauthorized");

                var cacheKey = $"tenant_agreements_{tenantId}";
                var cachedData = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    var cachedAgreements = JsonSerializer.Deserialize<List<TenancyAgreementResponseDto>>(cachedData);
                    if (cachedAgreements != null)
                        return ServiceResponse<List<TenancyAgreementResponseDto>>.CreateSuccess("Agreements retrieved from cache", cachedAgreements);
                }

                var agreements = await _context.TenancyAgreements
                    .Include(ta => ta.Property)
                    .Include(ta => ta.LeaseTerm)
                    .Include(ta => ta.NoticePeriod)
                    .Where(ta => ta.TenantId == tenantId && ta.IsActive)
                    .OrderByDescending(ta => ta.CreatedAt)
                    .Take(20)
                    .ToListAsync();

                var responseDtos = _mapper.Map<List<TenancyAgreementResponseDto>>(agreements);

                // Calculate total paid and balances
                foreach (var dto in responseDtos)
                {
                    dto.TotalPaid = await CalculateTotalPaidAsync(dto.Id);
                    dto.BalanceDue = await CalculateBalanceDueAsync(
                        agreements.First(a => a.Id == dto.Id));
                }

                // Cache for 5 minutes
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                };
                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(responseDtos), cacheOptions);

                return ServiceResponse<List<TenancyAgreementResponseDto>>.CreateSuccess("Tenant agreements retrieved", responseDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tenant agreements for tenant {TenantId}", tenantId);
                return ServiceResponse<List<TenancyAgreementResponseDto>>.CreateError("An error occurred");
            }
        }

        public async Task<ServiceResponse<List<TenancyAgreementResponseDto>>> GetLandlordAgreementsAsync(
            string landlordId, string userId)
        {
            try
            {
                // Check authorization
                if (landlordId != userId && GetUserRole() != "Admin" && GetUserRole() != "RCD_Officer")
                    return ServiceResponse<List<TenancyAgreementResponseDto>>.CreateError("Unauthorized");

                var cacheKey = $"landlord_agreements_{landlordId}";
                var cachedData = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    var cachedAgreements = JsonSerializer.Deserialize<List<TenancyAgreementResponseDto>>(cachedData);
                    if (cachedAgreements != null)
                        return ServiceResponse<List<TenancyAgreementResponseDto>>.CreateSuccess("Agreements retrieved from cache", cachedAgreements);
                }

                var agreements = await _context.TenancyAgreements
                    .Include(ta => ta.Property)
                    .Include(ta => ta.LeaseTerm)
                    .Include(ta => ta.NoticePeriod)
                    .Include(ta => ta.RentPayments
                        .Where(rp => rp.PaymentStatus == PaymentStatus.Completed)
                        .OrderByDescending(rp => rp.PaymentDate)
                        .Take(3))
                    .Where(ta => ta.LandlordId == landlordId && ta.IsActive)
                    .OrderByDescending(ta => ta.CreatedAt)
                    .Take(20)
                    .ToListAsync();

                var responseDtos = _mapper.Map<List<TenancyAgreementResponseDto>>(agreements);

                // Cache for 5 minutes
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                };
                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(responseDtos), cacheOptions);

                return ServiceResponse<List<TenancyAgreementResponseDto>>.CreateSuccess("Landlord agreements retrieved", responseDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting landlord agreements for landlord {LandlordId}", landlordId);
                return ServiceResponse<List<TenancyAgreementResponseDto>>.CreateError("An error occurred");
            }
        }

        public async Task<ServiceResponse<bool>> IssueNoticeAsync(
            Guid tenancyId, IssueNoticeDto dto, string userId)
        {
            try
            {
                var tenancy = await _context.TenancyAgreements
                    .FirstOrDefaultAsync(ta => ta.Id == tenancyId && ta.IsActive);

                if (tenancy == null)
                    return ServiceResponse<bool>.CreateError("Tenancy agreement not found");

                // Check authorization
                if (tenancy.LandlordId != userId && GetUserRole() != "Admin" && GetUserRole() != "RCD_Officer")
                    return ServiceResponse<bool>.CreateError("Unauthorized to issue notice");

                // Create notice document
                var noticeDocument = new AgreementDocument
                {
                    TenancyAgreementId = tenancyId,
                    DocumentType = DocumentType.Notice,
                    FileName = $"{dto.NoticeType}_Notice_{DateTime.UtcNow:yyyyMMdd}.pdf",
                    FilePath = $"/notices/{tenancyId}/{Guid.NewGuid()}.pdf",
                    Description = $"{dto.NoticeType} Notice - Effective {dto.EffectiveDate:yyyy-MM-dd}",
                    UploadedBy = userId,
                    UploadedAt = DateTime.UtcNow,
                    IsVerified = true
                };

                _context.AgreementDocuments.Add(noticeDocument);

                // Create history record
                var history = new TenancyHistory
                {
                    TenancyAgreementId = tenancyId,
                    Action = "NOTICE_ISSUED",
                    Description = $"{dto.NoticeType} notice issued. Reason: {dto.Reason}",
                    ChangedBy = userId,
                    IpAddress = GetIpAddress()
                };
                _context.TenancyHistories.Add(history);

                await _context.SaveChangesAsync();

                // Send notification
                await _notificationService.SendNoticeIssuedNotificationAsync(
                    tenancyId, dto.NoticeType, dto.Reason, dto.EffectiveDate);

                _logger.LogInformation("Notice issued for tenancy {TenancyId} by user {UserId}",
                    tenancyId, userId);

                return ServiceResponse<bool>.CreateSuccess("Notice issued successfully", true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error issuing notice for tenancy {TenancyId}", tenancyId);
                return ServiceResponse<bool>.CreateError("An error occurred while issuing notice");
            }
        }

        // Helper Methods
        private DateTime CalculateNextPaymentDate(DateTime startDate, PaymentFrequency frequency)
        {
            return frequency switch
            {
                PaymentFrequency.Monthly => startDate.AddMonths(1),
                PaymentFrequency.Quarterly => startDate.AddMonths(3),
                PaymentFrequency.SemiAnnually => startDate.AddMonths(6),
                PaymentFrequency.Annually => startDate.AddYears(1),
                PaymentFrequency.Weekly => startDate.AddDays(7),
                _ => startDate.AddMonths(1)
            };
        }

        private async Task<decimal> CalculateTotalPaidAsync(Guid tenancyId)
        {
            return await _context.RentPayments
                .Where(rp => rp.TenancyAgreementId == tenancyId &&
                           rp.PaymentStatus == PaymentStatus.Completed)
                .SumAsync(rp => rp.Amount);
        }

        private async Task<decimal> CalculateBalanceDueAsync(TenancyAgreement tenancy)
        {
            if (tenancy.Status != TenancyStatus.Active)
                return 0;

            var monthsElapsed = ((DateTime.UtcNow.Year - tenancy.StartDate.Year) * 12) +
                               DateTime.UtcNow.Month - tenancy.StartDate.Month;

            if (DateTime.UtcNow.Day < tenancy.StartDate.Day)
                monthsElapsed--;

            var totalRentDue = monthsElapsed * tenancy.MonthlyRent;
            var totalPaid = await CalculateTotalPaidAsync(tenancy.Id);

            return Math.Max(0, totalRentDue - totalPaid);
        }

        private async Task ClearTenancyCache(string landlordId, string tenantId)
        {
            await _cache.RemoveAsync($"tenant_agreements_{tenantId}");
            await _cache.RemoveAsync($"landlord_agreements_{landlordId}");
            await _cache.RemoveAsync($"tenancy_stats_{landlordId}");
            await _cache.RemoveAsync($"tenancy_stats_{tenantId}");
        }

        private string GetUserRole()
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value ?? "Tenant";
        }

        private string GetIpAddress()
        {
            return _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown";
        }
    }
}