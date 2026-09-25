using FinanceTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public const int MaxNoteLength = 500;

    public DbSet<User> Users => Set<User>();
    public DbSet<Expense> Expenses => Set<Expense>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            user.ToTable("users");
            // Id приходит из Telegram, БД его не генерирует.
            user.Property(u => u.Id).ValueGeneratedNever();
            user.Property(u => u.FirstName).HasMaxLength(256);
            user.Property(u => u.LastName).HasMaxLength(256);
            user.Property(u => u.Username).HasMaxLength(64);
            user.Property(u => u.LanguageCode).HasMaxLength(16);
        });

        modelBuilder.Entity<Expense>(expense =>
        {
            expense.ToTable("expenses", t =>
                t.HasCheckConstraint("ck_expenses_amount_positive", "\"Amount\" > 0"));

            expense.Property(e => e.Amount).HasPrecision(12, 2);
            expense.Property(e => e.Category).HasConversion<string>().HasMaxLength(32);
            expense.Property(e => e.Note).HasMaxLength(MaxNoteLength);

            expense.HasOne(e => e.User)
                .WithMany(u => u.Expenses)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Почти все запросы — "расходы пользователя за период".
            expense.HasIndex(e => new { e.UserId, e.Date });
        });
    }
}
