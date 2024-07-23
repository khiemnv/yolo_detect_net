

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;

namespace SQLiteWithEF
{
    [Table("EmployeeMaster")]
    public class EmployeeMaster
    {
        [Column("ID")]
        [Key]
        public int ID { get; set; }

        [Column("EmpName")]
        public string EmpName { get; set; }

        [Column("Salary")]
        public double Salary { get; set; }

        [Column("Designation")]
        public string Designation { get; set; }
    }
}