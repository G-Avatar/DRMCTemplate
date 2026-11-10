namespace Application.Common.Exceptions
{
    public class ConflictException(string message) : AppException(409, message);
}
