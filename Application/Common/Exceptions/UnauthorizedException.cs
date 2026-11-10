namespace Application.Common.Exceptions
{
    public class UnauthorizedException(string message) : AppException(401, message);
}
