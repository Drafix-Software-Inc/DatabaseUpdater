using Microsoft.Win32;
using System;
using System.Data;
using System.Data.Odbc;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace DatabaseUpdater
{
    internal class Engine
    {
        private readonly MainForm _Form;

        public Engine(MainForm TheForm)
        {
            _Form = TheForm;
        }

        public bool Run()
        {
            try
            {
                Logit($"Version {Assembly.GetExecutingAssembly().GetName().Version}");
				Logit("Getting Database Path");

                // Get the database path
                string databasePath = GetDatabasePath();

				Logit($"The Database Path is {databasePath}");

                // Check if the DrafixUpdate.mdf file exists
                string databaseFilePath = Path.Combine(databasePath, "DrafixUpdate.mdf");
                string logFilePath = Path.Combine(databasePath, "DrafixUpdate_log.ldf");

				Logit($"Checking to see if .mdf and .ldf files exist");

                if (!File.Exists(databaseFilePath))
                {
                    Logit($"Could not find {databaseFilePath}");
                    _Form.UpdateStatus("Cannot find database file", 100, 100, false);
                    return false;
                }
                else
                    Logit($"Found {databaseFilePath}");

                if (!File.Exists(logFilePath))
                {
					Logit($"Could not find {logFilePath}");
                    _Form.UpdateStatus("Cannot find database file", 100, 100, false);
                    return false;
                }
				else
					Logit($"Found {logFilePath}");

				// Check if the database is attached in SQL Server
				Logit($"Checking to see if Database is Attached");
                if (!IsDatabaseAttached("DrafixUpdate"))
                {
                    if (_Form.IsLoggingOn)
                        _Form.log.WriteLine($"The database is not attached, attempting to attach it");
                    _Form.UpdateStatus("Database 'DrafixUpdate' is not attached to SQL Server. Attempting to attach...", 100, 100, true);


                    // Attempt to attach the database
                    AttachDatabase("DrafixUpdate", databaseFilePath, logFilePath);
                }

				Logit($"The database is attached");

                Console.WriteLine("Database is attached and ready for use.");

				// Execute the stored procedure
				Logit($"Executing SPROC");

                ExecuteStoredProcedure("dbo.USP_Upgrade_26_0");

				//Close Connections
				Logit($"Closing database connections");

                CloseDatabaseConnections("DrafixUpdate");

				//Detach DrafixUpdate Database and Deletes the DrafixUpdate MDF and LDF files
				Logit($"About to detach database and delete DrafixUpdate files");
                DetachDatabase("DrafixUpdate");

				Logit($"Database was sucessfully detached");
                _Form.UpdateStatus("Database 'DrafixUpdate' detached successfully.", 100, 100, true);

				//Delete MDF and LDF files after succesful detachment
				Logit($"Deleting mdf and ldf files");

                if (File.Exists(databaseFilePath))
                {
                    File.Delete(databaseFilePath);
					Logit($"Deleted {databaseFilePath}");
                    Console.WriteLine("Deleted DrafixUpdate.mdf");
                }

                if (File.Exists(logFilePath))
                {
                    File.Delete(logFilePath);
					Logit($"Deleted {logFilePath}");
                    Console.WriteLine("Deleted DrafixUpdate_log.ldf");
                }
            }
            catch (Exception error)
            {
				Logit($"Error {error.Message}");
            }

			// Exit the application after the process is done
			Logit($"Done");
            Application.Exit();

            return true;
        }

        private void Logit(string message)
        {
			if (_Form.IsLoggingOn)
				_Form.log.WriteLine(message);
		}

		/// <summary>
		/// Gets the location of the database files.
		/// </summary>
		/// <returns>The location of the DrafixUpdate.mdf files.</returns>
		public static string GetDatabasePath()
        {
			RegistryKey k1 = Registry.LocalMachine.OpenSubKey("Software");

			string filePath;
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

                connection.Open();

                // Query to check if the database exists
                string checkDatabaseQuery = $"SELECT COUNT(*) FROM sys.databases WHERE name = ?";
                using (OdbcCommand command = new OdbcCommand(checkDatabaseQuery, connection))
                {
                    command.Parameters.AddWithValue("@databaseName", databaseName);
                    isAttached = (int)command.ExecuteScalar() > 0;
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
        private void AttachDatabase(string databaseName, string dataFilePath, string logFilePath)
        {
            //string connectionString = @"Server=localhost\SQLEXPRESS;Database=master;Trusted_Connection=True;";

            string connectionString = "DSN=DrafixSQL;Database=master;ExtendedAnsiSQL=1;TrustedConnection=True;";

            using (OdbcConnection connection = new OdbcConnection(connectionString))
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
                }
            }
        }

        /// <summary>
        /// Executes a stored procedure on a specific database.
        /// </summary>
        /// <param name="storedProcedureName">The name of the stored procedure to execute.</param>
        private void ExecuteStoredProcedure(string storedProcedureName)
        {
            //string connectionString = $@"Server=localhost\SQLEXPRESS;Database={databaseName};Trusted_Connection=True;";
            string connectionString = "DSN=DrafixSQL;Database=DrafixUpdate;ExtendedAnsiSQL=1;TrustedConnection=True;";

            using (OdbcConnection connection = new OdbcConnection(connectionString))
            {

                connection.Open();
                using (OdbcCommand command = new OdbcCommand(storedProcedureName, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    // Execute the stored procedure
                    command.ExecuteNonQuery();
                    _Form.UpdateStatus($"Stored procedure '{storedProcedureName}' executed successfully.", 100, 100, true);
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
        private void DetachDatabase(string databaseName)
        {
            //string connectionString = @"Server=localhost\SQLEXPRESS;Database=master;Trusted_Connection=True;";
            string connectionString = "DSN=DrafixSQL;Database=master;ExtendedAnsiSQL=1;TrustedConnection=True;";

            using (OdbcConnection connection = new OdbcConnection(connectionString))
            {

                connection.Open();

                // SQL command to detach the database
                string detachQuery = $@"EXEC sp_detach_db '{databaseName}', 'true';";

                using (OdbcCommand command = new OdbcCommand(detachQuery, connection))
                {
                    command.ExecuteNonQuery();
                    _Form.UpdateStatus($"Database '{databaseName}' detached successfully.", 100, 100, true);
                }
            }
        }
    }
}

