using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CG.Web.MegaApiClient;
using DiscordRPC;

namespace NexusBootstrapperV2
{
    public partial class MainWindow : Form
    {
        private BackgroundWorker downloadWorker;
        private BackgroundWorker unzipWorker;
        private MegaApiClient client;
        private INode node;
        private long totalBytes;
        private long downloadedBytes;
        private DiscordRpcClient dcclient;


        //FontManager fontManager = new FontManager();

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn
        (
            int nLeftRect,     // x-coordinate of upper-left corner
            int nTopRect,      // y-coordinate of upper-left corner
            int nRightRect,    // x-coordinate of lower-right corner
            int nBottomRect,   // y-coordinate of lower-right corner
            int nWidthEllipse, // width of ellipse
            int nHeightEllipse // height of ellipse
        );

        [DllImport("kernel32.dll")]
        static extern IntPtr GetVersion();

        public MainWindow()
        {
            InitializeComponent();
        }
        
        Point lastPoint;

        private void Form1_Load(object sender, EventArgs e)
        {
            // Fix this a bug that took 4 hours to fix then i realized i was being stupid
            this.TransparencyKey = Color.White;

            // Create a rounded rectangle region
            IntPtr region = CreateRoundRectRgn(0, 0, this.Width, this.Height, 15, 15);

            // Apply the region to the form
            this.Region = Region.FromHrgn(region);

            // Old font stuff. No longer relevant. If you are reading this, instead of pusing the actual font files, extract the raw font data with HxD and it will save you hours of pain.
            //fontManager.SetFontToRodium(TitleLogo);
            //fontManager.SetFontToMonster(GameDetection, 10);
            //fontManager.SetFontToMonster(StepLabel, 15);
            //fontManager.SetFontToMonster(ClientLabel, 30);
            //fontManager.SetFontToMonster(IP, 20);

            // Stuff for getting the windows version
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");

            foreach (ManagementObject os in searcher.Get())
            {
                // Get the version, caption, and build number
                string version = os["Version"].ToString();
                string caption = os["Caption"].ToString();
                string buildNumber = os["BuildNumber"].ToString();

                // Check if the process is 64-bit
                bool is64Bit = Environment.Is64BitProcess;

                // Create a string indicating the process architecture
                string processArchitecture = is64Bit ? "64-bit" : "32-bit";

                // Create a string with the OS version
                string windowsVersionString = $"Host: {caption} {version} {processArchitecture} (Build {buildNumber})";

                // Print the Windows version
                VersionLabel.Text = windowsVersionString;
            }
            StartRPC();
            DetectionLabel.ForeColor = Color.Red;
            timer1.Start();
            InitializeBackgroundWorkers();
        }

        
        // This is for handling the form movement. I am too lazy to swap this to the automatic handler I made for form movement.
        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            lastPoint = new Point(e.X, e.Y);
        }

        private void TitleBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.Left += e.X - lastPoint.X;
                this.Top += e.Y - lastPoint.Y;

            }
        }

        private void ExitButton_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void TitleBar_Paint(object sender, PaintEventArgs e)
        {

        }

        // Pretty sure this is useless and might have been removed but was originally for debugging.
        private void button1_Click(object sender, EventArgs e)
        {
            GameDetection.Text = "Game Started: TEST.EXE | ProcessID";
            DetectionLabel.ForeColor = Color.LimeGreen;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            // Stuff for detecting if the game is open. Needed for the RPC to be cool.
            string appExe = "SCPSL.exe";
            Process[] processes = Process.GetProcessesByName(appExe.Replace(".exe", ""));
            if (processes.Length > 0)
            {
                string pid = processes[0].Id.ToString();
                DetectionLabel.ForeColor = Color.LimeGreen;
                GameDetection.Text = $"Game Detected: {appExe} | {pid}";
                dcclient.SetPresence(new RichPresence()
                {
                    Details = "Nexus Bootstrapper V2.0",
                    State = "Playing SCP:SL v9.1.2",
                    Assets = new Assets()
                    {
                        LargeImageKey = "nexusicon",
                        LargeImageText = "Nexus",
                    }
                });
            }
            else
            {
                DetectionLabel.ForeColor = Color.Red;
                GameDetection.Text = "Waiting For Game Process";
                dcclient.SetPresence(new RichPresence()
                {
                    Details = "Nexus Bootstrapper V2.0",
                    State = "In the menus",
                    Assets = new Assets()
                    {
                        LargeImageKey = "nexusicon",
                        LargeImageText = "Nexus",
                    }
                });
            }
        }

        private void guna2Button1_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start("C:\\ProgramData\\Nexus\\9.1.2\\SCPSL.exe");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString() + "\n\nPlease contact the developers or reinstall your game.", "Nexus");
            }
        }

        private void guna2Button2_Click(object sender, EventArgs e)
        {
            // Specify the directory you want to open
            string directoryPath = @"C:\ProgramData\Nexus\9.1.2";

            // Open File Explorer to the specified directory
            Process.Start("explorer.exe", directoryPath);
        }

        private void guna2Button3_Click(object sender, EventArgs e)
        {
            Clipboard.SetText("68.50.38.219:7778");
        }

        // MEGA API Downloader for downloading the files. I ain't commenting all of this because it sucked to make and i ain't got time for allat
        private void InitializeBackgroundWorkers()
        {
            downloadWorker = new BackgroundWorker();
            downloadWorker.WorkerReportsProgress = true;
            downloadWorker.DoWork += DownloadWorker_DoWork;
            downloadWorker.ProgressChanged += DownloadWorker_ProgressChanged;
            downloadWorker.RunWorkerCompleted += DownloadWorker_RunWorkerCompleted;

            unzipWorker = new BackgroundWorker();
            unzipWorker.WorkerReportsProgress = true;
            unzipWorker.DoWork += UnzipWorker_DoWork;
            unzipWorker.ProgressChanged += UnzipWorker_ProgressChanged;
            unzipWorker.RunWorkerCompleted += UnzipWorker_RunWorkerCompleted;
        }

        private void DownloadWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            string fileLink = e.Argument as string;
            client = new MegaApiClient();
            client.LoginAnonymous();

            Uri link = new Uri(fileLink);
            node = client.GetNodeFromLink(link);
            totalBytes = node.Size;

            string outputFilePath = Path.Combine(Environment.CurrentDirectory, node.Name);
            using (var stream = client.Download(node))
            using (var fileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write))
            {
                byte[] buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fileStream.Write(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;
                    int progressPercentage = (int)((double)downloadedBytes / totalBytes * 100);
                    downloadWorker.ReportProgress(progressPercentage);
                }
            }

            client.Logout();
            e.Result = outputFilePath;
        }

        private void DownloadWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            guna2ProgressBar1.Value = e.ProgressPercentage;
        }

        private void DownloadWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            string outputFilePath = e.Result as string;
            StepLabel.Text = "2/2 - Decompressing game files";

            // Reset progress bar for unzipping
            guna2ProgressBar1.Value = 0;

            // Start unzipping in a separate thread
            unzipWorker.RunWorkerAsync(outputFilePath);
        }

        private void UnzipWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            string outputFilePath = e.Argument as string;
            string extractPath = "C:\\ProgramData\\Nexus\\9.1.2\\";

            using (ZipArchive archive = ZipFile.OpenRead(outputFilePath))
            {
                int totalEntries = archive.Entries.Count;
                int processedEntries = 0;

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destinationPath = Path.Combine(extractPath, entry.FullName);

                    // Ensure the directory exists
                    string directoryPath = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    if (entry.Name == "")
                    {
                        // Create directory
                        Directory.CreateDirectory(destinationPath);
                    }
                    else
                    {
                        // Extract file
                        entry.ExtractToFile(destinationPath, true);
                    }

                    processedEntries++;
                    int progressPercentage = (int)((double)processedEntries / totalEntries * 100);
                    unzipWorker.ReportProgress(progressPercentage);
                }
            }

            e.Result = extractPath;
        }


        private void UnzipWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            guna2ProgressBar1.Value = e.ProgressPercentage;
        }

        private void UnzipWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            StepLabel.Text = "Complete";
        }

        private void guna2Button4_Click(object sender, EventArgs e)
        {
            StepLabel.Text = "1/2 - Downloading";
            string fileLink = "https://mega.nz/file/fCxAABDD#bA7gsebS6jQxrP3dTqm9ARVXBEaOjFzgJp_3oNw84rA";
            downloadWorker.RunWorkerAsync(fileLink);
        }

        private void label1_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }


        private void StartRPC()
        {
            dcclient = new DiscordRpcClient("1257833952719274114");

            // Connect to Discord
            dcclient.Initialize();

            // Set the presence
            dcclient.SetPresence(new RichPresence()
            {
                Details = "Nexus Bootstrapper V2.0",
                State = "In the menus",
                Assets = new Assets()
                {
                    LargeImageKey = "nexusicon",
                    LargeImageText = "Nexus",
                }
            });
        }
    }
}
