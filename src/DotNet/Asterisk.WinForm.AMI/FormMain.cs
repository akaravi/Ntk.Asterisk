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
            this.tbAddress.Text = "192.168.15.10";
            this.tbUser.Text = "usercti";
            this.tbPassword.Text = "abc@2390702";
            this.textBoxActionFromNumber.Text = "09131183892";
            this.textBoxActionToNumber.Text = "09125210076";

        }

        private ManagerConnection manager = null;


        public void test1()
        {
            

            // تماس گرفتن با یک شماره موبایل
            OriginateAction firstCallAction = new OriginateAction();
            firstCallAction.Channel = "SIP/1001"; // شماره داخلی مورد نظر
            firstCallAction.Context = "from-internal"; // Context مورد نظر
            firstCallAction.Exten = "09123456789"; // شماره موبایل مورد نظر
            firstCallAction.Priority = "1";
            firstCallAction.Timeout = 30000;

            ManagerResponse firstCallResponse = manager.SendAction(firstCallAction, firstCallAction.Timeout);

            // بررسی نتیجه تماس
            if (firstCallResponse.IsSuccess())
            {
                Console.WriteLine("تماس با موفقیت برقرار شد.");

                // در صورتی که تماس پاسخ داده شود، تماس دوم را برقرار کنید
                if (firstCallResponse.UniqueId != null)
                {
                    OriginateAction secondCallAction = new OriginateAction();
                    secondCallAction.Channel = "SIP/1002"; // شماره داخلی مورد نظر برای تماس دوم
                    secondCallAction.Context = "from-internal"; // Context مورد نظر
                    secondCallAction.Exten = "09123456788"; // شماره موبایل دیگر مورد نظر
                    secondCallAction.Priority = "1";
                    secondCallAction.Timeout = 30000;

                    ManagerResponse secondCallResponse = manager.SendAction(secondCallAction, secondCallAction.Timeout);

                    // بررسی نتیجه تماس دوم
                    if (secondCallResponse.IsSuccess())
                    {
                        Console.WriteLine("تماس دوم با موفقیت برقرار شد.");
                    }
                    else
                    {
                        Console.WriteLine("خطا در برقراری تماس دوم: " + secondCallResponse.Message);
                    }
                }
            }
            else
            {
                Console.WriteLine("خطا در برقراری تماس: " + firstCallResponse.Message);
            }

            
        }

        public void test2()
        {
       
            // Make a call to the first mobile number
            OriginateAction originateAction1 = new OriginateAction();
            originateAction1.Channel = "SIP/YOUR_SIP_TRUNK/FIRST_MOBILE_NUMBER";
            originateAction1.Context = "from-internal";
            originateAction1.Exten = "1000";
            originateAction1.Priority = "1";
            ManagerResponse originateResponse1 = manager.SendAction(originateAction1, 30000);

            // Wait for the call to be answered
            System.Threading.Thread.Sleep(5000); // Wait for 5 seconds

            // Make a call to the second mobile number and connect it to the first call
            OriginateAction originateAction2 = new OriginateAction();
            originateAction2.Channel = "SIP/YOUR_SIP_TRUNK/SECOND_MOBILE_NUMBER";
            originateAction2.Context = "from-internal";
            originateAction2.Exten = "1000";
            originateAction2.Priority = "1";
            originateAction2.Application = "Dial";
            originateAction2.Data = "SIP/YOUR_SIP_TRUNK/FIRST_MOBILE_NUMBER";
            ManagerResponse originateResponse2 = manager.SendAction(originateAction2, 30000);
        }

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
            if(radioButtonActionMode1.Checked)
            { 
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
            }
            else if (radioButtonActionMode2.Checked)
            {
                #region
                if (string.IsNullOrEmpty(this.textBoxActionFromChannel.Text))
                    this.textBoxActionFromChannel.Text = "DAHDI/g1";
                if (this.textBoxActionFromChannel.Text.LastIndexOf("/") < this.textBoxActionFromChannel.Text.Length - 1)
                    this.textBoxActionFromChannel.Text = this.textBoxActionFromChannel.Text + "/";

                // Make a call to the first mobile number
                OriginateAction originateAction1 = new OriginateAction();
                originateAction1.Channel = this.textBoxActionFromChannel.Text + this.textBoxActionFromNumber.Text;
                originateAction1.Context = "from-internal";
                originateAction1.Exten = "21";
                originateAction1.Priority = "1";
                ManagerResponse originateResponse1 = manager.SendAction(originateAction1, 30000);

                // Wait for the call to be answered
                System.Threading.Thread.Sleep(5000); // Wait for 5 seconds

                // Make a call to the second mobile number and connect it to the first call
                OriginateAction originateAction2 = new OriginateAction();
                originateAction2.Channel = this.textBoxActionFromChannel.Text + this.textBoxActionToNumber.Text;
                originateAction2.Context = "from-internal";
                originateAction2.Exten = "21";
                originateAction2.Priority = "1";
                originateAction2.Application = "Dial";
                originateAction2.Data = this.textBoxActionFromChannel.Text + this.textBoxActionFromNumber.Text;
                ManagerResponse originateResponse2 = manager.SendAction(originateAction2, 30000);
                #endregion
            }else if (radioButtonActionMode3.Checked)
            {
                #region
                if (string.IsNullOrEmpty(this.textBoxActionFromChannel.Text))
                    this.textBoxActionFromChannel.Text = "DAHDI/g1";
                if (this.textBoxActionFromChannel.Text.LastIndexOf("/") < this.textBoxActionFromChannel.Text.Length - 1)
                    this.textBoxActionFromChannel.Text = this.textBoxActionFromChannel.Text + "/";

                // تماس گرفتن با یک شماره موبایل
                OriginateAction firstCallAction = new OriginateAction();
                firstCallAction.Channel =  this.textBoxActionFromChannel.Text + this.textBoxActionFromNumber.Text; // شماره داخلی مورد نظر
                firstCallAction.Context = "from-internal"; // Context مورد نظر
                firstCallAction.Exten = "1000";// this.textBoxActionFromNumber.Text;// this.textBoxActionFromNumber.Text; // شماره موبایل مورد نظر
                firstCallAction.Priority = "1";
                firstCallAction.Timeout = 30000;

                ManagerResponse firstCallResponse = manager.SendAction(firstCallAction, firstCallAction.Timeout);

                // بررسی نتیجه تماس
                if (firstCallResponse.IsSuccess())
                {
                    Console.WriteLine("تماس با موفقیت برقرار شد.");

                    // در صورتی که تماس پاسخ داده شود، تماس دوم را برقرار کنید
                    if (firstCallResponse.UniqueId != null)
                    {
                        OriginateAction secondCallAction = new OriginateAction();
                        secondCallAction.Channel =  this.textBoxActionFromChannel.Text + this.textBoxActionToNumber.Text; // شماره داخلی مورد نظر برای تماس دوم
                        secondCallAction.Context = "from-internal"; // Context مورد نظر
                        secondCallAction.Exten = "1000";//this.textBoxActionToNumber.Text;//this.textBoxActionToNumber.Text; // شماره موبایل دیگر مورد نظر
                        secondCallAction.Priority = "1";
                        secondCallAction.Timeout = 30000;

                        ManagerResponse secondCallResponse = manager.SendAction(secondCallAction, secondCallAction.Timeout);

                        // بررسی نتیجه تماس دوم
                        if (secondCallResponse.IsSuccess())
                        {
                            Console.WriteLine("تماس دوم با موفقیت برقرار شد.");
                        }
                        else
                        {
                            Console.WriteLine("خطا در برقراری تماس دوم: " + secondCallResponse.Message);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("خطا در برقراری تماس: " + firstCallResponse.Message);
                }
                #endregion
            }else if (radioButtonActionMode4.Checked)
            {
                #region
              
                #endregion


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
