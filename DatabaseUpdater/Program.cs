using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DatabaseUpdater
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool IsLoggingOn = false;

            var targetPath = Engine.GetDatabasePath();
            var logFile = Path.Combine(targetPath, "DatabaseUpdaterLog.txt");

            foreach (var arg in args)
            {
                if (arg.ToLower() == "/l" || arg.ToLower() == "-l" || arg.ToLower() == "--logging")
                    IsLoggingOn = true;
            }

            StreamWriter log = null;

            if (IsLoggingOn)
                log = new StreamWriter(logFile, false);

            try
            {
                Application.Run(new MainForm(args, log));
            }
            catch (Exception error)
            {
                if (IsLoggingOn)
                    log.WriteLine(error.ToString());
            }
            finally
            {
                if (IsLoggingOn)
                    log.Close();
            }

            //  Now that we have a log
            //  FTP that log to the FTP Site

            if ( IsLoggingOn )
            {
				using (var client = new WebClient())
				{
					client.Credentials = new NetworkCredential(ftpUsername, ftpPassword);
					client.UploadFile("ftp://host/path.zip", WebRequestMethods.Ftp.UploadFile, logFile);
				}
			}
        }
    }
}
        