using Microsoft.EntityFrameworkCore;
using Valky.Api.Models;

namespace Valky.Api.Data;

/// <summary>
/// Contexto de banco de dados do Entity Framework Core para a plataforma Valky.
/// Mapeado com identificadores em MAIÚSCULAS (padrão nativo do Oracle Database 19c/21c/23ai)
/// e conversão explícita de booleanos para NUMBER(1) (0/1), evitando erros ORA-00904 em servidores Oracle 19c.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<ContatoConfianca> ContatosConfianca => Set<ContatoConfianca>();
    public DbSet<Alerta> Alertas => Set<Alerta>();
    public DbSet<AtualizacaoStatus> AtualizacoesStatus => Set<AtualizacaoStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("TB_VALKY_USUARIOS");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasColumnName("ID").ValueGeneratedOnAdd();

            e.Property(u => u.Nome).HasColumnName("NOME").HasMaxLength(120).IsRequired();
            e.Property(u => u.Email).HasColumnName("EMAIL").HasMaxLength(150).IsRequired();
            e.Property(u => u.Cpf).HasColumnName("CPF").HasMaxLength(11).IsRequired();
            e.Property(u => u.Telefone).HasColumnName("TELEFONE").HasMaxLength(20).IsRequired();
            e.Property(u => u.SenhaHash).HasColumnName("SENHA_HASH").HasMaxLength(255).IsRequired();
            e.Property(u => u.Perfil).HasColumnName("PERFIL").HasMaxLength(30).IsRequired();
            e.Property(u => u.CodigoBo).HasColumnName("CODIGO_BO").HasMaxLength(30);
            e.Property(u => u.PinDisfarce).HasColumnName("PIN_DISFARCE").HasMaxLength(10);

            // Conversão explícita bool <-> int (0/1) para compatibilidade total com Oracle 19c (evita ORA-00904 TRUE/FALSE)
            e.Property(u => u.PossuiMedidaProtetiva).HasColumnName("POSSUI_MEDIDA_PROTETIVA").HasConversion<int>().IsRequired();
            e.Property(u => u.PermissaoMicrofone).HasColumnName("PERMISSAO_MICROFONE").HasConversion<int>().IsRequired();
            e.Property(u => u.PermissaoLocalizacao).HasColumnName("PERMISSAO_LOCALIZACAO").HasConversion<int>().IsRequired();
            e.Property(u => u.Ativo).HasColumnName("ATIVO").HasConversion<int>().IsRequired();

            e.Property(u => u.CriadoEm).HasColumnName("CRIADO_EM").IsRequired();
            e.Property(u => u.AtualizadoEm).HasColumnName("ATUALIZADO_EM").IsRequired();

            e.HasIndex(u => u.Email)
                .IsUnique()
                .HasDatabaseName("UK_VALKY_USUARIOS_EMAIL");

            e.HasIndex(u => u.Cpf)
                .IsUnique()
                .HasDatabaseName("UK_VALKY_USUARIOS_CPF");

            e.HasIndex(u => u.CodigoBo)
                .HasDatabaseName("IX_VALKY_USUARIOS_BO");
        });

        modelBuilder.Entity<ContatoConfianca>(e =>
        {
            e.ToTable("TB_VALKY_CONTATOS_CONFIANCA");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();

            e.Property(c => c.UsuarioId).HasColumnName("USUARIO_ID").IsRequired();
            e.Property(c => c.Nome).HasColumnName("NOME").HasMaxLength(120).IsRequired();
            e.Property(c => c.Telefone).HasColumnName("TELEFONE").HasMaxLength(20).IsRequired();
            e.Property(c => c.Email).HasColumnName("EMAIL").HasMaxLength(150);
            e.Property(c => c.Parentesco).HasColumnName("PARENTESCO").HasMaxLength(50).IsRequired();
            e.Property(c => c.NotificarPorSms).HasColumnName("NOTIFICAR_POR_SMS").HasConversion<int>().IsRequired();
            e.Property(c => c.NotificarPorApp).HasColumnName("NOTIFICAR_POR_APP").HasConversion<int>().IsRequired();
            e.Property(c => c.CriadoEm).HasColumnName("CRIADO_EM").IsRequired();

            e.HasIndex(c => new { c.UsuarioId, c.Telefone })
                .IsUnique()
                .HasDatabaseName("UK_VALKY_CONTATOS_USU_TEL");

            e.HasOne(c => c.Usuario)
                .WithMany(u => u.ContatosConfianca)
                .HasForeignKey(c => c.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Alerta>(e =>
        {
            e.ToTable("TB_VALKY_ALERTAS");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            e.Property(a => a.CodigoBo).HasColumnName("CODIGO_BO").HasMaxLength(30).IsRequired();
            e.Property(a => a.Latitude).HasColumnName("LATITUDE").HasPrecision(9, 6);
            e.Property(a => a.Longitude).HasColumnName("LONGITUDE").HasPrecision(9, 6);
            e.Property(a => a.Status).HasColumnName("STATUS").HasMaxLength(20).IsRequired();
            e.Property(a => a.CriadoEm).HasColumnName("CRIADO_EM").IsRequired();
            e.Property(a => a.AtualizadoEm).HasColumnName("ATUALIZADO_EM").IsRequired();
        });

        modelBuilder.Entity<AtualizacaoStatus>(e =>
        {
            e.ToTable("TB_VALKY_ATUALIZACOES_STATUS");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            e.Property(s => s.AlertaId).HasColumnName("ALERTA_ID").IsRequired();
            e.Property(s => s.Status).HasColumnName("STATUS").HasMaxLength(20).IsRequired();
            e.Property(s => s.Observacao).HasColumnName("OBSERVACAO").HasMaxLength(500);
            e.Property(s => s.Responsavel).HasColumnName("RESPONSAVEL").HasMaxLength(100).IsRequired();
            e.Property(s => s.CriadoEm).HasColumnName("CRIADO_EM").IsRequired();

            e.HasOne(s => s.Alerta)
                .WithMany(a => a.Atualizacoes)
                .HasForeignKey(s => s.AlertaId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
