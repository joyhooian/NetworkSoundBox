using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable

namespace NetworkSoundBox.Entities
{
    public partial class MySqlDbContext : DbContext
    {
        public MySqlDbContext()
        {
        }

        public MySqlDbContext(DbContextOptions<MySqlDbContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Device> Devices { get; set; }
        public virtual DbSet<DeviceType> DeviceTypes { get; set; }
        public virtual DbSet<Permission> Permissions { get; set; }
        public virtual DbSet<Role> Roles { get; set; }
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<UserDevice> UserDevices { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseMySql("server=8.130.51.198;database=NSB;user=root;password=!RXchtgH*uqeFir@FGzTy_6v", Microsoft.EntityFrameworkCore.ServerVersion.Parse("5.7.36-mysql"));
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            modelBuilder.Entity<Device>(entity =>
            {
                entity.ToTable("Device");

                entity.Property(e => e.Id)
                    .HasColumnType("int(11)")
                    .HasColumnName("id");

                entity.Property(e => e.ActivationKey)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("activationKey");

                entity.Property(e => e.CreateAt)
                    .HasColumnType("datetime")
                    .HasColumnName("createAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.DeviceReferenceId)
                    .HasMaxLength(255)
                    .HasColumnName("deviceReferenceId");

                entity.Property(e => e.IsActived)
                    .HasColumnType("int(11)")
                    .HasColumnName("isActived");

                entity.Property(e => e.LastOnline)
                    .HasColumnType("datetime")
                    .HasColumnName("lastOnline");

                entity.Property(e => e.Name)
                    .HasMaxLength(255)
                    .HasColumnName("name");

                entity.Property(e => e.Sn)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("sn");

                entity.Property(e => e.Type)
                    .HasColumnType("int(255)")
                    .HasColumnName("type");

                entity.Property(e => e.UpdateAt)
                    .HasColumnType("datetime")
                    .ValueGeneratedOnAddOrUpdate()
                    .HasColumnName("updateAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            modelBuilder.Entity<DeviceType>(entity =>
            {
                entity.ToTable("DeviceType");

                entity.Property(e => e.Id)
                    .HasColumnType("int(11)")
                    .HasColumnName("id");

                entity.Property(e => e.CreateAt)
                    .HasColumnType("datetime")
                    .HasColumnName("createAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.DeviceType1)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("deviceType");

                entity.Property(e => e.DeviceTypeId)
                    .HasColumnType("int(11)")
                    .HasColumnName("deviceTypeId");

                entity.Property(e => e.UpdateAt)
                    .HasColumnType("datetime")
                    .ValueGeneratedOnAddOrUpdate()
                    .HasColumnName("updateAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            modelBuilder.Entity<Permission>(entity =>
            {
                entity.ToTable("Permission");

                entity.Property(e => e.Id)
                    .HasColumnType("int(11)")
                    .HasColumnName("id");

                entity.Property(e => e.CreateAt)
                    .HasColumnType("datetime")
                    .HasColumnName("createAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.Permission1)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("permission");

                entity.Property(e => e.PermissionId)
                    .HasColumnType("int(11)")
                    .HasColumnName("permissionId");

                entity.Property(e => e.UpdateAt)
                    .HasColumnType("datetime")
                    .ValueGeneratedOnAddOrUpdate()
                    .HasColumnName("updateAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("Role");

                entity.Property(e => e.Id)
                    .HasColumnType("int(11)")
                    .HasColumnName("id");

                entity.Property(e => e.CreateAt)
                    .HasColumnType("datetime")
                    .HasColumnName("createAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.RoleId)
                    .HasColumnType("int(11)")
                    .HasColumnName("roleId");

                entity.Property(e => e.RoleName)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("roleName");

                entity.Property(e => e.UpdateAt)
                    .HasColumnType("datetime")
                    .ValueGeneratedOnAddOrUpdate()
                    .HasColumnName("updateAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("User");

                entity.Property(e => e.Id)
                    .HasColumnType("int(11)")
                    .HasColumnName("id");

                entity.Property(e => e.AvatarUrl)
                    .HasMaxLength(255)
                    .HasColumnName("avatarUrl");

                entity.Property(e => e.CreateAt)
                    .HasColumnType("datetime")
                    .HasColumnName("createAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.Name)
                    .HasMaxLength(255)
                    .HasColumnName("name");

                entity.Property(e => e.OpenId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("openId");

                entity.Property(e => e.Role)
                    .HasColumnType("int(11)")
                    .HasColumnName("role");

                entity.Property(e => e.UpdateAt)
                    .HasColumnType("datetime")
                    .ValueGeneratedOnAddOrUpdate()
                    .HasColumnName("updateAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UserRefrenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("userRefrenceId");
            });

            modelBuilder.Entity<UserDevice>(entity =>
            {
                entity.ToTable("User_Device");

                entity.Property(e => e.Id)
                    .HasColumnType("int(11)")
                    .HasColumnName("id");

                entity.Property(e => e.CreateAt)
                    .HasColumnType("datetime")
                    .HasColumnName("createAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.DeviceRefrenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("deviceRefrenceId");

                entity.Property(e => e.Permission)
                    .HasColumnType("int(11)")
                    .HasColumnName("permission");

                entity.Property(e => e.UpdateAt)
                    .HasColumnType("datetime")
                    .ValueGeneratedOnAddOrUpdate()
                    .HasColumnName("updateAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UserRefrenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("userRefrenceId");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
