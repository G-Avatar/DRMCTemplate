namespace Application.Common.Exceptions
{
    public abstract class AppException(int statusCode, string message) : Exception(message)
    {
        public int StatusCode { get; } = statusCode;
    }
}
