using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;

namespace DatabaseUpdater
{
    public partial class MainForm : Form
    {
        public delegate void WriteToStatusLine1Delegate(string message, int count, int total, bool success);
        public StreamWriter log;
        public bool IsLoggingOn = false;

        public MainForm(string[] args, StreamWriter log)
        {
            InitializeComponent();
            this.log = log;
            foreach (var arg in args)
            {
                if (arg.ToLower() == "/l" || arg.ToLower() == "-l" || arg.ToLower() == "--logging")
                    IsLoggingOn = true;
            }
        }

        /// <summary>
        /// Called when the form is loaded
        /// </summary>
        /// <param name="sender">The object that sent this notification</param>
        /// <param name="e">The <see cref="EventArgs"/> associated with this event</param>
        private void OnLoad(object sender, EventArgs e)
        {
            ProgressLabel.Text = "Updating Database";

            Task.Factory.StartNew(UpdateDatabase, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void UpdateStatus(string progressText, int count, int total, bool success) 
        {
            if (InvokeRequired)
            {
                var m = new WriteToStatusLine1Delegate(UpdateStatus);

                this.Invoke(m, new object[] { progressText, count, total, success });
            }
            else 
            {
                ProgressLabel.Text = progressText;
                Progress.Value = count;
                Progress.Maximum = total;
                Progress.Minimum = 0;
            }
        }

        private void UpdateDatabase()
        {
            UpdateStatus("Opening Connection to Database", 0, 100, true);
            var Engine = new Engine(this);

            Engine.Run();
        }
    }
}
