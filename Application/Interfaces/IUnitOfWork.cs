using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface IUnitOfWork
    {
        Task<T> InTransactionAsync<T>(Func<Task<T>> work);
    }
}