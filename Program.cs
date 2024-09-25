using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace Demo
{
    class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var services = new ServiceCollection()
                .AddSingleton<DemoConfigurationProvider>()
                .BuildServiceProvider();

            var app = new CommandLineApplication<Demo>();

            app.Conventions
                .UseDefaultConventions()
                .UseConstructorInjection(services);
            try
            {
                return app.Execute(args);
            }
            catch (CommandParsingException ex)
            {
                Console.WriteLine(ex.Message);
                if (ex is UnrecognizedCommandParsingException uex && uex.NearestMatches.Any())
                {
                    Console.WriteLine();
                    Console.WriteLine("Did you mean this?");
                    Console.WriteLine("    " + uex.NearestMatches.First());
                }

                return 1;
            }
            catch (Exception e)
            {
                Console.WriteLine("An exception occurred, please check if you wrote a valid command");
                Console.WriteLine("One of the common mistakes is specifying arguments in the incorrect order");
                Console.WriteLine("Run tool -h or tool <command name> -h to receive information about the command");
                Console.WriteLine($"Exception message: {e.Message}");

                return 1;
            }
        }

        [Command("demo")]
        [Subcommand(
            typeof(ExampleSubcommand)
        )]
        public class Demo : DemoCommandBase
        {
            public Demo(DemoConfigurationProvider configurationProvider) : base(configurationProvider)
            {
            }

            public override List<string>  CreateArgs()
            {
                // you can implement some base functionality like validation here
                return new List<string>();
            }
        }

        [Command(Name = "example", Description = "Example command, used for demonstration")]
        public class ExampleSubcommand : DemoCommandBase
        {
            [Argument(0, Description = "Example argument")]
            public string Command { get; set; }

            [Option("-opt", Description = "Example option")]
            public string MigrationName { get; set; }

            private Demo Parent { get; set; }

            public ExampleSubcommand(DemoConfigurationProvider configurationProvider) : base(configurationProvider)
            {
            }

            public override List<string> CreateArgs()
            {
                var args = Parent.CreateArgs();
                
                // fill the arguments with commands to execute, note that for now the base functionality only works for a single command
                // optionally you can ignore the args and just do any operation you would like instead, later just return the empty list

                return args;
            }
        }

        [HelpOption("-?|-h|--help")]
        public abstract class DemoCommandBase
        {
            public abstract List<string> CreateArgs();

            protected DemoConfiguration Configuration;

            public DemoCommandBase(DemoConfigurationProvider configurationProvider)
            {
                this.Configuration = configurationProvider.GetConfiguration();
            }

            protected virtual int OnExecute(CommandLineApplication app)
            {
                var args= CreateArgs();
                args.Add($"cd {Directory.GetCurrentDirectory()}");
                using (var proc = new Process())
                {
                    var startInfo = new ProcessStartInfo();
                    startInfo.FileName = "cmd.exe";
                    startInfo.UseShellExecute = false;
                    startInfo.RedirectStandardInput = true;
                    proc.StartInfo = startInfo;
                    proc.Start();

                    using (var buf = proc.StandardInput)
                    {
                        foreach (var cmd in args)
                        {
                            buf.DoCommand(cmd);
                        }
                    }

                    if (proc.HasExited)
                    {
                        proc.Close();
                    }
                }

                return 0;
            }
        }

        public class DemoConfigurationProvider
        {
            private readonly string ConfigurationPath = Environment.GetEnvironmentVariable("USERPROFILE") + $"{Path.DirectorySeparatorChar}yscqCliOptions.json";

            public DemoConfiguration GetConfiguration()
            {
                if (File.Exists(ConfigurationPath))
                {
                    var json = File.ReadAllText(ConfigurationPath);
                    var config = JsonConvert.DeserializeObject<DemoConfiguration>(json);
                    return config;
                }

                return ConstructOptions();
            }

            private DemoConfiguration ConstructOptions()
            {
                var baseDirectory = Prompt.GetString($"Set the base project directory (specify using {Path.DirectorySeparatorChar} )");

                while (!Directory.Exists(baseDirectory))
                {
                    ConsoleHelper.WriteInfo($"Specified directory {baseDirectory}");
                    baseDirectory = Prompt.GetString("Specified directory is invalid, try again");
                }

                var config = new DemoConfiguration
                {
                    BaseDir = baseDirectory,
                };

                CreateSettingsFile(config);

                return config;
            }

            public DemoConfiguration UpdateKey(string key, object value)
            {
                var cfg = GetConfiguration();

                PropertyInfo[] props = typeof(DemoConfiguration).GetProperties();
                props.FirstOrDefault(x => x.Name == key)?.SetValue(cfg, value);

                CreateSettingsFile(cfg);
                return cfg;
            }

            private void CreateSettingsFile(DemoConfiguration config)
            {
                var json = JsonConvert.SerializeObject(config);
                File.WriteAllText(ConfigurationPath, json);
            }
        }

        public class DemoConfiguration
        {
            public string BaseDir { get; set; }
        }
    }

    public static class ConsoleExtensions
    {
        public static void DoCommand(this StreamWriter sw, string command)
        {
            var cmdWithReturn = command + Environment.NewLine;
            sw.Write(cmdWithReturn);
            sw.Flush();
        }
    }

    public static class ConsoleHelper
    {
        public static void WriteColored(string message, ConsoleColor color)
        {
            var prevColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = prevColor;
        }

        public static void WriteError(string message)
        {
            WriteColored(message, ConsoleColor.Red);
        }

        public static void WriteInfo(string message)
        {
            WriteColored(message, ConsoleColor.Cyan);
        }
    }
}
