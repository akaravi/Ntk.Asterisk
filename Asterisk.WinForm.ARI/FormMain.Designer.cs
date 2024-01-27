namespace Asterisk.WinForm.ARI
{
    partial class FormMain
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            lable1 = new Label();
            tbAddress = new TextBox();
            groupBox1 = new GroupBox();
            label8 = new Label();
            tbApplication = new TextBox();
            btnDisconnect = new Button();
            btnConnect = new Button();
            label4 = new Label();
            label3 = new Label();
            tbUser = new TextBox();
            tbPassword = new TextBox();
            label2 = new Label();
            tbPort = new TextBox();
            groupBoxAction = new GroupBox();
            radioButtonActionMode2 = new RadioButton();
            radioButtonActionMode1 = new RadioButton();
            label7 = new Label();
            textBoxActionFromChannel = new TextBox();
            label6 = new Label();
            textBoxActionCallerId = new TextBox();
            buttonActionHungup = new Button();
            buttonActionCall = new Button();
            label5 = new Label();
            textBoxActionToNumber = new TextBox();
            label1 = new Label();
            textBoxActionFromNumber = new TextBox();
            groupBox1.SuspendLayout();
            groupBoxAction.SuspendLayout();
            SuspendLayout();
            // 
            // lable1
            // 
            lable1.AutoSize = true;
            lable1.Location = new Point(7, 28);
            lable1.Margin = new Padding(4, 0, 4, 0);
            lable1.Name = "lable1";
            lable1.Size = new Size(32, 15);
            lable1.TabIndex = 0;
            lable1.Text = "Host";
            // 
            // tbAddress
            // 
            tbAddress.Location = new Point(110, 22);
            tbAddress.Margin = new Padding(4, 3, 4, 3);
            tbAddress.Name = "tbAddress";
            tbAddress.Size = new Size(116, 23);
            tbAddress.TabIndex = 1;
            tbAddress.Text = "192.168.15.10";
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(label8);
            groupBox1.Controls.Add(tbApplication);
            groupBox1.Controls.Add(btnDisconnect);
            groupBox1.Controls.Add(btnConnect);
            groupBox1.Controls.Add(label4);
            groupBox1.Controls.Add(label3);
            groupBox1.Controls.Add(tbUser);
            groupBox1.Controls.Add(tbPassword);
            groupBox1.Controls.Add(label2);
            groupBox1.Controls.Add(tbPort);
            groupBox1.Controls.Add(lable1);
            groupBox1.Controls.Add(tbAddress);
            groupBox1.Location = new Point(1, 1);
            groupBox1.Margin = new Padding(4, 3, 4, 3);
            groupBox1.Name = "groupBox1";
            groupBox1.Padding = new Padding(4, 3, 4, 3);
            groupBox1.Size = new Size(233, 210);
            groupBox1.TabIndex = 2;
            groupBox1.TabStop = false;
            groupBox1.Text = "Parameters connection";
            groupBox1.Enter += groupBox1_Enter;
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new Point(7, 147);
            label8.Margin = new Padding(4, 0, 4, 0);
            label8.Name = "label8";
            label8.Size = new Size(68, 15);
            label8.TabIndex = 10;
            label8.Text = "Application";
            // 
            // tbApplication
            // 
            tbApplication.Location = new Point(110, 141);
            tbApplication.Margin = new Padding(4, 3, 4, 3);
            tbApplication.Name = "tbApplication";
            tbApplication.Size = new Size(116, 23);
            tbApplication.TabIndex = 11;
            tbApplication.Text = "bridge-ntk";
            // 
            // btnDisconnect
            // 
            btnDisconnect.Enabled = false;
            btnDisconnect.Location = new Point(108, 177);
            btnDisconnect.Margin = new Padding(4, 3, 4, 3);
            btnDisconnect.Name = "btnDisconnect";
            btnDisconnect.Size = new Size(117, 27);
            btnDisconnect.TabIndex = 9;
            btnDisconnect.Text = "Disconnect";
            btnDisconnect.UseVisualStyleBackColor = true;
            btnDisconnect.Click += btnDisconnect_Click;
            // 
            // btnConnect
            // 
            btnConnect.Location = new Point(8, 177);
            btnConnect.Margin = new Padding(4, 3, 4, 3);
            btnConnect.Name = "btnConnect";
            btnConnect.Size = new Size(88, 27);
            btnConnect.TabIndex = 8;
            btnConnect.Text = "Connect";
            btnConnect.UseVisualStyleBackColor = true;
            btnConnect.Click += btnConnect_Click;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(7, 118);
            label4.Margin = new Padding(4, 0, 4, 0);
            label4.Name = "label4";
            label4.Size = new Size(57, 15);
            label4.TabIndex = 6;
            label4.Text = "Password";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(7, 88);
            label3.Margin = new Padding(4, 0, 4, 0);
            label3.Name = "label3";
            label3.Size = new Size(30, 15);
            label3.TabIndex = 4;
            label3.Text = "User";
            // 
            // tbUser
            // 
            tbUser.Location = new Point(110, 82);
            tbUser.Margin = new Padding(4, 3, 4, 3);
            tbUser.Name = "tbUser";
            tbUser.Size = new Size(116, 23);
            tbUser.TabIndex = 5;
            tbUser.Text = "asterisk";
            // 
            // tbPassword
            // 
            tbPassword.Location = new Point(110, 112);
            tbPassword.Margin = new Padding(4, 3, 4, 3);
            tbPassword.Name = "tbPassword";
            tbPassword.Size = new Size(116, 23);
            tbPassword.TabIndex = 7;
            tbPassword.Text = "asterisk";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(7, 58);
            label2.Margin = new Padding(4, 0, 4, 0);
            label2.Name = "label2";
            label2.Size = new Size(29, 15);
            label2.TabIndex = 2;
            label2.Text = "Port";
            // 
            // tbPort
            // 
            tbPort.Location = new Point(110, 52);
            tbPort.Margin = new Padding(4, 3, 4, 3);
            tbPort.Name = "tbPort";
            tbPort.Size = new Size(116, 23);
            tbPort.TabIndex = 3;
            tbPort.Text = "8088";
            // 
            // groupBoxAction
            // 
            groupBoxAction.Controls.Add(radioButtonActionMode2);
            groupBoxAction.Controls.Add(radioButtonActionMode1);
            groupBoxAction.Controls.Add(label7);
            groupBoxAction.Controls.Add(textBoxActionFromChannel);
            groupBoxAction.Controls.Add(label6);
            groupBoxAction.Controls.Add(textBoxActionCallerId);
            groupBoxAction.Controls.Add(buttonActionHungup);
            groupBoxAction.Controls.Add(buttonActionCall);
            groupBoxAction.Controls.Add(label5);
            groupBoxAction.Controls.Add(textBoxActionToNumber);
            groupBoxAction.Controls.Add(label1);
            groupBoxAction.Controls.Add(textBoxActionFromNumber);
            groupBoxAction.Location = new Point(1, 236);
            groupBoxAction.Name = "groupBoxAction";
            groupBoxAction.Size = new Size(233, 244);
            groupBoxAction.TabIndex = 3;
            groupBoxAction.TabStop = false;
            groupBoxAction.Text = "Action";
            // 
            // radioButtonActionMode2
            // 
            radioButtonActionMode2.AutoSize = true;
            radioButtonActionMode2.Location = new Point(8, 163);
            radioButtonActionMode2.Name = "radioButtonActionMode2";
            radioButtonActionMode2.Size = new Size(105, 19);
            radioButtonActionMode2.TabIndex = 19;
            radioButtonActionMode2.Text = "After Call From";
            radioButtonActionMode2.UseVisualStyleBackColor = true;
            // 
            // radioButtonActionMode1
            // 
            radioButtonActionMode1.AutoSize = true;
            radioButtonActionMode1.Checked = true;
            radioButtonActionMode1.Location = new Point(8, 138);
            radioButtonActionMode1.Name = "radioButtonActionMode1";
            radioButtonActionMode1.Size = new Size(83, 19);
            radioButtonActionMode1.TabIndex = 18;
            radioButtonActionMode1.TabStop = true;
            radioButtonActionMode1.Text = "Same Time";
            radioButtonActionMode1.UseVisualStyleBackColor = true;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(4, 48);
            label7.Margin = new Padding(4, 0, 4, 0);
            label7.Name = "label7";
            label7.Size = new Size(82, 15);
            label7.TabIndex = 16;
            label7.Text = "From Channel";
            // 
            // textBoxActionFromChannel
            // 
            textBoxActionFromChannel.Location = new Point(107, 42);
            textBoxActionFromChannel.Margin = new Padding(4, 3, 4, 3);
            textBoxActionFromChannel.Name = "textBoxActionFromChannel";
            textBoxActionFromChannel.Size = new Size(116, 23);
            textBoxActionFromChannel.TabIndex = 17;
            textBoxActionFromChannel.Text = "DAHDI/g1";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(4, 19);
            label6.Margin = new Padding(4, 0, 4, 0);
            label6.Name = "label6";
            label6.Size = new Size(81, 15);
            label6.TabIndex = 14;
            label6.Text = "From Caller Id";
            // 
            // textBoxActionCallerId
            // 
            textBoxActionCallerId.Location = new Point(107, 13);
            textBoxActionCallerId.Margin = new Padding(4, 3, 4, 3);
            textBoxActionCallerId.Name = "textBoxActionCallerId";
            textBoxActionCallerId.Size = new Size(116, 23);
            textBoxActionCallerId.TabIndex = 15;
            textBoxActionCallerId.Text = "NTK Auto";
            // 
            // buttonActionHungup
            // 
            buttonActionHungup.Enabled = false;
            buttonActionHungup.Location = new Point(106, 211);
            buttonActionHungup.Margin = new Padding(4, 3, 4, 3);
            buttonActionHungup.Name = "buttonActionHungup";
            buttonActionHungup.Size = new Size(117, 27);
            buttonActionHungup.TabIndex = 10;
            buttonActionHungup.Text = "hungup";
            buttonActionHungup.UseVisualStyleBackColor = true;
            buttonActionHungup.Click += buttonActionHungup_Click;
            // 
            // buttonActionCall
            // 
            buttonActionCall.Location = new Point(6, 211);
            buttonActionCall.Margin = new Padding(4, 3, 4, 3);
            buttonActionCall.Name = "buttonActionCall";
            buttonActionCall.Size = new Size(88, 27);
            buttonActionCall.TabIndex = 10;
            buttonActionCall.Text = "Call";
            buttonActionCall.UseVisualStyleBackColor = true;
            buttonActionCall.Click += buttonActionCall_Click;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(3, 77);
            label5.Margin = new Padding(4, 0, 4, 0);
            label5.Name = "label5";
            label5.Size = new Size(82, 15);
            label5.TabIndex = 12;
            label5.Text = "From Number";
            // 
            // textBoxActionToNumber
            // 
            textBoxActionToNumber.Location = new Point(106, 71);
            textBoxActionToNumber.Margin = new Padding(4, 3, 4, 3);
            textBoxActionToNumber.Name = "textBoxActionToNumber";
            textBoxActionToNumber.Size = new Size(116, 23);
            textBoxActionToNumber.TabIndex = 13;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(4, 106);
            label1.Margin = new Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new Size(66, 15);
            label1.TabIndex = 10;
            label1.Text = "To Number";
            // 
            // textBoxActionFromNumber
            // 
            textBoxActionFromNumber.Location = new Point(107, 100);
            textBoxActionFromNumber.Margin = new Padding(4, 3, 4, 3);
            textBoxActionFromNumber.Name = "textBoxActionFromNumber";
            textBoxActionFromNumber.Size = new Size(116, 23);
            textBoxActionFromNumber.TabIndex = 11;
            // 
            // FormMain
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(237, 508);
            Controls.Add(groupBoxAction);
            Controls.Add(groupBox1);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Margin = new Padding(4, 3, 4, 3);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FormMain";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Client for Asterisk";
            TopMost = true;
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBoxAction.ResumeLayout(false);
            groupBoxAction.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Label lable1;
        private System.Windows.Forms.TextBox tbAddress;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox tbUser;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tbPort;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox tbPassword;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.Button btnDisconnect;
        private GroupBox groupBoxAction;
        private Button buttonActionHungup;
        private Button buttonActionCall;
        private Label label5;
        private TextBox textBoxActionToNumber;
        private Label label1;
        private TextBox textBoxActionFromNumber;
        private Label label6;
        private TextBox textBoxActionCallerId;
        private Label label7;
        private TextBox textBoxActionFromChannel;
        private RadioButton radioButtonActionMode1;
        private RadioButton radioButtonActionMode2;
        private Label label8;
        private TextBox tbApplication;
    }
}
