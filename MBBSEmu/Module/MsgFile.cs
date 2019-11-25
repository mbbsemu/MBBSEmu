using MBBSEmu.Module.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace MBBSEmu.Module
{
    public class MsgFile
    {
        private readonly string _modulePath;
        private readonly string _moduleName;
        public readonly List<MsgRecord> MsgRecords;

        public MsgFile(string modulePath, string moduleName)
        {
            _modulePath = modulePath;
            _moduleName = moduleName;

            if (!File.Exists($"{modulePath}{moduleName}.JSON"))
            {
                Console.WriteLine("Missing Converted JSON File!");
                Console.WriteLine("Converting MSG file to JSON....");
                ConvertToJson($"{modulePath}{moduleName}.MSG");
                Console.WriteLine("Conversion Complete!");
            }

            Console.WriteLine("Loading MSG file JSON object...");
            MsgRecords = JsonSerializer.Deserialize<List<MsgRecord>>(File.ReadAllText($"{modulePath}{moduleName}.JSON"));
            Console.WriteLine("MSG file JSON object loaded!");
        }

        private void ConvertToJson(string msgFile)
        {
            var currentLevel = EnumMsgFileLevel.None;
            var output = new List<MsgRecord>();
            var sbMsgRecord = new StringBuilder();
            var bMsgRecordMultiline = false;
            var ordinal = 0;
            foreach (var s in File.ReadAllLines(msgFile))
            {
                if (string.IsNullOrEmpty(s) || (!s.Contains('{') && !bMsgRecordMultiline))
                    continue;

                if (s.StartsWith("LEVEL3"))
                {
                    currentLevel = EnumMsgFileLevel.LEVEL3;
                    ordinal++;
                    continue;
                }

                if (s.StartsWith("LEVEL4"))
                {
                    currentLevel = EnumMsgFileLevel.LEVEL4;
                    ordinal++;
                    continue;
                }

                if (s.StartsWith("LEVEL99"))
                {
                    currentLevel = EnumMsgFileLevel.LEVEL99;
                    ordinal++;
                    continue;
                }

                sbMsgRecord.Append(s);

                if (!s.Contains('}'))
                {
                    //We found a starting bracket but not a closing, must be a multi-line value
                    bMsgRecordMultiline = true;
                    continue;
                }

                //If we're here, we found the end, begin processing record
                var newMsgRecord = new MsgRecord();
                var originalMsgString = sbMsgRecord.ToString().Trim();

                //Up to location of first { contains Record Name
                newMsgRecord.Name = originalMsgString.Substring(0, originalMsgString.IndexOf('{')).Trim();
                newMsgRecord.Ordinal = ordinal;
                //Language Definitions, usually the 1st line
                if (newMsgRecord.Name == "LANGUAGE")
                {
                    sbMsgRecord.Clear();
                    continue;
                }

                if (currentLevel == EnumMsgFileLevel.LEVEL3 || currentLevel == EnumMsgFileLevel.LEVEL4)
                {
                    //Extract the Prompt and Default Value
                    var promptStart = originalMsgString.IndexOf('{') + 1;
                    var promptEnd = originalMsgString.IndexOf('}');
                    var promptAndDefault = originalMsgString
                        .Substring(promptStart, promptEnd - promptStart).Split(':');

                    if (promptAndDefault.Length > 1)
                    {
                        newMsgRecord.Prompt = promptAndDefault[0].Trim();
                        newMsgRecord.DefaultValue = promptAndDefault[1].Trim();
                    }
                    else
                    {
                        newMsgRecord.DefaultValue = promptAndDefault[0];
                    }

                    var bPredicatePresent = false;
                    var predicateEnd = 0;
                    //Extract Predicate (if present)
                    if (originalMsgString.IndexOf('(', originalMsgString.IndexOf('}')) > -1)
                    {
                        bPredicatePresent = true;

                        var predicateStart = originalMsgString.IndexOf('(', originalMsgString.IndexOf('}')) + 1;
                        predicateEnd = originalMsgString.IndexOf(')', originalMsgString.IndexOf('}'));
                        var predicateLength = predicateEnd - predicateStart;

                        newMsgRecord.Predicate = originalMsgString.Substring(predicateStart, predicateLength);
                    }


                    var remainingLength = 0;
                    remainingLength = bPredicatePresent
                        ? originalMsgString.Length - predicateEnd - 1
                        : originalMsgString.Length - originalMsgString.IndexOf('}') - 2;

                    var typeDefinition = originalMsgString.Substring(originalMsgString.Length - remainingLength, remainingLength).Trim()
                        .Split(' ');

                    switch (typeDefinition.Length)
                    {
                        case 1 when typeDefinition[0] == "B":
                            newMsgRecord.DataType = "B";
                            break;
                        case 1 when typeDefinition[0] == "T":
                            newMsgRecord.DataType = "T";
                            break;
                        case 2 when typeDefinition[0] == "S":
                            newMsgRecord.DataType = "S";
                            newMsgRecord.MaxLength = int.Parse(typeDefinition[1]);
                            break;
                        case 2 when typeDefinition[0] == "T":
                            newMsgRecord.DataType = "T";
                            newMsgRecord.Description = typeDefinition[1];
                            break;
                        case 3 when typeDefinition[0] == "N":
                            newMsgRecord.DataType = "N";
                            newMsgRecord.MinValue = int.Parse(typeDefinition[1]);
                            newMsgRecord.MaxValue = int.Parse(typeDefinition[2]);
                            break;
                        case 3 when typeDefinition[0] == "S":
                            newMsgRecord.DataType = "S";
                            newMsgRecord.MaxLength = int.Parse(typeDefinition[1]);
                            newMsgRecord.Description = typeDefinition[2];
                            break;
                        case 3 when typeDefinition[0] == "L":
                            newMsgRecord.DataType = "L";
                            newMsgRecord.MinValue = int.Parse(typeDefinition[1]);
                            newMsgRecord.MaxValue = int.Parse(typeDefinition[2]);
                            break;
                        default:
                            throw new Exception("Unknown Type Definition in MSG File");
                    }
                }

                //These are mostly the ANSI/RIP Graphics
                if (currentLevel == EnumMsgFileLevel.LEVEL99)
                {
                        newMsgRecord.DataType = "T";
                        var valueStart = originalMsgString.IndexOf('{') + 1;
                        var valueEnd = originalMsgString.IndexOf('}');
                        var valueLength = valueEnd - valueStart;
                        var value = originalMsgString.Substring(valueStart, valueLength);
                        newMsgRecord.Value = value;
                }

                newMsgRecord.Ordinal = ordinal;
                bMsgRecordMultiline = false;
                output.Add(newMsgRecord);
                sbMsgRecord.Clear();
                ordinal++;
            }

            File.WriteAllText($"{_modulePath}{_moduleName}.json",JsonSerializer.Serialize(output, new JsonSerializerOptions() { WriteIndented = true}));
        }
    }
}
