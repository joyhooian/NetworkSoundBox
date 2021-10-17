using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NetworkSoundBox.Models;

namespace NetworkSoundBox
{
    public class MySqlDbContext : DbContext
    {
        protected string connectionString = "server=110.40.133.195;userid=root;password=Yjhyz_951103;database=soundbox;";
        public DbSet<User> User { get; set; }
        public DbSet<Device> Device { get; set; }
        public MySqlDbContext(DbContextOptions<MySqlDbContext> options) : base(options)
        {

        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseMySql(connectionString, MySqlServerVersion.LatestSupportedServerVersion);
        }
    }
}
