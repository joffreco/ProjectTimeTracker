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
        this.lblSecret = new Label();
        this.txtSecretPath = new TextBox();
        this.btnBrowse = new Button();
        this.btnConnect = new Button();
        this.lblProjectName = new Label();
        this.txtProjectName = new TextBox();
        this.btnAddProject = new Button();
        this.btnDeleteProject = new Button();
        this.lstProjects = new ListBox();
        this.lblToggle = new Label();
        this.btnNone = new Button();
        this.flpProjectButtons = new FlowLayoutPanel();
        this.lstDocs = new ListBox();
        this.lblStatus = new TextBox();
        this.SuspendLayout();
        // 
        // lblSecret
        // 
        this.lblSecret.AutoSize = true;
        this.lblSecret.Location = new System.Drawing.Point(18, 18);
        this.lblSecret.Name = "lblSecret";
        this.lblSecret.Size = new System.Drawing.Size(130, 15);
        this.lblSecret.TabIndex = 0;
        this.lblSecret.Text = "Client secret JSON path";
        // 
        // txtSecretPath
        // 
        this.txtSecretPath.Location = new System.Drawing.Point(18, 40);
        this.txtSecretPath.Name = "txtSecretPath";
        this.txtSecretPath.Size = new System.Drawing.Size(497, 23);
        this.txtSecretPath.TabIndex = 1;
        // 
        // btnBrowse
        // 
        this.btnBrowse.Location = new System.Drawing.Point(525, 39);
        this.btnBrowse.Name = "btnBrowse";
        this.btnBrowse.Size = new System.Drawing.Size(85, 25);
        this.btnBrowse.TabIndex = 2;
        this.btnBrowse.Text = "Browse";
        this.btnBrowse.UseVisualStyleBackColor = true;
        this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
        // 
        // btnConnect
        // 
        this.btnConnect.Location = new System.Drawing.Point(618, 39);
        this.btnConnect.Name = "btnConnect";
        this.btnConnect.Size = new System.Drawing.Size(119, 25);
        this.btnConnect.TabIndex = 3;
        this.btnConnect.Text = "Connect";
        this.btnConnect.UseVisualStyleBackColor = true;
        this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
        // 
        // lblProjectName
        // 
        this.lblProjectName.AutoSize = true;
        this.lblProjectName.Location = new System.Drawing.Point(18, 83);
        this.lblProjectName.Name = "lblProjectName";
        this.lblProjectName.Size = new System.Drawing.Size(75, 15);
        this.lblProjectName.TabIndex = 4;
        this.lblProjectName.Text = "Project name";
        // 
        // txtProjectName
        // 
        this.txtProjectName.Location = new System.Drawing.Point(18, 105);
        this.txtProjectName.Name = "txtProjectName";
        this.txtProjectName.Size = new System.Drawing.Size(220, 23);
        this.txtProjectName.TabIndex = 5;
        // 
        // btnAddProject
        // 
        this.btnAddProject.Location = new System.Drawing.Point(248, 104);
        this.btnAddProject.Name = "btnAddProject";
        this.btnAddProject.Size = new System.Drawing.Size(90, 25);
        this.btnAddProject.TabIndex = 6;
        this.btnAddProject.Text = "Add";
        this.btnAddProject.UseVisualStyleBackColor = true;
        this.btnAddProject.Click += new System.EventHandler(this.btnAddProject_Click);
        // 
        // btnDeleteProject
        // 
        this.btnDeleteProject.Location = new System.Drawing.Point(344, 104);
        this.btnDeleteProject.Name = "btnDeleteProject";
        this.btnDeleteProject.Size = new System.Drawing.Size(90, 25);
        this.btnDeleteProject.TabIndex = 7;
        this.btnDeleteProject.Text = "Delete";
        this.btnDeleteProject.UseVisualStyleBackColor = true;
        this.btnDeleteProject.Click += new System.EventHandler(this.btnDeleteProject_Click);
        // 
        // lstProjects
        // 
        this.lstProjects.FormattingEnabled = true;
        this.lstProjects.Location = new System.Drawing.Point(18, 137);
        this.lstProjects.Name = "lstProjects";
        this.lstProjects.Size = new System.Drawing.Size(416, 139);
        this.lstProjects.TabIndex = 8;
        // 
        // lblToggle
        // 
        this.lblToggle.AutoSize = true;
        this.lblToggle.Location = new System.Drawing.Point(450, 83);
        this.lblToggle.Name = "lblToggle";
        this.lblToggle.Size = new System.Drawing.Size(117, 15);
        this.lblToggle.TabIndex = 9;
        this.lblToggle.Text = "Toggle active project";
        // 
        // btnNone
        // 
        this.btnNone.Location = new System.Drawing.Point(450, 104);
        this.btnNone.Name = "btnNone";
        this.btnNone.Size = new System.Drawing.Size(82, 25);
        this.btnNone.TabIndex = 10;
        this.btnNone.Text = "None";
        this.btnNone.UseVisualStyleBackColor = true;
        this.btnNone.Click += new System.EventHandler(this.btnNone_Click);
        // 
        // flpProjectButtons
        // 
        this.flpProjectButtons.AutoScroll = true;
        this.flpProjectButtons.Location = new System.Drawing.Point(538, 104);
        this.flpProjectButtons.Name = "flpProjectButtons";
        this.flpProjectButtons.Size = new System.Drawing.Size(424, 172);
        this.flpProjectButtons.TabIndex = 11;
        // 
        // lstDocs
        // 
        this.lstDocs.FormattingEnabled = true;
        this.lstDocs.Location = new System.Drawing.Point(18, 294);
        this.lstDocs.Name = "lstDocs";
        this.lstDocs.Size = new System.Drawing.Size(944, 229);
        this.lstDocs.TabIndex = 12;
        // 
        // lblStatus
        // 
        this.lblStatus.BorderStyle = BorderStyle.None;
        this.lblStatus.Location = new System.Drawing.Point(18, 535);
        this.lblStatus.Multiline = true;
        this.lblStatus.Name = "lblStatus";
        this.lblStatus.ReadOnly = true;
        this.lblStatus.Size = new System.Drawing.Size(944, 22);
        this.lblStatus.TabIndex = 13;
        this.lblStatus.Text = "Select JSON to start";
        // 
        // Form1
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(980, 565);
        this.Controls.Add(this.lblStatus);
        this.Controls.Add(this.lstDocs);
        this.Controls.Add(this.flpProjectButtons);
        this.Controls.Add(this.btnNone);
        this.Controls.Add(this.lblToggle);
        this.Controls.Add(this.lstProjects);
        this.Controls.Add(this.btnDeleteProject);
        this.Controls.Add(this.btnAddProject);
        this.Controls.Add(this.txtProjectName);
        this.Controls.Add(this.lblProjectName);
        this.Controls.Add(this.btnConnect);
        this.Controls.Add(this.btnBrowse);
        this.Controls.Add(this.txtSecretPath);
        this.Controls.Add(this.lblSecret);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.Name = "Form1";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "ProjectTimeTracker";
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion

    private Label lblSecret;
    private TextBox txtSecretPath;
    private Button btnBrowse;
    private Button btnConnect;
    private Label lblProjectName;
    private TextBox txtProjectName;
    private Button btnAddProject;
    private Button btnDeleteProject;
    private ListBox lstProjects;
    private Label lblToggle;
    private Button btnNone;
    private FlowLayoutPanel flpProjectButtons;
    private ListBox lstDocs;
    private TextBox lblStatus;
}
