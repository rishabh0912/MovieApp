using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MovieService.Domain.Entities;

namespace MovieService.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options): base(options)
        {
        }

        public DbSet<Movie>Movies { get; set; }
        public DbSet<Genre> Genres { get; set; } 
        public DbSet<MovieGenre> MovieGenres { get; set; }  
        public DbSet<Cast> Casts { get; set; } 
        public DbSet<MovieCast> MovieCasts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //Composite PK for MovieGenre join table
            modelBuilder.Entity<MovieGenre>()
                .HasKey(mg => new {mg.MovieId, mg.GenreId});
            
            modelBuilder.Entity<MovieGenre>()
                .HasOne(mg => mg.Movie)
                .WithMany(m => m.MovieGenres)
                .HasForeignKey(mg => mg.MovieId);
            
            modelBuilder.Entity<MovieGenre>()
                .HasOne(mg => mg.Genre)
                .WithMany(g => g.MovieGenres)
                .HasForeignKey(mg => mg.GenreId);
            
            //Composite PK for MovieCast join table
            modelBuilder.Entity<MovieCast>()
                .HasKey(mc => new {mc.MovieId, mc.CastId});
            
            modelBuilder.Entity<MovieCast>()
                .HasOne(mc => mc.Movie)
                .WithMany(m => m.MovieCasts)
                .HasForeignKey(mc => mc.MovieId);
            
            modelBuilder.Entity<MovieCast>()
                .HasOne(mc => mc.Cast)
                .WithMany(c => c.MovieCasts)
                .HasForeignKey(mc => mc.CastId);
        }
    }
}