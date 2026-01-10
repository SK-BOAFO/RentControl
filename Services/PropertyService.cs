using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using NetTopologySuite.Geometries;
using RentControlSystem.Auth.API.Helpers;
using RentControlSystem.Tenancy.API.DTOs;
using RentControlSystem.Tenancy.API.Models;
using Newtonsoft.Json;
using RentControlSystem.API.Data;
using System.Drawing; // Added for System.Drawing.Point compatibility

namespace RentControlSystem.Tenancy.API.Services
{
    public interface IPropertyService
    {
        Task<ServiceResponse<PropertyResponseDto>> CreatePropertyAsync(CreatePropertyDto dto, string userId);
        Task<ServiceResponse<PropertyResponseDto>> GetPropertyByIdAsync(Guid id);
        Task<PaginatedServiceResponse<List<PropertyResponseDto>>> SearchPropertiesAsync(
            PropertySearchDto searchDto, int page, int pageSize);
        Task<ServiceResponse<PropertyResponseDto>> UpdatePropertyAsync(
            Guid id, UpdatePropertyDto dto, string userId);
        Task<ServiceResponse<bool>> DeletePropertyAsync(Guid id, string userId);
        Task<ServiceResponse<List<PropertyResponseDto>>> GetPropertiesByLandlordAsync(
            string landlordId);
        Task<ServiceResponse<PropertyStatisticsDto>> GetPropertyStatisticsAsync();
        Task<ServiceResponse<PropertyResponseDto>> AddPropertyAmenityAsync(
            Guid propertyId, PropertyAmenityDto dto, string userId);
        Task<ServiceResponse<bool>> RemovePropertyAmenityAsync(Guid amenityId, string userId);
        Task<ServiceResponse<PropertyResponseDto>> UploadPropertyImageAsync(
            Guid propertyId, IFormFile image, string description, bool isPrimary, string userId);
        Task<ServiceResponse<bool>> RemovePropertyImageAsync(Guid imageId, string userId);
        Task<ServiceResponse<List<PropertyResponseDto>>> GetNearbyPropertiesAsync(
            decimal latitude, decimal longitude, decimal radiusInKm);
        Task<ServiceResponse<RentAdjustmentDto>> AdjustRentAsync(
            Guid propertyId, CreateRentAdjustmentDto dto, string userId);
    }

    public class PropertyService : IPropertyService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IDistributedCache _cache;
        private readonly ILogger<PropertyService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PropertyService(
            ApplicationDbContext context,
            IMapper mapper,
            IDistributedCache cache,
            ILogger<PropertyService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _mapper = mapper;
            _cache = cache;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ServiceResponse<PropertyResponseDto>> CreatePropertyAsync(
            CreatePropertyDto dto, string userId)
        {
            try
            {
                // Check if property code already exists
                var existingProperty = await _context.Properties
                    .FirstOrDefaultAsync(p => p.PropertyCode == dto.PropertyCode);

                if (existingProperty != null)
                    return ServiceResponse<PropertyResponseDto>.CreateError("Property code already exists");

                // Create property
                var property = new Property
                {
                    LandlordId = dto.LandlordId,
                    PropertyCode = dto.PropertyCode,
                    PropertyType = dto.PropertyType,
                    Address = dto.Address,
                    GhanaPostGpsAddress = dto.GhanaPostGpsAddress,
                    City = dto.City,
                    Region = dto.Region,
                    SizeInSqMeters = dto.SizeInSqMeters,
                    NumberOfBedrooms = dto.NumberOfBedrooms,
                    NumberOfBathrooms = dto.NumberOfBathrooms,
                    NumberOfLivingRooms = dto.NumberOfLivingRooms,
                    NumberOfKitchens = dto.NumberOfKitchens,
                    MonthlyRent = dto.MonthlyRent,
                    IsFurnished = dto.IsFurnished,
                    HasParking = dto.HasParking,
                    HasSecurity = dto.HasSecurity,
                    HasWater = dto.HasWater,
                    HasElectricity = dto.HasElectricity,
                    Description = dto.Description,
                    AdditionalNotes = dto.AdditionalNotes,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                // Set location if coordinates provided
                if (dto.Latitude.HasValue && dto.Longitude.HasValue)
                {
                    // Create NetTopologySuite Point with proper SRID
                    property.Location = new NetTopologySuite.Geometries.Point((double)dto.Longitude.Value, (double)dto.Latitude.Value)
                    {
                        SRID = 4326 // WGS84 coordinate system
                    };
                }

                _context.Properties.Add(property);
                await _context.SaveChangesAsync();

                // Get created property with details
                var createdProperty = await _context.Properties
                    .Include(p => p.Amenities)
                    .Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.Id == property.Id);

                var responseDto = _mapper.Map<PropertyResponseDto>(createdProperty);

                // Clear cache
                await ClearPropertyCache(dto.LandlordId);

                _logger.LogInformation("Property created: {PropertyCode} by user {UserId}",
                    dto.PropertyCode, userId);

                return ServiceResponse<PropertyResponseDto>.CreateSuccess("Property created successfully", responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating property");
                return ServiceResponse<PropertyResponseDto>.CreateError("An error occurred while creating property");
            }
        }

        public async Task<ServiceResponse<PropertyResponseDto>> GetPropertyByIdAsync(Guid id)
        {
            try
            {
                var cacheKey = $"property_{id}";
                var cachedData = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    var cachedProperty = JsonConvert.DeserializeObject<PropertyResponseDto>(cachedData);
                    if (cachedProperty != null)
                        return ServiceResponse<PropertyResponseDto>.CreateSuccess("Property retrieved from cache", cachedProperty);
                }

                var property = await _context.Properties
                    .Include(p => p.Amenities)
                    .Include(p => p.Images)
                    .Include(p => p.RentAdjustments.OrderByDescending(ra => ra.EffectiveDate).Take(5))
                    .Include(p => p.TenancyAgreements
                        .Where(ta => ta.Status == TenancyStatus.Active))
                    .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

                if (property == null)
                    return ServiceResponse<PropertyResponseDto>.CreateError("Property not found");

                var responseDto = _mapper.Map<PropertyResponseDto>(property);

                // Calculate current occupancy count
                responseDto.CurrentOccupancyCount = await _context.Occupancies
                    .CountAsync(o => o.PropertyId == id && o.IsCurrent);

                // Cache for 10 minutes
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                };
                await _cache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(responseDto), cacheOptions);

                return ServiceResponse<PropertyResponseDto>.CreateSuccess("Property retrieved", responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving property {PropertyId}", id);
                return ServiceResponse<PropertyResponseDto>.CreateError("An error occurred");
            }
        }

        public async Task<PaginatedServiceResponse<List<PropertyResponseDto>>> SearchPropertiesAsync(
            PropertySearchDto searchDto, int page, int pageSize)
        {
            try
            {
                var cacheKey = $"property_search_{JsonConvert.SerializeObject(searchDto)}_page{page}_size{pageSize}";
                var cachedData = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    var cachedResponse = JsonConvert.DeserializeObject<PaginatedServiceResponse<List<PropertyResponseDto>>>(cachedData);
                    if (cachedResponse != null)
                        return cachedResponse;
                }

                var query = _context.Properties
                    .Include(p => p.Amenities)
                    .Include(p => p.Images)
                    .Where(p => p.IsActive)
                    .AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(searchDto.LandlordId))
                    query = query.Where(p => p.LandlordId == searchDto.LandlordId);

                if (!string.IsNullOrEmpty(searchDto.City))
                    query = query.Where(p => p.City.Contains(searchDto.City));

                if (!string.IsNullOrEmpty(searchDto.Region))
                    query = query.Where(p => p.Region.Contains(searchDto.Region));

                if (searchDto.PropertyType.HasValue)
                    query = query.Where(p => p.PropertyType == searchDto.PropertyType.Value);

                if (searchDto.PropertyStatus.HasValue)
                    query = query.Where(p => p.PropertyStatus == searchDto.PropertyStatus.Value);

                if (searchDto.MinRent.HasValue)
                    query = query.Where(p => p.MonthlyRent >= searchDto.MinRent.Value);

                if (searchDto.MaxRent.HasValue)
                    query = query.Where(p => p.MonthlyRent <= searchDto.MaxRent.Value);

                if (searchDto.MinBedrooms.HasValue)
                    query = query.Where(p => p.NumberOfBedrooms >= searchDto.MinBedrooms.Value);

                if (searchDto.MaxBedrooms.HasValue)
                    query = query.Where(p => p.NumberOfBedrooms <= searchDto.MaxBedrooms.Value);

                if (searchDto.HasParking.HasValue)
                    query = query.Where(p => p.HasParking == searchDto.HasParking.Value);

                if (searchDto.HasSecurity.HasValue)
                    query = query.Where(p => p.HasSecurity == searchDto.HasSecurity.Value);

                if (searchDto.IsFurnished.HasValue)
                    query = query.Where(p => p.IsFurnished == searchDto.IsFurnished.Value);

                if (!string.IsNullOrEmpty(searchDto.SearchTerm))
                {
                    query = query.Where(p =>
                        p.Address.Contains(searchDto.SearchTerm) ||
                        p.City.Contains(searchDto.SearchTerm) ||
                        p.Region.Contains(searchDto.SearchTerm) ||
                        p.Description.Contains(searchDto.SearchTerm) ||
                        p.PropertyCode.Contains(searchDto.SearchTerm));
                }

                // Get total count
                var totalCount = await query.CountAsync();

                // Apply ordering and pagination
                var properties = await query
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var responseDtos = _mapper.Map<List<PropertyResponseDto>>(properties);

                // Get occupancy counts
                foreach (var dto in responseDtos)
                {
                    dto.CurrentOccupancyCount = await _context.Occupancies
                        .CountAsync(o => o.PropertyId == dto.Id && o.IsCurrent);
                }

                var response = PaginatedServiceResponse<List<PropertyResponseDto>>.CreateSuccess(
                    "Properties retrieved successfully",
                    responseDtos,
                    totalCount,
                    page,
                    pageSize);

                // Cache for 5 minutes
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                };
                await _cache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(response), cacheOptions);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching properties");
                return PaginatedServiceResponse<List<PropertyResponseDto>>.CreateError("An error occurred");
            }
        }

        public async Task<ServiceResponse<PropertyResponseDto>> UpdatePropertyAsync(
            Guid id, UpdatePropertyDto dto, string userId)
        {
            try
            {
                var property = await _context.Properties
                    .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

                if (property == null)
                    return ServiceResponse<PropertyResponseDto>.CreateError("Property not found");

                // Check authorization - only landlord or admin can update
                var userRole = GetUserRole();
                if (property.LandlordId != userId && userRole != "Admin" && userRole != "RCD_Officer")
                    return ServiceResponse<PropertyResponseDto>.CreateError("Unauthorized to update property");

                // Update property fields
                if (!string.IsNullOrEmpty(dto.Address))
                    property.Address = dto.Address;

                if (!string.IsNullOrEmpty(dto.GhanaPostGpsAddress))
                    property.GhanaPostGpsAddress = dto.GhanaPostGpsAddress;

                if (!string.IsNullOrEmpty(dto.City))
                    property.City = dto.City;

                if (!string.IsNullOrEmpty(dto.Region))
                    property.Region = dto.Region;

                if (dto.SizeInSqMeters.HasValue)
                    property.SizeInSqMeters = dto.SizeInSqMeters.Value;

                if (dto.NumberOfBedrooms.HasValue)
                    property.NumberOfBedrooms = dto.NumberOfBedrooms.Value;

                if (dto.NumberOfBathrooms.HasValue)
                    property.NumberOfBathrooms = dto.NumberOfBathrooms.Value;

                if (dto.NumberOfLivingRooms.HasValue)
                    property.NumberOfLivingRooms = dto.NumberOfLivingRooms.Value;

                if (dto.NumberOfKitchens.HasValue)
                    property.NumberOfKitchens = dto.NumberOfKitchens.Value;

                if (dto.MonthlyRent.HasValue)
                    property.MonthlyRent = dto.MonthlyRent.Value;

                if (dto.IsFurnished.HasValue)
                    property.IsFurnished = dto.IsFurnished.Value;

                if (dto.HasParking.HasValue)
                    property.HasParking = dto.HasParking.Value;

                if (dto.HasSecurity.HasValue)
                    property.HasSecurity = dto.HasSecurity.Value;

                if (dto.HasWater.HasValue)
                    property.HasWater = dto.HasWater.Value;

                if (dto.HasElectricity.HasValue)
                    property.HasElectricity = dto.HasElectricity.Value;

                if (!string.IsNullOrEmpty(dto.Description))
                    property.Description = dto.Description;

                if (!string.IsNullOrEmpty(dto.AdditionalNotes))
                    property.AdditionalNotes = dto.AdditionalNotes;

                if (dto.PropertyStatus.HasValue)
                    property.PropertyStatus = dto.PropertyStatus.Value;

                // Set location if coordinates provided
                if (dto.Latitude.HasValue && dto.Longitude.HasValue)
                {
                    property.Location = new NetTopologySuite.Geometries.Point((double)dto.Longitude.Value, (double)dto.Latitude.Value)
                    {
                        SRID = 4326 // WGS84 coordinate system
                    };
                }

                property.UpdatedAt = DateTime.UtcNow;
                property.UpdatedBy = userId;

                await _context.SaveChangesAsync();

                // Clear cache
                await ClearPropertyCache(property.LandlordId);
                await _cache.RemoveAsync($"property_{id}");

                // Get updated property
                var updatedProperty = await GetPropertyByIdAsync(id);

                _logger.LogInformation("Property updated: {PropertyId} by user {UserId}", id, userId);

                return updatedProperty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating property {PropertyId}", id);
                return ServiceResponse<PropertyResponseDto>.CreateError("An error occurred while updating property");
            }
        }

        public async Task<ServiceResponse<bool>> DeletePropertyAsync(Guid id, string userId)
        {
            try
            {
                var property = await _context.Properties
                    .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

                if (property == null)
                    return ServiceResponse<bool>.CreateError("Property not found");

                // Check authorization - only landlord or admin can delete
                var userRole = GetUserRole();
                if (property.LandlordId != userId && userRole != "Admin" && userRole != "RCD_Officer")
                    return ServiceResponse<bool>.CreateError("Unauthorized to delete property");

                // Check if property has active tenancies
                var activeTenancies = await _context.TenancyAgreements
                    .AnyAsync(ta => ta.PropertyId == id && ta.Status == TenancyStatus.Active);

                if (activeTenancies)
                    return ServiceResponse<bool>.CreateError("Cannot delete property with active tenancies");

                // Soft delete
                property.IsActive = false;
                property.UpdatedAt = DateTime.UtcNow;
                property.UpdatedBy = userId;

                await _context.SaveChangesAsync();

                // Clear cache
                await ClearPropertyCache(property.LandlordId);
                await _cache.RemoveAsync($"property_{id}");

                _logger.LogInformation("Property deleted: {PropertyId} by user {UserId}", id, userId);

                return ServiceResponse<bool>.CreateSuccess("Property deleted successfully", true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting property {PropertyId}", id);
                return ServiceResponse<bool>.CreateError("An error occurred while deleting property");
            }
        }

        public async Task<ServiceResponse<List<PropertyResponseDto>>> GetPropertiesByLandlordAsync(string landlordId)
        {
            try
            {
                var cacheKey = $"properties_landlord_{landlordId}";
                var cachedData = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    var cachedProperties = JsonConvert.DeserializeObject<List<PropertyResponseDto>>(cachedData);
                    if (cachedProperties != null)
                        return ServiceResponse<List<PropertyResponseDto>>.CreateSuccess("Properties retrieved from cache", cachedProperties);
                }

                var properties = await _context.Properties
                    .Include(p => p.Amenities)
                    .Include(p => p.Images)
                    .Where(p => p.LandlordId == landlordId && p.IsActive)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                var responseDtos = _mapper.Map<List<PropertyResponseDto>>(properties);

                // Get occupancy counts
                foreach (var dto in responseDtos)
                {
                    dto.CurrentOccupancyCount = await _context.Occupancies
                        .CountAsync(o => o.PropertyId == dto.Id && o.IsCurrent);
                }

                // Cache for 10 minutes
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                };
                await _cache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(responseDtos), cacheOptions);

                return ServiceResponse<List<PropertyResponseDto>>.CreateSuccess("Properties retrieved successfully", responseDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting properties for landlord {LandlordId}", landlordId);
                return ServiceResponse<List<PropertyResponseDto>>.CreateError("An error occurred");
            }
        }

        public async Task<ServiceResponse<PropertyStatisticsDto>> GetPropertyStatisticsAsync()
        {
            try
            {
                var cacheKey = "property_statistics";
                var cachedData = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    var cachedStats = JsonConvert.DeserializeObject<PropertyStatisticsDto>(cachedData);
                    if (cachedStats != null)
                        return ServiceResponse<PropertyStatisticsDto>.CreateSuccess("Statistics retrieved from cache", cachedStats);
                }

                var totalProperties = await _context.Properties.CountAsync(p => p.IsActive);
                var occupiedProperties = await _context.Properties
                    .CountAsync(p => p.IsActive && p.PropertyStatus == PropertyStatus.Occupied);
                var vacantProperties = await _context.Properties
                    .CountAsync(p => p.IsActive && p.PropertyStatus == PropertyStatus.Available);

                var averageRent = await _context.Properties
                    .Where(p => p.IsActive)
                    .AverageAsync(p => (double?)p.MonthlyRent) ?? 0;

                var propertiesByType = await _context.Properties
                    .Where(p => p.IsActive)
                    .GroupBy(p => p.PropertyType)
                    .Select(g => new { Type = g.Key.ToString(), Count = g.Count() })
                    .ToDictionaryAsync(x => x.Type, x => x.Count);

                var statistics = new PropertyStatisticsDto
                {
                    TotalProperties = totalProperties,
                    OccupiedProperties = occupiedProperties,
                    VacantProperties = vacantProperties,
                    AverageRent = (decimal)averageRent,
                    PropertiesByType = propertiesByType
                };

                // Cache for 5 minutes
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                };
                await _cache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(statistics), cacheOptions);

                return ServiceResponse<PropertyStatisticsDto>.CreateSuccess("Property statistics retrieved", statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting property statistics");
                return ServiceResponse<PropertyStatisticsDto>.CreateError("An error occurred");
            }
        }

        public async Task<ServiceResponse<PropertyResponseDto>> AddPropertyAmenityAsync(
            Guid propertyId, PropertyAmenityDto dto, string userId)
        {
            try
            {
                var property = await _context.Properties
                    .Include(p => p.Amenities)
                    .FirstOrDefaultAsync(p => p.Id == propertyId && p.IsActive);

                if (property == null)
                    return ServiceResponse<PropertyResponseDto>.CreateError("Property not found");

                // Check authorization
                var userRole = GetUserRole();
                if (property.LandlordId != userId && userRole != "Admin" && userRole != "RCD_Officer")
                    return ServiceResponse<PropertyResponseDto>.CreateError("Unauthorized to add amenity");

                // Create amenity
                var amenity = new PropertyAmenity
                {
                    PropertyId = propertyId,
                    Name = dto.Name,
                    Description = dto.Description,
                    IsAvailable = dto.IsAvailable,
                    AdditionalInfo = dto.AdditionalInfo
                };

                property.Amenities.Add(amenity);
                property.UpdatedAt = DateTime.UtcNow;
                property.UpdatedBy = userId;

                await _context.SaveChangesAsync();

                // Clear cache
                await ClearPropertyCache(property.LandlordId);
                await _cache.RemoveAsync($"property_{propertyId}");

                // Get updated property
                var updatedProperty = await GetPropertyByIdAsync(propertyId);

                _logger.LogInformation("Amenity added to property {PropertyId} by user {UserId}", propertyId, userId);

                return updatedProperty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding amenity to property {PropertyId}", propertyId);
                return ServiceResponse<PropertyResponseDto>.CreateError("An error occurred while adding amenity");
            }
        }

        public async Task<ServiceResponse<bool>> RemovePropertyAmenityAsync(Guid amenityId, string userId)
        {
            try
            {
                var amenity = await _context.PropertyAmenities
                    .Include(a => a.Property)
                    .FirstOrDefaultAsync(a => a.Id == amenityId);

                if (amenity == null)
                    return ServiceResponse<bool>.CreateError("Amenity not found");

                // Check authorization
                var userRole = GetUserRole();
                if (amenity.Property.LandlordId != userId && userRole != "Admin" && userRole != "RCD_Officer")
                    return ServiceResponse<bool>.CreateError("Unauthorized to remove amenity");

                _context.PropertyAmenities.Remove(amenity);

                amenity.Property.UpdatedAt = DateTime.UtcNow;
                amenity.Property.UpdatedBy = userId;

                await _context.SaveChangesAsync();

                // Clear cache
                await ClearPropertyCache(amenity.Property.LandlordId);
                await _cache.RemoveAsync($"property_{amenity.PropertyId}");

                _logger.LogInformation("Amenity removed: {AmenityId} from property {PropertyId} by user {UserId}",
                    amenityId, amenity.PropertyId, userId);

                return ServiceResponse<bool>.CreateSuccess("Amenity removed successfully", true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing amenity {AmenityId}", amenityId);
                return ServiceResponse<bool>.CreateError("An error occurred while removing amenity");
            }
        }

        public async Task<ServiceResponse<PropertyResponseDto>> UploadPropertyImageAsync(
            Guid propertyId, IFormFile image, string description, bool isPrimary, string userId)
        {
            try
            {
                var property = await _context.Properties
                    .FirstOrDefaultAsync(p => p.Id == propertyId && p.IsActive);

                if (property == null)
                    return ServiceResponse<PropertyResponseDto>.CreateError("Property not found");

                // Check authorization
                var userRole = GetUserRole();
                if (property.LandlordId != userId && userRole != "Admin" && userRole != "RCD_Officer")
                    return ServiceResponse<PropertyResponseDto>.CreateError("Unauthorized to upload image");

                // Validate image
                if (image == null || image.Length == 0)
                    return ServiceResponse<PropertyResponseDto>.CreateError("No image provided");

                // Check file size (max 5MB)
                if (image.Length > 5 * 1024 * 1024)
                    return ServiceResponse<PropertyResponseDto>.CreateError("Image size exceeds 5MB limit");

                // Check file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                    return ServiceResponse<PropertyResponseDto>.CreateError("Invalid image format. Allowed: JPG, JPEG, PNG, GIF");

                // Generate unique filename
                var fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine("wwwroot", "property-images", fileName);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory!);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                // If setting as primary, reset existing primary images
                if (isPrimary)
                {
                    var existingPrimaryImages = await _context.PropertyImages
                        .Where(pi => pi.PropertyId == propertyId && pi.IsPrimary)
                        .ToListAsync();

                    foreach (var existingImage in existingPrimaryImages)
                    {
                        existingImage.IsPrimary = false;
                    }
                }

                // Create image record
                var propertyImage = new PropertyImage
                {
                    PropertyId = propertyId,
                    ImageUrl = $"/property-images/{fileName}",
                    Description = description,
                    IsPrimary = isPrimary,
                    UploadedAt = DateTime.UtcNow
                };

                _context.PropertyImages.Add(propertyImage);
                property.UpdatedAt = DateTime.UtcNow;
                property.UpdatedBy = userId;

                await _context.SaveChangesAsync();

                // Clear cache
                await ClearPropertyCache(property.LandlordId);
                await _cache.RemoveAsync($"property_{propertyId}");

                // Get updated property
                var updatedProperty = await GetPropertyByIdAsync(propertyId);

                _logger.LogInformation("Image uploaded for property {PropertyId} by user {UserId}", propertyId, userId);

                return updatedProperty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image for property {PropertyId}", propertyId);
                return ServiceResponse<PropertyResponseDto>.CreateError("An error occurred while uploading image");
            }
        }

        public async Task<ServiceResponse<bool>> RemovePropertyImageAsync(Guid imageId, string userId)
        {
            try
            {
                var image = await _context.PropertyImages
                    .Include(pi => pi.Property)
                    .FirstOrDefaultAsync(pi => pi.Id == imageId);

                if (image == null)
                    return ServiceResponse<bool>.CreateError("Image not found");

                // Check authorization
                var userRole = GetUserRole();
                if (image.Property.LandlordId != userId && userRole != "Admin" && userRole != "RCD_Officer")
                    return ServiceResponse<bool>.CreateError("Unauthorized to remove image");

                // Delete physical file
                var filePath = Path.Combine("wwwroot", image.ImageUrl.TrimStart('/'));
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                _context.PropertyImages.Remove(image);

                image.Property.UpdatedAt = DateTime.UtcNow;
                image.Property.UpdatedBy = userId;

                await _context.SaveChangesAsync();

                // If the removed image was primary, set another image as primary if available
                if (image.IsPrimary)
                {
                    var newPrimaryImage = await _context.PropertyImages
                        .Where(pi => pi.PropertyId == image.PropertyId)
                        .OrderByDescending(pi => pi.UploadedAt)
                        .FirstOrDefaultAsync();

                    if (newPrimaryImage != null)
                    {
                        newPrimaryImage.IsPrimary = true;
                        await _context.SaveChangesAsync();
                    }
                }

                // Clear cache
                await ClearPropertyCache(image.Property.LandlordId);
                await _cache.RemoveAsync($"property_{image.PropertyId}");

                _logger.LogInformation("Image removed: {ImageId} from property {PropertyId} by user {UserId}",
                    imageId, image.PropertyId, userId);

                return ServiceResponse<bool>.CreateSuccess("Image removed successfully", true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing image {ImageId}", imageId);
                return ServiceResponse<bool>.CreateError("An error occurred while removing image");
            }
        }

        public async Task<ServiceResponse<List<PropertyResponseDto>>> GetNearbyPropertiesAsync(
            decimal latitude, decimal longitude, decimal radiusInKm)
        {
            try
            {
                var cacheKey = $"nearby_properties_{latitude}_{longitude}_{radiusInKm}";
                var cachedData = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    var cachedProperties = JsonConvert.DeserializeObject<List<PropertyResponseDto>>(cachedData);
                    if (cachedProperties != null)
                        return ServiceResponse<List<PropertyResponseDto>>.CreateSuccess("Nearby properties retrieved from cache", cachedProperties);
                }

                // For NetTopologySuite Point, we need to use spatial queries
                var properties = await _context.Properties
                    .Include(p => p.Amenities)
                    .Include(p => p.Images)
                    .Where(p => p.IsActive && p.Location != null)
                    .ToListAsync();

                // Filter properties by distance in memory since EF Core spatial queries can be complex
                var nearbyProperties = properties
                    .Where(p => p.Location != null && CalculateDistance(
                        (double)latitude,
                        (double)longitude,
                        p.Location.Y,  // Changed from p.Location.Value.Y
                        p.Location.X)  // Changed from p.Location.Value.X
                        <= (double)radiusInKm)
                    .OrderBy(p => CalculateDistance(
                        (double)latitude,
                        (double)longitude,
                        p.Location.Y,  // Changed from p.Location.Value.Y
                        p.Location.X))  // Changed from p.Location.Value.X
                    .Take(50) // Limit results
                    .ToList();

                var responseDtos = _mapper.Map<List<PropertyResponseDto>>(nearbyProperties);

                // Cache for 15 minutes
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
                };
                await _cache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(responseDtos), cacheOptions);

                return ServiceResponse<List<PropertyResponseDto>>.CreateSuccess("Nearby properties retrieved", responseDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting nearby properties");
                return ServiceResponse<List<PropertyResponseDto>>.CreateError("An error occurred");
            }
        }

        public async Task<ServiceResponse<RentAdjustmentDto>> AdjustRentAsync(
            Guid propertyId, CreateRentAdjustmentDto dto, string userId)
        {
            try
            {
                var property = await _context.Properties
                    .FirstOrDefaultAsync(p => p.Id == propertyId && p.IsActive);

                if (property == null)
                    return ServiceResponse<RentAdjustmentDto>.CreateError("Property not found");

                // Check authorization
                var userRole = GetUserRole();
                if (property.LandlordId != userId && userRole != "Admin" && userRole != "RCD_Officer")
                    return ServiceResponse<RentAdjustmentDto>.CreateError("Unauthorized to adjust rent");

                // Create rent adjustment
                var rentAdjustment = new RentAdjustment
                {
                    PropertyId = propertyId,
                    PreviousRent = property.MonthlyRent,
                    NewRent = dto.NewRent,
                    PercentageChange = property.MonthlyRent > 0 ?
                        ((dto.NewRent - property.MonthlyRent) / property.MonthlyRent) * 100 : 0,
                    EffectiveDate = dto.EffectiveDate,
                    Reason = dto.Reason,
                    ApprovalReference = dto.ApprovalReference,
                    ApprovedAt = DateTime.UtcNow,
                    ApprovedBy = userId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.RentAdjustments.Add(rentAdjustment);

                // Update property rent immediately
                property.MonthlyRent = dto.NewRent;
                property.UpdatedAt = DateTime.UtcNow;
                property.UpdatedBy = userId;

                await _context.SaveChangesAsync();

                // Clear cache
                await ClearPropertyCache(property.LandlordId);
                await _cache.RemoveAsync($"property_{propertyId}");

                var responseDto = _mapper.Map<RentAdjustmentDto>(rentAdjustment);

                _logger.LogInformation("Rent adjustment created for property {PropertyId} by user {UserId}",
                    propertyId, userId);

                return ServiceResponse<RentAdjustmentDto>.CreateSuccess("Rent adjustment created", responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adjusting rent for property {PropertyId}", propertyId);
                return ServiceResponse<RentAdjustmentDto>.CreateError("An error occurred while adjusting rent");
            }
        }

        // Helper Methods
        private async Task ClearPropertyCache(string landlordId)
        {
            await _cache.RemoveAsync($"properties_landlord_{landlordId}");
            await _cache.RemoveAsync("property_statistics");
        }

        private string GetUserRole()
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Tenant";
        }

        // Helper method to calculate distance between two coordinates using Haversine formula
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Earth's radius in kilometers
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double angle)
        {
            return Math.PI * angle / 180.0;
        }
    }

    // DTOs for property statistics
    public class PropertyStatisticsDto
    {
        public int TotalProperties { get; set; }
        public int OccupiedProperties { get; set; }
        public int VacantProperties { get; set; }
        public decimal AverageRent { get; set; }
        public Dictionary<string, int> PropertiesByType { get; set; } = new();
    }

    // Missing enums that are referenced in the code
    public enum RentAdjustmentType
    {
        Increase,
        Decrease,
        Maintenance,
        MarketAdjustment
    }

    public enum RentAdjustmentStatus
    {
        Pending,
        Approved,
        Rejected,
        Implemented
    }

    // Missing DTOs that are referenced in the code
    public class PropertyAmenityDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsAvailable { get; set; }
        public string? AdditionalInfo { get; set; }
        public string? Value { get; set; } // Added this property to match the code usage
    }

    public class CreateRentAdjustmentDto
    {
        public Guid PropertyId { get; set; }
        public decimal NewRent { get; set; }
        public DateTime EffectiveDate { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? ApprovalReference { get; set; }
        public RentAdjustmentType AdjustmentType { get; set; }
        public bool IsApproved { get; set; } = false;
    }
}