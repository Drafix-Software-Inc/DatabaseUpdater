using System;
using System.Data.SqlClient;
using System.Data;
using System.IO;
using Microsoft.Win32;
using System.Windows.Forms;
using System.Diagnostics.Contracts;
using System.Data.Odbc;
using System.Reflection;

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
            if (_Form.IsLoggingOn)
                _Form.log.WriteLine($"Version {Assembly.GetExecutingAssembly().GetName().Version}");
            if (_Form.IsLoggingOn)
                _Form.log.WriteLine("Getting Database Path");
            // Get the database path
            string databasePath = GetDatabasePath();

            if (_Form.IsLoggingOn)
                _Form.log.WriteLine($"The Database Path is {databasePath}");

            // Check if the DrafixUpdate.mdf file exists
            string databaseFilePath = Path.Combine(databasePath, "DrafixUpdate.mdf");
            string logFilePath = Path.Combine(databasePath, "DrafixUpdate_log.ldf");

            if (_Form.IsLoggingOn)
                _Form.log.WriteLine($"Checking to see if .mdf and .ldf files exist");

            if (!File.Exists(databaseFilePath))
            {
                if (_Form.IsLoggingOn)
                    _Form.log.WriteLine($"Could not find the mdf file");
                _Form.UpdateStatus("Cannot find database file", 100, 100, false);
                return false;
            }

            if (!File.Exists(logFilePath))
            {
                if (_Form.IsLoggingOn)
                    _Form.log.WriteLine($"Could not find the ldf file");
                _Form.UpdateStatus("Cannot find database file", 100, 100, false);
                return false;
            }

            // Check if the database is attached in SQL Server
            if (_Form.IsLoggingOn)
                _Form.log.WriteLine($"Checking to see if Database is Attached");
            if (!IsDatabaseAttached("DrafixUpdate"))
            {
                if (_Form.IsLoggingOn)
                    _Form.log.WriteLine($"The database is not attached, attempting to attach it");
                _Form.UpdateStatus("Database 'DrafixUpdate' is not attached to SQL Server. Attempting to attach...", 100, 100, true);


                // Attempt to attach the database
                bool attachSuccess = AttachDatabase("DrafixUpdate", databaseFilePath, logFilePath);

                if (!attachSuccess)
                {
                    if (_Form.IsLoggingOn)
                        _Form.log.WriteLine($"Uable to attach database");
                    _Form.UpdateStatus("Failed to attach the database.", 100, 100, false);
                    return false;
                }
            }
            if (_Form.IsLoggingOn)
                _Form.log.WriteLine($"The database is attached");
            Console.WriteLine("Database is attached and ready for use.");

            // Execute the stored procedure
            if (_Form.IsLoggingOn)
                _Form.log.WriteLine($"Executing SPROC");

            var didItWork = false;
            if (ExecuteStoredProcedure("DrafixUpdate", "dbo.USP_Upgrade_26_0"))
            {
                didItWork = true;
                Console.WriteLine("Uploading new objects.");
            }
            //Close Connections
            if (_Form.IsLoggingOn)
                _Form.log.WriteLine($"Closing database connections");
            CloseDatabaseConnections("DrafixUpdate");

            //Detach DrafixUpdate Database and Deletes the DrafixUpdate MDF and LDF files
            if (_Form.IsLoggingOn)
                _Form.log.WriteLine($"About to detach database and delete DrafixUpdate files");
            bool detachSuccess = DetachDatabase("DrafixUpdate");

            if (detachSuccess)
            {
                if (_Form.IsLoggingOn)
                    _Form.log.WriteLine($"Database was sucessfully detached");
                _Form.UpdateStatus("Database 'DrafixUpdate' detached successfully.", 100, 100, true);

                if (didItWork)
                {
                    //Delete MDF and LDF files after succesful detachment
                    if (_Form.IsLoggingOn)
                        _Form.log.WriteLine($"Deleting mdf and ldf files");
                    try
                    {
                        if (File.Exists(databaseFilePath))
                        {
                            File.Delete(databaseFilePath);
                            if (_Form.IsLoggingOn)
                                _Form.log.WriteLine($"Deleted the mdf");
                            Console.WriteLine("Deleted DrafixUpdate.mdf");
                        }

                        if (File.Exists(logFilePath))
                        {
                            File.Delete(logFilePath);
                            if (_Form.IsLoggingOn)
                                _Form.log.WriteLine($"Deleted the ldf");
                            Console.WriteLine("Deleted DrafixUpdate_log.ldf");
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_Form.IsLoggingOn)
                            _Form.log.WriteLine($"Proplem deleting database files {ex.Message}");
                        _Form.UpdateStatus($"Error deleting database files: {ex.Message}", 100, 100, false);
                        return false;
                    }
                }
            }
            else
            {
                if (_Form.IsLoggingOn)
                    _Form.log.WriteLine($"Failed to detach");
                _Form.UpdateStatus("Failed to detach the database 'DrafixUpdate'.", 100, 100, false);
                return false;
            }

            // Exit the application after the process is done
            if (_Form.IsLoggingOn)
                _Form.log.WriteLine($"Done");
            Application.Exit();

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
            //string connectionString = @"Server=localhost\SQLEXPRESS;Database=master;Trusted_Connection=True;";
            string connectionString = "DSN=DrafixSQL;Database=master;ExtendedAnsiSQL=1;TrustedConnection=True;";


            using (OdbcConnection connection = new OdbcConnection(connectionString))
            {
                try
                {
                    connection.Open();

                    // Query to check if the database exists
                    string checkDatabaseQuery = $"SELECT COUNT(*) FROM sys.databases WHERE name = ?";
                    using (OdbcCommand command = new OdbcCommand(checkDatabaseQuery, connection))
                    {
                        command.Parameters.AddWithValue("@databaseName", databaseName);
                        isAttached = (int)command.ExecuteScalar() > 0;
                    }
                }
                catch (Exception ex)
                {
                    if (_Form.IsLoggingOn)
                        _Form.log.WriteLine($"Error {ex.Message}");
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
            //string connectionString = @"Server=localhost\SQLEXPRESS;Database=master;Trusted_Connection=True;";

            string connectionString = "DSN=DrafixSQL;Database=master;ExtendedAnsiSQL=1;TrustedConnection=True;";

            using (OdbcConnection connection = new OdbcConnection(connectionString))
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

                    using (OdbcCommand command = new OdbcCommand(attachQuery, connection))
                    {
                        command.ExecuteNonQuery();
                        _Form.UpdateStatus($"Database '{databaseName}' attached successfully.", 100, 100, true);

                        return true;
                    }
                }
                catch (Exception ex)
                {
                    if (_Form.IsLoggingOn)
                        _Form.log.WriteLine($"Error {ex.Message}");
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
        private bool ExecuteStoredProcedure(string databaseName, string storedProcedureName)
        {
            bool result = false;
            //string connectionString = $@"Server=localhost\SQLEXPRESS;Database={databaseName};Trusted_Connection=True;";
            string connectionString = "DSN=DrafixSQL;Database=master;ExtendedAnsiSQL=1;TrustedConnection=True;";


            using (OdbcConnection connection = new OdbcConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    using (OdbcCommand command = new OdbcCommand(storedProcedureName, connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        // Execute the stored procedure
                        command.ExecuteNonQuery();
                        _Form.UpdateStatus($"Stored procedure '{storedProcedureName}' executed successfully.", 100, 100, true);
                        result = true;
                    }
                }
                catch (Exception ex)
                {
                    if (_Form.IsLoggingOn)
                        _Form.log.WriteLine($"Error {ex.Message}");
                    _Form.UpdateStatus("Error executing stored procedure: " + ex.Message, 100, 100, false);

                }
                return result;
            }
        }

        /// <summary>
        /// Closeing connections to the database
        /// </summary>
        /// <param name="databaseName">The name of the database to detach.</param>
        /// <returns>True if the database was successfully detached; otherwise, false.</returns>
        private void CloseDatabaseConnections(string databaseName)
        {
            //string connectionString = @"Server=localhost\SQLEXPRESS;Database=master;Trusted_Connection=True;";
            string connectionString = "DSN=DrafixSQL;Database=master;ExtendedAnsiSQL=1;TrustedConnection=True;";

            using (OdbcConnection conn = new OdbcConnection(connectionString))
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

                using (OdbcCommand cmd = new OdbcCommand(sql, conn))
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
            //string connectionString = @"Server=localhost\SQLEXPRESS;Database=master;Trusted_Connection=True;";
            string connectionString = "DSN=DrafixSQL;Database=master;ExtendedAnsiSQL=1;TrustedConnection=True;";


            using (OdbcConnection connection = new OdbcConnection(connectionString))
            {
                try
                {
                    connection.Open();

                    // SQL command to detach the database
                    string detachQuery = $@"EXEC sp_detach_db '{databaseName}', 'true';";

                    using (OdbcCommand command = new OdbcCommand(detachQuery, connection))
                    {
                        command.ExecuteNonQuery();
                        _Form.UpdateStatus($"Database '{databaseName}' detached successfully.", 100, 100, true);

                        return true;
                    }
                }
                catch (Exception ex)
                {
                    if (_Form.IsLoggingOn)
                        _Form.log.WriteLine($"Error {ex.Message}");
                    _Form.UpdateStatus("Error detaching the database: " + ex.Message, 100, 100, false);

                    return false;
                }
            }
        }
    }
}
