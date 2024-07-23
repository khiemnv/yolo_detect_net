using System.Data.Entity.Migrations;
using System.Data.SQLite.EF6.Migrations;

namespace SQLiteWithEF
{
    public class Configuration : DbMigrationsConfiguration<DatabaseContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = true;
            SetSqlGenerator("System.Data.SQLite", new SQLiteMigrationSqlGenerator());// the Golden Key
        }
    }
}
