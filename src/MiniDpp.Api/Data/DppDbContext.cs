using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MiniDpp.Api.Domain;

namespace MiniDpp.Api.Data;

public sealed class DppDbContext(DbContextOptions<DppDbContext> options) : DbContext(options)
{
    public DbSet<ProductModel> ProductModels => Set<ProductModel>();

    public DbSet<Batch> Batches => Set<Batch>();

    public DbSet<BatchVersion> BatchVersions => Set<BatchVersion>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // SQLite DateTime'ı TEXT olarak saklar ve Kind bilgisini kaybeder; okurken UTC olarak işaretle.
        builder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();

        // SQLite sağlayıcısı decimal'ı "0.0###" biçiminde yazar (98 → "98.0"). Değeri olduğu gibi saklamak için
        // sondaki sıfırlar olmadan, kültürden bağımsız metin olarak tutulur. TEXT olduğu için hassasiyet kaybı da yoktur.
        builder.Properties<decimal>().HaveConversion<InvariantDecimalConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductModel>(e =>
        {
            e.ToTable("product_models");
            e.Property(m => m.Name).IsRequired().HasMaxLength(200);
            e.HasIndex(m => m.Name).IsUnique();
        });

        modelBuilder.Entity<Batch>(e =>
        {
            e.ToTable("batches", t => t.HasCheckConstraint(
                "ck_batches_gtin_14_digits",
                "length(\"Gtin\") = 14 AND \"Gtin\" NOT GLOB '*[^0-9]*'"));

            // string -> SQLite TEXT: baştaki sıfırlar korunur.
            e.Property(b => b.Gtin).IsRequired().HasMaxLength(14).IsFixedLength();
            e.HasIndex(b => b.Gtin).IsUnique();

            e.Property(b => b.BatchNo).IsRequired().HasMaxLength(100);
            e.Property(b => b.ProductionPlace).IsRequired().HasMaxLength(200);

            e.HasOne(b => b.ProductModel)
                .WithMany(m => m.Batches)
                .HasForeignKey(b => b.ProductModelId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BatchVersion>(e =>
        {
            e.ToTable("batch_versions");
            e.HasIndex(v => new { v.BatchId, v.Version }).IsUnique();
            e.HasOne(v => v.Batch)
                .WithMany(b => b.History)
                .HasForeignKey(v => v.BatchId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    internal sealed class InvariantDecimalConverter() : ValueConverter<decimal, string>(
        v => v.ToString("G29", CultureInfo.InvariantCulture),
        v => decimal.Parse(v, NumberStyles.Float, CultureInfo.InvariantCulture));
}
