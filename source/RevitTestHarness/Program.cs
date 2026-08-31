// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using UsdExporterRevit.Utilities;
// using UsdExporterRevit.Models;
using UsdExporterRevit.Settings;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.ComponentModel;
using RevitTestHarness.Session;

namespace RevitTestHarness
{
internal class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("\n----------------------");
        Console.WriteLine("- Revit Test Harness -");
        Console.WriteLine("----------------------\n");
        Console.WriteLine($"\n {String.Join(" ",args)}");

        if (args.Length == 0)
        {
#if DEBUG
            FileInfo exe = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string root = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(exe.Directory.FullName).FullName).FullName).FullName).FullName;
            string tests = Path.Combine(root, "tests");
            string v = "2025";
            args = new string[] { Path.Combine(tests, $"inputs\\{v}"), $"-v {v}", $"-o {Path.Combine(tests, $"_outputs\\{v}")}" };
#else
            Console.WriteLine("No arguments provided. Exiting.");
            Pause();
            return;
#endif
        }

        int version = -1;
        string input = args[0];
        string output = "";

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i].StartsWith("-o"))
            {
                output = args[i].Split(' ').Last();
                if (string.IsNullOrEmpty(output))
                {
                    output = args[i + 1];
                    if (string.IsNullOrEmpty(output))
                    {
                        throw new Exception("Could not read output file argument (-o). Please check your formatting.");
                    }
                }
            }
            else if (args[i].StartsWith("-v"))
            {
                if (!int.TryParse(args[i].Split(' ').Last(), out version) && !int.TryParse(args[i + 1], out version))
                {
                    throw new Exception("Could not read version argument (-v). Please check your formatting.");
                }
            }
        }
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        sw.Start();
        DateTime now = DateTime.Now;
        SessionData session = new SessionData($"Smoke Test {now.Year}{now.Month}{now.Day}_{now.Hour}:{now.Minute}", $"{version}");
        Harness harness = new Harness(version, input, output, session);
        harness.StartTest(input, output);
        int minutes = 1;
        while (harness.Status == Status.Running)
        {
            if (sw.Elapsed.TotalMinutes > minutes)
            {
                minutes++;
                harness.CheckConnection();
            }
        }
        session = harness.Session;
        session.TotalExportTime = session.Tests.Sum(s => s.Time);
        session.Duration = sw.Elapsed.TotalMinutes;
        sw.Stop();
        string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(session, Formatting.Indented);
        File.WriteAllText(Path.Combine(output, "data.json"), jsonString);
        if (harness.Status != Status.Complete)
        {
            Environment.Exit(1);
        }
    }

    static void Pause() => Thread.Sleep(3000);
}

internal class FileTest
{
    public readonly string RevitFileName;
    public readonly string RevitFilePath;
    public readonly string OutputDirectory;
    public readonly string[] Configs;

    private readonly ServerPipe _payloadPipe;
    private readonly HashSet<string> _configsRan = new HashSet<string>();

    private System.Diagnostics.Stopwatch _timer;

    // private string _activeConfig;
    private FileInfo _activeConfigFile;

    public List<TestData> Data = new List<TestData>();

    public int ExportsRemaining => Configs.Length - _configsRan.Count;

    public FileTest(string revitFilePath, string outputFilePath, string[] configs, ServerPipe payloadPipe)
    {
        RevitFilePath = revitFilePath;
        RevitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        OutputDirectory = outputFilePath;
        Configs = configs;
        this._payloadPipe = payloadPipe;
        _timer = new System.Diagnostics.Stopwatch();
    }

    public void SendFirstJson()
    {
        SendNextJson();
    }
    private bool SendNextJson()
    {
        if (_configsRan.Count < Configs.Length)
        {
            string nextConfig = Configs.Where(j => !_configsRan.Contains(j)).First();
            _activeConfigFile = new FileInfo(nextConfig);
            UsdExporterRevitSettings settings = UsdExporterRevitSettings.Read(_activeConfigFile.FullName);
            FileInfo revitFile = new FileInfo(RevitFilePath);
            settings.Batch.InputFolder = revitFile.Directory.FullName.Replace(@"\", "/");
            settings.Batch.OutputFolder = Path.Combine(OutputDirectory, _activeConfigFile.Name.Replace(".json", "")).Replace(@"\", "/");
            Data.Add(new TestData(_activeConfigFile.Name, RevitFileName, settings));
            string settingsJson = JsonConvert.SerializeObject(settings);
            _payloadPipe.WriteString(settingsJson);
            if (_timer.IsRunning)
            {
                _timer.Restart();
            }
            else
            {
                _timer.Start();
            }
            return true;
        }
        return false;
    }

    internal void CompletedExport()
    {
        Console.WriteLine();
        Console.WriteLine("completed");
        Console.WriteLine("----------------");
        Console.WriteLine($"JSON:  {_activeConfigFile.Name}");
        Console.WriteLine($"MODEL: {RevitFilePath}");
        Console.WriteLine($"TIME:  {_timer.ElapsedMilliseconds} ms");
        TestData data = Data.Where(t => t.Test == _activeConfigFile.Name).First();
        data.Time = _timer.Elapsed.TotalMinutes;
        data.ExitCode = 0;
        try
        {
            data.SetSizeAndCount(Path.Combine(OutputDirectory, _activeConfigFile.Name.Replace(".json", "")));
        }
        catch
        {
        }
        _configsRan.Add(_activeConfigFile.FullName);
        SendNextJson();
    }
}

internal class TestFolder
{
    public DirectoryInfo Folder;
    public bool Completed = false;
    public Dictionary<string, FileTest> TestFiles;
    public FileTest ActiveTest;
    public List<string> Configs;

    public TestFolder(DirectoryInfo folder, Dictionary<string, FileTest> tests, List<string> configs)
    {
        Folder = folder;
        TestFiles = tests;
        Configs = configs;
    }
}

internal class Harness
{
    public Status Status { get; private set; } = Status.Start;

    private DateTime lastPing;
#region Args

    public readonly int Version;
    public readonly string InputDirectory;
    public readonly string OutputDirectory;

#endregion

    private const string _appName = "UsdExporterRevitTestHarness";

    private static Dictionary<int, FileInfo> _revitLaunchPaths = new Dictionary<int, FileInfo>() {
        { 2024, new FileInfo(@"C:\Program Files\Autodesk\Revit 2024\Revit.exe") },
        { 2025, new FileInfo(@"C:\Program Files\Autodesk\Revit 2025\Revit.exe") },
        { 2026, new FileInfo(@"C:\Program Files\Autodesk\Revit 2026\Revit.exe") },
    };

#region IPC

    private static string payloadPipeName = $"{_appName}_Payload";
    private static string modelPipeName = $"{_appName}_OpenModel";
    private static string messagePipeName = $"{_appName}_Message";

    private ServerPipe payloadPipe;
    private ServerPipe modelPipe;
    private ServerPipe messagePipe;

#endregion

    private List<TestFolder> _testFolders;

    private TestFolder _activeFolder;

    private Process _revitProcess;

    public SessionData Session;

    public Harness(int version, string inputDirectory, string outputDirectory, SessionData session, string[] configs = null)
    {
        Console.WriteLine("Starting RevitTestHarness...");
        lastPing = DateTime.Now;
        Session = session;
        // Validate
        if (!_revitLaunchPaths.TryGetValue(version, out FileInfo fileInfo))
        {
            throw new Exception($"Unhandled or invalid revit version {version}");
        }
        else if (!Directory.Exists(inputDirectory))
        {
            throw new Exception($"Invalid input directory: {inputDirectory}");
        }
        else if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        Console.WriteLine($"Running Revit version {version} on folder {Path.GetFileName(inputDirectory)}");

        this.Status = Status.Running;
        this.Version = version;
        this.InputDirectory = inputDirectory;
        this.OutputDirectory = (outputDirectory.EndsWith(version.ToString())) ? outputDirectory : Path.Combine(outputDirectory, version.ToString());
    }

    public void StartTest(string inputDirectory, string outputDirectory)
    {
        OpenPipes();
        _testFolders = GetTestFolders(new DirectoryInfo(inputDirectory), OutputDirectory, payloadPipe);
        LaunchRevit();
    }

    private static List<TestFolder> GetTestFolders(DirectoryInfo dir, string outputDirectory, ServerPipe payloadPipe)
    {
        List<TestFolder> folders = new List<TestFolder>();
        foreach (DirectoryInfo d in dir.GetDirectories())
        {
            FileInfo[] files = d.GetFiles();
            string[] json = files.Where(f => f.Name.EndsWith(".json")).Select(f => f.FullName).ToArray();
            Dictionary<string, FileTest> tests =
                files.Where(f => f.Name.EndsWith(".rvt")).ToDictionary(s => Path.GetFileNameWithoutExtension(s.FullName), s => new FileTest(s.FullName, Path.Combine(outputDirectory, d.Name), json, payloadPipe), StringComparer.OrdinalIgnoreCase);
            folders.Add(new TestFolder(d, tests, json.ToList()));
        }
        return folders;
    }

    private void LaunchRevit()
    {
        if (_revitProcess != null)
        {
            return;
        }
        Console.WriteLine("Launching Revit...");
        Process p = new Process();
        p.StartInfo.FileName = _revitLaunchPaths[Version].FullName;
        p.StartInfo.Arguments = $"\"{GetFirstRevitFile()}\"";
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.EnvironmentVariables["USD_EXPORTER_REVIT_TEST_HARNESS"] = "1";
        p.EnableRaisingEvents = true;
        p.Exited += OnProcessExited;

        if (p.Start())
        {
            Console.WriteLine("Revit launched.");
            _revitProcess = p;
            _activeFolder = _testFolders.First();
            _activeFolder.ActiveTest = _activeFolder.TestFiles.First().Value;
        }
        else
        {
            Console.WriteLine("Failed to launch Revit.");
            Status = Status.Failed;
            CloseRevit();
        }
    }

    public void CloseRevit()
    {
        if (_revitProcess == null)
        {
            return;
        }
        try
        {
            Console.WriteLine("Closing Revit...");
            _revitProcess.Exited -= OnProcessExited;
            _revitProcess.CloseMainWindow();
            if (!_revitProcess.WaitForExit(10000))
            {
                _revitProcess.Kill();
                _revitProcess.WaitForExit(5000); // Wait for kill to complete
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error closing Revit process: {e.Message}");
        }
        finally
        {
            _revitProcess.Dispose();
            _revitProcess = null;
        }
    }

    private string GetFirstRevitFile()
    {
        return _testFolders.First().TestFiles.First().Value.RevitFilePath;
    }

    private void OnProcessExited(object sender, EventArgs e)
    {
        Process p = sender as Process;
        if (p.ExitCode == 0)
        {
            Status = Status.Complete;
        }
        else if (_activeFolder?.ActiveTest?.Data != null && _activeFolder.ActiveTest.Data.Count > 0)
        {
            TestData data = _activeFolder.ActiveTest.Data[_activeFolder.ActiveTest.Data.Count - 1];
            data.ExitCode = p.ExitCode;
            Session.Tests.AddRange(_activeFolder.ActiveTest.Data);
            Console.WriteLine("Process exited with code: " + p.ExitCode);
            Status = Status.Failed;
        }
        Console.WriteLine("Revit process exited.");
    }

    public void CheckConnection()
    {
        TimeSpan span = DateTime.Now - lastPing;
        Console.WriteLine("Checking Connection...");
        if (span.TotalMinutes > 5.0)
        {
            // Check if there's any test data before trying to access it
            if (_activeFolder?.ActiveTest?.Data != null && _activeFolder.ActiveTest.Data.Count > 0)
            {
                _activeFolder.ActiveTest.Data[_activeFolder.ActiveTest.Data.Count - 1].ExitCode = 1;
            }
            Console.WriteLine("CheckConnection failed");
            Status = Status.Failed;
            CloseRevit();
        }
    }

#region IPC

    private void OpenPipes()
    {
        payloadPipe = new ServerPipe($"{payloadPipeName}{Version}", p => p.StartStringReaderAsync());
        payloadPipe.DataReceived += PayloadPipe_DataReceived;
        payloadPipe.PipeConnected += PayloadPipe_PipeConnected;
        payloadPipe.BeginConnect();
        modelPipe = new ServerPipe($"{modelPipeName}{Version}", p => p.StartStringReaderAsync());
        modelPipe.DataReceived += ModelPipe_DataReceived;
        modelPipe.PipeConnected += ModelPipe_PipeConnected;
        modelPipe.BeginConnect();
        messagePipe = new ServerPipe($"{messagePipeName}{Version}", p => p.StartStringReaderAsync());
        messagePipe.DataReceived += MessagePipe_DataReceived;
        messagePipe.PipeConnected += MessagePipe_PipeConnected;
        messagePipe.BeginConnect();
        Console.WriteLine("Opened pipes.");
    }
    private void ClosePipes()
    {
        payloadPipe.Close();
        payloadPipe.DataReceived -= PayloadPipe_DataReceived;
        payloadPipe.PipeConnected -= PayloadPipe_PipeConnected;
        payloadPipe = null;
        modelPipe.Close();
        modelPipe.DataReceived -= ModelPipe_DataReceived;
        modelPipe.PipeConnected -= ModelPipe_PipeConnected;
        modelPipe = null;
        messagePipe.Close();
        messagePipe.DataReceived -= MessagePipe_DataReceived;
        messagePipe.PipeConnected -= MessagePipe_PipeConnected;
        messagePipe = null;
        Console.WriteLine("Closed pipes.");
    }
    private void PayloadPipe_PipeConnected(object sender, EventArgs e)
    {
        Console.WriteLine("PayloadPipe Connected.");
        lastPing = DateTime.Now;
        payloadPipe.WriteString("hello");
    }
    private void ModelPipe_PipeConnected(object sender, EventArgs e)
    {
        Console.WriteLine("ModelPipe Connected.");
        lastPing = DateTime.Now;
        modelPipe.WriteString("hello");
    }

    private void MessagePipe_PipeConnected(object sender, EventArgs e)
    {
        Console.WriteLine("MessagePipe Connected.");
        lastPing = DateTime.Now;
        messagePipe.WriteString("hello");
    }
    private void PayloadPipe_DataReceived(object sender, PipeEventArgs e)
    {
        lastPing = DateTime.Now;
    }
    private void ModelPipe_DataReceived(object sender, PipeEventArgs e)
    {
        lastPing = DateTime.Now;
    }
    private void MessagePipe_DataReceived(object sender, PipeEventArgs e)
    {
        lastPing = DateTime.Now;
        bool allDone = false;
        if (e.Msg == "task completed" || e.Msg == "exception thrown")
        {
            if (e.Msg == "exceptions thrown")
            {
                Console.WriteLine(e.Msg);

                if (_activeFolder?.ActiveTest?.Data != null && _activeFolder.ActiveTest.Data.Count > 0)
                {
                    _activeFolder.ActiveTest.Data[_activeFolder.ActiveTest.Data.Count - 1].ExitCode = 1;
                }
            }
            _activeFolder.ActiveTest.CompletedExport();

            if (_activeFolder.ActiveTest.ExportsRemaining == 0)
            {
                _activeFolder.Completed = true;
                Session.Tests.AddRange(_activeFolder.ActiveTest.Data);
                if (_testFolders.Any(f => !f.Completed))
                {
                    _activeFolder = _testFolders.Where(f => !f.Completed).First();
                    _activeFolder.ActiveTest = _activeFolder.TestFiles.First().Value;
                    modelPipe.WriteString(_activeFolder.ActiveTest.RevitFilePath);
                }
                else
                {
                    allDone = true;
                }
            }
        }
        if (e.Msg == "ready for payload")
        {
            _activeFolder.ActiveTest.SendFirstJson();
        }
        if (e.Msg.StartsWith("OPENFAIL"))
        {
            string failedModel = e.Msg.Replace("OPENFAIL ", "");
            Console.WriteLine($"Failed to open model at path {failedModel}");
        }
        if (allDone)
        {
            ClosePipes();
            CloseRevit();
            Status = Status.Complete;
        }
    }

#endregion
}

internal enum Status
{
    Error = -2,
    Failed = -1,
    Start = 0,
    Running,
    Complete,
}
}
