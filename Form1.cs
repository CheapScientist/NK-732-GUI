using NK732TwoChannel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NK732_GUI
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            Console.SetOut(new TextBoxWriter(txtOutput));
#if SIMULATION
            chkSimulate.Checked = true;
#endif
        }

        private void chkSimulate_CheckedChanged(object sender, EventArgs e)
        {
            TIAController.SetSimulate(chkSimulate.Checked);
            txtOutput.AppendText($"[GUI] Simulation mode: {chkSimulate.Checked}\r\n");
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                TIAController.ConnectAndPrintInfo();
                txtOutput.AppendText("Connected.\r\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            try
            {
                long n = (long)numMeas.Value;
                TIAController.StartMeasurement(n);
                txtOutput.AppendText($"Started measurement ({n}).\r\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            TIAController.Stop();
            txtOutput.AppendText("Stopped.\r\n");
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void txtOutput_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }

    // Redirect Console.WriteLine() → textbox
    public class TextBoxWriter : TextWriter
    {
        private readonly TextBox box;
        public TextBoxWriter(TextBox b) { box = b; }
        public override Encoding Encoding => Encoding.UTF8;
        public override void WriteLine(string value)
        {
            if (box.InvokeRequired)
                box.Invoke(new Action(() => box.AppendText(value + "\r\n")));
            else
                box.AppendText(value + "\r\n");
        }
    }
}
