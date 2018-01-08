using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using DRNumberCrunchers;
using DRLPTest;

namespace DRLPCommandLine
{
    class DRPLConsoleProgram
    {
        static private Rally rallyData = new Rally();
        static private RacenetApiParser racenetApiParser = new RacenetApiParser();

        static void WriteErrorFile(string error)
        {
            string outPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            System.IO.Directory.CreateDirectory(outPath + "\\DRLPResults\\");
            System.IO.StreamWriter fout = new System.IO.StreamWriter(outPath + "\\DRLPResults\\DRLPErr_" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".txt");
            fout.Write(error);
            fout.Close();
            Console.WriteLine(error);
        }

        static void Main(string[] args)
        {
            int EventID = int.MinValue;
            string LeagueURL = string.Empty;
            for (int i = 0; i < args.Length; i += 2)
            {
                if (args[i] == "-lurl")
                    LeagueURL = args[i + 1].Trim();

                if (args[i] == "-eid")
                    EventID = int.Parse(args[i + 1].Trim());
            }
            // TODO: print a full "usage" message if no arguments provided, or problems with the arguments
            /*if (LeagueURL == string.Empty)
            {
                WriteErrorFile("No eventID provided\nPlease provide an ID with the -lurl \"address\" parameter");
                return;
            }*/

            if (EventID <= 0)
            {
                WriteErrorFile("No eventID provided\nPlease provide an event ID with the -eid <number> parameter");
                return;
            }

            Console.WriteLine("Parsing data from event ID " + EventID.ToString());

            rallyData = new Rally();

            var progress = new Progress<int>(stagesProcessed =>
            {
                Console.WriteLine("Fetching data from Racenet... " + stagesProcessed + " stages retrieved");
            });

            var getDataTask = Task<Rally>.Factory.StartNew(() => racenetApiParser.GetRallyData(LeagueURL, EventID.ToString(), progress));
            //await getDataTask;
            rallyData = getDataTask.Result;
            if (rallyData == null)
            {
                WriteErrorFile("Failed to get data from Racenet");
                return;
            }

            //OK, don't bother with CalculateTimes() any more, as I've decided to just dump the (more raw) CalculatedOverallTime for each stage
            //rallyData.CalculateTimes();

            // Got the result, now lets sort it into the format we want!

            // original code basically uses three loops (!) to output the list of stage times for each driver (one driver per line):
            // first loop runs through the stages and net result is: number of stages and drivers are determined; driverNames & Vehicles (+ PlayerIDs now)
            // are extracted
            // second loop runs through stages and extracts the per-driver times to driver-specific DriverTime lists
            // third loop runs through the drivers and prints names, etc., then all stage times for that driver on one line
            // NB: the sequence of lines in the output file has no relation to the stage or overall times as far as I can see

            // want to replace this with one loop if poss, with output being similar (one driver per line) but including PlayerID and ProfileURL,
            // and also with sorting by overall rally time:
            // take final stage data, sort it, get the keys (PlayerIDs) from that sorted list
            // iterate over the sorted PlayerIDs and print required driver data + overall times from all stages 

            // Get the final stage data for the rally 

            var StageEnum = ((IEnumerable)rallyData).GetEnumerator();

            // OK, massive argh, not loving C# right now - can't find a way to do this cleanly. Only the Generic Lists have a Last() method,
            // so I have to do stupid crap like advancing the enumerator several times to get to the last stage. Beyond crap. The only other option
            // I could see was to make the stage data public (currently private). Basically I'm a C# noob and plan to keep it that way after this
            // experience.
            int argh;
            for (argh = 0; argh < rallyData.StageCount; argh++) StageEnum.MoveNext();
            //WriteErrorFile(argh.ToString());
            Stage finalStage = (Stage)StageEnum.Current;

            // Now sort it by overall time (pinched various bits of the code below from printOverallTimes() in DRParserTest.xaml.cs)
            List<KeyValuePair<int, DriverTime>> sortedFinalStageData = finalStage.DriverTimes.ToList();

            sortedFinalStageData.Sort((x, y) =>
            {
                if (x.Value != null && y.Value == null)
                    return -1;
                else if (x.Value == null && y.Value != null)
                    return 1;
                else if (x.Value == null && y.Value == null)
                    return 0;
                else
                    return x.Value.CalculatedOverallTime.CompareTo(y.Value.CalculatedOverallTime);
            });

            // And finally, iterate over all drivers and generate our output file
            string outPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            System.IO.Directory.CreateDirectory(outPath + "\\DRLPResults\\");
            System.IO.StreamWriter fout = new System.IO.StreamWriter(outPath + "\\DRLPResults\\DRLPRes_" + EventID.ToString() + "_" +
                DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".csv");

            // Output a header line, so next stage of processing has names for all fields
            fout.Write("PlayerID, DriverName, ProfileURL, Vehicle");
            for (int i = 0; i < rallyData.StageCount; i++) fout.Write(", " + (i+1).ToString());
            fout.WriteLine();

                foreach (KeyValuePair<int, DriverTime> driverTimeKvp in sortedFinalStageData)
            {
                var driverPlayerID = driverTimeKvp.Key;
                var driverTime = driverTimeKvp.Value;

                // a driver may not have a time on the final stage - if that's the case, work backwards through the stages until we
                // find a time, so we can choose the most recent DriverName used... ARGH, no, can't do that - IEnumerators don't have a
                // MovePrev() method, so stuff it, we can just work forwards through the stages until we get some driver data :-/
                StageEnum.Reset();
                while (driverTime == null)
                {
                    StageEnum.MoveNext();
                    Stage tempStage = (Stage)StageEnum.Current;
                    tempStage.DriverTimes.TryGetValue(driverPlayerID, out driverTime);
                }

                // generate the driver info
                var line =
                        driverTime.PlayerID + "," +
                        "\"" + driverTime.DriverName + "\"" + "," +
                        driverTime.ProfileURL + "," +
                        driverTime.Vehicle;

                // and now all stage times
                foreach (Stage stage in rallyData)
                {
                    line += ",";
                    DriverTime stageTime = stage.DriverTimes[driverPlayerID];
                    if (stageTime != null) line += stageTime.CalculatedOverallTime.TotalSeconds.ToString("0.000");
                }
                line += "\n";

                // and write the output to the file
                fout.Write(line);
            }

            fout.Close();
            Console.WriteLine("Output written");
            return;
        }
    }
}
