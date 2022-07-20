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

        public virtual DbSet<Audio> Audios { get; set; }
        public virtual DbSet<Cloud> Clouds { get; set; }
        public virtual DbSet<CronTask> CronTasks { get; set; }
        public virtual DbSet<DelayTask> DelayTasks { get; set; }
        public virtual DbSet<Device> Devices { get; set; }
        public virtual DbSet<DeviceAudio> DeviceAudios { get; set; }
        public virtual DbSet<DeviceGroup> DeviceGroups { get; set; }
        public virtual DbSet<DeviceGroupDevice> DeviceGroupDevices { get; set; }
        public virtual DbSet<DeviceGroupUser> DeviceGroupUsers { get; set; }
        public virtual DbSet<DeviceTask> DeviceTasks { get; set; }
        public virtual DbSet<DeviceType> DeviceTypes { get; set; }
        public virtual DbSet<Permission> Permissions { get; set; }
        public virtual DbSet<Playlist> Playlists { get; set; }
        public virtual DbSet<PlaylistAudio> PlaylistAudios { get; set; }
        public virtual DbSet<Role> Roles { get; set; }
        public virtual DbSet<TaskType> TaskTypes { get; set; }
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<UserDevice> UserDevices { get; set; }
        public virtual DbSet<UserType> UserTypes { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseMySql("server=8.130.51.198;userid=root;password=!RXchtgH*uqeFir@FGzTy_6v;database=NSB", Microsoft.EntityFrameworkCore.ServerVersion.Parse("5.7.36-mysql"));
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            modelBuilder.Entity<Audio>(entity =>
            {
                entity.HasKey(e => e.AudioKey)
                    .HasName("PRIMARY");

                entity.ToTable("Audio");

                entity.Property(e => e.AudioKey)
                    .HasColumnType("int(11)")
                    .HasColumnName("audioKey");

                entity.Property(e => e.AudioName)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("audioName");

                entity.Property(e => e.AudioPath)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("audioPath");

                entity.Property(e => e.AudioReferenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("audioReferenceId");

                entity.Property(e => e.CloudReferenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("cloudReferenceId");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasColumnName("createdAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.Duration)
                    .HasColumnType("time")
                    .HasColumnName("duration");

                entity.Property(e => e.IsCached)
                    .IsRequired()
                    .HasMaxLength(1)
                    .HasColumnName("isCached")
                    .IsFixedLength(true);

                entity.Property(e => e.Size)
                    .HasColumnType("int(11)")
                    .HasColumnName("size");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime")
                    .ValueGeneratedOnAddOrUpdate()
                    .HasColumnName("updatedAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            modelBuilder.Entity<Cloud>(entity =>
            {
                entity.HasKey(e => e.CloudKey)
                    .HasName("PRIMARY");

                entity.ToTable("Cloud");

                entity.Property(e => e.CloudKey)
                    .HasColumnType("int(11)")
                    .HasColumnName("cloudKey");

                entity.Property(e => e.Capacity)
                    .HasColumnType("int(11)")
                    .HasColumnName("capacity");

                entity.Property(e => e.CloudReferenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("cloudReferenceId");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasColumnName("createdAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime")
                    .ValueGeneratedOnAddOrUpdate()
                    .HasColumnName("updatedAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UserReferenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("userReferenceId");
            });

            modelBuilder.Entity<CronTask>(entity =>
            {
                entity.HasKey(e => e.CronKey)
                    .HasName("PRIMARY");

                entity.ToTable("CronTask");

                entity.Property(e => e.CronKey)
                    .HasColumnType("int(11)")
                    .HasColumnName("cronKey");

                entity.Property(e => e.AudioReferenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("audioReferenceId");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasColumnName("createdAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.CronReferenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("cronReferenceId");

                entity.Property(e => e.EndTime)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("endTime");

                entity.Property(e => e.Relay)
                    .HasColumnType("int(11)")
                    .HasColumnName("relay");

                entity.Property(e => e.StartTime)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("startTime");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime")
                    .ValueGeneratedOnAddOrUpdate()
                    .HasColumnName("updatedAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UserReferenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("userReferenceId");

                entity.Property(e => e.Volume)
                    .HasColumnType("int(11)")
                    .HasColumnName("volume");

                entity.Property(e => e.Weekdays)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("weekdays");
            });

            modelBuilder.Entity<DelayTask>(entity =>
            {
                entity.HasKey(e => e.DelayKey)
                    .HasName("PRIMARY");

                entity.ToTable("DelayTask");

                entity.Property(e => e.DelayKey)
                    .HasColumnType("int(11)")
                    .HasColumnName("delayKey");

                entity.Property(e => e.AudioReferenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("audioReferenceId");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasColumnName("createdAt");

                entity.Property(e => e.DelayReferenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("delayReferenceId");

                entity.Property(e => e.DelayTime)
                    .HasColumnType("int(11)")
                    .HasColumnName("delayTime");

                entity.Property(e => e.Relay)
                    .HasColumnType("int(11)")
                    .HasColumnName("relay");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime")
                    .HasColumnName("updatedAt");

                entity.Property(e => e.UserReferenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("userReferenceId");

                entity.Property(e => e.Volume)
                    .HasColumnType("int(11)")
                    .HasColumnName("volume");
            });

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

                entity.Property(e => e.ActiveCount)
                    .HasColumnType("int(11)")
                    .HasColumnName("activeCount");

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

                entity.Property(e => e.PlaylistReferenceId)
                    .HasMaxLength(255)
                    .HasColumnName("playlistReferenceId");

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

            modelBuilder.Entity<DeviceAudio>(entity =>
            {
                entity.HasKey(e => e.DeviceAudioKey)
                    .HasName("PRIMARY");

                entity.ToTable("DeviceAudio");

                entity.Property(e => e.DeviceAudioKey)
                    .HasColumnType("int(11)")
                    .HasColumnName("deviceAudioKey");

                entity.Property(e => e.AudioReferenceId)
                    .HasMaxLength(255)
                    .HasColumnName("audioReferenceId");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasColumnName("createdAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.DeviceReferenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("deviceReferenceId");

                entity.Property(e => e.Index)
                    .HasColumnType("int(11)")
                    .HasColumnName("index");

                entity.Property(e => e.IsSynced)
                    .HasMaxLength(1)
                    .HasColumnName("isSynced")
                    .IsFixedLength(true);

                entity.Property(e => e.SyncCpltFlag)
                    .HasMaxLength(255)
                    .HasColumnName("syncCpltFlag");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime")
                    .ValueGeneratedOnAddOrUpdate()
                    .HasColumnName("updatedAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            modelBuilder.Entity<DeviceGroup>(entity =>
            {
                entity.HasKey(e => e.Key)
                    .HasName("PRIMARY");

                entity.ToTable("DeviceGroup");

                entity.Property(e => e.Key)
                    .HasColumnType("int(11)")
                    .HasColumnName("key");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasColumnName("createdAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.CronTaskListReferenceId)
                    .HasMaxLength(255)
                    .HasColumnName("cronTaskListReferenceId");

                entity.Property(e => e.DelayTaskReferenceId)
                    .HasMaxLength(255)
                    .HasColumnName("delayTaskReferenceId");

                entity.Property(e => e.DeviceGroupReferenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("deviceGroupReferenceId");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("name");

                entity.Property(e => e.PlaylistReferenceId)
                    .HasMaxLength(255)
                    .HasColumnName("playlistReferenceId");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime")
                    .ValueGeneratedOnAddOrUpdate()
                    .HasColumnName("updatedAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UsingStatus)
                    .HasColumnType("int(11)")
                    .HasColumnName("usingStatus");
            });

            modelBuilder.Entity<DeviceGroupDevice>(entity =>
            {
                entity.HasKey(e => e.Key)
                    .HasName("PRIMARY");

                entity.ToTable("DeviceGroup_Device");

                entity.Property(e => e.Key)
                    .HasColumnType("int(11)")
                    .HasColumnName("key");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasColumnName("createdAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.DeviceGroupReferenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("deviceGroupReferenceId");

                entity.Property(e => e.DeviceReferenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("deviceReferenceId");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime")
                    .ValueGeneratedOnAddOrUpdate()
                    .HasColumnName("updatedAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            modelBuilder.Entity<DeviceGroupUser>(entity =>
            {
                entity.HasKey(e => e.Key)
                    .HasName("PRIMARY");

                entity.ToTable("DeviceGroup_User");

                entity.Property(e => e.Key)
                    .HasColumnType("int(11)")
                    .HasColumnName("key");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasColumnName("createdAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.DeviceGroupReferenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("deviceGroupReferenceId");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime")
                    .ValueGeneratedOnAddOrUpdate()
                    .HasColumnName("updatedAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UserReferenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("userReferenceId");
            });

            modelBuilder.Entity<DeviceTask>(entity =>
            {
                entity.HasKey(e => e.DeviceTaskKey)
                    .HasName("PRIMARY");

                entity.ToTable("DeviceTask");

                entity.Property(e => e.DeviceTaskKey)
                    .HasColumnType("int(11)")
                    .HasColumnName("deviceTaskKey");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasColumnName("createdAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.DeviceReferenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("deviceReferenceId");

                entity.Property(e => e.TaskReferenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("taskReferenceId");

                entity.Property(e => e.TaskTypeKey)
                    .HasColumnType("int(11)")
                    .HasColumnName("taskTypeKey");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime")
                    .ValueGeneratedOnAddOrUpdate()
                    .HasColumnName("updatedAt")
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

            modelBuilder.Entity<Playlist>(entity =>
            {
                entity.HasKey(e => e.PlaylistKey)
                    .HasName("PRIMARY");

                entity.ToTable("Playlist");

                entity.Property(e => e.PlaylistKey)
                    .HasColumnType("int(11)")
                    .HasColumnName("playlistKey");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasColumnName("createdAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.PlaylistName)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("playlistName");

                entity.Property(e => e.PlaylistReferenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("playlistReferenceId");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime")
                    .ValueGeneratedOnAddOrUpdate()
                    .HasColumnName("updatedAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UserReferenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("userReferenceId");
            });

            modelBuilder.Entity<PlaylistAudio>(entity =>
            {
                entity.HasKey(e => e.PlaylistAudioKey)
                    .HasName("PRIMARY");

                entity.ToTable("PlaylistAudio");

                entity.Property(e => e.PlaylistAudioKey)
                    .HasColumnType("int(11)")
                    .HasColumnName("playlistAudioKey");

                entity.Property(e => e.AudioReferenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("audioReferenceId");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasColumnName("createdAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.PlaylistReferenceId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("playlistReferenceId");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime")
                    .ValueGeneratedOnAddOrUpdate()
                    .HasColumnName("updatedAt")
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

            modelBuilder.Entity<TaskType>(entity =>
            {
                entity.HasKey(e => e.TaskTypeKey)
                    .HasName("PRIMARY");

                entity.ToTable("TaskType");

                entity.Property(e => e.TaskTypeKey)
                    .HasColumnType("int(11)")
                    .HasColumnName("taskTypeKey");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasColumnName("createdAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.TaskTypeShortName)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("taskTypeShortName");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime")
                    .ValueGeneratedOnAddOrUpdate()
                    .HasColumnName("updatedAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("User");

                entity.HasIndex(e => e.UserTypeKey, "User_UserType_userTypeKey_fk");

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

                entity.Property(e => e.UserTypeKey)
                    .HasColumnType("int(11)")
                    .HasColumnName("userTypeKey")
                    .HasDefaultValueSql("'4'");

                entity.HasOne(d => d.UserTypeKeyNavigation)
                    .WithMany(p => p.Users)
                    .HasForeignKey(d => d.UserTypeKey)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("User_UserType_userTypeKey_fk");
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

            modelBuilder.Entity<UserType>(entity =>
            {
                entity.HasKey(e => e.UserTypeKey)
                    .HasName("PRIMARY");

                entity.ToTable("UserType");

                entity.Property(e => e.UserTypeKey)
                    .HasColumnType("int(11)")
                    .HasColumnName("userTypeKey");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasColumnName("createdAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.Type)
                    .IsRequired()
                    .HasMaxLength(16)
                    .HasColumnName("type");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime")
                    .ValueGeneratedOnAddOrUpdate()
                    .HasColumnName("updatedAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
