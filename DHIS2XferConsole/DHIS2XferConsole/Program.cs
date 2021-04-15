using Newtonsoft.Json.Linq;
using System;
using System.IO;
using XferCore;

namespace DHIS2XferConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                ConsoleFactory.LoadConfig();
                ConsoleFactory.ParseParameters(args);

                RunXfer();

            }
            else
            {
                Console.WriteLine("");
                Console.WriteLine("Loading Config...");
                ConsoleFactory.LoadConfig();

                try
                {
                    RunXfer();

                    Console.WriteLine("Completed processing Xfer file(s)");
                    ConsoleFactory.WriteEnd();
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                
            }
        }

        public static void RunXfer()
        {
            XferCore.Core core = new Core();

            core.LogEvent += Core_LogEvent;
            
            //If no directory provided, check if the Xfer entry includes the entire directory
            if(Global.Directory == "")
            {
                //If the Xfer entry contains the full directory, break it down
                if (File.Exists(Global.XferID))
                {
                    string path = Global.XferID;
                    Global.Directory = Path.GetDirectoryName(path);
                    Global.XferID = Path.GetFileName(path);
                }
                else
                {
                    Console.WriteLine("No directory provided in config or parameter");
                }
            }
            else
            {
                //If there is no xfer id provide, run all files in the directory
                if (Global.XferID != "" && Global.XferID != null)
                {
                    Console.WriteLine("Processing single Xfer File...");
                    core.ProcessXfer(Global.XferID, Global.Directory, Global.DryRun);
                }
                else
                {
                    Console.WriteLine("Processing Xfer files from provided directory...");
                    core.ProcessXfers(Global.Directory, Global.DryRun);
                }
            }
            
                
        }

        private static void Core_LogEvent(string log)
        {
            if (Global.DisplayGUI)
            {
                if (Global.DisplayLog != "NONE")
                {
                    try
                    {
                        JObject jsonLog = JObject.Parse(log);
                        JObject meta = (JObject)jsonLog.GetValue("meta");
                        JObject r = (JObject)jsonLog.GetValue("response");

                        string logLine = "DataElement: " + meta.GetValue("dataElement").ToString() + ", Org Unit: " + meta.GetValue("orgUnit").ToString() + ", Period: " + meta.GetValue("period").ToString() + ", Status : " + r.GetValue("status").ToString() + ", Description : " + r.GetValue("description").ToString();
                        string status;
                        switch (Global.DisplayLog)
                        {
                            case "ALL":
                                ConsoleFactory.WriteLine(logLine);

                                break;
                            case "SUCCESS":
                                status = r.GetValue("status").ToString();
                                if (status == "SUCCESS")
                                    ConsoleFactory.WriteLine(logLine);
                                break;
                            case "ERROR":
                                status = r.GetValue("status").ToString();
                                if (status == "ERROR")
                                    ConsoleFactory.WriteLine(logLine);
                                break;
                            default:
                                ConsoleFactory.WriteLine(logLine);
                                break;
                        }
                    }
                    catch(Exception ex)
                    {
                        ConsoleFactory.WriteLine(log);
                    }
                    
                    
                    
                }
            }
        }
    }
}
