using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;

// Use Leap
using Leap;

// Sockets related DLLs
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Text.RegularExpressions;
using SocketIOClient;
using SocketIOClient.Messages;
using System.Net;

namespace SoD_Sensor_Leap
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

        public partial class MainWindow : Window, ILeapEventDelegate
        {
            #region Global Local Variables & Constants
            private Controller controller = new Controller();
            private LeapEventListener listener;
            private Boolean isClosing = false;

            private string leapID;                  // Client ID
            private static Client socket;           // socket object


            private const String IP_REGEX = @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b";
            private const String URL_REGEX = @"^(http|https|ftp|)\://|[a-zA-Z0-9\-\.]+\.[a-zA-Z](:[a-zA-Z0-9]*)?/?([a-zA-Z0-9\-\._\?\,\'/\\\+&amp;%\$#\=~])*[^\.\,\)\(\s]$";
            private const String PORT_REGEX = @"^(4915[0-1]|491[0-4]\d|490\d\d|4[0-8]\d{3}|[1-3]\d{4}|[1-9]\d{0,3}|0)$";
            #endregion

            public MainWindow()
            {
                InitializeComponent();

                IPHostEntry host;
                string localIP = "?";
                host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (IPAddress ip in host.AddressList)
                {
                    if (ip.AddressFamily.ToString() == "InterNetwork")
                    {
                        localIP = ip.ToString();
                    }
                }
                ServerTextBox.Text = localIP;

               
                
                this.controller = new Controller();
                this.listener = new LeapEventListener(this);
                controller.AddListener(listener);




            }

            private void Window_Loaded(object sender, RoutedEventArgs e)
            {
                leapID = System.Environment.MachineName;
                NameTextBox.Text = leapID;

                /*try
                {
                    if (!TestKinect2Availability())
                    {
                        //no Kinects or Kinect2s connected
                        Application.Current.Shutdown(0);
                    }
                    else
                    {
                        InitializeKinect2();
                    }
                }
                catch (MissingMethodException)
                {
                    MessageBox.Show("Missing method exception, most likely due to no Kinect2 being plugged in.");
                    Application.Current.Shutdown(0);
                }
                */

                StatusSubmit.Click += new RoutedEventHandler(StatusSubmit_Click);
            }

            void StatusSubmit_Click(object sender, RoutedEventArgs e)
            {
                if (!isValidInput()) { return; }

                if (StatusSubmit.Content.Equals("Connect")) //Users wants to connect
                {
                    //Connect to server//      
                    string address = "http://" + ServerTextBox.Text + ":" + PortTextBox.Text + "/";
                    Console.WriteLine("Connecting to: " + address);
                    socket = new Client(address);
                    socket.Connect();

                    //Debug, check if sensor still exists// Disable for Kinect2
                    Console.WriteLine("Sensor: Leap");
                    ///////////////////////////////////////
                    TimeSpan maxDuration = TimeSpan.FromMilliseconds(1000);
                    Stopwatch sw = Stopwatch.StartNew();

                    while (sw.Elapsed < maxDuration && !socket.IsConnected)
                    {
                        // wait for connected
                    }

                    if (socket.IsConnected)
                    {
                        StatusSubmit.Content = "Disconnect";
                        StatusLabel.Text = "Connected";

                        if (TellServerAboutSensor())
                        {
                            //sensor registered with server
                        }
                        else
                        {
                            //no sensor was registered with server!
                        }
                        //socket.Message += new EventHandler<MessageEventArgs>(socket_Message);
                        SubscribeToRoutes(socket);
                    }
                    else
                    {
                        Console.WriteLine("Device never registered with server!");
                    }
                }
                else //Users wants to disconnect
                {
                    socket.Close();
                    if (!socket.IsConnected) //replace true with condition for successful disconnect
                    {
                        //disconnected, cleanup
                        StatusSubmit.Content = "Connect";
                        StatusLabel.Text = "Disconnected";
                    }
                    else
                    {
                        MessageBox.Show("Server failed to disconnect properly.");
                    }
                }
            }
            public void SubscribeToRoutes(Client socket)
            {
                socket.On("connect", (fn) =>
                {
                    Console.WriteLine("\r\nConnected ...\r\n");
                    TellServerAboutSensor();
                });
            }
            private bool TellServerAboutSensor()
            {
                if (true)//kinectSensor != null)
                {
                    socket.Emit("registerSensor", new RegisterCapsule("Leap"));
                    Console.WriteLine("registered Leap with server");
                    return true;
                }
                else
                {
                    //no sensor available
                    return false;
                }
            }

            private bool isValidInput()
            {
                if (!Regex.IsMatch(ServerTextBox.Text, IP_REGEX) && !Regex.IsMatch(ServerTextBox.Text, URL_REGEX))
                {
                    MessageBox.Show("\"" + ServerTextBox.Text + "\" is an invalid server address!", "Invalid Input");
                    return false;
                }
                else if (!Regex.IsMatch(PortTextBox.Text, PORT_REGEX))
                {
                    MessageBox.Show("\"" + PortTextBox.Text + "\" is an invalid port number!", "Invalid Input");
                    return false;
                }

                return true;
            }

            delegate void LeapEventDelegate(string EventName);
            public void LeapEventNotification(string EventName)
            {
                if (this.CheckAccess())
                {
                    switch (EventName)
                    {
                        case "onInit":
                            Debug.WriteLine("Init");
                            break;
                        case "onConnect":
                            this.connectHandler();
                            break;
                        case "onFrame":
                            if (!this.isClosing)
                                this.newFrameHandler(this.controller.Frame());
                            break;
                    }
                }
                else
                {
                    Dispatcher.Invoke(new LeapEventDelegate(LeapEventNotification), new object[] { EventName });
                }
            }

            void connectHandler()
            {
                this.controller.SetPolicy(Controller.PolicyFlag.POLICY_IMAGES);
                this.controller.EnableGesture(Gesture.GestureType.TYPE_SWIPE);
                this.controller.Config.SetFloat("Gesture.Swipe.MinLength", 100.0f);
            }

            void newFrameHandler(Leap.Frame frame)
            {
                displayID.Text = frame.Id.ToString();
                //this.displayTimestamp.Content = frame.Timestamp.ToString();
                //this.displayFPS.Content = frame.CurrentFramesPerSecond.ToString();
                //this.displayIsValid.Content = frame.IsValid.ToString();
                displayGestureCount.Text = frame.Gestures().Count.ToString();
                //this.displayImageCount.Content = frame.Images.Count.ToString();
            }

            void MainWindow_Closing(object sender, EventArgs e)
            {
                this.isClosing = true;
                this.controller.RemoveListener(this.listener);
                this.controller.Dispose();
            }
        }

        public interface ILeapEventDelegate
        {
            void LeapEventNotification(string EventName);
        }

        public class LeapEventListener : Listener
        {
            ILeapEventDelegate eventDelegate;

            public LeapEventListener(ILeapEventDelegate delegateObject)
            {
                this.eventDelegate = delegateObject;
            }
            public override void OnInit(Controller controller)
            {
                this.eventDelegate.LeapEventNotification("onInit");
            }
            public override void OnConnect(Controller controller)
            {
                controller.SetPolicy(Controller.PolicyFlag.POLICY_IMAGES);
                controller.EnableGesture(Gesture.GestureType.TYPE_SWIPE);
                this.eventDelegate.LeapEventNotification("onConnect");
            }

            public override void OnFrame(Controller controller)
            {
                this.eventDelegate.LeapEventNotification("onFrame");
            }
            public override void OnExit(Controller controller)
            {
                this.eventDelegate.LeapEventNotification("onExit");
            }
            public override void OnDisconnect(Controller controller)
            {
                this.eventDelegate.LeapEventNotification("onDisconnect");
            }

        }

        public class RegisterCapsule
        {
            public RegisterCapsule(string sensorType)//,float FOV, int rangeInMM, TranslateRule rule)
            {
                this.sensorType = sensorType;
                //this.FOV = FOV;
                //this.rangeInMM = rangeInMM;
                if (sensorType == "Leap")
                {
                    Console.WriteLine("We got a Leap!!");
                    //this.frameHeight = 480;
                    //this.frameWidth = 640;
                }
            }
            public string sensorType;
            //public float FOV;
            //public int rangeInMM;
            //public int frameHeight, frameWidth;
        }


        /*public partial class MainWindow : Window
        {
            public MainWindow()
            {
                InitializeComponent();
                // Init Controller
                try{
                    SampleListener listener = new SampleListener();
                    Leap.Controller controller = new Leap.Controller();
                    controller.AddListener(listener);
                    Console.WriteLine("haha");
                    System.Threading.Thread.Sleep(15000);

                    Console.WriteLine("Press Enter to quit...");
                    Console.ReadLine();

                    // dispose controller and listener process
                    controller.RemoveListener(listener);
                    controller.Dispose();
                }
                catch (Exception e){
                    Console.WriteLine("Exception yo: "+e);
                }

            


            
            }     
        }
        class SampleListener : Leap.Listener
        {
       
            private Object thisLock = new Object();
        
            private void SafeWriteLine(String line)
            {
                lock (thisLock)
                {
                    Console.WriteLine(line);
                }
            }

            public override void OnConnect(Controller controller)
            {
                SafeWriteLine("Connected");
            }


            public override void OnFrame(Controller controller)
            {
                SafeWriteLine("Frame available");
                Leap.Frame frame = controller.Frame();

                SafeWriteLine("Frame id: " + frame.Id
                         + ", timestamp: " + frame.Timestamp
                         + ", hands: " + frame.Hands.Count
                         + ", fingers: " + frame.Fingers.Count
                         + ", tools: " + frame.Tools.Count
                         + ", gestures: " + frame.Gestures().Count);

            }
        
        }*/
    
}
