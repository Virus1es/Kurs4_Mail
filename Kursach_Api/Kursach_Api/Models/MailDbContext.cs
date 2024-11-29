using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Kursach_Api.Models;

public partial class MailDbContext : DbContext
{
    public MailDbContext()
    {
    }

    public MailDbContext(DbContextOptions<MailDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<MailBoxesKey> MailBoxesKeys { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=DESKTOP-N88PL21;Database=Mail_DB;Trusted_Connection=True;MultipleActiveResultSets=False;Encrypt=False;TrustServerCertificate=False;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MailBoxesKey>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("MailBoxes_PK");

            entity.Property(e => e.EncriptOpenKey).HasMaxLength(255);
            entity.Property(e => e.SignOpenKey).HasMaxLength(255);
            entity.Property(e => e.UserFrom).HasMaxLength(255);
            entity.Property(e => e.UserTo).HasMaxLength(255);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
