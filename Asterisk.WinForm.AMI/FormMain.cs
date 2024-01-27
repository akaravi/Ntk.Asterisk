using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using System.Diagnostics;
using Ntk.AsterNet.AMI.Manager;
using Ntk.AsterNet.AMI.Manager.Event;
using Ntk.AsterNet.AMI.Manager.Action;
using Ntk.AsterNet.AMI.Manager.Response;


namespace Asterisk.WinForm.AMI
{
    public partial class FormMain : Form
    {
        public FormMain()
        {
            InitializeComponent();
          

            groupBoxAction.Visible = false;
            
        }

        private ManagerConnection manager = null;
        private void btnConnect_Click(object sender, EventArgs e)
        {
            string address = this.tbAddress.Text;
            int port = int.Parse(this.tbPort.Text);
            string user = this.tbUser.Text;
            string password = this.tbPassword.Text;

            btnConnect.Enabled = false;
            manager = new ManagerConnection(address, port, user, password);
            manager.UnhandledEvent += new EventHandler<ManagerEvent>(manager_Events);
            try
            {
                // Uncomment next 2 line comments to Disable timeout (debug mode)
                // manager.DefaultResponseTimeout = 0;
                // manager.DefaultEventTimeout = 0;
                manager.Login();
                manager.DialBegin += manager_DialBegin;         // this also can be omit
                manager.DialEnd += manager_DialEnd;
                manager.Hangup += call_hangup_Events;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error connect\n" + ex.Message);
                manager.Logoff();
                this.Close();
            }
            btnDisconnect.Enabled = true;
            groupBoxAction.Visible = true;
            buttonActionCall.Enabled = true;
            buttonActionHungup.Enabled = false;
        }

        void manager_Events(object sender, ManagerEvent e)
        {
            Debug.WriteLine("Event : " + e.GetType().Name);
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            btnConnect.Enabled = true;
            if (this.manager != null)
            {
                manager.Logoff();
                this.manager = null;
            }
            this.buttonActionHungup_Click(sender, e);
            btnDisconnect.Enabled = false;
            groupBoxAction.Visible = false;
            buttonActionCall.Enabled = true;
            buttonActionHungup.Enabled = false;

        }
        private bool ActionCallStatus = false;
        private ManagerResponse originateResponse;

        private void buttonActionCall_Click(object sender, EventArgs e)
        {
            switch (radioButtonActionMode.Text)
            {
                case "SameTime":
                    #region
                    OriginateAction oc = new OriginateAction();
                    oc.Timeout = 15000;
                    oc.Context = "from-internal";
                    oc.Priority = "1";
                    oc.CallerId = this.textBoxActionCallerId.Text;
                    if (string.IsNullOrEmpty(this.textBoxActionFromChannel.Text))
                        this.textBoxActionFromChannel.Text = "DAHDI/g1";
                    if (this.textBoxActionFromChannel.Text.LastIndexOf("/") < this.textBoxActionFromChannel.Text.Length - 1)
                        this.textBoxActionFromChannel.Text = this.textBoxActionFromChannel.Text + "/";
                    if (this.textBoxActionFromNumber.Text.Length < 3)
                    {
                        oc.Channel = "SIP/" + this.textBoxActionFromNumber.Text;
                    }
                    else
                    {
                        oc.Channel = this.textBoxActionFromChannel.Text + this.textBoxActionFromNumber.Text;
                    }
                    oc.Exten = this.textBoxActionToNumber.Text;
                    originateResponse = manager.SendAction(oc, oc.Timeout);
                    #endregion
                    break;
                default:
                    break;
            }

            
            if (originateResponse.IsSuccess())
            {
                MessageBox.Show(originateResponse.Message, "تماس موفقیت آمیز");
                ActionCallStatus = true;
                buttonActionCall.Enabled = !ActionCallStatus;
                buttonActionHungup.Enabled = ActionCallStatus;
            }
            else
            {
                MessageBox.Show(originateResponse.Message, "برروز خطا");
                ActionCallStatus = false;
                buttonActionCall.Enabled = !ActionCallStatus;
                buttonActionHungup.Enabled = ActionCallStatus;
            }

        }

        private void buttonActionHungup_Click(object sender, EventArgs e)
        {
            if (!ActionCallStatus)
                return;
            
            buttonActionCall.Enabled = true;
            buttonActionHungup.Enabled = false;
        }
        #region DialEndEvent

        static void manager_DialEnd(object sender, DialEndEvent e)
        {

            DateTime dtAnswerTime = e.DateReceived;
            string sUnique = e.DestUniqueId;

            //this.Invoke((MethodInvoker)delegate
            //{

            //    while (true)
            //    {
            //        for (int i = 0; i < this.dataGridView1.Rows.Count; i++)
            //        {
            //            if (this.dataGridView1.Rows[i].Cells["uniqueID"].Value.ToString() == sUnique)
            //            {
            //                this.dataGridView1.Rows[i].Cells["B answer time"].Value = dtAnswerTime;
            //                return;
            //            }

            //        }
            //    }
            //    //listBox1.Items.Add(sUnique);

            //});

            return;

        }

        static void manager_DialBegin(object sender, DialBeginEvent e)
        {
            string srtAnumber = e.Attributes["destcalleridnum"].ToString();
            string strBnumber = e.DialString;
            DateTime dtRingTime = e.DateReceived;
            string sUnique = e.DestUniqueId;

            //this.Invoke((MethodInvoker)delegate
            //{
            //    int index = this.dataGridView1.Rows.Add();
            //    this.dataGridView1.Rows[index].Cells["A number"].Value = srtAnumber;
            //    this.dataGridView1.Rows[index].Cells["B number"].Value = strBnumber;
            //    this.dataGridView1.Rows[index].Cells["B ring time"].Value = dtRingTime;
            //    this.dataGridView1.Rows[index].Cells["uniqueID"].Value = sUnique;

            //    this.dataGridView1.Rows[index].HeaderCell.Value = String.Format("{0}", index + 1);

            //    //this.dataGridView1.Refresh();
            //});

            return;

        }

        static void call_hangup_Events(object sender, HangupEvent e)
        {

            //string sUnique = e.UniqueId;


            //DateTime dtAnswerTime = e.DateReceived;


            //this.Invoke((MethodInvoker)delegate
            //{
            //    for (int i = 0; i < this.dataGridView1.Rows.Count; i++)
            //    {
            //        if (this.dataGridView1.Rows[i].Cells["uniqueID"].Value.ToString() == sUnique)
            //        {
            //            this.dataGridView1.Rows[i].Cells["B hangup time"].Value = dtAnswerTime;
            //        }

            //    }
            //});

            //return;


            DateTime dtAnswerTime = e.DateReceived;
            string sUnique = e.Attributes["linkedid"].ToString();

            //this.Invoke((MethodInvoker)delegate
            //{

            //    while (true)
            //    {
            //        for (int i = 0; i < this.dataGridView1.Rows.Count; i++)
            //        {
            //            if (this.dataGridView1.Rows[i].Cells["uniqueID"].Value.ToString() == sUnique)
            //            {
            //                this.dataGridView1.Rows[i].Cells["B hangup time"].Value = dtAnswerTime;
            //                return;
            //            }

            //        }
            //    }
            //    //listBox1.Items.Add(sUnique);

            //});

            return;

        }
        #endregion
    }
}
