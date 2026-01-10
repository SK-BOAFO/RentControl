namespace RentControlSystem.Auth.API.Helpers
{
    public class ApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
        public List<string>? Errors { get; set; }

        public ApiResponse(bool success, string message, object? data = null, List<string>? errors = null)
        {
            Success = success;
            Message = message;
            Data = data;
            Errors = errors;
        }
    }

    public class PaginatedApiResponse : ApiResponse
    {
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        public PaginatedApiResponse(bool success, string message, object? data, int totalCount, int currentPage, int pageSize)
            : base(success, message, data)
        {
            TotalCount = totalCount;
            CurrentPage = currentPage;
            PageSize = pageSize;
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        }
    }
}