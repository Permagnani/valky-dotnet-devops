using Microsoft.EntityFrameworkCore;
using Valky.Api.Models;

namespace Valky.Api.Data;

/// <summary>
/// Mapeamento EF Core. As tabelas são criadas pelo scripts/script-bd.sql (executado na Release).
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Alerta> Alertas => Set<Alerta>();
    public DbSet<AtualizacaoStatus> AtualizacoesStatus => Set<AtualizacaoStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Alerta>(e =>
        {
            e.ToTable("Alertas");
            e.HasKey(a => a.Id);
            e.Property(a => a.CodigoBo).HasMaxLength(30).IsRequired();
            e.Property(a => a.Latitude).HasPrecision(9, 6);
            e.Property(a => a.Longitude).HasPrecision(9, 6);
            e.Property(a => a.Status).HasMaxLength(20).IsRequired();
        });

        modelBuilder.Entity<AtualizacaoStatus>(e =>
        {
            e.ToTable("AtualizacoesStatus");
            e.HasKey(s => s.Id);
            e.Property(s => s.Status).HasMaxLength(20).IsRequired();
            e.Property(s => s.Observacao).HasMaxLength(500);
            e.Property(s => s.Responsavel).HasMaxLength(100).IsRequired();
            e.HasOne(s => s.Alerta)
                .WithMany(a => a.Atualizacoes)
                .HasForeignKey(s => s.AlertaId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
