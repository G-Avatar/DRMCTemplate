namespace Application.Common.Exceptions
{
    public class ValidationException(Dictionary<string, string[]> errors, string message) : AppException(400, message)
    {
        public Dictionary<string, string[]> Errors { get; } = errors;
    }
}
