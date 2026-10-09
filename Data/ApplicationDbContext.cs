using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using EmployeeMvc.Models;

namespace EmployeeMvc.Data
{

    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Employee> Employees { get; set; } = null!;
        public DbSet<TaskItem> Tasks { get; set; } = null!;
        public DbSet<TaskSubmission> TaskSubmissions { get; set; } = null!;
        public DbSet<EmailVerificationCode> EmailVerificationCodes { get; set; } = null!;
        public DbSet<Group> Groups { get; set; } = null!;
        public DbSet<GroupMember> GroupMembers { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TaskItem>()
                .HasOne(t => t.Employee)
                .WithMany()
                .HasForeignKey(t => t.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TaskSubmission>()
                .HasOne(s => s.TaskItem)
                .WithMany(t => t.Submissions)
                .HasForeignKey(s => s.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.AccountId)
                .IsUnique();


            modelBuilder.Entity<Group>(entity =>
            {
                entity.HasOne(g => g.Owner)
                    .WithMany()
                    .HasForeignKey(g => g.OwnerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(g => g.OwnerId);
            });


            modelBuilder.Entity<GroupMember>(entity =>
            {
                entity.HasKey(m => new { m.GroupId, m.UserId });

                entity.HasOne(m => m.Group)
                    .WithMany(g => g.Members)
                    .HasForeignKey(m => m.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(m => m.User)
                    .WithMany()
                    .HasForeignKey(m => m.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Employee>().HasData(
                new Employee
                {
                    Id = 1,
                    FullName = "გიორგი მაისურაძე",
                    Position = "Software Engineer",
                    Department = "IT",
                    Email = "g.maisuradze@company.ge",
                    HireDate = new DateTime(2023, 3, 15),
                    IsWorking = true
                },
                new Employee
                {
                    Id = 2,
                    FullName = "ნინო ბერიძე",
                    Position = "HR Manager",
                    Department = "HR",
                    Email = "n.beridze@company.ge",
                    HireDate = new DateTime(2021, 6, 1),
                    IsWorking = true
                },
                new Employee
                {
                    Id = 3,
                    FullName = "ლუკა კვარაცხელია",
                    Position = "Accountant",
                    Department = "Finance",
                    Email = "l.kvaratskhelia@company.ge",
                    HireDate = new DateTime(2019, 11, 20),
                    IsWorking = false
                }
            );
        }
    }
}
