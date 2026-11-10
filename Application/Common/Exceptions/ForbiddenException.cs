namespace Application.Common.Exceptions
{
    public class ForbiddenException(string message) : AppException(403, message);
}
