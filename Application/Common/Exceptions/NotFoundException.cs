namespace Application.Common.Exceptions
{
    public class NotFoundException(string message) : AppException(404, message);
}
