using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

            if (LeagueURL == string.Empty)
            {
                WriteErrorFile("No eventID provided\nPlease provide an ID with the -lurl \"address\" parameter");
                return;
            }

            if (EventID <= 0)
            {
                WriteErrorFile("No eventID provided\nPlease provide an event ID with the -lid <number> parameter");
                return;
            }

            Console.WriteLine("Parsing league data from league ID " + EventID.ToString());

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

            rallyData.CalculateTimes();

            // Got the result, now lets sort it into the format we want!
            int numStages = 0;
            int numDrivers = 0;

            List<string> driverNames = new List<string>();
            List<string> driverVehicles = new List<string>();
            foreach (Stage a in rallyData)
            {
                numStages++;
                if (numDrivers == 0)
                {
                    foreach (KeyValuePair<string, DriverTime> b in a.DriverTimes)
                    {
                        driverNames.Add(b.Key);
                        driverVehicles.Add(b.Value.Vehicle);
                        numDrivers++;
                    }
                }
            }

            List<DriverTime>[] Times = new List<DriverTime>[numDrivers];
            for(int i = 0; i < numDrivers; i++)
                Times[i] = new List<DriverTime>();

            int driverIndex = 0;
            int stageIndex = 0;
            foreach (Stage a in rallyData)
            {
                driverIndex = 0;
                foreach (String b in driverNames) {
                    Times[driverIndex].Add(a.DriverTimes[b]);
                    driverIndex++;
                }
            }

            string outPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            System.IO.Directory.CreateDirectory(outPath + "\\DRLPResults\\");
            //+DateTime.Now.ToString("") + ".csv"
            System.IO.StreamWriter fout = new System.IO.StreamWriter(outPath + "\\DRLPResults\\DRLPRes_" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".csv");
            driverIndex = 0;
            foreach(string a in driverNames)
            {
                fout.Write(a + ",");
                fout.Write(driverVehicles[driverIndex] + ",");
                foreach (DriverTime t in Times[driverIndex])
                {
                    if(t != null)
                        fout.Write(t.CalculatedStageTime.TotalSeconds.ToString("0.000"));
                    fout.Write(",");
                }
                fout.Write("\n");
                driverIndex++;
            }
            fout.Close();
            
            return;
        }
    }
}
