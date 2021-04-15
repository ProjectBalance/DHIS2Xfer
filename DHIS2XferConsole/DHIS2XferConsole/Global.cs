using System;
using System.Collections.Generic;
using System.Text;

namespace DHIS2XferConsole
{
    public class Global
    {
        //Config options
        public static string XferID;
        public static string Directory;
        public static string StartPeriod;
        public static string EndPeriod;

        public static bool DryRun;

        public static bool DisplayGUI = false;
        public static string DisplayLog;

        public static bool WriteToLog = true;

        public static void OnError(string message)
        {
            ConsoleFactory.WriteLine("An error has occured:");
            ConsoleFactory.WriteLine(message);

            if (DisplayGUI)
            {
                Console.WriteLine("Press enter to exit", true);
                Console.ReadLine();
            }


            Environment.Exit(0);
        }
    }
}
