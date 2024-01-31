using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using System.Diagnostics;

using Ntk.AsterNet.ARI;
using Ntk.AsterNet.ARI.Models;


namespace Asterisk.WinForm.ARI
{
    public partial class FormMain : Form
    {
        public FormMain()
        {
            InitializeComponent();
            groupBoxAction.Visible = false;





        }

        public AriClient ActionClient;
        private void btnConnect_Click(object sender, EventArgs e)
        {
            string address = this.tbAddress.Text;
            int port = int.Parse(this.tbPort.Text);
            string user = this.tbUser.Text;
            string password = this.tbPassword.Text;

            btnConnect.Enabled = false;
            // Create a new Ari Connection
            ActionClient = new AriClient(new StasisEndpoint(address, port, user, password), tbApplication.Text);
            // Hook into required events
            ActionClient.OnStasisStartEvent += c_OnStasisStartEvent;
            ActionClient.OnChannelDtmfReceivedEvent += ActionClientOnChannelDtmfReceivedEvent;
            ActionClient.OnConnectionStateChanged += ActionClientOnConnectionStateChanged;
            try
            {
                ActionClient.Connect();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error connect\n" + ex.Message);

                this.Close();
            }
            btnDisconnect.Enabled = true;
            groupBoxAction.Visible = true;
            buttonActionCall.Enabled = true;
            buttonActionHungup.Enabled = false;
        }



        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            btnConnect.Enabled = true;
            if (this.ActionClient != null)
            {
                ActionClient.Disconnect();
                this.ActionClient = null;
            }
            this.buttonActionHungup_Click(sender, e);
            btnDisconnect.Enabled = false;
            groupBoxAction.Visible = false;
            buttonActionCall.Enabled = true;
            buttonActionHungup.Enabled = false;

        }
        private bool ActionCallStatus = false;

        public Bridge simpleBridge;


        private void buttonActionCall_Click(object sender, EventArgs e)
        {
            if (ActionCallStatus)
                return;

            ActionCallStatus = true;
            buttonActionCall.Enabled = !ActionCallStatus;
            buttonActionHungup.Enabled = ActionCallStatus;

            if (radioButtonActionMode1.Checked)
            {
                #region
                //ActionClient.Asterisk.ca
                try
                {

                    // Create simple bridge
                    simpleBridge = ActionClient.Bridges.Create("mixing", Guid.NewGuid().ToString(), tbApplication.Text);

                    // subscribe to bridge events
                    ActionClient.Applications.Subscribe(tbApplication.Text, "bridge:" + simpleBridge.Id);

                    // start MOH on bridge
                    ActionClient.Bridges.StartMoh(simpleBridge.Id, "default");




                    var c=ActionClient.Channels.Create("", tbApplication.Text);
                




                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }

                //OriginateAction oc = new OriginateAction();
                //oc.Timeout = 15000;
                //oc.Context = "from-internal";
                //oc.Priority = "1";
                //oc.CallerId = this.textBoxActionCallerId.Text;
                //if (string.IsNullOrEmpty(this.textBoxActionFromChannel.Text))
                //    this.textBoxActionFromChannel.Text = "DAHDI/g1";
                //if (this.textBoxActionFromChannel.Text.LastIndexOf("/") < this.textBoxActionFromChannel.Text.Length - 1)
                //    this.textBoxActionFromChannel.Text = this.textBoxActionFromChannel.Text + "/";
                //if (this.textBoxActionFromNumber.Text.Length < 3)
                //{
                //    oc.Channel = "SIP/" + this.textBoxActionFromNumber.Text;
                //}
                //else
                //{
                //    oc.Channel = this.textBoxActionFromChannel.Text + this.textBoxActionFromNumber.Text;
                //}
                //oc.Exten = this.textBoxActionToNumber.Text;
                //originateResponse = manager.SendAction(oc, oc.Timeout);
                #endregion
            }
            else if (radioButtonActionMode2.Checked)
            {
                #region

                #endregion
            }



        }

        private void buttonActionHungup_Click(object sender, EventArgs e)
        {
            if (!ActionCallStatus)
                return;

            ActionClient.Bridges.Destroy(simpleBridge.Id);
            ActionCallStatus = false;
            buttonActionCall.Enabled = true;
            buttonActionHungup.Enabled = false;
        }
        #region DialEndEvent

        private void c_OnStasisStartEvent(IAriClient sender, StasisStartEvent e)
        {
            // Answer the channel
            sender.Channels.Answer(e.Channel.Id);

            // Play an announcement
            //sender.Channels.Play(e.Channel.Id, "sound:hello-world");
        }
        private void ActionClientOnConnectionStateChanged(object sender)
        {
            System.Console.WriteLine("Connection state is now {0}", ActionClient?.Connected);
        }

        private void ActionClientOnChannelDtmfReceivedEvent(IAriClient sender, ChannelDtmfReceivedEvent e)
        {
            // When DTMF received
            switch (e.Digit)
            {
                case "*":
                    sender.Channels.Play(e.Channel.Id, "sound:asterisk-friend");
                    break;
                case "#":
                    sender.Channels.Play(e.Channel.Id, "sound:goodbye");
                    sender.Channels.Hangup(e.Channel.Id, "normal");
                    break;
                default:
                    sender.Channels.Play(e.Channel.Id, string.Format("sound:digits/{0}", e.Digit));
                    break;
            }
        }
        #endregion


     
    }
}