using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Newtonsoft.Json;
using Python.Runtime;

namespace PoseBridgeApp.Views;

public partial class MainWindow : Window
{
    private bool _isEngineRunning=false;
    private bool _isPythonInited = false;
    
    public static CancellationTokenSource TokenSource = new CancellationTokenSource();
    CancellationToken _cancellationToken = TokenSource.Token;
    
    public string PyDir="uninitialized";
    public string WorkDir = "/home/myoukin/PoseBridgeEngine";
    
    public MainWindow()
    {
        InitializeComponent();
        PyDir = JsonConfigHelper.ReadConfig("pyDir");
        if (string.IsNullOrEmpty(PyDir) || PyDir == "uninitialized")
        {
            PyDir = "uninitialized";
            ShowPyLocateButton.Content = "选择Python可执行文件";
        }
        else
        {
            ShowPyLocateButton.Content = PyDir;
        }
    }
    
    public async void RunPython()
    {
        if (!_isPythonInited)
        {
            string dllpath = Path.Combine(PyDir, "lib/libpython3.11.so");
            Console.WriteLine(dllpath);
            Runtime.PythonDLL = "/home/myoukin/miniconda3/envs/posebridge/lib/libpython3.11.so";
            Console.WriteLine("dll设置");
            PythonEngine.PythonHome = PyDir;
            // PythonEngine.PythonPath = dllpath;
            PythonEngine.Initialize();
            Console.WriteLine("init");
            dynamic sys = Py.Import("sys");
            sys.path.append(WorkDir);
            _isPythonInited = true;
        }
        
        await Task.Run(() =>
        {
            var threadState = PythonEngine.BeginAllowThreads();
            try
            {
                using (Py.GIL())
                {
                    dynamic engine = Py.Import("engine");
                    engine.init();
                    while (!TokenSource.IsCancellationRequested)
                    {
                        engine.run();
                    }
        
                    Console.WriteLine("Task canceled");
                    engine.stop();
                }
            }
            catch(Exception e)
            {
                Console.WriteLine("引擎遇到错误如下：");
                Console.WriteLine(e);
            }
            finally
            {
                PythonEngine.EndAllowThreads(threadState);
            }
        }, _cancellationToken);
    }
    
    private void Start_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_isEngineRunning)
        {
            TokenSource.Cancel();
            StartButton.Content = "以当前设置启动";
            _isEngineRunning = false;
        }
        else
        {
            if (PyDir != "uninitialized")
            {
                TokenSource?.Dispose(); //空检查
                TokenSource = new CancellationTokenSource();
                _cancellationToken = TokenSource.Token;
                _isEngineRunning = true;
                StartButton.Content = "停止";
                RunPython();
            }
        }
    }

    private async void PyPicker_OnClick(object? sender, RoutedEventArgs e)
    {
        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择Python可执行文件",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Python可执行文件") { Patterns = new[] { "*.*" } },
            ]
        });

        if (result.Count > 0)
        {
            var selectedPath = result[0].Path.LocalPath;
            PyDir=selectedPath;
            
            PyDir = GetUpDir(PyDir);
            PyDir = GetUpDir(PyDir);
            //get两次得到最上层的py目录
            
            Console.WriteLine(PyDir);
            JsonConfigHelper.WriteConfig("pyDir", PyDir);
            ShowPyLocateButton.Content = PyDir;
        }
    }

    private string GetUpDir(string dir)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Console.WriteLine("当前操作系统是 Windows");
            dir = dir.Substring(0, dir.LastIndexOf("\\"));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Console.WriteLine("当前操作系统是 Linux");
            dir = dir.Substring(0, dir.LastIndexOf("/"));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Console.WriteLine("当前操作系统是 macOS");
            dir = dir.Substring(0, dir.LastIndexOf("/"));
        }

        return dir;
    }
    
    public class JsonConfigHelper
    {
 
        private static Dictionary<string, string> configDic = new Dictionary<string, string>();
 
        /// <summary>
        /// 读取配置信息
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string ReadConfig(string key)
        {
            if (File.Exists("config.json") == false)//如果不存在就创建file文件夹
            {
                FileStream f = File.Create("config.json");
                f.Close();
            }
            string s = File.ReadAllText("config.json");
            try
            {
                configDic = JsonConvert.DeserializeObject<Dictionary<string, string>>(s);
            }
            catch
            {
                configDic = new Dictionary<string, string>();
            }
 
            if (configDic != null && configDic.ContainsKey(key))
            {
                return configDic[key];
            }
            else
            {
                return string.Empty;
            }
        }
 
        /// <summary>
        /// 添加配置信息
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void WriteConfig(string key, string value)
        {
            if (configDic == null)
            {
                configDic = new Dictionary<string, string>();
            }
            configDic[key] = value;
            string s = JsonConvert.SerializeObject(configDic);
            File.WriteAllText("config.json", s);
        }
 
        /// <summary>
        /// 删除配置信息
        /// </summary>
        /// <param name="key"></param>
        public static void DeleteConfig(string key)
        {
            if (configDic != null && configDic.ContainsKey(key))
            {
                configDic.Remove(key);
                string s = JsonConvert.SerializeObject(configDic);
                File.WriteAllText("config.json", s);
            }
        }
    }
}