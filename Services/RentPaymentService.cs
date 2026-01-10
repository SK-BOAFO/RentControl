using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json; // Added this missing using directive
using RentControlSystem.API.Data;
using RentControlSystem.Auth.API.Helpers;
using RentControlSystem.Tenancy.API.DTOs;
using RentControlSystem.Tenancy.API.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace RentControlSystem.Tenancy.API.Services
{
    public interface IRentPaymentService
    {
        Task<ServiceResponse<RentPaymentResponseDto>> CreateRentPaymentAsync(
            CreateRentPaymentDto dto, string userId);
        Task<ServiceResponse<RentPaymentResponseDto>> GetPaymentByIdAsync(Guid id, string userId);
        Task<PaginatedServiceResponse<List<RentPaymentResponseDto>>> GetPaymentsByTenancyAsync(
            Guid tenancyId, DateTime? fromDate, DateTime? toDate, int page, int pageSize, string userId);
        Task<PaginatedServiceResponse<List<RentPaymentResponseDto>>> GetPaymentsByTenantAsync(
            string tenantId, DateTime? fromDate, DateTime? toDate, int page, int pageSize, string userId);
        Task<PaginatedServiceResponse<List<RentPaymentResponseDto>>> GetPaymentsByLandlordAsync(
            string landlordId, DateTime? fromDate, DateTime? toDate, int page, int pageSize, string userId);
        Task<ServiceResponse<RentPaymentResponseDto>> VerifyPaymentAsync(
            Guid id, VerifyPaymentDto dto, string userId);
        Task<ServiceResponse<PaymentStatisticsDto>> GetPaymentStatisticsAsync(
            DateTime? fromDate, DateTime? toDate, string userId);
        Task<PaginatedServiceResponse<List<RentPaymentResponseDto>>> GetOverduePaymentsAsync(
            int page, int pageSize, string userId);
        Task<ServiceResponse<bool>> ProcessMobileMoneyCallbackAsync(MobileMoneyCallbackDto dto);
        Task<ServiceResponse<List<RentPaymentResponseDto>>> GenerateRentInvoicesAsync(
            DateTime month, string userId);
        Task<ServiceResponse<bool>> SendPaymentRemindersAsync();
    }

    public class RentPaymentService : IRentPaymentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IDistributedCache _cache;
        private readonly ILogger<RentPaymentService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public RentPaymentService(
            ApplicationDbContext context,
            IMapper mapper,
            IDistributedCache cache,
            ILogger<RentPaymentService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _mapper = mapper;
            _cache = cache;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ServiceResponse<RentPaymentResponseDto>> CreateRentPaymentAsync(
            CreateRentPaymentDto dto, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Get tenancy agreement
                var tenancy = await _context.TenancyAgreements
                    .FirstOrDefaultAsync(ta => ta.Id == dto.TenancyAgreementId && ta.IsActive);

                if (tenancy == null)
                    return ServiceResponse<RentPaymentResponseDto>.CreateError("Tenancy agreement not found");

                if (tenancy.Status != TenancyStatus.Active)
                    return ServiceResponse<RentPaymentResponseDto>.CreateError("Tenancy is not active");

                // Check authorization - tenant can only pay their own rent
                if (tenancy.TenantId != userId && GetUserRole() != "Admin" && GetUserRole() != "RCD_Officer")
                    return ServiceResponse<RentPaymentResponseDto>.CreateError("Unauthorized to make payment");

                // Check if payment for this period already exists
                var existingPayment = await _context.RentPayments
                    .FirstOrDefaultAsync(rp =>
                        rp.TenancyAgreementId == dto.TenancyAgreementId &&
                        rp.PeriodStartDate == dto.PeriodStartDate &&
                        rp.PeriodEndDate == dto.PeriodEndDate &&
                        rp.PaymentStatus == PaymentStatus.Completed);

                if (existingPayment != null)
                    return ServiceResponse<RentPaymentResponseDto>.CreateError("Payment for this period already exists");

                // Validate payment amount
                if (dto.Amount <= 0)
                    return ServiceResponse<RentPaymentResponseDto>.CreateError("Payment amount must be greater than zero");

                // For non-advance payments, validate period
                if (!dto.IsAdvancePayment)
                {
                    if (dto.PeriodStartDate < tenancy.StartDate)
                        return ServiceResponse<RentPaymentResponseDto>.CreateError("Payment period cannot be before tenancy start date");

                    if (dto.PeriodEndDate > tenancy.EndDate)
                        return ServiceResponse<RentPaymentResponseDto>.CreateError("Payment period cannot be after tenancy end date");
                }

                // Create payment record
                var payment = new RentPayment
                {
                    TenancyAgreementId = dto.TenancyAgreementId,
                    TenantId = tenancy.TenantId,
                    LandlordId = tenancy.LandlordId,
                    Amount = dto.Amount,
                    PaymentMethod = dto.PaymentMethod,
                    PaymentStatus = dto.PaymentMethod == PaymentMethod.Cash ?
                        PaymentStatus.Completed : PaymentStatus.Pending,
                    ReferenceNumber = dto.ReferenceNumber,
                    PaymentDate = DateTime.UtcNow,
                    PeriodStartDate = dto.PeriodStartDate,
                    PeriodEndDate = dto.PeriodEndDate,
                    IsAdvancePayment = dto.IsAdvancePayment,
                    Notes = dto.Notes
                };

                // Generate transaction ID for electronic payments
                if (dto.PaymentMethod != PaymentMethod.Cash)
                {
                    payment.TransactionId = GenerateTransactionId();
                }

                _context.RentPayments.Add(payment);

                // If cash payment, update next payment date
                if (dto.PaymentMethod == PaymentMethod.Cash && !dto.IsAdvancePayment)
                {
                    tenancy.NextPaymentDate = CalculateNextPaymentDate(
                        dto.PeriodEndDate, tenancy.PaymentFrequency);
                    tenancy.UpdatedAt = DateTime.UtcNow;
                    tenancy.UpdatedBy = userId;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Get created payment with details
                var createdPayment = await _context.RentPayments
                    .Include(rp => rp.TenancyAgreement)
                    .FirstOrDefaultAsync(rp => rp.Id == payment.Id);

                var responseDto = _mapper.Map<RentPaymentResponseDto>(createdPayment);

                // Clear cache
                await ClearPaymentCache(tenancy.TenantId, tenancy.LandlordId, tenancy.Id);

                _logger.LogInformation("Rent payment created: {PaymentId} by user {UserId}",
                    payment.Id, userId);

                return ServiceResponse<RentPaymentResponseDto>.CreateSuccess(
                    "Payment created successfully", responseDto);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating rent payment");
                return ServiceResponse<RentPaymentResponseDto>.CreateError("An error occurred while creating payment");
            }
        }

        public async Task<ServiceResponse<RentPaymentResponseDto>> GetPaymentByIdAsync(
            Guid id, string userId)
        {
            try
            {
                var cacheKey = $"payment_{id}";
                var cachedData = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    var cachedPayment = JsonConvert.DeserializeObject<RentPaymentResponseDto>(cachedData);
                    if (cachedPayment != null)
                        return ServiceResponse<RentPaymentResponseDto>.CreateSuccess("Payment retrieved from cache", cachedPayment);
                }

                var payment = await _context.RentPayments
                    .Include(rp => rp.TenancyAgreement)
                    .FirstOrDefaultAsync(rp => rp.Id == id);

                if (payment == null)
                    return ServiceResponse<RentPaymentResponseDto>.CreateError("Payment not found");

                // Check authorization
                var userRole = GetUserRole();
                if (payment.TenantId != userId &&
                    payment.LandlordId != userId &&
                    userRole != "Admin" && userRole != "RCD_Officer")
                    return ServiceResponse<RentPaymentResponseDto>.CreateError("Unauthorized access");

                var responseDto = _mapper.Map<RentPaymentResponseDto>(payment);

                // Cache for 5 minutes
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                };
                await _cache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(responseDto), cacheOptions);

                return ServiceResponse<RentPaymentResponseDto>.CreateSuccess("Payment retrieved", responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payment {PaymentId}", id);
                return ServiceResponse<RentPaymentResponseDto>.CreateError("An error occurred");
            }
        }

        public async Task<PaginatedServiceResponse<List<RentPaymentResponseDto>>> GetPaymentsByTenancyAsync(
            Guid tenancyId, DateTime? fromDate, DateTime? toDate, int page, int pageSize, string userId)
        {
            try
            {
                // Get tenancy to verify access
                var tenancy = await _context.TenancyAgreements
                    .FirstOrDefaultAsync(ta => ta.Id == tenancyId);

                if (tenancy == null)
                    return PaginatedServiceResponse<List<RentPaymentResponseDto>>.CreateError("Tenancy not found");

                // Check authorization
                var userRole = GetUserRole();
                if (tenancy.TenantId != userId &&
                    tenancy.LandlordId != userId &&
                    userRole != "Admin" && userRole != "RCD_Officer")
                    return PaginatedServiceResponse<List<RentPaymentResponseDto>>.CreateError("Unauthorized access");

                var cacheKey = $"payments_tenancy_{tenancyId}_{fromDate}_{toDate}_page{page}_size{pageSize}";
                var cachedData = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    var cachedResponse = JsonConvert.DeserializeObject<PaginatedServiceResponse<List<RentPaymentResponseDto>>>(cachedData);
                    if (cachedResponse != null)
                        return cachedResponse;
                }

                var query = _context.RentPayments
                    .Include(rp => rp.TenancyAgreement)
                    .Where(rp => rp.TenancyAgreementId == tenancyId)
                    .AsQueryable();

                // Apply date filters
                if (fromDate.HasValue)
                    query = query.Where(rp => rp.PaymentDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(rp => rp.PaymentDate <= toDate.Value);

                // Get total count
                var totalCount = await query.CountAsync();

                // Apply ordering and pagination
                var payments = await query
                    .OrderByDescending(rp => rp.PaymentDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var responseDtos = _mapper.Map<List<RentPaymentResponseDto>>(payments);

                var response = PaginatedServiceResponse<List<RentPaymentResponseDto>>.CreateSuccess(
                    "Payments retrieved successfully",
                    responseDtos,
                    totalCount,
                    page,
                    pageSize);

                // Cache for 2 minutes
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
                };
                await _cache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(response), cacheOptions);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payments for tenancy {TenancyId}", tenancyId);
                return PaginatedServiceResponse<List<RentPaymentResponseDto>>.CreateError("An error occurred");
            }
        }

        public async Task<PaginatedServiceResponse<List<RentPaymentResponseDto>>> GetPaymentsByTenantAsync(
            string tenantId, DateTime? fromDate, DateTime? toDate, int page, int pageSize, string userId)
        {
            try
            {
                // Check authorization
                if (tenantId != userId && GetUserRole() != "Admin" && GetUserRole() != "RCD_Officer")
                    return PaginatedServiceResponse<List<RentPaymentResponseDto>>.CreateError("Unauthorized access");

                var cacheKey = $"payments_tenant_{tenantId}_{fromDate}_{toDate}_page{page}_size{pageSize}";
                var cachedData = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    var cachedResponse = JsonConvert.DeserializeObject<PaginatedServiceResponse<List<RentPaymentResponseDto>>>(cachedData);
                    if (cachedResponse != null)
                        return cachedResponse;
                }

                var query = _context.RentPayments
                    .Include(rp => rp.TenancyAgreement)
                    .Where(rp => rp.TenantId == tenantId)
                    .AsQueryable();

                // Apply date filters
                if (fromDate.HasValue)
                    query = query.Where(rp => rp.PaymentDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(rp => rp.PaymentDate <= toDate.Value);

                // Get total count
                var totalCount = await query.CountAsync();

                // Apply ordering and pagination
                var payments = await query
                    .OrderByDescending(rp => rp.PaymentDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var responseDtos = _mapper.Map<List<RentPaymentResponseDto>>(payments);

                var response = PaginatedServiceResponse<List<RentPaymentResponseDto>>.CreateSuccess(
                    "Payments retrieved successfully",
                    responseDtos,
                    totalCount,
                    page,
                    pageSize);

                // Cache for 2 minutes
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
                };
                await _cache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(response), cacheOptions);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payments for tenant {TenantId}", tenantId);
                return PaginatedServiceResponse<List<RentPaymentResponseDto>>.CreateError("An error occurred");
            }
        }

        public async Task<PaginatedServiceResponse<List<RentPaymentResponseDto>>> GetPaymentsByLandlordAsync(
            string landlordId, DateTime? fromDate, DateTime? toDate, int page, int pageSize, string userId)
        {
            try
            {
                // Check authorization
                if (landlordId != userId && GetUserRole() != "Admin" && GetUserRole() != "RCD_Officer")
                    return PaginatedServiceResponse<List<RentPaymentResponseDto>>.CreateError("Unauthorized access");

                var cacheKey = $"payments_landlord_{landlordId}_{fromDate}_{toDate}_page{page}_size{pageSize}";
                var cachedData = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    var cachedResponse = JsonConvert.DeserializeObject<PaginatedServiceResponse<List<RentPaymentResponseDto>>>(cachedData);
                    if (cachedResponse != null)
                        return cachedResponse;
                }

                var query = _context.RentPayments
                    .Include(rp => rp.TenancyAgreement)
                    .Where(rp => rp.LandlordId == landlordId)
                    .AsQueryable();

                // Apply date filters
                if (fromDate.HasValue)
                    query = query.Where(rp => rp.PaymentDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(rp => rp.PaymentDate <= toDate.Value);

                // Get total count
                var totalCount = await query.CountAsync();

                // Apply ordering and pagination
                var payments = await query
                    .OrderByDescending(rp => rp.PaymentDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var responseDtos = _mapper.Map<List<RentPaymentResponseDto>>(payments);

                var response = PaginatedServiceResponse<List<RentPaymentResponseDto>>.CreateSuccess(
                    "Payments retrieved successfully",
                    responseDtos,
                    totalCount,
                    page,
                    pageSize);

                // Cache for 2 minutes
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
                };
                await _cache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(response), cacheOptions);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payments for landlord {LandlordId}", landlordId);
                return PaginatedServiceResponse<List<RentPaymentResponseDto>>.CreateError("An error occurred");
            }
        }

        public async Task<ServiceResponse<RentPaymentResponseDto>> VerifyPaymentAsync(
            Guid id, VerifyPaymentDto dto, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var payment = await _context.RentPayments
                    .Include(rp => rp.TenancyAgreement)
                    .FirstOrDefaultAsync(rp => rp.Id == id);

                if (payment == null)
                    return ServiceResponse<RentPaymentResponseDto>.CreateError("Payment not found");

                // Only pending payments can be verified
                if (payment.PaymentStatus != PaymentStatus.Pending)
                    return ServiceResponse<RentPaymentResponseDto>.CreateError("Payment is not pending verification");

                // Update payment status
                payment.PaymentStatus = dto.PaymentStatus;
                payment.TransactionId = dto.TransactionId;

                // If payment is completed, update next payment date
                if (dto.PaymentStatus == PaymentStatus.Completed && payment.TenancyAgreement != null)
                {
                    var tenancy = payment.TenancyAgreement;
                    tenancy.NextPaymentDate = CalculateNextPaymentDate(
                        payment.PeriodEndDate, tenancy.PaymentFrequency);
                    tenancy.UpdatedAt = DateTime.UtcNow;
                    tenancy.UpdatedBy = userId;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Get updated payment
                var updatedPayment = await GetPaymentByIdAsync(id, userId);

                // Clear cache
                await ClearPaymentCache(payment.TenantId, payment.LandlordId, payment.TenancyAgreementId);

                _logger.LogInformation("Payment verified: {PaymentId} by user {UserId}", id, userId);

                return updatedPayment;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error verifying payment {PaymentId}", id);
                return ServiceResponse<RentPaymentResponseDto>.CreateError("An error occurred while verifying payment");
            }
        }

        public async Task<ServiceResponse<PaymentStatisticsDto>> GetPaymentStatisticsAsync(
            DateTime? fromDate, DateTime? toDate, string userId)
        {
            try
            {
                var userRole = GetUserRole();
                IQueryable<RentPayment> query = _context.RentPayments;

                // Apply date filters
                if (fromDate.HasValue)
                    query = query.Where(rp => rp.PaymentDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(rp => rp.PaymentDate <= toDate.Value);

                // Filter by user role
                if (userRole == "Tenant")
                    query = query.Where(rp => rp.TenantId == userId);
                else if (userRole == "Landlord")
                    query = query.Where(rp => rp.LandlordId == userId);

                var totalPayments = await query.CountAsync();
                var completedPayments = await query.CountAsync(rp => rp.PaymentStatus == PaymentStatus.Completed);
                var pendingPayments = await query.CountAsync(rp => rp.PaymentStatus == PaymentStatus.Pending);
                var failedPayments = await query.CountAsync(rp => rp.PaymentStatus == PaymentStatus.Failed);

                var totalAmount = await query
                    .Where(rp => rp.PaymentStatus == PaymentStatus.Completed)
                    .SumAsync(rp => rp.Amount);

                var averagePayment = totalPayments > 0 ? totalAmount / totalPayments : 0;

                // Payments by method
                var paymentsByMethod = await query
                    .Where(rp => rp.PaymentStatus == PaymentStatus.Completed)
                    .GroupBy(rp => rp.PaymentMethod)
                    .Select(g => new { Method = g.Key.ToString(), Total = g.Sum(rp => rp.Amount) })
                    .ToDictionaryAsync(x => x.Method, x => x.Total);

                // Monthly trend (last 6 months)
                var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
                var monthlyTrend = await query
                    .Where(rp => rp.PaymentStatus == PaymentStatus.Completed && rp.PaymentDate >= sixMonthsAgo)
                    .GroupBy(rp => new { Year = rp.PaymentDate.Year, Month = rp.PaymentDate.Month })
                    .Select(g => new {
                        Month = $"{g.Key.Year}-{g.Key.Month:D2}",
                        Total = g.Sum(rp => rp.Amount),
                        Count = g.Count()
                    })
                    .ToDictionaryAsync(x => x.Month, x => new { x.Total, x.Count });

                var statistics = new PaymentStatisticsDto
                {
                    TotalPayments = totalPayments,
                    CompletedPayments = completedPayments,
                    PendingPayments = pendingPayments,
                    FailedPayments = failedPayments,
                    TotalAmount = totalAmount,
                    AveragePayment = averagePayment,
                    PaymentsByMethod = paymentsByMethod,
                    MonthlyTrend = monthlyTrend.ToDictionary(
                        x => x.Key,
                        x => new MonthlyPaymentTrend { TotalAmount = x.Value.Total, PaymentCount = x.Value.Count })
                };

                return ServiceResponse<PaymentStatisticsDto>.CreateSuccess("Payment statistics retrieved", statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payment statistics");
                return ServiceResponse<PaymentStatisticsDto>.CreateError("An error occurred");
            }
        }

        public async Task<PaginatedServiceResponse<List<RentPaymentResponseDto>>> GetOverduePaymentsAsync(
            int page, int pageSize, string userId)
        {
            try
            {
                var userRole = GetUserRole();
                var today = DateTime.UtcNow.Date;

                // Get active tenancies with overdue payments
                var overdueTenancies = await _context.TenancyAgreements
                    .Where(ta => ta.Status == TenancyStatus.Active &&
                                ta.NextPaymentDate.HasValue &&
                                ta.NextPaymentDate < today)
                    .ToListAsync();

                var tenancyIds = overdueTenancies.Select(ta => ta.Id).ToList();

                var query = _context.RentPayments
                    .Include(rp => rp.TenancyAgreement)
                    .Where(rp => tenancyIds.Contains(rp.TenancyAgreementId) &&
                                rp.PaymentStatus == PaymentStatus.Pending &&
                                rp.PeriodEndDate < today)
                    .AsQueryable();

                // Filter by user role
                if (userRole == "Landlord")
                    query = query.Where(rp => rp.LandlordId == userId);
                else if (userRole == "Tenant")
                    query = query.Where(rp => rp.TenantId == userId);

                // Get total count
                var totalCount = await query.CountAsync();

                // Apply ordering and pagination
                var payments = await query
                    .OrderBy(rp => rp.PeriodEndDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var responseDtos = _mapper.Map<List<RentPaymentResponseDto>>(payments);

                return PaginatedServiceResponse<List<RentPaymentResponseDto>>.CreateSuccess(
                    "Overdue payments retrieved",
                    responseDtos,
                    totalCount,
                    page,
                    pageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting overdue payments");
                return PaginatedServiceResponse<List<RentPaymentResponseDto>>.CreateError("An error occurred");
            }
        }

        public async Task<ServiceResponse<bool>> ProcessMobileMoneyCallbackAsync(MobileMoneyCallbackDto dto)
        {
            try
            {
                // Find payment by reference number
                var payment = await _context.RentPayments
                    .FirstOrDefaultAsync(rp => rp.ReferenceNumber == dto.ReferenceNumber);

                if (payment == null)
                    return ServiceResponse<bool>.CreateError("Payment not found");

                // Update payment status based on callback
                payment.PaymentStatus = dto.IsSuccessful ? PaymentStatus.Completed : PaymentStatus.Failed;
                payment.TransactionId = dto.TransactionId;

                // If payment successful, update next payment date
                if (dto.IsSuccessful && payment.TenancyAgreement != null)
                {
                    var tenancy = payment.TenancyAgreement;
                    tenancy.NextPaymentDate = CalculateNextPaymentDate(
                        payment.PeriodEndDate, tenancy.PaymentFrequency);
                    tenancy.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                return ServiceResponse<bool>.CreateSuccess("Mobile money callback processed", true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing mobile money callback");
                return ServiceResponse<bool>.CreateError("An error occurred");
            }
        }

        public async Task<ServiceResponse<List<RentPaymentResponseDto>>> GenerateRentInvoicesAsync(
            DateTime month, string userId)
        {
            try
            {
                var userRole = GetUserRole();
                if (userRole != "Admin" && userRole != "RCD_Officer")
                    return ServiceResponse<List<RentPaymentResponseDto>>.CreateError("Unauthorized");

                var startDate = new DateTime(month.Year, month.Month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                // Get all active tenancies
                var activeTenancies = await _context.TenancyAgreements
                    .Where(ta => ta.Status == TenancyStatus.Active)
                    .ToListAsync();

                var generatedPayments = new List<RentPaymentResponseDto>();

                foreach (var tenancy in activeTenancies)
                {
                    // Check if invoice already exists for this period
                    var existingInvoice = await _context.RentPayments
                        .AnyAsync(rp => rp.TenancyAgreementId == tenancy.Id &&
                                       rp.PeriodStartDate == startDate &&
                                       rp.PeriodEndDate == endDate);

                    if (!existingInvoice)
                    {
                        // Create pending payment/invoice
                        var payment = new RentPayment
                        {
                            TenancyAgreementId = tenancy.Id,
                            TenantId = tenancy.TenantId,
                            LandlordId = tenancy.LandlordId,
                            Amount = tenancy.MonthlyRent,
                            PaymentMethod = PaymentMethod.MobileMoney, // Default
                            PaymentStatus = PaymentStatus.Pending,
                            PeriodStartDate = startDate,
                            PeriodEndDate = endDate,
                            IsAdvancePayment = false,
                            Notes = $"Rent invoice for {month:MMMM yyyy}"
                        };

                        _context.RentPayments.Add(payment);
                        generatedPayments.Add(_mapper.Map<RentPaymentResponseDto>(payment));
                    }
                }

                await _context.SaveChangesAsync();

                return ServiceResponse<List<RentPaymentResponseDto>>.CreateSuccess(
                    $"Generated {generatedPayments.Count} rent invoices for {month:MMMM yyyy}",
                    generatedPayments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating rent invoices");
                return ServiceResponse<List<RentPaymentResponseDto>>.CreateError("An error occurred");
            }
        }

        public async Task<ServiceResponse<bool>> SendPaymentRemindersAsync()
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var reminderDate = today.AddDays(3); // Remind 3 days before due date

                // Get tenancies with payments due in 3 days
                var tenanciesDue = await _context.TenancyAgreements
                    .Where(ta => ta.Status == TenancyStatus.Active &&
                                ta.NextPaymentDate.HasValue &&
                                ta.NextPaymentDate.Value.Date == reminderDate)
                    .ToListAsync();

                foreach (var tenancy in tenanciesDue)
                {
                    // Check if payment already made for this period
                    var paymentMade = await _context.RentPayments
                        .AnyAsync(rp => rp.TenancyAgreementId == tenancy.Id &&
                                       rp.PaymentStatus == PaymentStatus.Completed &&
                                       rp.PeriodEndDate >= tenancy.NextPaymentDate);

                    if (!paymentMade)
                    {
                        // Reminder logic without notification
                        _logger.LogInformation("Payment reminder needed for tenancy {TenancyId}", tenancy.Id);
                    }
                }

                return ServiceResponse<bool>.CreateSuccess("Payment reminders processed", true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending payment reminders");
                return ServiceResponse<bool>.CreateError("An error occurred");
            }
        }

        // Helper Methods
        private string GenerateTransactionId()
        {
            return $"TRX-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
        }

        private DateTime CalculateNextPaymentDate(DateTime currentEndDate, PaymentFrequency frequency)
        {
            return frequency switch
            {
                PaymentFrequency.Monthly => currentEndDate.AddMonths(1),
                PaymentFrequency.Quarterly => currentEndDate.AddMonths(3),
                PaymentFrequency.SemiAnnually => currentEndDate.AddMonths(6),
                PaymentFrequency.Annually => currentEndDate.AddYears(1),
                PaymentFrequency.Weekly => currentEndDate.AddDays(7),
                _ => currentEndDate.AddMonths(1)
            };
        }

        private async Task ClearPaymentCache(string tenantId, string landlordId, Guid tenancyId)
        {
            await _cache.RemoveAsync($"payments_tenant_{tenantId}");
            await _cache.RemoveAsync($"payments_landlord_{landlordId}");
            await _cache.RemoveAsync($"payments_tenancy_{tenancyId}");
        }

        private string GetUserRole()
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value ?? "Tenant";
        }
    }

    // DTOs for payment statistics and callbacks
    public class PaymentStatisticsDto
    {
        public int TotalPayments { get; set; }
        public int CompletedPayments { get; set; }
        public int PendingPayments { get; set; }
        public int FailedPayments { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AveragePayment { get; set; }
        public Dictionary<string, decimal> PaymentsByMethod { get; set; } = new();
        public Dictionary<string, MonthlyPaymentTrend> MonthlyTrend { get; set; } = new();
    }

    public class MonthlyPaymentTrend
    {
        public decimal TotalAmount { get; set; }
        public int PaymentCount { get; set; }
    }

    public class MobileMoneyCallbackDto
    {
        public string TransactionId { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public bool IsSuccessful { get; set; }
        public string? FailureReason { get; set; }
        public DateTime TransactionDate { get; set; }
        public string? Provider { get; set; } // MTN, Vodafone, AirtelTigo
    }

    public class TerminateTenancyDto
    {
        [Required]
        public string Reason { get; set; } = string.Empty;

        public DateTime? ActualVacateDate { get; set; }
    }
}