using Microsoft.EntityFrameworkCore;
using ViscosityDeterminator.Models;

namespace ViscosityDeterminator.Data
{
    public class KnowledgeBaseContext : DbContext
    {
        public DbSet<Diagram> Diagrams =>
            Set<Diagram>();

        public DbSet<DiagramGridLine> DiagramGridLines =>
            Set<DiagramGridLine>();

        public DbSet<DiagramViscosityIsoline> DiagramViscosityIsolines =>
            Set<DiagramViscosityIsoline>();

        protected override void OnConfiguring(
            DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite(
                    "Data Source=knowledge_base.db");
            }
        }
    }
}