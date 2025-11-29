using FifaTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FifaTracker.Infrastructure.Persistence.Configurations;

public class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.ToTable("Matches");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.SessionId)
            .IsRequired();

        builder.Property(m => m.IsGenerated)
            .IsRequired();

        builder.Property(m => m.IsCompleted)
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .IsRequired();

        // Priority is not persisted to database - calculated on-demand
        builder.Ignore(m => m.Priority);

        builder.HasOne(m => m.Session)
            .WithMany(s => s.Matches)
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.MatchTeams)
            .WithOne(mt => mt.Match)
            .HasForeignKey(mt => mt.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.SessionId);
        builder.HasIndex(m => m.IsCompleted);
    }
}
