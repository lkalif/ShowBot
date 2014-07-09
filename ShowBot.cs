using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;

namespace ShowBot
{
    public class CommandLine
    {
        [Option("c", "conf", HelpText = "Load this config file. Default is showbot.conf")]
        public string Username = "showbot.conf";

        public HelpText GetHeader()
        {
            HelpText header = new HelpText("ShowBot 1.0");
            header.AdditionalNewLineAfterOption = true;
            header.Copyright = new CopyrightInfo("Latif Khalifa", 2014, 2014);
            return header;
        }

        [HelpOption("h", "help", HelpText = "Display this help screen.")]
        public string GetUsage()
        {
            HelpText usage = GetHeader();
            usage.AddOptions(this);
            // usage.AddPostOptionsLine("Example: automatically login user called Some Resident to his last location on the Second Life main grid (agni)");
            return usage.ToString();
        }
    }

    class ShowBot
    {
        static void Main(string[] args)
        {
        }
    }
}
