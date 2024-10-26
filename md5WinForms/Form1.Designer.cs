namespace md5WinForms
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Button btnSelectFolder;
        // private System.Windows.Forms.Button btnFindDuplicates;
        private System.Windows.Forms.ListView listView1;
        private System.Windows.Forms.ColumnHeader filePathColumn1;
        private System.Windows.Forms.ColumnHeader filePathColumn2;
        private System.Windows.Forms.ColumnHeader md5Column;
        private System.Windows.Forms.ColumnHeader moreThanTwoColumn;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Label lblScanTime;
        private System.Windows.Forms.Label lblMd5Count;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.FlowLayoutPanel statusPanel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

    private void InitializeComponent()
{
    this.btnSelectFolder = new System.Windows.Forms.Button();
    this.listView1 = new System.Windows.Forms.ListView();
    this.filePathColumn1 = new System.Windows.Forms.ColumnHeader();
    this.filePathColumn2 = new System.Windows.Forms.ColumnHeader();
    this.md5Column = new System.Windows.Forms.ColumnHeader();
    this.moreThanTwoColumn = new System.Windows.Forms.ColumnHeader();
    this.progressBar1 = new System.Windows.Forms.ProgressBar();
    this.lblStatus = new System.Windows.Forms.Label();
    this.lblScanTime = new System.Windows.Forms.Label();
    this.lblMd5Count = new System.Windows.Forms.Label();
    this.btnSave = new System.Windows.Forms.Button();
    this.statusPanel = new System.Windows.Forms.FlowLayoutPanel();
    this.SuspendLayout();

    // 
    // btnSelectFolder
    // 
    this.btnSelectFolder.Location = new System.Drawing.Point(12, 12);
    this.btnSelectFolder.Name = "btnSelectFolder";
    this.btnSelectFolder.Size = new System.Drawing.Size(120, 40);
    this.btnSelectFolder.TabIndex = 0;
    this.btnSelectFolder.Text = "选择U盘目录";
    this.btnSelectFolder.UseVisualStyleBackColor = true;
    this.btnSelectFolder.Click += new System.EventHandler(this.btnSelectFolder_Click);

    // 
    // listView1
    // 
    this.listView1.Columns.AddRange(new System.Windows.Forms.ColumnHeader[]
    {
        this.filePathColumn1,
        this.filePathColumn2,
        this.md5Column,
        this.moreThanTwoColumn
    });
    this.listView1.FullRowSelect = true;
    this.listView1.GridLines = true;
    this.listView1.Location = new System.Drawing.Point(12, 70);
    this.listView1.Name = "listView1";
    this.listView1.Size = new System.Drawing.Size(776, 300);
    this.listView1.TabIndex = 1;
    this.listView1.View = System.Windows.Forms.View.Details;

// 
    // filePathColumn1
    // 
    this.filePathColumn1.Text = "文件路径1";
    this.filePathColumn1.Width = 300;
            
    // 
    // filePathColumn2
    // 
    this.filePathColumn2.Text = "文件路径2";
    this.filePathColumn2.Width = 300;
            
    // 
    // md5Column
    // 
    this.md5Column.Text = "MD5";
    this.md5Column.Width = 200;

    // 
    // moreThanTwoColumn
    // 
    this.moreThanTwoColumn.Text = "是否超过2个";
    this.moreThanTwoColumn.Width = 100;



    // 
    // progressBar1
    // 
    this.progressBar1.Location = new System.Drawing.Point(12, 380);
    this.progressBar1.Name = "progressBar1";
    this.progressBar1.Size = new System.Drawing.Size(776, 23);
    this.progressBar1.TabIndex = 2;

    // 
    // lblStatus
    // 
    this.lblStatus.AutoSize = true;
    this.lblStatus.Location = new System.Drawing.Point(12, 415);
    this.lblStatus.Name = "lblStatus";
    this.lblStatus.Size = new System.Drawing.Size(67, 15);
    this.lblStatus.TabIndex = 3;
    this.lblStatus.Text = "准备就绪";

    // 
    // statusPanel
    // 
    this.statusPanel.Location = new System.Drawing.Point(12, 440); // 在 lblStatus 下方
    this.statusPanel.Name = "statusPanel";
    this.statusPanel.Size = new System.Drawing.Size(776, 30);
    this.statusPanel.TabIndex = 4;
    this.statusPanel.FlowDirection = FlowDirection.LeftToRight; // 从左到右排列
    this.statusPanel.WrapContents = false; // 不换行
    this.statusPanel.AutoSize = true;
    this.statusPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
    this.statusPanel.BorderStyle = BorderStyle.None;
    this.statusPanel.Padding = new Padding(0);
    this.statusPanel.Margin = new Padding(0);
    this.statusPanel.Controls.Add(this.lblScanTime);
    this.statusPanel.Controls.Add(this.lblMd5Count);

    // 
    // lblScanTime
    // 
    this.lblScanTime.AutoSize = true;
    this.lblScanTime.Text = "扫描时间：0 秒";
    this.lblScanTime.Margin = new Padding(0, 0, 100, 0); // 右侧添加50像素的间距

  

    // 
    // lblMd5Count
    // 
    this.lblMd5Count.AutoSize = true;
    this.lblMd5Count.Location = new System.Drawing.Point(130, 0); // 放在同一行
    this.lblMd5Count.Name = "lblMd5Count";
    this.lblMd5Count.Size = new System.Drawing.Size(150, 15);
    this.lblMd5Count.Text = "MD5 相同文件数量：0";

    // 
    // btnSave
    // 
    this.btnSave.Location = new System.Drawing.Point(668, 12);
    this.btnSave.Name = "btnSave";
    this.btnSave.Size = new System.Drawing.Size(120, 40);
    this.btnSave.TabIndex = 5;
    this.btnSave.Text = "保存MD5对照表";
    this.btnSave.UseVisualStyleBackColor = true;
    this.btnSave.Click += new System.EventHandler(this.btnSave_Click);

    // 
    // Form1
    // 
    this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
    this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
    this.ClientSize = new System.Drawing.Size(800, 500); // 调整窗体大小
    this.Controls.Add(this.statusPanel); // 添加Panel到窗体
    this.Controls.Add(this.lblStatus);
    this.Controls.Add(this.progressBar1);
    this.Controls.Add(this.listView1);
    this.Controls.Add(this.btnSelectFolder);
    this.Controls.Add(this.btnSave);
    this.Name = "Form1";
    this.Text = "U盘文件MD5扫描器";
    this.ResumeLayout(false);
    this.PerformLayout();
}
    }
}
