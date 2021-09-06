using System;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Windows.Forms;

//TODO 
/*
 
Optional:

- Finish the binary mode (add option to load a file, display received messages in HEX when inputMode is set to InputMode.Binary)
- Autobauding (I'm neither smart, nor ambitious enough to do this)
- Hand control and pin change display (probaly easy)
  
*/

namespace IWSK_RS232
{
    public partial class RS232Form : Form
    {
        private static char[] hexCharacters = new char[] { 'a', 'b', 'c', 'd', 'e', 'f', 'A', 'B', 'C', 'D', 'E', 'F', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };
        private static readonly string pingMessage = "_ping_";
        private static readonly string pingResponse = "_gnip_";

        private static string tmpString;
        private static string messageString;
        private SerialPort serialPort = new SerialPort();
        private InputMode inputMode = InputMode.Text;

        private DateTime? lastPing;

        public RS232Form()
        {
            InitializeComponent();

            updatePorts(SerialPort.GetPortNames());
            speedComboBox.SelectedIndex = 5;
            dataBitsComboBox.SelectedIndex = 1;
            stopBitsComboBox.SelectedIndex = 1;
            parityComboBox.SelectedIndex = 0;
            flowControlComboBox.SelectedIndex = 0;
            timeoutEdit.Text = "1000";
        }

        private void printMsg(string msg)
        {
            outputConsole.AppendText(DateTime.Now.ToString("HH:mm:ss:fff") + ": " + msg);
            outputConsole.AppendText(Environment.NewLine);
        }

        private void asyncPrintMsg(string msg)
        {
            Invoke(new Action<string>(printMsg), msg);
        }

        private void searchButton_Click(object sender, EventArgs e)
        {
            updatePorts(SerialPort.GetPortNames());
        }

        private void updatePorts(string[] ports)
        {
            portComboBox.Items.Clear();
            portComboBox.Items.AddRange(ports);

            string msg = "found " + ports.Length + " ports: ";
            foreach (string port in ports)
            {
                msg += port + ", ";
            }
            printMsg(msg);
        }

        private void applyButton_Click(object sender, EventArgs e)
        {
            if (portNotSelected())
            {
                printMsg("Select a port to open");
                return;
            }

            setNewPortConfiguration();
            try
            {
                serialPort.Open();
                printMsg("opened port " + serialPort.PortName);
            }
            catch (Exception)
            {
                printMsg("could not open port " + serialPort.PortName);
            }
        }

        private bool portNotSelected()
        {
            return portComboBox.Text.Length == 0;
        }

        private void setNewPortConfiguration()
        {
            if (serialPort != null)
                serialPort.Close();

            serialPort = new SerialPort();
            serialPort.PortName = portComboBox.Text;
            serialPort.BaudRate = int.Parse(speedComboBox.Text);
            serialPort.DataBits = int.Parse(dataBitsComboBox.Text);
            serialPort.StopBits = stopBitsComboBox.Text == "1" ? StopBits.One : StopBits.Two;
            serialPort.Parity = (Parity)Enum.Parse(typeof(Parity), parityComboBox.Text);
            serialPort.Handshake = (Handshake)Enum.Parse(typeof(Handshake), flowControlComboBox.Text);
            serialPort.DataReceived += new SerialDataReceivedEventHandler(dataReceivedHandler);
            serialPort.ReadTimeout = int.Parse(timeoutEdit.Text);
            serialPort.WriteTimeout = int.Parse(timeoutEdit.Text);
            
        }

        private void dataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            if (!readMessage())
                return;

            string indata = messageString;
            if (indata == pingMessage)
            {
                asyncPrintMsg("received ping message");
                serialPort.Write(pingResponse + getTerminationCharacters());
                asyncPrintMsg("responded to ping message");
                return;
            }
            if (indata == pingResponse && lastPing.HasValue)
            {
                double milliseconds = (DateTime.Now - lastPing).Value.TotalMilliseconds;
                asyncPrintMsg("received ping response, round trip delay = " + ((long)milliseconds).ToString());
                lastPing = null;
                return;
            }

            asyncPrintMsg("Data received - " + indata);
        }

        private bool readMessage()
        {
            tmpString += serialPort.ReadExisting();
            if (tmpString.Contains(getTerminationCharacters())) {
                messageString = tmpString;
                tmpString = string.Empty;
                if (getTerminationCharacters() != "")
                    messageString = removeTerminator(messageString, getTerminationCharacters());
                return true;
            }

            return false;
        }

        private string removeTerminator(string msg, string terminator)
        {
            return msg.Replace(terminator, "");
        }

        private void pingButton_Click(object sender, EventArgs e)
        {
            printMsg("sending ping message");
            lastPing = DateTime.Now;
            serialPort.Write(pingMessage + getTerminationCharacters());
        }

        private void sendButton_Click(object sender, EventArgs e)
        {
            if (!serialPort.IsOpen)
                return;

            sendMessage();

        }

        private void sendMessage()
        {
            string msg;
            switch(inputMode)
            {
                case InputMode.Text:
                    msg = textInput.Text;
                    break;
                case InputMode.Binary:
                    msg = hexToBytes(binaryInput.Text);
                    break;
                default:
                    msg = "";
                    break;
            }
            try
            {
                serialPort.Write(msg + getTerminationCharacters());
                textInput.Text = string.Empty;
            } 
            catch (InvalidOperationException)
            {
                printMsg("ERROR - could not send message");
            }
            catch (Exception)
            {
                printMsg("transaction timed out");
            }
        }

        private string getTerminationCharacters()
        {
            return hexToBytes(terminatorTextBox.Text);
        }

        private string hexToBytes(string hex)
        {
            if (stringNotAHexNumber(hex))
                throw new FormatException("passed string does not contain a valid hex number");
            
            string text = "";
            char[] hexArray = hex.ToCharArray();
            for(int i = 0; i < hexArray.Length; i += 2)
            {
                string c = hex.Substring(i, 2);
                
                text += (char)Convert.ToInt32(c, 16);
            }
            return text;
        }

        private bool stringNotAHexNumber(string hex)
        {
            bool length_not_correct = hex.Length != 0 && (hex.Length % 2) == 1;
            bool contains_invalid_characters = false;
            foreach(char c in hex.ToCharArray())
                if(!hexCharacters.Contains(c))
                {
                    contains_invalid_characters = true;
                    break;
                }
            return length_not_correct || contains_invalid_characters;
        }

        private void terminatorTextBox_Validating(object sender, CancelEventArgs e)
        {
            if (stringNotAHexNumber(terminatorTextBox.Text))
            {
                terminatorTextBox.Text = string.Empty;
            }
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch(tabControl1.SelectedIndex)
            {
                case 0:
                    inputMode = InputMode.Text;
                    break;
                case 1:
                    inputMode = InputMode.Binary;
                    break;
                default:
                    throw new Exception("not supported tab selected");
            }

            if (inputMode != InputMode.Binary)
                sendButton.Enabled = true;
            else
                sendButton.Enabled = !stringNotAHexNumber(binaryInput.Text);
        }

        enum InputMode
        {
            Text,
            Binary
        }

        private void binaryInput_TextChanged(object sender, EventArgs e)
        {
            sendButton.Enabled = !stringNotAHexNumber(binaryInput.Text);
        }
    }
}
