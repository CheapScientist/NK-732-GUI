using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NK732TwoChannel
{
    public static class TIAController
    {
#if SIMULATION
        private static bool simulate = true;
#else
        private static bool simulate = false;
#endif
        private static bool isConnected = false;

        public static void SetSimulate(bool value) => simulate = value;

        public static void ConnectAndPrintInfo()
        {
            if (simulate)
            {
                Console.WriteLine("[Sim] Connecting to NK732...");
                Console.WriteLine("[Sim] Instrument Type: INST_TIASIM  Model: NK732-SIM");
                Console.WriteLine("[Sim] Serial Number: 0000001  FPGA Rev: 0x01");
                isConnected = true;
                return;
            }

            // Real hardware path (disabled until BiDrv.dll present)
            Sample06CS.OpenBiDriverAndInstruments();
            Sample06CS.PrintInstrumentInfo(null);
            isConnected = true;
        }

        public static void StartMeasurement(long numMeas)
        {
            if (!isConnected)
                throw new InvalidOperationException("Device not connected.");

            if (simulate)
            {
                Console.WriteLine($"[Sim] Started measurement with {numMeas} points...");
                var rand = new Random();
                for (int i = 0; i < 10; i++)
                {
                    var crA = 1000 + rand.NextDouble() * 20;
                    var crB = 980 + rand.NextDouble() * 20;
                    Console.WriteLine($"[Sim] CountRateA={crA:F2} Hz, CountRateB={crB:F2} Hz");
                    System.Threading.Thread.Sleep(100);
                }
                Console.WriteLine("[Sim] Acquisition complete.");
                return;
            }

            // Real hardware path here later
        }

        public static void Stop()
        {
            if (!isConnected) return;

            if (simulate)
            {
                Console.WriteLine("[Sim] Disconnected simulated device.");
                isConnected = false;
                return;
            }

            Sample06CS.CloseBiDriver();
            isConnected = false;
        }
    }
}
