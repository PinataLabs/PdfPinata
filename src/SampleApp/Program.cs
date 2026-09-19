using System.CommandLine;
using SampleApp.Infrastructure;

namespace SampleApp;

public static class Program
{
    public static int Main(string[] args) =>
        CommandLineApp.Build().Parse(args).Invoke();
}
