using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using System.Windows.Forms;
using Zemna.WindowsServiceController.Properties;

namespace Zemna.WindowsServiceController
{
    public partial class frmMain : Form
    {
        private ServiceController scServer;

        private byte reqCnt = 0;

        private ushort oldFlag = 0;
        private ushort flag = 0;
        private bool bFirst = true;

        private Thread logThread = null;
        private bool bThreadStop = false;
        private int lastLogIndex = -1;

        private string serviceName;

        private EventLog eventLog;

        public frmMain()
        {
            InitializeComponent();
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            serviceName = Settings.Default.ServiceName;

            notifyIcon1.Visible = true;
            notifyIcon1.BalloonTipTitle = serviceName;
            notifyIcon1.BalloonTipText = "System has started.";
            notifyIcon1.BalloonTipIcon = ToolTipIcon.Info;
            notifyIcon1.ShowBalloonTip(10);

            topMostToolStripMenuItem.Checked = Settings.Default.TopMost;
            this.TopMost = topMostToolStripMenuItem.Checked;

            autoToolStripMenuItem.Checked = Settings.Default.AutoControl;
            chkAuto.Checked = autoToolStripMenuItem.Checked;

            eventLog = new EventLog(Settings.Default.EventLogName, Settings.Default.EventLogMachineName, Settings.Default.EventLogSource);
            eventLog.EnableRaisingEvents = true;

            // 제일 마지막 로그 인덱스로 설정해 주기
            if (eventLog.Entries.Count == 0)
            {
                lastLogIndex = -1;
            }
            else
            {
                lastLogIndex = eventLog.Entries[eventLog.Entries.Count - 1].Index;
            }

            logThread = new Thread(new ThreadStart(LogThread));
            logThread.Start();

            CheckServiceStatus();
        }

        private void TerminateThread()
        {
            bThreadStop = true;

            if (logThread != null)
            {
                logThread.Join(1000);

                if (logThread.IsAlive)
                {
                    logThread.Abort();
                }

                logThread = null;
            }
        }

        private delegate void WriteListBoxHandler(string message);
        private void OnWriteListBox(string message)
        {
            listLog.Items.Add(message);

            if (listLog.Items.Count > 200)
            {
                listLog.Items.Clear();
            }

            listLog.SelectedIndex = listLog.Items.Count - 1;
        }

        private void LogThread()
        {
            while (!bThreadStop)
            {
                // 새로운 엔트리가 있다면
                while (eventLog.Entries.Count > 0 && eventLog.Entries[eventLog.Entries.Count - 1].Index > lastLogIndex)
                {
                    // 엔트리 시작점 찾기
                    lock (eventLog)
                    {
                        if (bThreadStop) return;

                        int startIdx = eventLog.Entries.Count - 1;

                        // 만일 lastLogIndex가 -1이라면 처음부터 처리
                        if (lastLogIndex < 0)
                        {
                            lastLogIndex = eventLog.Entries[0].Index;
                        }

                        while (true)
                        {
                            if (eventLog.Entries[startIdx].Index > lastLogIndex)
                            {
                                if (bThreadStop) return;

                                startIdx--;
                            }
                            else
                                break;
                        }

                        for (int i = startIdx + 1; i < eventLog.Entries.Count; i++)
                        {
                            if (bThreadStop) return;

                            this.Invoke(new WriteListBoxHandler(OnWriteListBox), eventLog.Entries[i].Message);
                        }

                        lastLogIndex = eventLog.Entries[eventLog.Entries.Count - 1].Index;
                    }
                }

                Thread.Sleep(200);
            }
        }

        private void ValidateService()
        {
            ServiceController[] services = ServiceController.GetServices();

            foreach (ServiceController sc in services)
            {
                if (sc.ServiceName == serviceName)
                {
                    scServer = sc;
                }
            }
        }

        private void ServiceControl(Byte act)
        {
            switch (act)
            {
                case 0:
                    if (scServer.CanStop)
                        scServer.Stop();
                    break;
                case 1:
                    scServer.Start();
                    break;
                case 2:
                    if (scServer.CanStop)
                        scServer.Stop();
                    scServer.WaitForStatus(ServiceControllerStatus.Stopped);
                    System.Threading.Thread.Sleep(1000);
                    scServer.Start();
                    break;
            }
        }

        private void CheckServiceStatus()
        {
            ValidateService();

            CheckService();

            AutoProcess();
        }

        private void CheckService()
        {
            try
            {
                if (scServer == null)
                {
                    dataCollectorToolStripMenuItem.Enabled = false;
                    groupBox1.Enabled = false;
                    startToolStripMenuItem.Enabled = false;
                    btnStart.Enabled = false;
                    stopToolStripMenuItem.Enabled = false;
                    btnStop.Enabled = false;
                    restartToolStripMenuItem.Enabled = false;
                    btnRestart.Enabled = false;
                    autoToolStripMenuItem.Enabled = false;
                    chkAuto.Enabled = false;
                    this.Text = Settings.Default.ServiceName + " Controller";
                    notifyIcon1.Text = this.Text;
                    notifyIcon1.BalloonTipTitle = this.Text;
                    flag = 2;
                    return;
                }

                this.Text = scServer.DisplayName + " Controller";
                notifyIcon1.Text = this.Text;
                notifyIcon1.BalloonTipTitle = this.Text;

                dataCollectorToolStripMenuItem.Enabled = true;
                groupBox1.Enabled = true;
                chkAuto.Enabled = true;

                if (autoToolStripMenuItem.Checked)
                {
                    startToolStripMenuItem.Enabled = false;
                    btnStart.Enabled = false;
                    stopToolStripMenuItem.Enabled = false;
                    btnStop.Enabled = false;
                    restartToolStripMenuItem.Enabled = false;
                    btnRestart.Enabled = false;
                }

                scServer.Refresh();
                switch (scServer.Status)
                {
                    case ServiceControllerStatus.Running:
                        if (!autoToolStripMenuItem.Checked)
                        {
                            startToolStripMenuItem.Enabled = false;
                            btnStart.Enabled = false;
                            stopToolStripMenuItem.Enabled = true;
                            btnStop.Enabled = true;
                            restartToolStripMenuItem.Enabled = true;
                            btnRestart.Enabled = true;
                        }
                        flag = 0;
                        break;
                    case ServiceControllerStatus.Stopped:
                        if (!autoToolStripMenuItem.Checked)
                        {
                            startToolStripMenuItem.Enabled = true;
                            btnStart.Enabled = true;
                            stopToolStripMenuItem.Enabled = false;
                            btnStop.Enabled = false;
                            restartToolStripMenuItem.Enabled = false;
                            btnRestart.Enabled = false;
                        }
                        flag = 1;
                        break;
                    case ServiceControllerStatus.Paused:
                    case ServiceControllerStatus.ContinuePending:
                    case ServiceControllerStatus.PausePending:
                    case ServiceControllerStatus.StartPending:
                        if (!autoToolStripMenuItem.Checked)
                        {
                            startToolStripMenuItem.Enabled = false;
                            btnStart.Enabled = false;
                            stopToolStripMenuItem.Enabled = false;
                            btnStop.Enabled = false;
                            restartToolStripMenuItem.Enabled = false;
                            btnRestart.Enabled = false;
                        }
                        flag = 1;
                        break;
                }
            }
            catch (System.Exception)
            {
                scServer.Close();
                scServer = null;
            }

            if (flag != oldFlag)
                tmNotify_Tick(null, null);
        }

        private void AutoControl()
        {
            autoToolStripMenuItem.Checked = !autoToolStripMenuItem.Checked;

            startToolStripMenuItem.Enabled = !autoToolStripMenuItem.Checked;
            stopToolStripMenuItem.Enabled = !autoToolStripMenuItem.Checked;
            restartToolStripMenuItem.Enabled = !autoToolStripMenuItem.Checked;

            chkAuto.Checked = autoToolStripMenuItem.Checked;

            btnStart.Enabled = startToolStripMenuItem.Enabled;
            btnStop.Enabled = stopToolStripMenuItem.Enabled;
            btnRestart.Enabled = restartToolStripMenuItem.Enabled;

            SaveSetting();

            if (scServer == null)
                return;

            switch (scServer.Status)
            {
                case ServiceControllerStatus.Running:
                    btnStart.Enabled = false;
                    break;
                case ServiceControllerStatus.Stopped:
                    btnStop.Enabled = false;
                    break;
            }
        }

        private void AutoProcess()
        {
            if (scServer == null)
                return;

            if (autoToolStripMenuItem.Checked == false)
                return;

            switch (scServer.Status)
            {
                case ServiceControllerStatus.Running:
                    reqCnt = 0;
                    break;
                case ServiceControllerStatus.StartPending:
                case ServiceControllerStatus.StopPending:
                    return;
                case ServiceControllerStatus.Stopped:
                    if (!contextMenuStrip1.Visible)
                    {
                        notifyIcon1.ShowBalloonTip(10, serviceName, "Starting " + serviceName + ".", ToolTipIcon.Info);
                    }
                    scServer.Start();
                    reqCnt++;
                    if (reqCnt > 3)
                    {
                        autoToolStripMenuItem.Checked = false;
                        if (!contextMenuStrip1.Visible)
                        {
                            notifyIcon1.ShowBalloonTip(10, serviceName, "Can't start " + serviceName + ".\r\nPlease check event log.", ToolTipIcon.Error);
                        }
                    }
                    break;
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            ServiceControl(1);
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            ServiceControl(0);
        }

        private void btnRestart_Click(object sender, EventArgs e)
        {
            ServiceControl(2);
        }

        private void exitToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            exitToolStripMenuItem_Click(null, null);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void SaveSetting()
        {
            Settings.Default.AutoControl = autoToolStripMenuItem.Checked;
            Settings.Default.TopMost = topMostToolStripMenuItem.Checked;

            Settings.Default.Save();
        }

        private void tmService_Tick(object sender, EventArgs e)
        {
            try
            {
                CheckServiceStatus();
            }
            catch (Exception ex)
            {
                eventLog.WriteEntry(ex.ToString(), EventLogEntryType.Error);
            }
        }

        private void topMostToolStripMenuItem_Click(object sender, EventArgs e)
        {
            topMostToolStripMenuItem.Checked = !topMostToolStripMenuItem.Checked;
            this.TopMost = topMostToolStripMenuItem.Checked;
            SaveSetting();
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (MessageBox.Show("Are you sure?", "Exit Program", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
            {
                e.Cancel = true;
                return;
            }

            tmService.Stop();
            tmService.Dispose();

            tmNotify.Stop();
            tmNotify.Dispose();

            SaveSetting();

            notifyIcon1.Visible = false;
            notifyIcon1.Dispose();

            scServer = null;

            TerminateThread();
        }

        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            this.Visible = true;
        }

        private void viewServicesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process proc = new Process();
            proc.EnableRaisingEvents = false;
            proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            proc.StartInfo.FileName = "cmd";
            proc.StartInfo.Arguments = "/c services.msc";
            proc.Start();
        }

        private void startToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ServiceControl(1);
        }

        private void stopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ServiceControl(0);
        }

        private void restartToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ServiceControl(2);
        }

        private void autoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AutoControl();
        }

        private void tmNotify_Tick(object sender, EventArgs e)
        {
            try
            {
                if (contextMenuStrip1.Visible)
                    return;

                if (oldFlag == flag && !bFirst)
                    return;

                switch (flag)
                {
                    case 0:
                        notifyIcon1.ShowBalloonTip(10, serviceName, "All services are running.", ToolTipIcon.Info);
                        break;
                    case 1:
                        notifyIcon1.ShowBalloonTip(30, serviceName, serviceName + " service has stopped!", ToolTipIcon.Error);
                        break;
                    case 2:
                        notifyIcon1.ShowBalloonTip(10, serviceName, "Can't find " + serviceName + " service.", ToolTipIcon.Warning);
                        break;
                }

                oldFlag = flag;
                bFirst = false;
            }
            catch (Exception ex)
            {
                eventLog.WriteEntry(ex.ToString(), EventLogEntryType.Error);
            }
        }

        private void chkAuto_Click(object sender, EventArgs e)
        {
            AutoControl();
        }
    }
}
