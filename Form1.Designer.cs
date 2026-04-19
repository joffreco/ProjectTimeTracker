namespace ProjectTimeTracker;

partial class Form1
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
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
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        this.lblToggle = new Label();
        this.btnNone = new Button();
        this.flpProjectButtons = new FlowLayoutPanel();
        this.lblStatus = new TextBox();
        this.lstNav = new ListBox();
        this.pnlContent = new Panel();
        this.pnlBottom = new Panel();
        this.SuspendLayout();
        //
        // lstNav (left navigation)
        //
        this.lstNav.Dock = DockStyle.Left;
        this.lstNav.Width = 160;
        this.lstNav.IntegralHeight = false;
        this.lstNav.Font = new Font("Segoe UI", 10F);
        this.lstNav.BorderStyle = BorderStyle.FixedSingle;
        this.lstNav.Name = "lstNav";
        this.lstNav.TabIndex = 0;
        //
        // pnlContent (center)
        //
        this.pnlContent.Dock = DockStyle.Fill;
        this.pnlContent.Padding = new Padding(0);
        this.pnlContent.Name = "pnlContent";
        this.pnlContent.TabIndex = 1;
        //
        // lblToggle
        //
        this.lblToggle.AutoSize = true;
        this.lblToggle.Location = new System.Drawing.Point(8, 6);
        this.lblToggle.Name = "lblToggle";
        this.lblToggle.Text = "Toggle active project";
        //
        // btnNone
        //
        this.btnNone.Location = new System.Drawing.Point(8, 26);
        this.btnNone.Size = new System.Drawing.Size(82, 28);
        this.btnNone.Name = "btnNone";
        this.btnNone.Text = "None";
        this.btnNone.UseVisualStyleBackColor = true;
        this.btnNone.Click += new System.EventHandler(this.btnNone_Click);
        //
        // flpProjectButtons
        //
        this.flpProjectButtons.AutoScroll = true;
        this.flpProjectButtons.Location = new System.Drawing.Point(96, 26);
        this.flpProjectButtons.Size = new System.Drawing.Size(870, 60);
        this.flpProjectButtons.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.flpProjectButtons.Name = "flpProjectButtons";
        //
        // lblStatus
        //
        this.lblStatus.BorderStyle = BorderStyle.None;
        this.lblStatus.Location = new System.Drawing.Point(8, 92);
        this.lblStatus.Size = new System.Drawing.Size(960, 20);
        this.lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        this.lblStatus.Multiline = true;
        this.lblStatus.ReadOnly = true;
        this.lblStatus.Name = "lblStatus";
        this.lblStatus.Text = "Connecting...";
        //
        // pnlBottom (toggle area, always visible)
        //
        this.pnlBottom.Dock = DockStyle.Bottom;
        this.pnlBottom.Height = 120;
        this.pnlBottom.BorderStyle = BorderStyle.FixedSingle;
        this.pnlBottom.Name = "pnlBottom";
        this.pnlBottom.Controls.Add(this.lblToggle);
        this.pnlBottom.Controls.Add(this.btnNone);
        this.pnlBottom.Controls.Add(this.flpProjectButtons);
        this.pnlBottom.Controls.Add(this.lblStatus);
        //
        // Form1
        //
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(980, 600);
        // Order matters: Fill must be added first so Dock=Left/Bottom claim space first.
        this.Controls.Add(this.pnlContent);
        this.Controls.Add(this.lstNav);
        this.Controls.Add(this.pnlBottom);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
        this.MaximizeBox = true;
        this.Name = "Form1";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "ProjectTimeTracker";
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion

    private Label lblToggle;
    private Button btnNone;
    private FlowLayoutPanel flpProjectButtons;
    private TextBox lblStatus;
    private ListBox lstNav;
    private Panel pnlContent;
    private Panel pnlBottom;
}
