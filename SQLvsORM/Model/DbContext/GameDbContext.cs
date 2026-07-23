using Microsoft.EntityFrameworkCore;
using SQLvsORM.Model.DbEntities;

namespace SQLvsORM.Model
{
    public class GameDbContext : DbContext
    {
        public DbSet<Game> Games { get; set; }
        public DbSet<AttributeText> AttributeTexts { get; set; }
        public DbSet<AttributeNumber> AttributeNumbers { get; set; }
        public DbSet<AttributeBoolean> AttributeBooleans { get; set; }
        public DbSet<AttributeDate> AttributeDates { get; set; }

        public GameDbContext(DbContextOptions<GameDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql("Host=localhost;Database=VGDatabase2;Username=postgres;Password=PikPok666");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // AttributeText - составной ключ
            modelBuilder.Entity<AttributeText>()
                .HasKey(a => new { a.game_id, a.attribute_name });

            // AttributeNumber - составной ключ
            modelBuilder.Entity<AttributeNumber>()
                .HasKey(a => new { a.game_id, a.attribute_name });

            // AttributeBoolean - составной ключ
            modelBuilder.Entity<AttributeBoolean>()
                .HasKey(a => new { a.game_id, a.attribute_name });

            // AttributeDate - составной ключ
            modelBuilder.Entity<AttributeDate>()
                .HasKey(a => new { a.game_id, a.attribute_name });

            // Связи
            modelBuilder.Entity<AttributeText>()
                .HasOne(a => a.Game)
                .WithMany(g => g.AttributeTexts)
                .HasForeignKey(a => a.game_id);

            modelBuilder.Entity<AttributeNumber>()
                .HasOne(a => a.Game)
                .WithMany(g => g.AttributeNumbers)
                .HasForeignKey(a => a.game_id);

            modelBuilder.Entity<AttributeBoolean>()
                .HasOne(a => a.Game)
                .WithMany(g => g.AttributeBooleans)
                .HasForeignKey(a => a.game_id);

            modelBuilder.Entity<AttributeDate>()
                .HasOne(a => a.Game)
                .WithMany(g => g.AttributeDates)
                .HasForeignKey(a => a.game_id);
        }
    }
}
