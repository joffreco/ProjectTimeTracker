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
        this.lblCollection = new Label();
        this.txtCollection = new TextBox();
        this.btnLoadDocs = new Button();
        this.btnAddSample = new Button();
        this.lstDocs = new ListBox();
        this.lblStatus = new Label();
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
        // lblCollection
        // 
        this.lblCollection.AutoSize = true;
        this.lblCollection.Location = new System.Drawing.Point(18, 80);
        this.lblCollection.Name = "lblCollection";
        this.lblCollection.Size = new System.Drawing.Size(93, 15);
        this.lblCollection.TabIndex = 4;
        this.lblCollection.Text = "Collection name";
        // 
        // txtCollection
        // 
        this.txtCollection.Location = new System.Drawing.Point(18, 102);
        this.txtCollection.Name = "txtCollection";
        this.txtCollection.Size = new System.Drawing.Size(190, 23);
        this.txtCollection.TabIndex = 5;
        this.txtCollection.Text = "demo";
        // 
        // btnLoadDocs
        // 
        this.btnLoadDocs.Location = new System.Drawing.Point(221, 101);
        this.btnLoadDocs.Name = "btnLoadDocs";
        this.btnLoadDocs.Size = new System.Drawing.Size(130, 25);
        this.btnLoadDocs.TabIndex = 6;
        this.btnLoadDocs.Text = "Load documents";
        this.btnLoadDocs.UseVisualStyleBackColor = true;
        this.btnLoadDocs.Click += new System.EventHandler(this.btnLoadDocs_Click);
        // 
        // btnAddSample
        // 
        this.btnAddSample.Location = new System.Drawing.Point(362, 101);
        this.btnAddSample.Name = "btnAddSample";
        this.btnAddSample.Size = new System.Drawing.Size(153, 25);
        this.btnAddSample.TabIndex = 7;
        this.btnAddSample.Text = "Add sample document";
        this.btnAddSample.UseVisualStyleBackColor = true;
        this.btnAddSample.Click += new System.EventHandler(this.btnAddSample_Click);
        // 
        // lstDocs
        // 
        this.lstDocs.FormattingEnabled = true;
        this.lstDocs.Location = new System.Drawing.Point(18, 141);
        this.lstDocs.Name = "lstDocs";
        this.lstDocs.Size = new System.Drawing.Size(719, 229);
        this.lstDocs.TabIndex = 8;
        // 
        // lblStatus
        // 
        this.lblStatus.AutoSize = true;
        this.lblStatus.Location = new System.Drawing.Point(18, 386);
        this.lblStatus.Name = "lblStatus";
        this.lblStatus.Size = new System.Drawing.Size(113, 15);
        this.lblStatus.TabIndex = 9;
        this.lblStatus.Text = "Select JSON to start";
        // 
        // Form1
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(756, 420);
        this.Controls.Add(this.lblStatus);
        this.Controls.Add(this.lstDocs);
        this.Controls.Add(this.btnAddSample);
        this.Controls.Add(this.btnLoadDocs);
        this.Controls.Add(this.txtCollection);
        this.Controls.Add(this.lblCollection);
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
    private Label lblCollection;
    private TextBox txtCollection;
    private Button btnLoadDocs;
    private Button btnAddSample;
    private ListBox lstDocs;
    private Label lblStatus;
}
