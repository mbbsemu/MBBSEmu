namespace NetSQLite
{
    public class SQLiteInitializer
    {
        public static void Initialize()
        {
            //SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
            SQLitePCL.Batteries.Init();
        }
    }
}