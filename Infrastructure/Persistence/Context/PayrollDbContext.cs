using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Context
{
    public class PayrollDbContext(DbContextOptions<PayrollDbContext> options) : DbContext(options)
    {

    }
}
