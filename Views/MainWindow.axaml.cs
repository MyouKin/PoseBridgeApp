using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Python.Runtime;

namespace PoseBridgeApp.Views;

public partial class MainWindow : Window
{
    public bool isEngineRunning=false;
    public string workDir = "/home/myoukin/PoseBridgeEngine/";
    public static CancellationTokenSource tokenSource = new CancellationTokenSource();
    CancellationToken cancellationToken = tokenSource.Token;
    public bool isPythonInited = false;
    
    public MainWindow()
    {
        InitializeComponent();
    }
    
    public async void runPython()
    {
        if (!isPythonInited)
        {
            Runtime.PythonDLL = "/home/myoukin/miniforge3/envs/posebridge/lib/libpython3.11.so";
            PythonEngine.Initialize();
            dynamic sys = Py.Import("sys");
            sys.path.append(workDir);
            isPythonInited = true;
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
                    while (!tokenSource.IsCancellationRequested)
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
        }, cancellationToken);
    }
    
    private void StartOnClick(object? sender, RoutedEventArgs e)
    {
        if (isEngineRunning)
        {
            tokenSource.Cancel();
            StartButton.Content = "以当前设置启动";
            isEngineRunning = false;
        }
        else
        {
            tokenSource?.Dispose(); //空检查
            tokenSource = new CancellationTokenSource();
            cancellationToken = tokenSource.Token;
            isEngineRunning = true;
            StartButton.Content = "停止";

            runPython();
        }
    }
    
}