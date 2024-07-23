using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace SQLiteWithEF
{
    public class DatabaseContext : DbContext
    {
        static string _db = "C:\\deploy\\doordetect\\workingDir\\DB\\db.sqlite";

        public DatabaseContext() :
            base(new SQLiteConnection()
            {
                ConnectionString = new SQLiteConnectionStringBuilder() { DataSource = _db, ForeignKeys = true, Pooling = false }.ConnectionString,
            }, true)
        {
        }

        public void Migrate()
        {
            Debug.WriteLine("Migrate");
            var internalContext = this.GetType().GetProperty("InternalContext", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this);
            var providerName = (string)internalContext.GetType().GetProperty("ProviderName").GetValue(internalContext);
            var configuration = new Configuration()
            {
                TargetDatabase = new DbConnectionInfo(this.Database.Connection.ConnectionString, providerName)
            };
            var migrator = new DbMigrator(configuration);
            migrator.Update();
        }

        public DatabaseContext(string db) : base(new SQLiteConnection()
        {
            ConnectionString = new SQLiteConnectionStringBuilder() { DataSource = db, ForeignKeys = true, Pooling = false }.ConnectionString,
        }, true)
        {
            _db = db;
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();
            base.OnModelCreating(modelBuilder);
        }

        //public DbSet<EmployeeMaster> EmployeeMaster { get; set; }
        public DbSet<SessionMaster> SessionMaster { get; set; }

        public static void InitlocalDB(string dbFile = null)
        {
            _db = dbFile != null ? Path.GetFullPath(dbFile) : _db;
            if (File.Exists(_db)) { return; }

            // this creates a zero-byte file
            Directory.CreateDirectory(Path.GetDirectoryName(_db));
            SQLiteConnection.CreateFile(_db);

            string connectionString = $"Data Source={_db};Version=3;";
            SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString);
            m_dbConnection.Open();

            string sql = "CREATE TABLE SessionMaster (Id TEXT Primary Key, Count INT, Data TEXT, Status INT, CreatedDate DATETIME);";
            SQLiteCommand sqliteCmd = m_dbConnection.CreateCommand();
            sqliteCmd.CommandText = sql;
            sqliteCmd.ExecuteNonQuery();

            sqliteCmd.CommandText = "CREATE INDEX IndexOnSessionMasterCreatedDate ON SessionMaster (CreatedDate);";
            sqliteCmd.ExecuteNonQuery();

            m_dbConnection.Close();
            m_dbConnection.Dispose();
        }
    }
}