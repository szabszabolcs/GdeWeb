using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GdeWebLA09DB.Entities;
using Microsoft.EntityFrameworkCore;

//# Powershell
//dotnet tool install --global dotnet-ef

//# STARTUP projekt (GdeWebLA09API) – IDE KELL A Design csomag!
//dotnet add GdeWebLA09API package Microsoft.EntityFrameworkCore.Design

//# DbContext projekt (GdeWebLA09DB) – EF és Sqlite provider
//dotnet add GdeWebLA09DB package Microsoft.EntityFrameworkCore
//dotnet add GdeWebLA09DB package Microsoft.EntityFrameworkCore.Sqlite
//dotnet add GdeWebLA09DB package Microsoft.EntityFrameworkCore.Tools

// Migráció létrehozása és alkalmazása SQLite adatbázisra
//dotnet ef migrations add InitialSqlite -p GdeWebLA09DB -s GdeWebLA09API
//dotnet ef database update -p GdeWebLA09DB -s GdeWebLA09API

//Magyarázat:
//-p GdeWebLA09DB → a projekt, ahol a GdeDbContext van
//-s GdeWebLA09API → a startup projekt, ahol a Program.cs van (ez tölti be a konfigurációt és indítja az EF-et)

// Remove migration
//dotnet ef migrations remove -p GdeWebLA09DB -s GdeWebLA09API

namespace GdeWebLA09DB
{
    public class GdeDbContext : DbContext
    {
        public DbSet<User> T_USER => Set<User>();
        public DbSet<Role> T_ROLE => Set<Role>();
        public DbSet<UserRole> K_USER_ROLES => Set<UserRole>();
        public DbSet<AuthToken> T_AUTHENTICATION => Set<AuthToken>();

        public GdeDbContext(DbContextOptions<GdeDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder mb)
        {
            // T_USER
            mb.Entity<User>(e =>
            {
                e.ToTable("T_USER");
                e.HasKey(x => x.USERID);
                e.Property(x => x.USERID).ValueGeneratedOnAdd();
                e.Property(x => x.GUID).HasConversion(v => v.ToString(), s => Guid.Parse(s));
                e.Property(x => x.PASSWORD).HasMaxLength(200).IsRequired();
                e.Property(x => x.FIRSTNAME).HasMaxLength(50).IsRequired();
                e.Property(x => x.LASTNAME).HasMaxLength(50).IsRequired();
                e.Property(x => x.EMAIL).HasMaxLength(100);
                // SQLite-nál default értéket érdemes app-oldalon beállítani:
                e.Property(x => x.ACTIVE).HasDefaultValue(false);
                e.Property(x => x.MODIFICATIONDATE);
            });

            // T_ROLE
            mb.Entity<Role>(e =>
            {
                e.ToTable("T_ROLE");
                e.HasKey(x => x.ROLEID);
                e.Property(x => x.ROLEID).ValueGeneratedOnAdd();
                e.Property(x => x.ROLENAME).HasMaxLength(100).IsRequired();
                e.Property(x => x.VALID).HasDefaultValue(false);
                e.Property(x => x.MODIFIER).IsRequired();
            });

            // K_USER_ROLES
            mb.Entity<UserRole>(e =>
            {
                e.ToTable("K_USER_ROLES");
                e.HasKey(x => x.USERROLES);
                e.Property(x => x.USERROLES).ValueGeneratedOnAdd();

                e.HasOne(ur => ur.User)
                 .WithMany(u => u.UserRoles)
                 .HasForeignKey(ur => ur.USERID)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(ur => ur.Role)
                 .WithMany(r => r.UserRoles)
                 .HasForeignKey(ur => ur.ROLEID)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // T_AUTHENTICATION
            mb.Entity<AuthToken>(e =>
            {
                e.ToTable("T_AUTHENTICATION");
                e.HasKey(x => x.TOKENID);
                e.Property(x => x.TOKENID).ValueGeneratedOnAdd();

                e.HasOne(t => t.User)
                 .WithMany(u => u.Tokens)
                 .HasForeignKey(t => t.USERID)
                 .OnDelete(DeleteBehavior.Cascade);
            });


            var seedTime = new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc);


            // Opcionális seed (a csatolt MSSQL script mintáival szinkronban)
            // Figyelem: MSSQL-ben NEWID()/GETDATE() volt – itt konkrét értékeket adunk.
            mb.Entity<Role>().HasData(
                new Role { ROLEID = 1, ROLENAME = "Admin", VALID = true, MODIFIER = 1, MODIFICATIONDATE = seedTime },
                new Role { ROLEID = 2, ROLENAME = "User", VALID = true, MODIFIER = 1, MODIFICATIONDATE = seedTime }
            );

            // Ha szeretnéd, a minta felhasználót is feltölthetjük ugyanazzal a hash-sel:
            // A csatolt script hash-e (SHA-512) a PASSWORD oszlopban (első user sorban) :contentReference[oaicite:1]{index=1}
            mb.Entity<User>().HasData(
                new User
                {
                    USERID = 1,
                    GUID = Guid.Parse("0d2f1a91-ba24-4203-9b89-2d7f19ac9a7a"), // FIX GUID!
                    PASSWORD = "3774b62586f7e44b343a0f2cc8e0e336c0d03549e09162cb73dfcb9a72c15a95c40f5a5b1608a08a28a79f46450ad3391f0a9342a19fdebb56511b556ba6aabb", // x.1234ABCD
                    FIRSTNAME = "Dávid",
                    LASTNAME = "Jakab",
                    EMAIL = "jakab.d@gmail.com",
                    ACTIVE = true,
                    MODIFICATIONDATE = seedTime
                }
            );

            mb.Entity<UserRole>().HasData(
                new UserRole
                {
                    USERROLES = 1,
                    USERID = 1,
                    ROLEID = 1,
                    CREATOR = 1,
                    CREATINGDATE = seedTime
                }
            );
        }
    }
}
