using Application.Interfaces;
using Infrastructure.Persistence.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Persistence
{
    public class UnitOfWork(PayrollDbContext db) : IUnitOfWork
    {
        public async Task<T> InTransactionAsync<T>(Func<Task<T>> work)
        {
            // Nested call: join the outer transaction instead of starting a second one.
            if (db.Database.CurrentTransaction is not null)
                return await work();

            await using var tx = await db.Database.BeginTransactionAsync();
            var result = await work();
            await tx.CommitAsync();
            return result; // an exception skips Commit, and disposing tx rolls back
        }
    }
}