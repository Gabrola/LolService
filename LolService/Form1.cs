using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using LolService.RTMPS;
using System.Threading;

namespace LolService
{
    public partial class Form1 : Form
    {
        public static Form1 Instance;

        public Form1()
        {
            InitializeComponent();
            Form1.Instance = this;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            new Thread(
                delegate()
                {
                    LolRTMPSClient client = new LolRTMPSClient("NA", "username", "password", "3.9.13_07_17_01_27");
                }
            ) { IsBackground = true }.Start();
        }

        public static void Log(string Text)
        {
            Form1.Instance.Invoke(new MethodInvoker(delegate {
                Form1.Instance.textBox1.Text += Text + Environment.NewLine;
                Form1.Instance.textBox1.SelectionStart = Form1.Instance.textBox1.Text.Length;
                Form1.Instance.textBox1.ScrollToCaret();
            }));
        }
    }
}
