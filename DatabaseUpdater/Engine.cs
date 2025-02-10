using System;
using System.Data.SqlClient;
using System.Data;
using System.IO;
using Microsoft.Win32;
using System.Windows.Forms;
using System.Diagnostics.Contracts;

namespace DatabaseUpdater
{
    internal class Engine
    {
        private MainForm _Form;

        public Engine(MainForm TheForm) 
        {
            _Form = TheForm;
        }

        public bool Run()
        {

            // Get the database path
            string databasePath = GetDatabasePath();

            // Check if the DrafixUpdate.mdf file exists
            string databaseFilePath = Path.Combine(databasePath, "DrafixUpdate.mdf");
            string logFilePath = Path.Combine(databasePath, "DrafixUpdate_log.ldf");


            if (!File.Exists(databaseFilePath))
            {
                _Form.UpdateStatus("Cannot find database file", 100, 100, false);
                return false; 
            }

            // Check if the database is attached in SQL Server
            if (!IsDatabaseAttached("DrafixUpdate"))
            {
                _Form.UpdateStatus("Database 'DrafixUpdate' is not attached to SQL Server. Attempting to attach...", 100, 100, true);


                // Attempt to attach the database
                bool attachSuccess = AttachDatabase("DrafixUpdate", databaseFilePath, logFilePath);

                if (!attachSuccess)
                {
                    _Form.UpdateStatus("Failed to attach the database.", 100, 100, false);
                    return false;
                }
            }

            Console.WriteLine("Database is attached and ready for use.");

            // Execute the stored procedure
            ExecuteStoredProcedure("DrafixUpdate", "dbo.USP_Upgrade_26_0");

            //Close Connections
            CloseDatabaseConnections("DrafixUpdate");

            //Detach DrafixUpdate Database
            bool detachSuccess = DetachDatabase("DrafixUpdate");

            if (detachSuccess)
            {
                _Form.UpdateStatus("Database 'DrafixUpdate' detached successfully.", 100, 100, true);

            }
            else
            {
                _Form.UpdateStatus("Failed to detach the database 'DrafixUpdate'.", 100, 100, false);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the location of the database files.
        /// </summary>
        /// <returns>The location of the DrafixUpdate.mdf files.</returns>
        public static string GetDatabasePath()
        {
            string filePath = string.Empty;
            RegistryKey k1 = Registry.LocalMachine.OpenSubKey("Software");

            if (k1 != null)
            {
                RegistryKey k2 = k1.OpenSubKey("Drafix");

                if (k2 != null)
                {
                    RegistryKey k3 = k2.OpenSubKey("DrafixUtil");

                    if (k3 != null)
                    {
                        RegistryKey k4 = k3.OpenSubKey("26.0");

                        if (k4 != null)
                        {
                            RegistryKey k5 = k4.OpenSubKey("Settings");

                            if (k5 == null)
                            {
                                filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "PRO Landscape\\Database");
                            }
                            else
                            {
                                filePath = Convert.ToString(k5.GetValue("DatabasePath", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "PRO Landscape\\Database")));
                            }
                        }
                        else
                        {
                            filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "PRO Landscape\\Database");
                        }
                    }
                    else
                    {
                        filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "PRO Landscape\\Database");
                    }
                }
                else
                {
                    filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "PRO Landscape\\Database");
                }
            }
            else
            {
                filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "PRO Landscape\\Database");
            }

            return filePath;
        }
        /// <summary>
        /// Checks if a database is attached to the SQL Server.
        /// </summary>
        /// <param name="databaseName">The name of the database to check.</param>
        /// <returns>True if the database is attached; otherwise, false.</returns>
        private bool IsDatabaseAttached(string databaseName)
        {
            bool isAttached = false;
            string connectionString = @"Server=localhost\SQLEXPRESS;Database=master;Trusted_Connection=True;";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Open();

                    // Query to check if the database exists
                    string checkDatabaseQuery = $"SELECT COUNT(*) FROM sys.databases WHERE name = @databaseName";
                    using (SqlCommand command = new SqlCommand(checkDatabaseQuery, connection))
                    {
                        command.Parameters.AddWithValue("@databaseName", databaseName);
                        isAttached = (int)command.ExecuteScalar() > 0;
                    }
                }
                catch (Exception ex)
                {
                    _Form.UpdateStatus("Error checking database attachment: " + ex.Message, 100, 100, false);
                }
            }

            return isAttached;
        }

        /// <summary>
        /// Attaches a database to SQL Server.
        /// </summary>
        /// <param name="databaseName">The name of the database to attach.</param>
        /// <param name="dataFilePath">The full path to the database MDF file.</param>
        /// <param name="logFilePath">The full path to the database LDF (log) file.</param>
        /// <returns>True if the database was successfully attached; otherwise, false.</returns>
        private bool AttachDatabase(string databaseName, string dataFilePath, string logFilePath)
        {
            string connectionString = @"Server=localhost\SQLEXPRESS;Database=master;Trusted_Connection=True;";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Open();

                    // SQL command to attach the database
                    string attachQuery = $@"
                        CREATE DATABASE [{databaseName}]
                        ON (FILENAME = '{dataFilePath}'),
                           (FILENAME = '{logFilePath}')
                        FOR ATTACH;";

                    using (SqlCommand command = new SqlCommand(attachQuery, connection))
                    {
                        command.ExecuteNonQuery();
                        _Form.UpdateStatus($"Database '{databaseName}' attached successfully.", 100, 100, true);

                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _Form.UpdateStatus("Error attaching the database: " + ex.Message, 100, 100, false);

                    return false;
                }
            }
        }

        /// <summary>
        /// Executes a stored procedure on a specific database.
        /// </summary>
        /// <param name="databaseName">The name of the database where the stored procedure is located.</param>
        /// <param name="storedProcedureName">The name of the stored procedure to execute.</param>
        private void ExecuteStoredProcedure(string databaseName, string storedProcedureName)
        {
            string connectionString = $@"Server=localhost\SQLEXPRESS;Database={databaseName};Trusted_Connection=True;";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(storedProcedureName, connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        // Execute the stored procedure
                        command.ExecuteNonQuery();
                        _Form.UpdateStatus($"Stored procedure '{storedProcedureName}' executed successfully.", 100, 100, true);

                    }
                }
                catch (Exception ex)
                {
                    _Form.UpdateStatus("Error executing stored procedure: " + ex.Message, 100, 100, false);

                }
            }
        }

        /// <summary>
        /// Closeing connections to the database
        /// </summary>
        /// <param name="databaseName">The name of the database to detach.</param>
        /// <returns>True if the database was successfully detached; otherwise, false.</returns>
        private void CloseDatabaseConnections(string databaseName)
        {
            string connectionString = @"Server=localhost\SQLEXPRESS;Database=master;Trusted_Connection=True;";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string sql = $@"
        DECLARE @DBID INT = DB_ID('{databaseName}');
        IF @DBID IS NOT NULL
        BEGIN
            DECLARE @SQL NVARCHAR(MAX) = '';
            SELECT @SQL = @SQL + 'KILL ' + CAST(spid AS NVARCHAR(10)) + '; '
            FROM sys.sysprocesses
            WHERE dbid = @DBID AND spid <> @@SPID; -- Exclude the current process

            IF @SQL <> ''
                EXEC sp_executesql @SQL;
        END";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }



        /// <summary>
        /// Detaches a database from SQL Server.
        /// </summary>
        /// <param name="databaseName">The name of the database to detach.</param>
        /// <returns>True if the database was successfully detached; otherwise, false.</returns>
        private bool DetachDatabase(string databaseName)
        {
            string connectionString = @"Server=localhost\SQLEXPRESS;Database=master;Trusted_Connection=True;";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Open();

                    // SQL command to detach the database
                    string detachQuery = $@"EXEC sp_detach_db '{databaseName}', 'true';";

                    using (SqlCommand command = new SqlCommand(detachQuery, connection))
                    {
                        command.ExecuteNonQuery();
                        _Form.UpdateStatus($"Database '{databaseName}' detached successfully.", 100, 100, true);

                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _Form.UpdateStatus("Error detaching the database: " + ex.Message, 100, 100, false);

                    return false;
                }
            }
        }
    }
}
