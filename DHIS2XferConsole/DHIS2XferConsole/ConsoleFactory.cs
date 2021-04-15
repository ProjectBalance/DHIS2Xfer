using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DHIS2XferConsole
{
    public class ConsoleFactory
    {
        public static void LoadConfig()
        {
            string[] config = File.ReadAllLines("XferConfig.cfg");
            foreach (var option in config)
            {
                string[] keyValue = option.Split("|");
                switch (keyValue[0])
                {
                    case "Xfer":
                        Global.XferID = keyValue[1];
                        break;
                    case "Directory":
                        Global.Directory = keyValue[1];
                        break;
                    case "StartPeriod":
                        Global.StartPeriod = keyValue[1];
                        break;
                    case "EndPeriod":
                        Global.EndPeriod = keyValue[1];
                        break;
                    case "DryRun":
                        Global.DryRun = bool.Parse(keyValue[1]);
                        break;
                    case "DisplayGUI":
                        if(keyValue[1] != "")
                            Global.DisplayGUI = bool.Parse(keyValue[1]);
                        break;
                    case "DisplayLog":
                        Global.DisplayLog = keyValue[1];
                        break;
                    default:
                        break;
                }
            }
        }

        public static void ParseParameters(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("-"))
                {
                    switch (args[i])
                    {
                        case "-Xfer":
                            Global.XferID = args[i + 1];
                            break;
                        case "-Directory":
                            Global.Directory = args[i + 1];
                            break;
                        case "-StartPeriod":
                            Global.StartPeriod = args[i + 1];
                            break;
                        case "-EndPeriod":
                            Global.EndPeriod = args[i + 1];
                            break;
                        case "-DryRun":
                            Global.DryRun = bool.Parse(args[i + 1]);
                            break;
                        case "-DisplayGUI":
                            Global.DisplayGUI = bool.Parse(args[i + 1]);
                            break;
                        case "-DisplayLog":
                            Global.DisplayLog = args[i + 1];
                            break;
                        default:
                            break;
                    }
                }
            }


        }

        public static void WriteLine(string message)
        {
            if (Global.DisplayGUI)
            {
                Console.WriteLine(message);
            }

            //Write to logfile
            if (Global.WriteToLog)
            {
                using (StreamWriter file = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "xferLog.log", true))
                {
                    file.WriteLine($@"{DateTime.Now}:   {message}");
                }
            }
        }

        public static void WriteEnd()
        {
            //Write to logfile
            if (Global.WriteToLog)
            {
                using (StreamWriter file = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "xferLog.log", true))
                {
                    file.WriteLine($@"-----------------------------------------------------------------------------------------------");
                    file.WriteLine(" ");
                }
            }
        }

    }
}
