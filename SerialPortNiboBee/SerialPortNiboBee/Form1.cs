using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Management;
using System.IO.Ports;
using Utilities;
using System.Text;
using System.Windows.Threading;

namespace SerialPortNiboBee
{
    public partial class Form1 : Form
    {
        globalKeyboardHook gkh = new globalKeyboardHook();
        private SerialPort _incomingSerialPort;
        private SerialPort _outgoingSerialPort;
        private Keys _previousKey;
        private bool WKey = false;
        private bool AKey = false;
        private bool SKey = false;
        private bool DKey = false;

        public Form1()
        {
            InitializeComponent();

            label4.Text = "Motor Speed: 500";

#if DEBUG
            button2.Visible = true;
            textBox1.Visible = true;
            listBox1.Visible = true;
            button4.Visible = true;
#else
            button2.Visible = false;
            textBox1.Visible = false;
            listBox1.Visible = false;
            button4.Visible = false;
#endif

            gkh.HookedKeys.Add(Keys.A);
            gkh.HookedKeys.Add(Keys.D);
            gkh.HookedKeys.Add(Keys.W);
            gkh.HookedKeys.Add(Keys.S);
            gkh.HookedKeys.Add(Keys.Q);
            gkh.HookedKeys.Add(Keys.E);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (comboBox1.SelectedItem == null)
            {
                MessageBox.Show("Bitte wählen Sie einen COM-Port aus.");
                return;
            }


            _incomingSerialPort = new SerialPort(comboBox1.SelectedItem.ToString())
            {
                BaudRate = 9600
            };

            _outgoingSerialPort = new SerialPort(comboBox2.SelectedItem.ToString())
            {
                BaudRate = 9600
            };


            _incomingSerialPort.DataReceived += MyPort_DataReceived;
            _outgoingSerialPort.ErrorReceived += _outgoingSerialPort_ErrorReceived;
            _outgoingSerialPort.DataReceived += _outgoingSerialPort_DataReceived;

            if (_incomingSerialPort.IsOpen && _outgoingSerialPort.IsOpen) return;
            try
            {
                _incomingSerialPort.Open();
                _outgoingSerialPort.Open();
                setDriveMode();
                clearMotorRegisters();
                button1.Enabled = false;
                button3.Enabled = true;
                button5.Enabled = true;
                button6.Enabled = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.InnerException + "\n" + ex.Message);
                _incomingSerialPort.Close();
                _incomingSerialPort.Dispose();
                _outgoingSerialPort.Dispose();
                MessageBox.Show("Verbindung konnte nicht hergestellt werden.\nSchauen Sie sich das Troubleshooting in der Anwenderdokumentation an.");
            }
        }

        private void _outgoingSerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            Console.WriteLine(_outgoingSerialPort.ReadExisting());
        }

        private void _outgoingSerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            Console.WriteLine(e.EventType.ToString());
        }

        private void MyPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            //read data waiting in the buffer
            string msg = _incomingSerialPort.ReadExisting();
            //display the data to the user
            Console.WriteLine(msg + "\n");
        }

        private static IEnumerable<ManagementObject> GetAllComPort()
        {
            var comPort = new List<ManagementObject>();
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM  Win32_SerialPort "))
            {
                comPort.AddRange(searcher.Get().Cast<ManagementObject>());
            }
            return comPort;
        }


        public List<string> GetAllPorts()
        {
            return SerialPort.GetPortNames().ToList();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            comboBox1.Items.Clear();
            comboBox2.Items.Clear();

            var test = GetAllComPort();
            foreach (var prop in test.SelectMany(managementObject => managementObject.Properties.Cast<PropertyData>().Where(prop => prop.Name == "DeviceID")))
            {
                comboBox1.Items.Add(prop.Value);
                comboBox2.Items.Add(prop.Value);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            _incomingSerialPort.Close();
            _outgoingSerialPort.Close();
            _outgoingSerialPort.Dispose();
            _incomingSerialPort.Dispose();
            button1.Enabled = true;
            button3.Enabled = false;
            button5.Enabled = false;
            button6.Enabled = false;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (!_outgoingSerialPort.IsOpen) return;
            var request = textBox1.Text;

            try
            {
                _outgoingSerialPort.WriteLine(request);
            }
            catch (Exception)
            {
                _outgoingSerialPort.Close();
                _incomingSerialPort.Close();
                throw;
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            gkh.KeyDown += new KeyEventHandler(gkh_KeyDown);
            gkh.KeyUp += new KeyEventHandler(gkh_KeyUp);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            gkh.KeyDown -= new KeyEventHandler(gkh_KeyDown);
            gkh.KeyUp -= new KeyEventHandler(gkh_KeyUp);
        }


        void gkh_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.W) WKey = false;
            if (e.KeyCode == Keys.A) AKey = false;
            if (e.KeyCode == Keys.S) SKey = false;
            if (e.KeyCode == Keys.D) DKey = false;

            if (e.KeyCode == Keys.Q || e.KeyCode == Keys.E)
            {
                hideLED();
                _previousKey = Keys.Back;
                e.Handled = true;
                return;
            }

            _previousKey = Keys.Back;
            Drive(GetDriveDirection());
            e.Handled = true;
        }

        void gkh_KeyDown(object sender, KeyEventArgs e)
        {
            // Check if lastKey is the same as the current one
            if (_previousKey == e.KeyCode)
            {
                e.Handled = true;
                return;
            }
            _previousKey = e.KeyCode;

           if (e.KeyCode == Keys.Q)
            {
                showLED("Left");
                e.Handled = true;
                return;
            }
            if(e.KeyCode == Keys.E)
            {
                showLED("Right");
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.W) WKey = true;
            if (e.KeyCode == Keys.A) AKey = true;
            if (e.KeyCode == Keys.S) SKey = true;
            if (e.KeyCode == Keys.D) DKey = true;

            Drive(GetDriveDirection());
            e.Handled = true;
        }

        private void setDriveMode()
        {
            if (!_outgoingSerialPort.IsOpen) return;

            try
            {
                _outgoingSerialPort.WriteLine("request set 6, 3");
            }
            catch (Exception)
            {
                _outgoingSerialPort.Close();
                _incomingSerialPort.Close();
                throw;
            }
        }

        private void clearMotorRegisters()
        {
            if (!_outgoingSerialPort.IsOpen) return;


            try
            {
                _outgoingSerialPort.WriteLine("request set 7, 0 set 8, 0");
            }
            catch (Exception)
            {
                _outgoingSerialPort.Close();
                _incomingSerialPort.Close();
                throw;
            }
        }

        private void showLED(string direction)
        {
            if (!_outgoingSerialPort.IsOpen) return;


            try
            {
                if(direction == "Left")
                {
                    _outgoingSerialPort.WriteLine("request set 3, 35");
                }
                else if (direction == "Right")
                {
                    _outgoingSerialPort.WriteLine("request set 3, 60");
                }

            }
            catch (Exception)
            {
                _outgoingSerialPort.Close();
                _incomingSerialPort.Close();
                throw;
            }
        }

        private void hideLED()
        {
            if (!_outgoingSerialPort.IsOpen) return;


            try
            {
                    _outgoingSerialPort.WriteLine("request set 3, 0");
            }
            catch (Exception)
            {
                _outgoingSerialPort.Close();
                _incomingSerialPort.Close();
                throw;
            }
        }

        private void Drive(string request)
        {
            //var _request = ConvertStringToHex(request, Encoding.Unicode);
            //Console.WriteLine(_request);
            if (!_outgoingSerialPort.IsOpen) return;


            try
            {
                _outgoingSerialPort.WriteLine(request);
                listBox1.Items.Add(request);
            }
            catch (Exception)
            {
                _outgoingSerialPort.Close();
                _incomingSerialPort.Close();
                throw;
            }
        }

        private string GetDriveDirection()
        {
            if ((WKey && SKey) || (!WKey && !SKey))
            {
                if ((AKey && DKey) || (!AKey && !DKey)) return Stop;
                if (AKey) return DriveLeft;
                if (DKey) return DriveRight;
            }
            if (WKey)
            {
                if ((AKey && DKey) || (!AKey && !DKey)) return DriveForward;
                if (AKey) return DriveForwardLeft;
                if (DKey) return DriveForwardRight;
            }
            if (SKey)
            {
                if ((AKey && DKey) || (!AKey && !DKey)) return DriveBackward;
                if (AKey) return DriveBackwardLeft;
                if (DKey) return DriveBackwardRight;
            }
            return Stop;
        }

        public void getMotorSpeed()
        {
            if (!_outgoingSerialPort.IsOpen) return;

            try
            {
                _outgoingSerialPort.WriteLine("request get 6 get 7 get 8");
            }
            catch (Exception)
            {
                _outgoingSerialPort.Close();
                _incomingSerialPort.Close();
                throw;
            }
        }

        private string DriveForward { get { return "request set 7, " + trackBar1.Value.ToString() + " set 8, " + trackBar1.Value.ToString(); }} //left 100, right 100
        private string DriveForwardLeft { get { return "request set 7, 0 set 8, " + trackBar1.Value.ToString(); }} //left 0, right 100
        private string DriveForwardRight { get { return "request set 7, " + trackBar1.Value.ToString() + " set 8, 0"; }} //left 100, right 0
        private string DriveLeft { get { return "request set 7, -" + trackBar1.Value.ToString() + " set 8, " + trackBar1.Value.ToString(); }} //left -100, right 100
        private string DriveRight { get { return "request set 7, " + trackBar1.Value.ToString() + " set 8, -" + trackBar1.Value.ToString(); }} //left 100, right -100
        private string DriveBackward { get { return "request set 7, -" + trackBar1.Value.ToString() + " set 8, -" + trackBar1.Value.ToString(); }} //left -100, right -100
        private string DriveBackwardLeft { get { return "request set 7, 0 set 8, " + trackBar1.Value.ToString(); }} //left 0, right 100
        private string DriveBackwardRight { get { return "request set 7, " + trackBar1.Value.ToString() + " set 8, 0"; }} //left 100, right 0
        private string Stop { get { return "request set 7, 0 set 8, 0"; }} //left 0, right 0

        private void trackBar1_ValueChanged(object sender, EventArgs e)
        {
            label4.Text = "Motor Speed: " + trackBar1.Value;
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            if(_outgoingSerialPort != null) Drive(GetDriveDirection());
        }


        public static string ConvertStringRequestToHexRequest(String input, TransmissionType type, System.Text.Encoding encoding)
        {
            string _request = null;
            if(type == TransmissionType.Text)
            {
                _request += "$";
            }

            byte[] stringBytes = encoding.GetBytes(input);
            StringBuilder sbBytes = new StringBuilder(stringBytes.Length * 2);
            foreach (byte b in stringBytes)
            {
                sbBytes.AppendFormat("{0:X2}", b);
            }
            return sbBytes.ToString();
        }

        public static string ConvertHexToString(string HexValue)
        {
            string StrValue = "";
            while (HexValue.Length > 0)
            {
                StrValue += System.Convert.ToChar(System.Convert.ToUInt32(HexValue.Substring(0, 2), 16)).ToString();
                HexValue = HexValue.Substring(2, HexValue.Length - 2);
            }
            return StrValue;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var ports = GetAllComPort();
            foreach (var prop in ports.SelectMany(managementObject => managementObject.Properties.Cast<PropertyData>().Where(prop => prop.Name == "DeviceID")))
            {
                comboBox1.Items.Add(prop.Value);
                comboBox2.Items.Add(prop.Value);
            }
        }
    }
}
