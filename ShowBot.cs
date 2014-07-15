using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;

namespace ShowBot
{
    public class CommandLine
    {
        [Option("c", "conf", HelpText = "Load this config file. Default is showbot.conf")]
        public string Conf = "bot.conf";

        public HelpText GetHeader()
        {
            HelpText header = new HelpText("ShowBot 1.0");
            header.AdditionalNewLineAfterOption = true;
            header.Copyright = new CopyrightInfo("Latif Khalifa", 2014);
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

    public class ShowBot
    {
        public static CommandLine CommandLine;

        static void Main(string[] args)
        {
            // Read command line options
            CommandLine = new CommandLine();

            CommandLineParser parser = new CommandLineParser(new CommandLineParserSettings(Console.Error));
            if (!parser.ParseArguments(args, CommandLine))
            {
                Environment.Exit(1);
            }

            var bot = new ShowBot();
            bot.Run();
        }

        IrcServerInfo GetConfig(string configFilename)
        {
            IrcServerInfo ret = new IrcServerInfo();
            foreach (var line in System.IO.File.ReadAllLines(configFilename))
            {
                string l = line.Trim();
                var pos = l.IndexOf(';');
                if (pos >= 0)
                {
                    l = l.Substring(0, pos);
                }

                pos = l.IndexOf('=');
                if (pos < 1) continue;

                string key = l.Substring(0, pos).Trim();
                string val = l.Substring(pos + 1).Trim();

                switch (key)
                {
                    case "ServerID":
                        ret.ServerID = val;
                        break;

                    case "ServerHost":
                        ret.ServerHost = val;
                        break;

                    case "ServerPort":
                        int port = 6667;
                        int.TryParse(val, out port);
                        ret.ServerPort = port;
                        break;

                    case "Nick":
                        ret.Nick = val;
                        break;

                    case "Username":
                        ret.Username = val;
                        break;

                    case "Password":
                        ret.Password = val;
                        break;

                    case "RealName":
                        ret.RealName = val;
                        break;

                    case "ApiAuth":
                        ret.ApiAuth = val;
                        break;

                    case "WebBase":
                        ret.WebBase = val;
                        break;

                    case "ServerSSL":
                        if (val == "1" || val.ToLower() == "true" || val.ToLower() == "yes")
                        {
                            ret.ServerSSL = true;
                        }
                        else
                        {
                            ret.ServerSSL = false;
                        }
                        break;

                    case "Channel":
                    case "Channels":
                        foreach (var chan in Regex.Split(val, @"[\s,]+"))
                        {
                            if (chan.StartsWith("#"))
                            {
                                ret.Channels.Add(chan);
                            }
                            else
                            {
                                ret.Channels.Add("#" + chan);
                            }
                        }
                        break;

                    case "AdditionalOps":
                        foreach (var op in Regex.Split(val, @"[\s,]+"))
                        {
                            ret.AdditionalOps.Add(op.Trim());
                        }
                        break;

                    default:
                        throw new ArgumentException("Uknown configuration setting " + key);
                }
            }

            if (
                string.IsNullOrEmpty(ret.ServerID) ||
                string.IsNullOrEmpty(ret.ServerHost) ||
                string.IsNullOrEmpty(ret.Nick) ||
                string.IsNullOrEmpty(ret.Username) ||
                string.IsNullOrEmpty(ret.ApiAuth) ||
                string.IsNullOrEmpty(ret.WebBase) ||
                ret.Channels.Count == 0
                )
            {
                throw new ArgumentNullException("Required configuration field missing");
            }

            return ret;
        }

        void Run()
        {
            string cmd = String.Empty;

            IrcServerInfo ircCfg = null;

            try
            {
                ircCfg = GetConfig(CommandLine.Conf);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error reading config: " + ex.Message);
                Environment.Exit(1);
            }

            using (var bot = new IrcBot(ircCfg))
            {
                bot.Connect();

                while (cmd != null && cmd != "exit" && cmd != "quit")
                {
                    try
                    {
                        Console.Write("> ");
                        cmd = Console.ReadLine();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message);
                    }
                }
            }
        }
    }
}
