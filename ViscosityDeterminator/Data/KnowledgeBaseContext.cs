using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using ViscosityDeterminator.Models;

namespace ViscosityDeterminator.Data
{
    public class KnowledgeBaseContext : DbContext
    {
        public DbSet<Diagram> Diagrams => Set<Diagram>();

        public DbSet<DiagramGridLine> DiagramGridLines { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=knowledge_base.db");
        }
    }
}
