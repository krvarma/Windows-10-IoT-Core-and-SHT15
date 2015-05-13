## Windows 10 IoT Core and SHT15 Sensor ##

Here is another project using Window 10 IoT. In this project I am using SHT15 Temperature an Humidity sensor. This sensor give accurate temperature and humidity readings. 

I used [this library](https://github.com/practicalarduino/SHT1x) and converted to C#. The port was easy and simple. Only at one point I was stuck, where we wait for the result after sending a command to the sensor. In the original code, after sending temperature or humidity commands to the sensor, it waits for the data to be ready for reading. The code loops 100 times and see whether the data pin is high or not. It turns out for Windows 10 IoT looping 100 times is not enough, after I increased it to 3000 and it was working. I don't why this is so, someone expert in this area has to find out and explain.

The project reads Temperature, Humidity and calculate the Dew Point. The Dew Point calculation is taken from [Arduino DHT Library](http://playground.arduino.cc/Main/DHT11Lib).

**Wiring** 

 1. SHT15 Vcc to 3.3v 
 2. SHT15 Gnd to GND 
 3. SHT15 Data to GPIO24 (Pin #18)
 4. SHT15 Sck to GPIO23 (Pin #16)

**Using the sample**

Open the project in Visual Studio 2015. Start the application using the 'Remote Device' option under the Debug Tab. Configure the IP Address of the RPi2.

To see the sensor values, access the internal HTTP Server using the URL `http://<<RPi2 IP Address>>:8080`. The web page automatically refresh every 5 seconds.

![RPi2 and SHT15](https://raw.githubusercontent.com/krvarma/Windows-10-IoT-Core-and-SHT15/master/images/IMG_0031.JPG)

![RPI2 and SHT15](https://raw.githubusercontent.com/krvarma/Windows-10-IoT-Core-and-SHT15/master/images/IMG_0029.JPG)

![Browser Output](https://raw.githubusercontent.com/krvarma/Windows-10-IoT-Core-and-SHT15/master/images/browser.png)

![Fritzing](https://raw.githubusercontent.com/krvarma/Windows-10-IoT-Core-and-SHT15/master/images/w10_sht15_bb.png)