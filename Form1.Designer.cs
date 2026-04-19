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
        // lblProjectName
        // 
        this.lblProjectName.AutoSize = true;
        this.lblProjectName.Location = new System.Drawing.Point(18, 22);
        this.lblProjectName.Name = "lblProjectName";
        this.lblProjectName.Size = new System.Drawing.Size(75, 15);
        this.lblProjectName.TabIndex = 1;
        this.lblProjectName.Text = "Project name";
        // 
        // txtProjectName
        // 
        this.txtProjectName.Location = new System.Drawing.Point(18, 44);
        this.txtProjectName.Name = "txtProjectName";
        this.txtProjectName.Size = new System.Drawing.Size(220, 23);
        this.txtProjectName.TabIndex = 2;
        // 
        // btnAddProject
        // 
        this.btnAddProject.Location = new System.Drawing.Point(248, 43);
        this.btnAddProject.Name = "btnAddProject";
        this.btnAddProject.Size = new System.Drawing.Size(90, 25);
        this.btnAddProject.TabIndex = 3;
        this.btnAddProject.Text = "Add";
        this.btnAddProject.UseVisualStyleBackColor = true;
        this.btnAddProject.Click += new System.EventHandler(this.btnAddProject_Click);
        // 
        // btnDeleteProject
        // 
        this.btnDeleteProject.Location = new System.Drawing.Point(344, 43);
        this.btnDeleteProject.Name = "btnDeleteProject";
        this.btnDeleteProject.Size = new System.Drawing.Size(90, 25);
        this.btnDeleteProject.TabIndex = 4;
        this.btnDeleteProject.Text = "Delete";
        this.btnDeleteProject.UseVisualStyleBackColor = true;
        this.btnDeleteProject.Click += new System.EventHandler(this.btnDeleteProject_Click);
        // 
        // lstProjects
        // 
        this.lstProjects.FormattingEnabled = true;
        this.lstProjects.Location = new System.Drawing.Point(18, 76);
        this.lstProjects.Name = "lstProjects";
        this.lstProjects.Size = new System.Drawing.Size(416, 199);
        this.lstProjects.TabIndex = 5;
        // 
        // lblToggle
        // 
        this.lblToggle.AutoSize = true;
        this.lblToggle.Location = new System.Drawing.Point(450, 22);
        this.lblToggle.Name = "lblToggle";
        this.lblToggle.Size = new System.Drawing.Size(117, 15);
        this.lblToggle.TabIndex = 6;
        this.lblToggle.Text = "Toggle active project";
        // 
        // btnNone
        // 
        this.btnNone.Location = new System.Drawing.Point(450, 43);
        this.btnNone.Name = "btnNone";
        this.btnNone.Size = new System.Drawing.Size(82, 25);
        this.btnNone.TabIndex = 7;
        this.btnNone.Text = "None";
        this.btnNone.UseVisualStyleBackColor = true;
        this.btnNone.Click += new System.EventHandler(this.btnNone_Click);
        // 
        // flpProjectButtons
        // 
        this.flpProjectButtons.AutoScroll = true;
        this.flpProjectButtons.Location = new System.Drawing.Point(538, 76);
        this.flpProjectButtons.Name = "flpProjectButtons";
        this.flpProjectButtons.Size = new System.Drawing.Size(424, 199);
        this.flpProjectButtons.TabIndex = 8;
        // 
        // lstDocs
        // 
        this.lstDocs.FormattingEnabled = true;
        this.lstDocs.Location = new System.Drawing.Point(18, 294);
        this.lstDocs.Name = "lstDocs";
        this.lstDocs.Size = new System.Drawing.Size(944, 229);
        this.lstDocs.TabIndex = 9;
        // 
        // lblStatus
        // 
        this.lblStatus.BorderStyle = BorderStyle.None;
        this.lblStatus.Location = new System.Drawing.Point(18, 535);
        this.lblStatus.Multiline = true;
        this.lblStatus.Name = "lblStatus";
        this.lblStatus.ReadOnly = true;
        this.lblStatus.Size = new System.Drawing.Size(944, 22);
        this.lblStatus.TabIndex = 10;
        this.lblStatus.Text = "Connecting...";
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
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
        this.MaximizeBox = true;
        this.Name = "Form1";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "ProjectTimeTracker";
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion

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
