using MBBSEmu.Btrieve;
using MBBSEmu.DependencyInjection;
using NLog;
using System.Linq;
using System.IO;
using System;

namespace MBBSEmu
{
    public class Program
    {
        static void Main(string[] args)
        {
            new Program().Run(args);
        }

        private void Run(string[] args)
        {
          var serviceResolver = new ServiceResolver();
          var logger = serviceResolver.GetService<ILogger>();

          if (args.Length == 0)
              Console.WriteLine("Usage: MBBSDatabase [view|convert] [files]");

          var convert = (args[0] == "convert");

          foreach (string s in args.Skip(1))
          {
            BtrieveFile file = new BtrieveFile();
            try
            {
              file.LoadFile(logger, s);
              if (convert)
              {
                  using var processor = new BtrieveFileProcessor();
                  processor.CreateSqliteDB(Path.ChangeExtension(s, ".DB"), file);
              }
            }
            catch (Exception e)
            {
              logger.Error(e, $"Failed to load Btrieve file {e.Message}\n{e.StackTrace}");
            }
          }
        }
    }
}
