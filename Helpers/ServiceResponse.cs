namespace RentControlSystem.Auth.API.Helpers
{
    public class ServiceResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }

        // Factory methods with different names
        public static ServiceResponse<T> CreateSuccess(string message, T data)
        {
            return new ServiceResponse<T>
            {
                Success = true,
                Message = message,
                Data = data
            };
        }

        public static ServiceResponse<T> CreateError(string message, List<string>? errors = null)
        {
            return new ServiceResponse<T>
            {
                Success = false,
                Message = message,
                Errors = errors
            };
        }
    }

    public class PaginatedServiceResponse<T> : ServiceResponse<T>
    {
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }

        public static PaginatedServiceResponse<T> CreateSuccess(string message, T data, int totalCount, int currentPage = 1, int pageSize = 20)
        {
            return new PaginatedServiceResponse<T>
            {
                Success = true,
                Message = message,
                Data = data,
                TotalCount = totalCount,
                CurrentPage = currentPage,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        public static new PaginatedServiceResponse<T> CreateError(string message, List<string>? errors = null)
        {
            return new PaginatedServiceResponse<T>
            {
                Success = false,
                Message = message,
                Errors = errors
            };
        }
    }
}