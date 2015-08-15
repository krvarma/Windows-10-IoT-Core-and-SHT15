using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Gpio;

namespace sht15
{
    /// <summary>
    /// SHT15 Sensor Class
    /// </summary>
    class SHT15
    {
        private const double D1 = -40.0;  // for 14 Bit @ 5V
        private const double D2 = 0.01;  // for 14 Bit DEGC
        private const double D3 = 0.018;  // for 14 Bit DEGF

        private const double CC1 = -4.0;       // for 12 Bit
        private const double CC2 = 0.0405;    // for 12 Bit
        private const double CC3 = -0.0000028; // for 12 Bit
        private const double CT1 = 0.01;      // for 14 Bit @ 5V
        private const double CT2 = 0.00008;   // for 14 Bit @ 5V

        private GpioPin DataPin = null;
        private GpioPin SckPin = null;

        public SHT15(int dpn, int spn)
        {
            // Get GPIO Controller
            var gpio = GpioController.GetDefault();

            if (gpio == null)
            {
                Debug.WriteLine("Error opening GPIO");

                return;
            }

            // Get Pins
            DataPin = gpio.OpenPin(dpn);
            SckPin = gpio.OpenPin(spn);
        }

        // Read Raw Temperature
        public int ReadRawTemperature()
        {
            int TemperatureCommand = 3; // 00000011

            // Send Temperature Command
            SendSHTCommand(TemperatureCommand);

            // Wait for result
            WaitForResult();

            // Read interger value
            int Temperature = GetData16Bit();

            // Skip CRC
            SkipCRC();

            return Temperature;
        }

        public double ReadHumidity(double temperature)
        {
            int HumidityCommand = 5; // 00000101

            // Send Temperature Command
            SendSHTCommand(HumidityCommand);

            // Wait for result
            WaitForResult();

            // Read interger value
            int val = GetData16Bit();

            // Skip CRC
            SkipCRC();

            // Calculate Humiduty
            double LinearHumidity = CC1 + CC2 * val + CC3 * val * val;
            double CorrectedHumidity = (temperature - 25.0) * (CT1 + CT2 * val) + LinearHumidity;

            return CorrectedHumidity;
        }

        // Returns Temperature in C
        public double ReadTemperatureC()
        {
            return CalculateTemperatureC(ReadRawTemperature());
        }

        // Returns Temperature in F
        public double ReadTemperatureF()
        {
            return CalculateTemperatureF(ReadRawTemperature());
        }

        // Calculate Temperature in C
        public double CalculateTemperatureC(int Temperature)
        {
            return CalculateTemperature(Temperature, D2);
        }

        // Calculate Temperature in F
        public double CalculateTemperatureF(int Temperature)
        {
            return CalculateTemperature(Temperature, D3);
        }

        // Calculate Temperature in, if mult is D2 then in C, if mult is D3 then in F
        public double CalculateTemperature(int RawTemperature, double mult)
        {
            return (((double)RawTemperature * mult) + D1);
        }

        public double DewPoint(double celsius, double humidity)
        {
            // (1) Saturation Vapor Pressure = ESGG(T)
            double RATIO = 373.15 / (273.15 + celsius);
            double RHS = -7.90298 * (RATIO - 1);
            RHS += 5.02808 * Math.Log10(RATIO);
            RHS += -1.3816e-7 * (Math.Pow(10, (11.344 * (1 - 1 / RATIO))) - 1);
            RHS += 8.1328e-3 * (Math.Pow(10, (-3.49149 * (RATIO - 1))) - 1);
            RHS += Math.Log10(1013.246);

            // factor -3 is to adjust units - Vapor Pressure SVP * humidity
            double VP = Math.Pow(10, RHS - 3) * humidity;

            // (2) DEWPOINT = F(Vapor Pressure)
            double T = Math.Log(VP / 0.61078);   // temp var
            return (241.88 * T) / (17.558 - T);
        }

        // Cleanup
        public void Dispose()
        {
            DataPin.Dispose();
            SckPin.Dispose();
        }

        // Send SHT Command
        private void SendSHTCommand(int command)
        {
            // Set pin mode
            DataPinModeOutput();
            SckPinModeOutput();

            // Start Transmission
            DataHigh();
            SckHigh();
            DataLow();
            SckLow();
            SckHigh();
            DataHigh();
            SckLow();

            // Shift Out Send command 
            ShiftOut(command);

            // Set Data Pin Mode
            DataPinModeInput();

            // Check ACKs
            SckHigh();
            
            if (GpioPinValue.Low != ReadDataPin())
            {
                Debug.WriteLine("Error 1001");
            }

            SckLow();

            if (GpioPinValue.High != ReadDataPin())
            {
                Debug.WriteLine("Error 1002");
            }
        }

        // Set SCK Pin Mode Input
        private void SckPinModeInput()
        {
            SckPin.SetDriveMode(GpioPinDriveMode.Input);
        }

        // Set SCK Pin Mode Output
        private void SckPinModeOutput()
        {
            SckPin.SetDriveMode(GpioPinDriveMode.Output);
        }

        // Set Data Pin Mode Input
        private void DataPinModeInput()
        {
            DataPin.SetDriveMode(GpioPinDriveMode.Input);
        }

        // Set Data Pin Mode Output
        private void DataPinModeOutput()
        {
            DataPin.SetDriveMode(GpioPinDriveMode.Output);
        }

        // Read Data Pin
        private GpioPinValue ReadDataPin()
        {
            return DataPin.Read();
        }

        // Set SCK to High
        private void SckHigh()
        {
            SckPin.Write(GpioPinValue.High);
        }

        // Set SCK to Low
        private void SckLow()
        {
            SckPin.Write(GpioPinValue.Low);
        }

        // Set Data to High
        private void DataHigh()
        {
            DataPin.Write(GpioPinValue.High);
        }

        // Set Data to Low
        private void DataLow()
        {
            DataPin.Write(GpioPinValue.Low);
        }

        // Shit Out
        private void ShiftOut(long command)
        {
            DataPinModeOutput();
            SckPinModeOutput();

            long bit = 0;

            for (int i = 0; i < 8; ++i)
            {
                bit = (command & (1 << (7 - i)));

                if (bit == 0) DataLow();
                else DataHigh();

                SckHigh();
                SckLow();
            }
        }

        // Shift In
        private int ShiftIn(int bits)
        {
            DataPinModeInput();
            SckPinModeOutput();

            int RetVal = 0;

            GpioPinValue pinvalue = GpioPinValue.High;
            int val = 0;

            for (int i = 0; i < bits; ++i)
            {
                SckHigh();
                pinvalue = ReadDataPin();

                val = (pinvalue == GpioPinValue.High ? 1 : 0);

                RetVal = (RetVal * 2) + val;
                SckLow();
            }

            return RetVal;
        }

        // Wait until result is ready or max iteration reached
        private bool WaitForResult()
        {
            DataPinModeInput();

            GpioPinValue ack = GpioPinValue.High;

            bool RetVal = false;
            int i = 0;

            for (i = 0; i < 20000; ++i)
            {
                ack = DataPin.Read();

                if (GpioPinValue.Low == ack)
                {
                    RetVal = true;

                    break;
                }
            }

            Debug.WriteLine("Max Loop: " + i);

            return RetVal;
        }

        // Read 16bit integer value
        private int GetData16Bit()
        {
            // Set Pin Modes
            DataPinModeInput();
            SckPinModeOutput();

            int data = 0;

            // Read data
            data = ShiftIn(8);
            data *= 256;

            // Set Data Pin Mode
            DataPinModeOutput();

            DataHigh();
            DataLow();
            SckHigh();
            SckLow();

            DataPinModeInput();

            // Read Data
            data |= ShiftIn(8);

            return data;
        }

        // Skip CRC
        private void SkipCRC()
        {
            DataPinModeOutput();
            SckPinModeOutput();

            DataHigh();
            SckHigh();
            SckLow();
        }
    }
}
