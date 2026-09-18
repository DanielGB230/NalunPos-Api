using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Entities;

namespace Pos.Infrastructure.Persistence.Configurations;

public class ContainerConfiguration : IEntityTypeConfiguration<Container>
{
    public void Configure(EntityTypeBuilder<Container> builder)
    {
        builder.ToTable("Containers");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.IsActive)
            .IsRequired();

        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(c => c.WarehouseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Container>()
            .WithMany()
            .HasForeignKey(c => c.ParentContainerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.WarehouseId, c.Name })
            .IsUnique();
    }
}
