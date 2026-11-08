using Microsoft.EntityFrameworkCore;

namespace Sms.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeInstance> EmployeeInstances => Set<EmployeeInstance>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // No employee number exists, so first+last+address is the business key.
        b.Entity<Employee>().HasIndex(e => new { e.FirstName, e.LastName, e.Address }).IsUnique();
        b.Entity<Employee>().HasMany(e => e.Instances).WithOne(i => i.Employee!)
            .HasForeignKey(i => i.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<EmployeeInstance>().HasIndex(i => i.PhoneNumber);

        // Idempotency enforced by the database, not just by application code.
        b.Entity<Message>().HasIndex(m => m.IdempotencyKey).IsUnique();
        b.Entity<Message>().HasIndex(m => new { m.Status, m.CreatedAt });
    }
}
