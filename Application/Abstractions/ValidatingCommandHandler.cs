using FluentValidation;
using FluentValidation.Results;
using ValidationException = Application.Common.Exceptions.ValidationException;

namespace Application.Abstractions
{
    public class ValidatingCommandHandler<TCommand, TResult>(ICommandHandler<TCommand, TResult> inner, IEnumerable<IValidator<TCommand>> validators)
    : ICommandHandler<TCommand, TResult>
    {
        public async Task<TResult> HandleAsync(TCommand command)
        {
            var failures = new List<ValidationFailure>();
            foreach (var validator in validators)
                failures.AddRange((await validator.ValidateAsync(command)).Errors);

            // Our own ValidationException, so ErrorHandlingMiddleware returns the same { message, errors } shape as every other error.
            if (failures.Count > 0)
                throw new ValidationException(
                    failures.GroupBy(f => f.PropertyName).ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray()),
                    "Validation failed.");

            return await inner.HandleAsync(command);
        }
    }
}
