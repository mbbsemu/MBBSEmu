using MBBSEmu.Btrieve;
using MBBSEmu.DependencyInjection;
using MBBSEmu.Logging;
using MBBSEmu.Logging.Targets;
using System;
using System.IO;
using System.Linq;

namespace MBBSDatabase {
  /// <summary>
  ///   An MBBSEmu database (.DB) utility program.
  ///
  ///   </para/>Currently supports two modes of operation, view and convert.
  ///   View mode shows information about the specified DAT file, such as key information.
  ///   Convert mode converts the DAT file into a .DB file.
  /// </summary>
  public class Program {
    static void Main(string[] args) {
      new Program().Run(args);
    }

    private void Run(string[] args) {
      var serviceResolver = new ServiceResolver();
      var logger = new MessageLogger(new ConsoleTarget());

      if (args.Length == 0) {
        Console.WriteLine("Usage: MBBSDatabase [view|convert] [files]");
        return;
      }

      var convert = (args[0] == "convert");

      foreach (string s in args.Skip(1)) {
        BtrieveFile file = new BtrieveFile();
        try {
          file.LoadFile(logger, s);
          if (convert) {
            //using var processor = new BtrieveFileProcessor();

            var dbPath = Path.ChangeExtension(s, ".DB");
            if (File.Exists(dbPath))
              File.Delete(dbPath);

            // processor.CreateSqliteDB(dbPath, file);
          }
        } catch (Exception e) {
          logger.Error(e, $"Failed to load Btrieve file {s}: {e.Message}\n{e.StackTrace}");
        }
      }
    }
  }
}
