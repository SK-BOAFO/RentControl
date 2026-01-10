using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentControlSystem.Auth.API.Helpers;
using RentControlSystem.Tenancy.API.DTOs;
using RentControlSystem.Tenancy.API.Services;
using System.Security.Claims;

namespace RentControlSystem.Tenancy.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PropertiesController : ControllerBase
    {
        private readonly IPropertyService _propertyService;
        private readonly ILogger<PropertiesController> _logger;

        public PropertiesController(
            IPropertyService propertyService,
            ILogger<PropertiesController> logger)
        {
            _propertyService = propertyService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> CreateProperty([FromBody] CreatePropertyDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _propertyService.CreatePropertyAsync(dto, userId);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message, null, result.Errors));

                return CreatedAtAction(nameof(GetPropertyById),
                    new { id = result.Data?.Id },
                    new ApiResponse(true, result.Message, result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating property");
                return StatusCode(500, new ApiResponse(false, "An error occurred while creating property"));
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPropertyById(Guid id)
        {
            try
            {
                var result = await _propertyService.GetPropertyByIdAsync(id);

                if (!result.Success)
                    return NotFound(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, result.Message, result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting property {PropertyId}", id);
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchProperties(
            [FromQuery] PropertySearchDto searchDto,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var result = await _propertyService.SearchPropertiesAsync(searchDto, page, pageSize);

                return Ok(new PaginatedApiResponse(
                    result.Success,
                    result.Message,
                    result.Data,
                    result.TotalCount,
                    page,
                    pageSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching properties");
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpGet("landlord/{landlordId}")]
        public async Task<IActionResult> GetPropertiesByLandlord(string landlordId)
        {
            try
            {
                var result = await _propertyService.GetPropertiesByLandlordAsync(landlordId);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, result.Message, result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting properties for landlord {LandlordId}", landlordId);
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpGet("nearby")]
        public async Task<IActionResult> GetNearbyProperties(
            [FromQuery] decimal latitude,
            [FromQuery] decimal longitude,
            [FromQuery] decimal radius = 5) // Default 5km radius
        {
            try
            {
                var result = await _propertyService.GetNearbyPropertiesAsync(latitude, longitude, radius);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, result.Message, result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting nearby properties");
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProperty(Guid id, [FromBody] UpdatePropertyDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _propertyService.UpdatePropertyAsync(id, dto, userId);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message, null, result.Errors));

                return Ok(new ApiResponse(true, result.Message, result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating property {PropertyId}", id);
                return StatusCode(500, new ApiResponse(false, "An error occurred while updating property"));
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProperty(Guid id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _propertyService.DeletePropertyAsync(id, userId);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, result.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting property {PropertyId}", id);
                return StatusCode(500, new ApiResponse(false, "An error occurred while deleting property"));
            }
        }

        [HttpPost("{propertyId}/amenities")]
        public async Task<IActionResult> AddAmenity(Guid propertyId, [FromBody] Services.PropertyAmenityDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _propertyService.AddPropertyAmenityAsync(propertyId, dto, userId);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, result.Message, result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding amenity to property {PropertyId}", propertyId);
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpDelete("amenities/{amenityId}")]
        public async Task<IActionResult> RemoveAmenity(Guid amenityId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _propertyService.RemovePropertyAmenityAsync(amenityId, userId);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, result.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing amenity {AmenityId}", amenityId);
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpPost("{propertyId}/images")]
        public async Task<IActionResult> UploadImage(
            Guid propertyId,
            IFormFile file,
            [FromForm] string? description = null,
            [FromForm] bool isPrimary = false)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                if (file == null || file.Length == 0)
                    return BadRequest(new ApiResponse(false, "No file uploaded"));

                var result = await _propertyService.UploadPropertyImageAsync(
                    propertyId, file, description, isPrimary, userId);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, result.Message, result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image for property {PropertyId}", propertyId);
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpDelete("images/{imageId}")]
        public async Task<IActionResult> RemoveImage(Guid imageId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _propertyService.RemovePropertyImageAsync(imageId, userId);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, result.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing image {ImageId}", imageId);
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                var result = await _propertyService.GetPropertyStatisticsAsync();

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, result.Message, result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting property statistics");
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpPost("{propertyId}/rent-adjustment")]
        public async Task<IActionResult> AdjustRent(
            Guid propertyId,
            [FromBody] Services.CreateRentAdjustmentDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _propertyService.AdjustRentAsync(propertyId, dto, userId);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, result.Message, result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adjusting rent for property {PropertyId}", propertyId);
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }
    }
}