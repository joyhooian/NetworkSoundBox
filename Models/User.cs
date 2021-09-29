﻿using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Models
{
    [Table("users")]
    public class User
    {
        [Key]
        [Column("id")]
        public int UserId { get; set; }
        public string Name { get; set; }
        [Column("tel_num")]
        public string TelNum { get; set; }
    }
}