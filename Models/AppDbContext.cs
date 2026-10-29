
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ngohoangduc.Models;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(
        DbContextOptions<AppDbContext> options
    ) : base(options)
    {
    }

    // Ho so nhan vien
    public DbSet<Employee> Employees => Set<Employee>();

    // Ca lam viec
    public DbSet<WorkShift> WorkShifts => Set<WorkShift>();

    protected override void OnModelCreating(
        ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Giu nguyen schema cua ASP.NET Identity
        builder.HasDefaultSchema("identity");

        builder.Entity<AppUser>(entity =>
        {
            entity.Property(x => x.FullName)
                .HasMaxLength(150);

            entity.Property(x => x.EmployeeCode)
                .HasMaxLength(30);
        });

        builder.Entity<Employee>(entity =>
        {
            entity.ToTable("Employees", "dbo");

            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.EmployeeCode)
                .IsUnique();

            entity.HasIndex(x => x.Email)
                .IsUnique();

            entity.Property(x => x.FullName)
                .HasMaxLength(150);
        });

        builder.Entity<WorkShift>(entity =>
        {
            entity.ToTable("WorkShifts", "dbo");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .HasMaxLength(100);
        });
    }
}
