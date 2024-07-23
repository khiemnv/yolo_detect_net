using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SQLiteWithEF
{
    [Table("SessionMaster")]
    public class SessionMaster
    {
        [MaxLength(32)]
        [Column("ID")]
        [Key]
        public string Id { get; set; }

        [Column("Count")]
        public int Count { get; set; }

        [Column("Data")]
        public string Data { get; set; }

        [Column("Status")]
        public int Status { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Index]
        [Column("CreatedDate")]
        public DateTime CreatedDate { get; set; }
    }

    public enum SessionMasterStatus
    {
        NG,
        OK,
    }

    [Table("ModelConfig")]
    public class ModelConfig
    {
        [MaxLength(32)]
        [Column("ID")]
        [Key]
        public string Id { get; set; }

        [Column("Data")]
        public string Data { get; set; }

        [Column("CreatedDate")]
        public DateTime CreatedDate { get; set; }
    }
}