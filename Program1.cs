using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NK732TwoChannel
{
	class Sample06CS
	{
		//***************************
		//	Declarations
		//***************************
		//This is a structure definition only.  It has all
		//the global variables associated with one TIA
		struct CountRate
        {
			public double count;
			public double curTime;
			public double count2;
			public double cr1;
			public double cr2;
        }
		struct TiaGlobals
		{
			public BiDrv.Inst.Tia Tia;                              //The instrument object
			public BiDrv.Inst.Tia.Settings InstSettings;            //The saved settings of the instrument
			public BiDrv.Meas.MeasTia.Settings MeasSettings;        //The saved settings of a measurement
			public BiDrv.Disp.TableTia.Settings TableSettings1;      //The saved settings of a table display
			public BiDrv.Disp.TableTia.Settings TableSettings2;      //The saved settings of a table display
			public BiDrv.Disp.TableTia TableDisp1;                   //The display object
			public BiDrv.Disp.TableTia TableDisp2;
		}

		//Structure definition for command line input parameters
		struct UserArgs
		{
			public BiDrv.Enums.DispFunc Function;   //The display function (which dictates the measurement function)
			public System.Int64 NumMeas;            //Number of measurement points to take
			public bool UseBeArming;                //True for By-Events, false for By-Time
			public double BeBtVal;                  //The By-Event count or By-Time time value
			public double Vth_A;                    //Threshold for Ch A (trigger level)
			public double Vth_B;                    //Threshold for Ch B (trigger level)
			public bool WriteToFile;                //Enable writing data to file
			public string BaseFile;                 //Name for file for data (includes path).  the program will
													//append a numeric index for each run and ".txt"
			public bool RunStreaming;               //Run in streaming mode
			public bool UseExtClk;                  //Use external 10 MHz clock for reference
			public bool RunQuietly;                 //Do not wait for keyboard input and do not display on screen
		}
		const int MinCmdArgs = 8;               //Minimum number of command line args expected
		const int MaxCmdArgs = 10;              //Maximum number of command line args expected
												//***************************
												//	Global Vars
												//***************************
												//Declare the global structures
		static TiaGlobals Glb = new TiaGlobals();
		static UserArgs CmdLineArgs = new UserArgs();
		static System.IO.StreamWriter FileStream;

		static void Main(string[] args)
		{
			//Table display result data class
			BiDrv.Disp.TableTia.TableTiaData DispTableResults1 = new BiDrv.Disp.TableTia.TableTiaData();
			BiDrv.Disp.TableTia.TableTiaData DispTableResults2 = new BiDrv.Disp.TableTia.TableTiaData();

			//Exit if not enough command line paramters
			if (ParseCommandLineArgs(args, out CmdLineArgs) >= MinCmdArgs)
			{
				WriteToConsole("Opening Driver and Instruments...");
				try
				{
					OpenBiDriverAndInstruments();
					//Display information about the instrument
					PrintInstrumentInfo(Glb.Tia);

					PrepareSetups();
					//Create table display
					Glb.TableDisp1 = Glb.Tia.Meas.AddTableTiaDisplay();

					// new
					Glb.TableDisp2 = Glb.Tia.Meas.AddTableTiaDisplay();

					//Write the instrument settings to the board
					Glb.Tia.InstSetup.Write(Glb.InstSettings);

					//Write the measurement setup to the board
					Glb.Tia.Meas.WriteSetup(Glb.MeasSettings);

					//Write the display setup to the board
					Glb.TableDisp1.WriteSetup(Glb.TableSettings1);
					Glb.TableDisp2.WriteSetup(Glb.TableSettings2);

					bool done = false;
					int fileIndex = 0;
					CountRate crObj = new CountRate();
					crObj.count = 0;
					crObj.curTime = DateTime.Now.Ticks;
					crObj.count2 = 0;
					crObj.cr1 = 0;
					crObj.cr2 = 0;
					while (!done)
					{
						WriteToConsole("Acquiring...");
						string fullName1 = CmdLineArgs.BaseFile + 'A' + fileIndex.ToString("D4") + ".txt"; //Complete file name
						string fullName2 = CmdLineArgs.BaseFile + 'B' + fileIndex.ToString("D4") + ".txt";
						//The streaming mode requires only a small amount of memory regardless of
						//the size of the acquisition.  When the data size is greater than the size
						//of the memory on the board you must read the results out fast enough to
						//avoid a memory overflow.  You can use the streaming mode for small amounts
						//of data also.
						if (CmdLineArgs.RunStreaming)
						{
							//Run in streaming mode to continuously read from the instrument and write
							//to the console and the file

							//Start instrument
							Glb.Tia.Start();

							//Loop for streaming results
							bool measComplete = false;
							System.Int64 measPtAcq1 = 0;         //Save the number of meas points acquired so far
							System.Int64 measPtAcq2 = 0;
							while (!measComplete)
							{

								Glb.Tia.ReadToBuffer(); //Read some more data out of the instrument
								DispTableResults1 = Glb.TableDisp1.Read(true);    //Process data for this display
								DispTableResults2 = Glb.TableDisp2.Read(true);    //Process data for this display
								measComplete = DispTableResults1.ProcComplete && DispTableResults2.ProcComplete;

								//Check if there is new data 
								if (DispTableResults1.Data.ColumnLengths.End.MeasPtCount != measPtAcq1 || DispTableResults2.Data.ColumnLengths.End.MeasPtCount != measPtAcq2)
								{
									//ColumnLengths.Start.MeasPtCount is the measurement number at the beginneing
									//of the array, while ColumnLengths.End.MeasPtCount is the last measurement in the array
									measPtAcq1 = DispTableResults1.Data.ColumnLengths.End.MeasPtCount;
									measPtAcq2 = DispTableResults2.Data.ColumnLengths.End.MeasPtCount;

									//Display resutls and write to file
									crObj = WriteResultsToFileAndConsole(fullName1, DispTableResults1, DispTableResults2, crObj);
								}
							}
						}
						else
						{
							//Run a measurement (this call returns only when acquisition is complete)
							bool timeoutErr1 = Measure1(out DispTableResults1);
							bool timeoutErr2 = Measure2(out DispTableResults2);
							if (timeoutErr1)
							{
								System.Console.WriteLine("Channel A Timeout error");
								done = true;
							}
							else
							{
								//Display resutls and write to file
								crObj = WriteResultsToFileAndConsole(fullName1, DispTableResults1, DispTableResults2, crObj);
							}
							if (timeoutErr2)
							{
								System.Console.WriteLine("Channel B Timeout error");
								done = true;
							}
							else
							{
								//Display resutls and write to file
								crObj = WriteResultsToFileAndConsole(fullName2, DispTableResults2, DispTableResults1, crObj);
							}
						}
						fileIndex++;

						WriteToConsole("");
						if (WaitForKey("Press SPACE to exit, any other key to repeat") == ' ') done = true;
					}
				}
				catch (System.ApplicationException ex)
				{
					System.Console.WriteLine(ex.Message);
					WriteToConsole("");
					WaitForKey("Press any key to exit");
				}

				CloseBiDriver();
			}
			else
			{
				WaitForKey("Press any key to exit");
			}
		}
		//***************************
		//	Open Driver And Instruments
		//***************************
		public static void OpenBiDriverAndInstruments()
		{
			//Open the driver itself
			BiDrv.Driver.Open(BiDrv.Enums.DriverOpenOptions.USEPCI);

			//Use the first TIA found by the driver
			//Real instruments are listed first, followed by simulated ones
			for (int index = 0; index < BiDrv.Driver.InstrumentList.Length; index++)
			{
				if ((BiDrv.Driver.InstrumentList[index].Type == BiDrv.Enums.InstType.INST_TIA) ||
					(BiDrv.Driver.InstrumentList[index].Type == BiDrv.Enums.InstType.INST_TIASIM))
				{
					//There must be at least a simulated instrument
					Glb.Tia = new BiDrv.Inst.Tia(BiDrv.Driver.InstrumentList[index]);
					break;
				}
			}
		}
		static void PrepareSetups()
		{
			//This function prepares instrument, measurement, and display setups for use later.  This makes it easy to
			//reuse settings and is good for documentation
			//Set to external clock if requested
			Glb.InstSettings = new BiDrv.Inst.Tia.Settings();
			if (CmdLineArgs.UseExtClk)
			{
				Glb.InstSettings.ClockSource = BiDrv.Enums.ClockSource.CLK_SRC_EXT;
			}
			else
			{
				Glb.InstSettings.ClockSource = BiDrv.Enums.ClockSource.CLK_SRC_INT;
			}

			//Measurement setup
			Glb.MeasSettings = new BiDrv.Meas.MeasTia.Settings();
			Glb.MeasSettings.Vth.A0 = CmdLineArgs.Vth_A;            //VthA0 applies in all functions.  Vth.A1 is for riseTime and Falltime functions
			Glb.MeasSettings.Vth.B0 = CmdLineArgs.Vth_B;

			Glb.MeasSettings.NumBlocks = 1;                         //This example always runs one block
			Glb.MeasSettings.MeasPerBlock = CmdLineArgs.NumMeas;

			if (CmdLineArgs.UseBeArming)
			{
				Glb.MeasSettings.Sa.Mode = BiDrv.Enums.TiaStartArmMode.SA_BY_EVENTS; //Start Arm mode
				Glb.MeasSettings.Sa.BeCount = (uint)CmdLineArgs.BeBtVal;                        //The number of cycles between timetags in BY_EVENTS mode for Start Arm
			}
			else
			{
				Glb.MeasSettings.Sa.Mode = BiDrv.Enums.TiaStartArmMode.SA_BY_TIME;
				Glb.MeasSettings.Sa.Time = CmdLineArgs.BeBtVal;
			}
			// newly added
			Glb.MeasSettings.OtherChEnb = true;
			Glb.MeasSettings.OtherCh.Sa.Mode = BiDrv.Enums.TiaStartArmMode.SA_BY_EVENTS;
			Glb.MeasSettings.OtherCh.Sa.BeCount = 1;
			Glb.MeasSettings.OtherCh.MeasPerBlock = Glb.MeasSettings.MeasPerBlock;
			//Display setup

			Glb.TableSettings1 = new BiDrv.Disp.TableTia.Settings();
			Glb.TableSettings1.StreamingMode = CmdLineArgs.RunStreaming;
			Glb.TableSettings1.Func = CmdLineArgs.Function;
			Glb.TableSettings1.ComputeMeasPtData = true;     //Request measurement result data array
			Glb.TableSettings1.ComputeStTimeRtData = false;   //Request start times array (Real Time format)
			Glb.TableSettings1.ComputeStEventData = true;    //Request start event array
			Glb.TableSettings1.BaseTime = BiDrv.Enums.TiaBaseTime.FIRST_BLOCK_ARM;

			Glb.TableSettings2 = new BiDrv.Disp.TableTia.Settings();
			Glb.TableSettings2.StreamingMode = CmdLineArgs.RunStreaming;

			Glb.TableSettings2.StartCh = BiDrv.Enums.Chan.CH_B;
			Glb.TableSettings2.Func = CmdLineArgs.Function;
			Glb.TableSettings2.ComputeMeasPtData = true;     //Request measurement result data array
			Glb.TableSettings2.ComputeStTimeRtData = false;   //Request start times array (Real Time format)
			Glb.TableSettings2.ComputeStEventData = true;    //Request start event array
			Glb.TableSettings2.BaseTime = BiDrv.Enums.TiaBaseTime.FIRST_BLOCK_ARM;


			//Disable all other arrays
			Glb.TableSettings1.ComputeBaTimeData = false;    //Block arm times
			Glb.TableSettings1.ComputeBlockStatsData = false;    //Block statistics
			Glb.TableSettings1.ComputeRawData = false;       //Raw data (for diagnostics
			Glb.TableSettings1.ComputeSpTimeData = false;    //Stop times (double)
			Glb.TableSettings1.ComputeSpTimeRtData = false;  //Stop times (Real Time format)
			Glb.TableSettings1.ComputeStTimeData = true;    //Start time (double)

			Glb.TableSettings2.ComputeBaTimeData = false;    //Block arm times
			Glb.TableSettings2.ComputeBlockStatsData = false;    //Block statistics
			Glb.TableSettings2.ComputeRawData = false;       //Raw data (for diagnostics
			Glb.TableSettings2.ComputeSpTimeData = false;    //Stop times (double)
			Glb.TableSettings2.ComputeSpTimeRtData = false;  //Stop times (Real Time format)
			Glb.TableSettings2.ComputeStTimeData = true;    //Start time (double)
		}
		static bool Measure1(out BiDrv.Disp.TableTia.TableTiaData Results1)
		{
			bool timeoutErr;

			//Start instrument
			Glb.Tia.Start();

			//Wait for complete results
			double timeoutTime = 0.0;   //Set to a number other than 0.0 for a timeout time for the whole acquisition
			Results1 = Glb.TableDisp1.Results(timeoutTime, out timeoutErr);

			return timeoutErr;
		}
		static bool Measure2(out BiDrv.Disp.TableTia.TableTiaData Results2)
		{
			bool timeoutErr;

			//Start instrument
			Glb.Tia.Start();

			//Wait for complete results
			double timeoutTime = 0.0;   //Set to a number other than 0.0 for a timeout time for the whole acquisition
			Results2 = Glb.TableDisp2.Results(timeoutTime, out timeoutErr);

			return timeoutErr;
		}
		static CountRate WriteResultsToFileAndConsole(string fileName, BiDrv.Disp.TableTia.TableTiaData Results, BiDrv.Disp.TableTia.TableTiaData Results2, CountRate cr)
		{
			string lineOut;
			string na = "N/A";
			double deltat = 10000000; // deltat times 100 ns is the real time diff e.g 10 means 1 us
            // in C sharp datatime.now.tick, one tick is 100 ns. So to set deltat
            // to 0.1s, set deltat = 1000000; 
            if (CmdLineArgs.WriteToFile)
			{
				//In streaming mode, start a new file only on first data (start value on column lengths is 0 and
				//end value is non-zero
				if (!CmdLineArgs.RunStreaming ||
					(CmdLineArgs.RunStreaming && (Results.Data.ColumnLengths.Start.MeasPtCount == 0) &&
					(Results.Data.ColumnLengths.End.MeasPtCount != 0)))
				{
					//Open a file.  Use ASCII encoding (8-bit standard text)
					FileStream = new System.IO.StreamWriter(fileName, false, System.Text.Encoding.ASCII);

					//Start a new line on screen
					WriteToConsole("");
					//Show on console and save data to file (arrrays of start times and event data were enabled in the display setup
					lineOut = "     CountRate_A,          StartTime_A,   StartEvent_A,     CountRate_B,          StartTime_B,   StartEvent_B";
					WriteToConsole(lineOut);
					FileStream.WriteLine(lineOut);
				}
			}

			System.Int64 dataLength = 0;
			System.Int64 dataLength2 = 0;
			double now = DateTime.Now.Ticks;
			double timebefore = cr.curTime;
			double countbefore = cr.count;
			double count2before = cr.count2;
			double countrate1 = cr.cr1;
			double countrate2 = cr.cr2;
			// delta t = 100 ms; when printing data
			if (!CmdLineArgs.RunStreaming)
			{
				//In non-streaming mode this method is called only after the data is coplete.
				//Check that the data arrays are available.  There must be the same number of
				//start times and start events as measurement points
				dataLength = Results.Data.MeasPt.Length;    //Display and save all data
				if ((dataLength != Results.Data.StTime.Length) ||
					 (dataLength != Results.Data.StEvent.Length))
				{
					dataLength = 0;
				}
				dataLength2 = Results2.Data.MeasPt.Length;    //Display and save all data
				if ((dataLength2 != Results2.Data.StTime.Length) ||
					 (dataLength2 != Results2.Data.StEvent.Length))
				{
					dataLength2 = 0;
				}
			}
			else
			{
				//In non-streaming mode the StartCount is always 0 and the EndCount represents the count
				//of the data processed so far.
				//In streaming mode the StartCount is the last EndCount and
				//EndCount minus Start count is the quantity of data.
				//For example, in streaming mode, if the first 5 measurements are processed
				//the StartCount would be 0 and the EndCount would be 5.  If 3 more
				//measurements are proceesed before the next read, the StartCount
				//would be 5 and the EndCount would be 8.  The valid ranges
				//of data would be Measurement[0] to Measurment[4], then
				//Measurement[5] to Measurement[7].
				dataLength = Results.Data.ColumnLengths.End.MeasPtCount - Results.Data.ColumnLengths.Start.MeasPtCount;
				//Check that there is data for StTimeRt and for StEvent
				if (Results.Data.ColumnLengths.End.StTimeCount != Results.Data.ColumnLengths.End.MeasPtCount)
				{
					dataLength = 0;
				}
				if (Results.Data.ColumnLengths.End.StEventCount != Results.Data.ColumnLengths.End.MeasPtCount)
				{
					dataLength = 0;
				}
				dataLength2 = Results2.Data.ColumnLengths.End.MeasPtCount - Results2.Data.ColumnLengths.Start.MeasPtCount;
				//Check that there is data for StTimeRt and for StEvent
				if (Results2.Data.ColumnLengths.End.StTimeCount != Results2.Data.ColumnLengths.End.MeasPtCount)
				{
					dataLength2 = 0;
				}
				if (Results2.Data.ColumnLengths.End.StEventCount != Results2.Data.ColumnLengths.End.MeasPtCount)
				{
					dataLength2 = 0;
				}
			}
			if (dataLength > dataLength2)
            {
				for (System.Int64 i = 0; i < dataLength; i++)
                {
					
					if (i > dataLength2)
                    {

						lineOut = string.Format("{0:e5},{1:e15},{2},{3},{4},{5}",
							countrate1,
							Results.Data.StTime[i],      //Parameter is digits after decimal
							Results.Data.StEvent[i],
							na,
							na,
							na);

						if (now - timebefore > deltat)
						{
							countrate1 = (Results.Data.StEvent[i] - countbefore);
							cr.curTime = now;
							cr.count = Results.Data.StEvent[i];
							cr.cr1 = countrate1;
							WriteToConsole(lineOut);
						}
						if (CmdLineArgs.WriteToFile) FileStream.WriteLine(lineOut);
					}
					else
                    {
						lineOut = string.Format("{0:e5},{1:e15},{2},{3:e5},{4:e15},{5}",
							countrate1,
							Results.Data.StTime[i],      //Parameter is digits after decimal
							Results.Data.StEvent[i],
							countrate2,
							Results2.Data.StTime[i],
							Results2.Data.StEvent[i]);
						if (now - timebefore > deltat)
						{
							countrate1 = (Results.Data.StEvent[i] - countbefore);
							countrate2 = (Results2.Data.StEvent[i] - count2before);
							cr.curTime = now;
							cr.count = Results.Data.StEvent[i];
							cr.cr1 = countrate1;
							cr.count2 = Results2.Data.StEvent[i];
							cr.cr2 = countrate2;
							WriteToConsole(lineOut);
						}

						if (CmdLineArgs.WriteToFile) FileStream.WriteLine(lineOut);
					}
                }
            }
			else
            {
				for (System.Int64 i = 0; i < dataLength2; i++)
				{

					if (i > dataLength)
					{
						lineOut = string.Format("{0},{1},{2},{3:e5},{4:e15},{5}",
							na,
							na,
							na,
							countrate2,
							Results2.Data.StTime[i],      //Parameter is digits after decimal
							Results2.Data.StEvent[i]);
						if (now - timebefore > deltat)
						{
							countrate2 = (Results2.Data.StEvent[i] - count2before);
							cr.curTime = now;
							cr.count2 = Results2.Data.StEvent[i];
							cr.cr2 = countrate2;
							WriteToConsole(lineOut);
						}

						if (CmdLineArgs.WriteToFile) FileStream.WriteLine(lineOut);
					}
					else
					{
						lineOut = string.Format("{0:e5},{1:e15},{2},{3:e5},{4:e15},{5}",
							countrate1,
							Results.Data.StTime[i],      //Parameter is digits after decimal
							Results.Data.StEvent[i],
							countrate2,
							Results2.Data.StTime[i],
							Results2.Data.StEvent[i]);
						if (now - timebefore > deltat)
						{
							countrate1 = (Results.Data.StEvent[i] - countbefore);
							countrate2 = (Results2.Data.StEvent[i] - count2before);
							cr.curTime = now;
							cr.count = Results.Data.StEvent[i];
							cr.count2 = Results2.Data.StEvent[i];
							cr.cr1 = countrate1;
							cr.cr2 = countrate2;
							WriteToConsole(lineOut);
						}

						if (CmdLineArgs.WriteToFile) FileStream.WriteLine(lineOut);
					}
				}

			}

			if (Results.ProcComplete && Results2.ProcComplete)
			{
				if (CmdLineArgs.WriteToFile) FileStream.Close();
			}
			return cr;
		}
		//***************************
		public static void CloseBiDriver()
		{
			BiDrv.Driver.Close();
		}
		//***************************
		//	Parse Command Line Arguments
		//***************************
		static int ParseCommandLineArgs(string[] Args, out UserArgs OutArgs)
		{
			int len = Args.Length;
			if (len > MaxCmdArgs) len = MaxCmdArgs;

			//Default values (never really used)
			OutArgs.Function = BiDrv.Enums.DispFunc.TIA_FREQ_AVG;
			OutArgs.NumMeas = 10;
			OutArgs.UseBeArming = true;
			OutArgs.BeBtVal = 1.0;
			OutArgs.Vth_A = 0.0;
			OutArgs.Vth_B = 0.0;
			OutArgs.WriteToFile = false;
			OutArgs.BaseFile = "";
			OutArgs.RunStreaming = false;
			OutArgs.UseExtClk = false;
			OutArgs.RunQuietly = false;

			if (len < MinCmdArgs)
			{
				//Not enough arguments - display message
				System.Console.WriteLine(@"This program takes the specified measurements on Ch A (or Ch A and B) and");
				System.Console.WriteLine(@"saves them to files");
				System.Console.WriteLine();
				System.Console.WriteLine(@"USAGE:");
				System.Console.WriteLine(@" Sample06CS <Func> <NumMeas> <BeBt> <BeBtVal> <Vth_A> <Vth_B>");
				System.Console.WriteLine(@"           <BaseFile> <Stream> [ExtClk] [Quite]");
				System.Console.WriteLine(@" <Func>      Measurement function: freq_avg, per_avg, cti, ti1, ti2, per, pw");
				System.Console.WriteLine(@" <NumMeas>   Number of measurements to take");
				System.Console.WriteLine(@" <BeBt>      be for By-Events arming, bt for By-Time arming");
				System.Console.WriteLine(@" <BeBtVal>   Number of events for By-Events, the time for By-Time (seconds)");
				System.Console.WriteLine(@" <Vth_A>     Threshold for Channel A");
				System.Console.WriteLine(@" <Vth_B>     Threshold for Channel B (required even if unused)");
				System.Console.WriteLine(@" <BaseFile>  Base file name (use '-' to not write to a file).  Program adds an");
				System.Console.WriteLine(@"                       index number and a .TXT. Destination folder must exist");
				System.Console.WriteLine(@" <Stream>    If 1 run in streaming mode");
				System.Console.WriteLine(@" [ExtClk]    Optional.  If 1 use external 10MHz reference");
				System.Console.WriteLine(@" [Quite]     Optional.  If 1 run quitely (no display or keyboard input, but");
				System.Console.WriteLine(@"                       still writes to file)");
				System.Console.WriteLine();
				System.Console.WriteLine(@" Example:  Sample06CS ti1 1000 bt 1e-6 1.25 1.25 C:\test\TiData 1 1 0");
			}
			else
			{
				if (Args[0].ToLower() == "freq_avg") OutArgs.Function = BiDrv.Enums.DispFunc.TIA_FREQ_AVG;
				if (Args[0].ToLower() == "per_avg") OutArgs.Function = BiDrv.Enums.DispFunc.TIA_PER_AVG;
				if (Args[0].ToLower() == "cti") OutArgs.Function = BiDrv.Enums.DispFunc.TIA_CTI;
				if (Args[0].ToLower() == "ti1") OutArgs.Function = BiDrv.Enums.DispFunc.TIA_TI1;
				if (Args[0].ToLower() == "ti2") OutArgs.Function = BiDrv.Enums.DispFunc.TIA_TI2;
				if (Args[0].ToLower() == "per") OutArgs.Function = BiDrv.Enums.DispFunc.TIA_PER;
				if (Args[0].ToLower() == "pw") OutArgs.Function = BiDrv.Enums.DispFunc.TIA_PW;

				OutArgs.NumMeas = System.Convert.ToInt64(Args[1]);

				if (Args[2].ToLower() == "be") OutArgs.UseBeArming = true;
				else OutArgs.UseBeArming = false;

				OutArgs.BeBtVal = System.Convert.ToDouble(Args[3]);

				OutArgs.Vth_A = System.Convert.ToDouble(Args[4]);
				OutArgs.Vth_B = System.Convert.ToDouble(Args[5]);
				if (Args[6] != "-")
				{
					OutArgs.WriteToFile = true;
					OutArgs.BaseFile = Args[6];
				}
				else
				{
					OutArgs.WriteToFile = false;
				}

				if (len > 7)
				{
					if (Args[7] == "1")
					{
						OutArgs.RunStreaming = true;
					}
				}
				else
				{
					OutArgs.RunStreaming = false;
				}

				if (len > 8)
				{
					if (Args[8] == "1")
					{
						OutArgs.UseExtClk = true;
					}
				}
				else
				{
					OutArgs.UseExtClk = false;
				}

				if (len > 9)
				{
					if (Args[9] == "1")
					{
						OutArgs.RunQuietly = true;
					}
				}
				else
				{
					OutArgs.RunQuietly = false;
				}
			}

			return len;
		}
		//***************************
		//	Print Details of Instrument
		//***************************
		public static void PrintInstrumentInfo(BiDrv.Inst.Tia tia)
		{
			string type = "Instrument Type: " + tia.MyId.Type.ToString() + " Instrument Model: " + tia.MyId.Model;
			WriteToConsole(type);

			string fpgaRev;
			if (tia.MyId.Model == BiDrv.Enums.InstModel.NK732)
			{
				fpgaRev = tia.MyId.FPGA_Rev.ToString();     //Decimal
			}
			else
			{
				fpgaRev = tia.MyId.FPGA_Rev.ToString("X");  //Hex
			}
			string serial = "Serial Number: " + tia.MyId.SerialNum + " FPGA Rev: " + fpgaRev; ;
			WriteToConsole(serial);

			switch (tia.MyId.Interface)
			{
				case BiDrv.Enums.Interface.SIM:
					WriteToConsole("Found only a simulated instrument");
					break;
				case BiDrv.Enums.Interface.PCI:
					string pci = "PCI Bus Number: " + tia.MyId.PciBusNum + " PCI Slot: " + tia.MyId.PciSlotNum;
					WriteToConsole(pci);
					break;
				default:
					break;
			}
		}
		//***************************
		//	Wait for Key
		//***************************
		static char WaitForKey(string LineToConsole)
		{
			if (!CmdLineArgs.RunQuietly)
			{
				System.Console.WriteLine(LineToConsole);
				System.ConsoleKeyInfo key = System.Console.ReadKey();
				return key.KeyChar;
			}
			else return ' ';    //Space character
		}
		//***************************
		//	Write to Console
		//***************************
		static void WriteToConsole(string ConsoleStr)
		{
			if (!CmdLineArgs.RunQuietly)
			{
				System.Console.WriteLine(ConsoleStr);
			}
		}

	}
}
