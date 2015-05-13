
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace sht15
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const int DATA_PIN = 24;
        private const int SCK_PIN = 23;

        // Timer
        private DispatcherTimer ReadSensorTimer;
        // SHT15 Sensor
        private SHT15 sht15 = null;

        // Sensor values
        private static double TemperatureC = 0.0;
        private static double TemperatureF = 0.0;
        private static double Humidity = 0.0;
        private static double CalculatedDewPoint = 0.0;
        
        public MainPage()
        {
            this.InitializeComponent();
            
            // Start Timer every 3 seconds
            ReadSensorTimer = new DispatcherTimer();
            ReadSensorTimer.Interval = TimeSpan.FromMilliseconds(5000);
            ReadSensorTimer.Tick += Timer_Tick;
            ReadSensorTimer.Start();

            Unloaded += MainPage_Unloaded;

            InitializeSensor(DATA_PIN, SCK_PIN);

            // Initialize and Start HTTP Server
            HttpServer WebServer = new HttpServer();

            var asyncAction = ThreadPool.RunAsync((w) => { WebServer.StartServer(); });
        }

        private void MainPage_Unloaded(object sender, object args)
        {
            // Cleanup Sensor
            sht15.Dispose();
        }

        // Timer Routine
        private void Timer_Tick(object sender, object e)
        {
            // Read Raw Temperature and Humidity
            int RawTemperature = sht15.ReadRawTemperature();

            TemperatureC = sht15.CalculateTemperatureC(RawTemperature);
            TemperatureF = sht15.CalculateTemperatureF(RawTemperature);
            Humidity = sht15.ReadHumidity(TemperatureC);
            CalculatedDewPoint = sht15.DewPoint(TemperatureC, Humidity);

            Debug.WriteLine("Temperature: " + TemperatureC + " C, " + TemperatureF + " F, " + "Humidity: " + Humidity + ", Dew Point: " + CalculatedDewPoint);
        }

        private void InitializeSensor(int datapin, int sckpin)
        {
            sht15 = new SHT15(DATA_PIN, SCK_PIN);
        }

        // Http Server class
        public sealed class HttpServer : IDisposable
        {
            private const uint bufLen = 8192;
            private int defaultPort = 8080;
            private readonly StreamSocketListener sock;

            public object[] TimeStamp { get; private set; }

            public HttpServer()
            {
                sock = new StreamSocketListener();

                sock.ConnectionReceived += (s, e) => ProcessRequestAsync(e.Socket);
            }

            public async void StartServer()
            {
                await sock.BindServiceNameAsync(defaultPort.ToString());
            }

            private async void ProcessRequestAsync(StreamSocket socket)
            {
                // Read in the HTTP request, we only care about type 'GET'
                StringBuilder request = new StringBuilder();
                using (IInputStream input = socket.InputStream)
                {
                    byte[] data = new byte[bufLen];
                    IBuffer buffer = data.AsBuffer();
                    uint dataRead = bufLen;
                    while (dataRead == bufLen)
                    {
                        await input.ReadAsync(buffer, bufLen, InputStreamOptions.Partial);
                        request.Append(Encoding.UTF8.GetString(data, 0, data.Length));
                        dataRead = buffer.Length;
                    }
                }

                using (IOutputStream output = socket.OutputStream)
                {
                    string requestMethod = request.ToString().Split('\n')[0];
                    string[] requestParts = requestMethod.Split(' ');
                    await WriteResponseAsync(requestParts, output);
                }
            }

            private async Task WriteResponseAsync(string[] requestTokens, IOutputStream outstream)
            {
                // Content body
                string respBody = string.Format(@"<html>
                                                    <head>
                                                        <title>SHT15 Sensor Values</title>
                                                        <meta http-equiv='refresh' content='3' />
                                                    </head>
                                                    <body>
                                                        <p><font size='6'><b>Windows 10 IoT Core and SHT15 Sensor</b></font></p>
                                                        <hr/>
                                                        <br/>
                                                        <table>
                                                            <tr>
                                                                <td><font size='3'>Time</font></td>
                                                                <td><font size='3'>{0}</font></td>
                                                            </tr>
                                                            <tr>
                                                                <td><font size='5'>Temperature</font></td>
                                                                <td><font size='6'><b>{1}&deg;C</b></font></td>
                                                            </tr>
                                                            <tr>
                                                                <td><font size='5'>Temperature</font></td>
                                                                <td><font size='6'><b>{2}F</b></font></td>
                                                            </tr>
                                                            <tr>
                                                                <td><font size='5'>Humidity</font></td>
                                                                <td><font size='6'><b>{3}%</b></font></td>
                                                            </tr>
                                                            <tr>
                                                                <td><font size='5'>Dew Point</font></td>
                                                                <td><font size='6'><b>{4}&deg;C</b></font></td>
                                                            </tr>
                                                        </table>
                                                    </body>
                                                  </html>",

                                                DateTime.Now.ToString("h:mm:ss tt"),
                                                String.Format("{0:0.00}", TemperatureC),
                                                String.Format("{0:0.00}", TemperatureF),
                                                String.Format("{0:0.00}", Humidity),
                                                String.Format("{0:0.00}", CalculatedDewPoint));

                string htmlCode = "200 OK";

                using (Stream resp = outstream.AsStreamForWrite())
                {
                    byte[] bodyArray = Encoding.UTF8.GetBytes(respBody);
                    MemoryStream stream = new MemoryStream(bodyArray);

                    // Response heeader
                    string header = string.Format("HTTP/1.1 {0}\r\n" +
                                                  "Content-Type: text/html\r\n" +
                                                  "Content-Length: {1}\r\n" +
                                                  "Connection: close\r\n\r\n",
                                                  htmlCode, stream.Length);

                    byte[] headerArray = Encoding.UTF8.GetBytes(header);
                    await resp.WriteAsync(headerArray, 0, headerArray.Length);
                    await stream.CopyToAsync(resp);
                    await resp.FlushAsync();
                }
            }

            public void Dispose()
            {
                sock.Dispose();
            }
        }
    }
}