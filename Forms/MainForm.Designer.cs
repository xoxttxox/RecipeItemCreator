namespace RecipeItemCreator.Forms
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            leftPanel = new Panel();
            exportButton = new Button();
            chooseTemplateButton = new Button();
            chooseItemButton = new Button();
            templatePathTextBox = new RecipeItemCreator.Controls.DarkTextBox();
            itemPathTextBox = new RecipeItemCreator.Controls.DarkTextBox();
            itemIdTextBox = new RecipeItemCreator.Controls.DarkTextBox();
            outputSizeComboBox = new ComboBox();
            templateLabel = new Label();
            itemImageLabel = new Label();
            outputLabel = new Label();
            itemIdLabel = new Label();
            formTitleLabel = new Label();
            footerStatusStrip = new StatusStrip();
            footerStatusLabel = new ToolStripStatusLabel();
            versionStatusLabel = new ToolStripStatusLabel();
            githubStatusLabel = new ToolStripStatusLabel();
            rightPanel = new Panel();
            previewPictureBox = new PictureBox();
            previewTitleLabel = new Label();
            leftPanel.SuspendLayout();
            footerStatusStrip.SuspendLayout();
            rightPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)previewPictureBox).BeginInit();
            SuspendLayout();
            // 
            // leftPanel
            // 
            leftPanel.BackColor = Color.FromArgb(30, 31, 34);
            leftPanel.BorderStyle = BorderStyle.FixedSingle;
            leftPanel.Controls.Add(exportButton);
            leftPanel.Controls.Add(chooseTemplateButton);
            leftPanel.Controls.Add(chooseItemButton);
            leftPanel.Controls.Add(templatePathTextBox);
            leftPanel.Controls.Add(itemPathTextBox);
            leftPanel.Controls.Add(itemIdTextBox);
            leftPanel.Controls.Add(outputSizeComboBox);
            leftPanel.Controls.Add(templateLabel);
            leftPanel.Controls.Add(itemImageLabel);
            leftPanel.Controls.Add(outputLabel);
            leftPanel.Controls.Add(itemIdLabel);
            leftPanel.Controls.Add(formTitleLabel);
            leftPanel.Location = new Point(8, 8);
            leftPanel.Name = "leftPanel";
            leftPanel.Size = new Size(282, 292);
            leftPanel.TabIndex = 0;
            // 
            // exportButton
            // 
            exportButton.BackColor = Color.FromArgb(46, 48, 52);
            exportButton.FlatAppearance.BorderColor = Color.FromArgb(76, 79, 85);
            exportButton.FlatStyle = FlatStyle.Flat;
            exportButton.Location = new Point(10, 250);
            exportButton.Name = "exportButton";
            exportButton.Size = new Size(264, 29);
            exportButton.TabIndex = 16;
            exportButton.Text = "Export PNG";
            exportButton.UseVisualStyleBackColor = false;
            exportButton.Click += ExportButton_Click;
            // 
            // chooseTemplateButton
            // 
            chooseTemplateButton.BackColor = Color.FromArgb(46, 48, 52);
            chooseTemplateButton.FlatAppearance.BorderColor = Color.FromArgb(76, 79, 85);
            chooseTemplateButton.FlatStyle = FlatStyle.Flat;
            chooseTemplateButton.Location = new Point(200, 212);
            chooseTemplateButton.Name = "chooseTemplateButton";
            chooseTemplateButton.Size = new Size(74, 27);
            chooseTemplateButton.TabIndex = 15;
            chooseTemplateButton.Text = "Other...";
            chooseTemplateButton.UseVisualStyleBackColor = false;
            chooseTemplateButton.Click += ChooseTemplateButton_Click;
            // 
            // chooseItemButton
            // 
            chooseItemButton.BackColor = Color.FromArgb(46, 48, 52);
            chooseItemButton.FlatAppearance.BorderColor = Color.FromArgb(76, 79, 85);
            chooseItemButton.FlatStyle = FlatStyle.Flat;
            chooseItemButton.Location = new Point(200, 159);
            chooseItemButton.Name = "chooseItemButton";
            chooseItemButton.Size = new Size(74, 27);
            chooseItemButton.TabIndex = 14;
            chooseItemButton.Text = "Choose...";
            chooseItemButton.UseVisualStyleBackColor = false;
            chooseItemButton.Click += ChooseItemButton_Click;
            // 
            // templatePathTextBox
            // 
            templatePathTextBox.BackColor = Color.FromArgb(40, 42, 46);
            templatePathTextBox.Font = new Font("Segoe UI", 9F);
            templatePathTextBox.ForeColor = Color.FromArgb(236, 238, 241);
            templatePathTextBox.Location = new Point(10, 212);
            templatePathTextBox.MinimumSize = new Size(40, 24);
            templatePathTextBox.Name = "templatePathTextBox";
            templatePathTextBox.ReadOnly = true;
            templatePathTextBox.Size = new Size(184, 27);
            templatePathTextBox.TabIndex = 13;
            templatePathTextBox.Text = "recipe_template.png";
            // 
            // itemPathTextBox
            // 
            itemPathTextBox.BackColor = Color.FromArgb(40, 42, 46);
            itemPathTextBox.Font = new Font("Segoe UI", 9F);
            itemPathTextBox.ForeColor = Color.FromArgb(236, 238, 241);
            itemPathTextBox.Location = new Point(10, 159);
            itemPathTextBox.MinimumSize = new Size(40, 24);
            itemPathTextBox.Name = "itemPathTextBox";
            itemPathTextBox.ReadOnly = true;
            itemPathTextBox.Size = new Size(184, 27);
            itemPathTextBox.TabIndex = 12;
            // 
            // itemIdTextBox
            // 
            itemIdTextBox.BackColor = Color.FromArgb(40, 42, 46);
            itemIdTextBox.Font = new Font("Segoe UI", 9F);
            itemIdTextBox.ForeColor = Color.FromArgb(236, 238, 241);
            itemIdTextBox.Location = new Point(10, 57);
            itemIdTextBox.MinimumSize = new Size(40, 24);
            itemIdTextBox.Name = "itemIdTextBox";
            itemIdTextBox.PlaceholderText = "sushi_recipe";
            itemIdTextBox.Size = new Size(264, 27);
            itemIdTextBox.TabIndex = 9;
            // 
            // outputSizeComboBox
            // 
            outputSizeComboBox.BackColor = Color.FromArgb(40, 42, 46);
            outputSizeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            outputSizeComboBox.FlatStyle = FlatStyle.Flat;
            outputSizeComboBox.ForeColor = Color.FromArgb(238, 239, 241);
            outputSizeComboBox.FormattingEnabled = true;
            outputSizeComboBox.Items.AddRange(new object[] { "128 x 128", "256 x 256", "512 x 512" });
            outputSizeComboBox.Location = new Point(10, 109);
            outputSizeComboBox.Name = "outputSizeComboBox";
            outputSizeComboBox.Size = new Size(264, 23);
            outputSizeComboBox.TabIndex = 8;
            outputSizeComboBox.Tag = "";
            outputSizeComboBox.SelectedIndexChanged += OutputSizeComboBox_SelectedIndexChanged;
            // 
            // templateLabel
            // 
            templateLabel.AutoSize = true;
            templateLabel.ForeColor = Color.FromArgb(176, 180, 188);
            templateLabel.Location = new Point(10, 194);
            templateLabel.Name = "templateLabel";
            templateLabel.Size = new Size(56, 15);
            templateLabel.TabIndex = 6;
            templateLabel.Text = "Template";
            // 
            // itemImageLabel
            // 
            itemImageLabel.AutoSize = true;
            itemImageLabel.ForeColor = Color.FromArgb(176, 180, 188);
            itemImageLabel.Location = new Point(10, 141);
            itemImageLabel.Name = "itemImageLabel";
            itemImageLabel.Size = new Size(75, 15);
            itemImageLabel.TabIndex = 5;
            itemImageLabel.Text = "Image (.png)";
            // 
            // outputLabel
            // 
            outputLabel.AutoSize = true;
            outputLabel.ForeColor = Color.FromArgb(176, 180, 188);
            outputLabel.Location = new Point(10, 91);
            outputLabel.Name = "outputLabel";
            outputLabel.Size = new Size(45, 15);
            outputLabel.TabIndex = 4;
            outputLabel.Text = "Output";
            // 
            // itemIdLabel
            // 
            itemIdLabel.AutoSize = true;
            itemIdLabel.ForeColor = Color.FromArgb(176, 180, 188);
            itemIdLabel.Location = new Point(10, 39);
            itemIdLabel.Name = "itemIdLabel";
            itemIdLabel.Size = new Size(18, 15);
            itemIdLabel.TabIndex = 1;
            itemIdLabel.Text = "ID";
            // 
            // formTitleLabel
            // 
            formTitleLabel.AutoSize = true;
            formTitleLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            formTitleLabel.Location = new Point(10, 9);
            formTitleLabel.Name = "formTitleLabel";
            formTitleLabel.Size = new Size(136, 20);
            formTitleLabel.TabIndex = 0;
            formTitleLabel.Text = "Create recipe item";
            // 
            // footerStatusStrip
            // 
            footerStatusStrip.BackColor = Color.FromArgb(30, 31, 34);
            footerStatusStrip.Items.AddRange(new ToolStripItem[] { footerStatusLabel, versionStatusLabel, githubStatusLabel });
            footerStatusStrip.Location = new Point(0, 307);
            footerStatusStrip.Name = "footerStatusStrip";
            footerStatusStrip.RenderMode = ToolStripRenderMode.Professional;
            footerStatusStrip.Size = new Size(650, 24);
            footerStatusStrip.SizingGrip = false;
            footerStatusStrip.TabIndex = 1;
            // 
            // footerStatusLabel
            // 
            footerStatusLabel.ForeColor = Color.FromArgb(205, 208, 214);
            footerStatusLabel.Name = "footerStatusLabel";
            footerStatusLabel.Size = new Size(486, 19);
            footerStatusLabel.Spring = true;
            footerStatusLabel.Text = "Ready";
            footerStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // versionStatusLabel
            // 
            versionStatusLabel.BorderSides = ToolStripStatusLabelBorderSides.Left;
            versionStatusLabel.ForeColor = Color.FromArgb(205, 208, 214);
            versionStatusLabel.Name = "versionStatusLabel";
            versionStatusLabel.Size = new Size(41, 19);
            versionStatusLabel.Text = "v1.0.0";
            // 
            // githubStatusLabel
            // 
            githubStatusLabel.BorderSides = ToolStripStatusLabelBorderSides.Left;
            githubStatusLabel.ForeColor = Color.FromArgb(205, 208, 214);
            githubStatusLabel.Name = "githubStatusLabel";
            githubStatusLabel.Size = new Size(108, 19);
            githubStatusLabel.Text = "GitHub: not set up";
            githubStatusLabel.Click += GitHubStatusLabel_Click;
            // 
            // rightPanel
            // 
            rightPanel.BackColor = Color.FromArgb(30, 31, 34);
            rightPanel.BorderStyle = BorderStyle.FixedSingle;
            rightPanel.Controls.Add(previewPictureBox);
            rightPanel.Controls.Add(previewTitleLabel);
            rightPanel.Location = new Point(302, 8);
            rightPanel.Name = "rightPanel";
            rightPanel.Size = new Size(340, 292);
            rightPanel.TabIndex = 2;
            // 
            // previewPictureBox
            // 
            previewPictureBox.BackColor = Color.FromArgb(20, 21, 23);
            previewPictureBox.BorderStyle = BorderStyle.FixedSingle;
            previewPictureBox.Location = new Point(13, 32);
            previewPictureBox.Name = "previewPictureBox";
            previewPictureBox.Size = new Size(312, 247);
            previewPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            previewPictureBox.TabIndex = 1;
            previewPictureBox.TabStop = false;
            previewPictureBox.DragDrop += PreviewPictureBox_DragDrop;
            previewPictureBox.DragEnter += PreviewPictureBox_DragEnter;
            // 
            // previewTitleLabel
            // 
            previewTitleLabel.AutoSize = true;
            previewTitleLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            previewTitleLabel.Location = new Point(10, 10);
            previewTitleLabel.Name = "previewTitleLabel";
            previewTitleLabel.Size = new Size(53, 15);
            previewTitleLabel.TabIndex = 0;
            previewTitleLabel.Text = "Preview";
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(16, 17, 19);
            ClientSize = new Size(650, 331);
            Controls.Add(rightPanel);
            Controls.Add(footerStatusStrip);
            Controls.Add(leftPanel);
            DoubleBuffered = true;
            ForeColor = Color.FromArgb(238, 239, 241);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            MaximumSize = new Size(666, 370);
            MinimumSize = new Size(666, 370);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Recipe Item Creator";
            leftPanel.ResumeLayout(false);
            leftPanel.PerformLayout();
            footerStatusStrip.ResumeLayout(false);
            footerStatusStrip.PerformLayout();
            rightPanel.ResumeLayout(false);
            rightPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)previewPictureBox).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Panel leftPanel;
        private StatusStrip footerStatusStrip;
        private ToolStripStatusLabel footerStatusLabel;
        private ToolStripStatusLabel versionStatusLabel;
        private ToolStripStatusLabel githubStatusLabel;
        private Panel rightPanel;
        private Label formTitleLabel;
        private Label itemImageLabel;
        private Label outputLabel;
        private Label itemIdLabel;
        private Label templateLabel;
        private ComboBox outputSizeComboBox;
        private Controls.DarkTextBox itemIdTextBox;
        private Controls.DarkTextBox itemPathTextBox;
        private Controls.DarkTextBox templatePathTextBox;
        private Button chooseItemButton;
        private Button chooseTemplateButton;
        private Button exportButton;
        private Label previewTitleLabel;
        private PictureBox previewPictureBox;
    }
}
