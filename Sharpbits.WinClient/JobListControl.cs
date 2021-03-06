﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using SharpBits.Base;
using SharpBits.WinClient.Controls;
using SharpBits.WinClient.Properties;

namespace SharpBits.WinClient
{
    public partial class JobListControl : UserControl
    {
        // Fields
        private ColumnHeader clhBytes;
        private ColumnHeader clhFiles;
        private ColumnHeader clhJobName;
        private ColumnHeader clhJobState;
        private ColumnHeader clhProgress;
        private ToolStripComboBox ctxdlPriority;
        private ContextMenuStrip ctxJobControl;
        private ToolStripMenuItem ctxmiJobOwnerSettings;
        private ToolStripMenuItem ctxmiaddDownload;
        private ToolStripMenuItem ctxmiAddDownloads;
        private ToolStripMenuItem ctxmiAddUpload;
        private ToolStripMenuItem ctxmiCancel;
        private ToolStripMenuItem ctxmiComplete;
        private ToolStripMenuItem ctxmiJobProperties;
        private ToolStripMenuItem ctxmiPriorityMap;
        private ToolStripMenuItem ctxmiRefresh;
        private ToolStripMenuItem ctxmiResume;
        private ToolStripMenuItem ctxmiSuspend;
        private ToolStripSeparator ctxsepJob1;
        private ToolStripSeparator ctxsepJob2;
        private ToolStripSeparator ctxsepJob3;
        private JobType dragJobType;
        private ImageList imglDirection;
        private JobWrapperCollection jobWrappers = new JobWrapperCollection();
        private BitsListView lvJobList;
        private BitsManager bitsManager;
        private readonly int textDelta;
        private JobMessageLevelCallback notificationEvent;
        private EventHandler onJobListOwnerChanged;
        private Image jobOwnerImage;
        private string jobOwnerTooltip;
        private string jobOwnerText;

        // Events
        internal event JobMessageLevelCallback NotificationEvent
        {
            add
            {
                this.notificationEvent = (JobMessageLevelCallback)Delegate.Combine(this.notificationEvent, value);
            }
            remove
            {
                this.notificationEvent = (JobMessageLevelCallback)Delegate.Remove(this.notificationEvent, value);

            }
        }

        public event EventHandler OnJobListOwnerChanged
        {
            add
            {
                this.onJobListOwnerChanged = (EventHandler)Delegate.Combine(this.onJobListOwnerChanged, value);
            }
            remove
            {
                this.onJobListOwnerChanged = (EventHandler)Delegate.Remove(this.onJobListOwnerChanged, value);
            }
        }

        // Methods
        public JobListControl()
        {
            this.InitializeComponent();
            this.textDelta = this.imglDirection.ImageSize.Width + 2;
            this.ctxdlPriority.ComboBox.DrawMode = DrawMode.OwnerDrawVariable;
            this.ctxdlPriority.ComboBox.DrawItem += new DrawItemEventHandler(this.ComboBox_DrawItem);
            this.ctxdlPriority.ComboBox.MeasureItem += new MeasureItemEventHandler(this.ComboBox_MeasureItem);
            this.ctxdlPriority.ComboBox.DropDownHeight = 180;
            this.ctxdlPriority.BeginUpdate();
            this.ctxdlPriority.Items.Add("Foreground");
            this.ctxdlPriority.Items.Add("High");
            this.ctxdlPriority.Items.Add("Normal");
            this.ctxdlPriority.Items.Add("Low");
            this.ctxdlPriority.EndUpdate();
        }

        public void PasteUrl()
        {
            string[] strArray;
            JobType jobType = CopyPasteHandler.GetJobType(out strArray);
            if (jobType != JobType.Unknown)
            {
                this.CreateNewJob(jobType, strArray);
            }
        }

        private void actRefresh_Execute(object sender, EventArgs e)
        {
            this.UpdateControl();
        }

        public void AddFile(string url)
        {
            if (base.Enabled)
            {
                JobType jobType = FileHandler.GetJobType(url);
                if (jobType != JobType.Unknown)
                {
                    this.CreateNewJob(jobType, new string[] { url });
                }
            }
        }

        private void ComboBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            Color controlText = SystemColors.ControlText;
            if ((e.State & DrawItemState.Focus) != DrawItemState.None)
            {
                e.DrawFocusRectangle();
                controlText = SystemColors.HighlightText;
            }
            TextRenderer.DrawText(e.Graphics, this.ctxdlPriority.Items[e.Index].ToString(), this.ctxdlPriority.Font, e.Bounds, controlText, TextFormatFlags.LeftAndRightPadding | TextFormatFlags.VerticalCenter);
            Image pinned = Resources.Pinned;
            switch (e.Index)
            {
                case 0:
                    pinned = Resources.Pinned;
                    break;

                case 1:
                    pinned = Resources.FlagRed;
                    break;

                case 2:
                    pinned = Resources.FlagGreen;
                    break;

                case 3:
                    pinned = Resources.FlagBlue;
                    break;
            }
            Rectangle rect = new Rectangle((e.Bounds.X + e.Bounds.Width) - 0x12, e.Bounds.Y, 0x10, 0x10);
            e.Graphics.DrawImageUnscaledAndClipped(pinned, rect);
        }

        private void ComboBox_MeasureItem(object sender, MeasureItemEventArgs e)
        {
            e.ItemHeight = 20;
            e.ItemWidth = this.ctxdlPriority.Width;
        }

        private void ControlsFromJobState()
        {
            BitsJob bitsJob = null;
            if (this.lvJobList.SelectedItems.Count > 0)
            {
                JobWrapper tag = this.lvJobList.SelectedItems[0].Tag as JobWrapper;
                bitsJob = tag.BitsJob;
            }
            if (bitsJob == null)
            {
                this.ctxmiCancel.Visible = false;
                this.ctxmiComplete.Visible = false;
                this.ctxmiSuspend.Visible = false;
                this.ctxmiResume.Visible = false;
                this.ctxdlPriority.Visible = false;
                this.ctxmiPriorityMap.Visible = false;
                this.ctxmiJobProperties.Visible = false;
                this.ctxsepJob2.Visible = this.ctxsepJob3.Visible = false;
            }
            else
            {
                this.ctxmiJobProperties.Visible = true;
                this.ctxmiCancel.Visible = true;
                this.ctxmiComplete.Visible = true;
                this.ctxmiSuspend.Visible = true;
                this.ctxmiResume.Visible = true;
                this.ctxmiPriorityMap.Visible = true;
                this.ctxdlPriority.Visible = true;
                this.ctxsepJob2.Visible = this.ctxsepJob3.Visible = true;
                switch (bitsJob.State)
                {
                    case JobState.Queued:
                        this.ctxmiResume.Enabled = true;
                        this.ctxmiSuspend.Enabled = true;
                        this.ctxmiCancel.Enabled = true;
                        this.ctxmiComplete.Enabled = true;
                        this.ctxdlPriority.Enabled = true;
                        this.ctxmiJobProperties.Enabled = true;
                        break;

                    case JobState.Connecting:
                        this.ctxmiResume.Enabled = false;
                        this.ctxmiSuspend.Enabled = true;
                        this.ctxmiCancel.Enabled = true;
                        this.ctxmiComplete.Enabled = true;
                        this.ctxdlPriority.Enabled = true;
                        this.ctxmiJobProperties.Enabled = true;
                        break;

                    case JobState.Transferring:
                        this.ctxmiResume.Enabled = false;
                        this.ctxmiSuspend.Enabled = true;
                        this.ctxmiCancel.Enabled = true;
                        this.ctxmiComplete.Enabled = true;
                        this.ctxdlPriority.Enabled = true;
                        this.ctxmiJobProperties.Enabled = true;
                        break;

                    case JobState.Suspended:
                        this.ctxmiResume.Enabled = true;
                        this.ctxmiSuspend.Enabled = false;
                        this.ctxmiCancel.Enabled = true;
                        this.ctxmiComplete.Enabled = true;
                        this.ctxdlPriority.Enabled = true;
                        this.ctxmiJobProperties.Enabled = true;
                        break;

                    case JobState.Error:
                        this.ctxmiResume.Enabled = true;
                        this.ctxmiSuspend.Enabled = true;
                        this.ctxmiCancel.Enabled = true;
                        this.ctxmiComplete.Enabled = true;
                        this.ctxdlPriority.Enabled = true;
                        this.ctxmiJobProperties.Enabled = true;
                        break;

                    case JobState.TransientError:
                        this.ctxmiResume.Enabled = true;
                        this.ctxmiSuspend.Enabled = false;
                        this.ctxmiCancel.Enabled = true;
                        this.ctxmiComplete.Enabled = true;
                        this.ctxdlPriority.Enabled = true;
                        this.ctxmiJobProperties.Enabled = true;
                        break;

                    case JobState.Transferred:
                        this.ctxmiResume.Enabled = false;
                        this.ctxmiSuspend.Enabled = false;
                        this.ctxmiCancel.Enabled = true;
                        this.ctxmiComplete.Enabled = true;
                        this.ctxdlPriority.Enabled = true;
                        this.ctxmiJobProperties.Enabled = true;
                        break;

                    case JobState.Acknowledged:
                        this.ctxmiCancel.Enabled = false;
                        this.ctxmiComplete.Enabled = false;
                        this.ctxmiSuspend.Enabled = false;
                        this.ctxmiResume.Enabled = false;
                        this.ctxdlPriority.Enabled = false;
                        this.ctxmiJobProperties.Enabled = false;
                        break;

                    case JobState.Canceled:
                        this.ctxmiCancel.Enabled = false;
                        this.ctxmiComplete.Enabled = false;
                        this.ctxmiSuspend.Enabled = false;
                        this.ctxmiResume.Enabled = false;
                        this.ctxdlPriority.Enabled = false;
                        this.ctxmiJobProperties.Enabled = false;
                        break;
                }
                this.ctxdlPriority.SelectedIndexChanged -= new EventHandler(this.ctxdlPriority_SelectedIndexChanged);
                this.ctxdlPriority.SelectedIndex = (int)bitsJob.Priority;
                this.ctxdlPriority.SelectedIndexChanged += new EventHandler(this.ctxdlPriority_SelectedIndexChanged);
            }
        }

        private void CreateNewJob(JobType jobType, string[] files)
        {
            BitsJob job = null;
            job = this.bitsManager.CreateJob(jobType.ToString(), jobType);
            this.lvJobList.Items[job.JobId.ToString()].Selected = true;
            JobWrapper wrapper = this.jobWrappers[job.JobId];
            wrapper.FileList = files;
            this.ShowJobDetails(wrapper, true);
        }

        private void ctxdlPriority_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.lvJobList.SelectedItems.Count > 0)
            {
                JobWrapper tag = this.lvJobList.SelectedItems[0].Tag as JobWrapper;
                tag.BitsJob.Priority = (JobPriority)this.ctxdlPriority.SelectedIndex;
            }
        }

        private void FillListViewItem(ListViewItem listItem, JobWrapper jobWrapper)
        {
            BitsJob bitsJob = jobWrapper.BitsJob;
            listItem.SubItems.Clear();
            listItem.ImageIndex = (bitsJob.JobType == JobType.Download) ? 0 : 1;
            listItem.ToolTipText = string.IsNullOrEmpty(bitsJob.Description) ? bitsJob.DisplayName : bitsJob.Description;
            listItem.Text = bitsJob.DisplayName;
            listItem.Name = bitsJob.JobId.ToString();
            ListViewItem.ListViewSubItem item = new ListViewItem.ListViewSubItem(listItem, bitsJob.State.ToString());
            item.Name = ColumnNames.JobState;
            listItem.SubItems.Add(item);
            item = new ListViewItem.ListViewSubItem(listItem, bitsJob.Progress.FilesTransferred.ToString() + "/" + bitsJob.Progress.FilesTotal.ToString());
            item.Name = ColumnNames.FileCount;
            listItem.SubItems.Add(item);
            item = new ListViewItem.ListViewSubItem(listItem, Utils.Bytes2Size(bitsJob.Progress.BytesTransferred) + "/" + Utils.Bytes2Size(bitsJob.Progress.BytesTotal));
            item.Name = ColumnNames.ByteCount;
            listItem.SubItems.Add(item);
            item = new ListViewItem.ListViewSubItem(listItem, Utils.Percentage(bitsJob.Progress.BytesTotal, bitsJob.Progress.BytesTransferred));
            item.Name = ColumnNames.Progress;
            listItem.SubItems.Add(item);
            if (bitsJob.JobType == JobType.Upload)
            {
                listItem.Group = this.lvJobList.Groups[1];
            }
            if (bitsJob.JobType == JobType.Download)
            {
                listItem.Group = this.lvJobList.Groups[0];
            }
            listItem.Tag = jobWrapper;
        }

        public void InitializeControl(BitsManager bitsManager)
        {
            if (bitsManager == null)
            {
                base.Enabled = false;
            }
            else
            {
                this.bitsManager = bitsManager;
                Settings.Default.ShowAllJobs = !Settings.Default.ShowAllJobs;
                this.ShowAllJobs();
                bitsManager.OnJobError += new EventHandler<ErrorNotificationEventArgs>(this.manager_OnJobErrorEvent);
                bitsManager.OnJobModified += new EventHandler<NotificationEventArgs>(this.manager_OnJobModifiedEvent);
                bitsManager.OnJobTransferred += new EventHandler<NotificationEventArgs>(this.manager_OnJobTransferredEvent);
                bitsManager.OnJobAdded += new EventHandler<NotificationEventArgs>(this.manager_OnJobAdded);
            }
        }

        private void lvJobList_AfterLabelEdit(object sender, LabelEditEventArgs e)
        {
            if (e.Label != null)
            {
                JobWrapper tag = this.lvJobList.Items[e.Item].Tag as JobWrapper;
                tag.BitsJob.DisplayName = e.Label;
            }
        }

        private void lvJobList_DoubleClick(object sender, EventArgs e)
        {
            this.ctxmiJobProperties_Click(sender, e);
        }

        private void lvJobList_DragDrop(object sender, DragEventArgs e)
        {
            string[] strArray = DragDropHandler.DragDrop(e);
            DragDropCallback method = new DragDropCallback(this.CreateNewJob);
            base.BeginInvoke(method, new object[] { this.dragJobType, strArray });
        }

        private void lvJobList_DragEnter(object sender, DragEventArgs e)
        {
            this.dragJobType = DragDropHandler.DragEnter(e);
        }

        private void lvJobList_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void lvJobList_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            if (e.Item.Focused)
            {
                Pen pen = new Pen(SystemColors.ControlDark, 1f);
                pen.DashStyle = DashStyle.Dot;
                Rectangle rect = new Rectangle(e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
                e.Graphics.DrawRectangle(pen, rect);
                pen.Dispose();
            }
        }

        private void lvJobList_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            Rectangle rectangle;
            Brush gradientActiveCaption;
            JobWrapper tag = e.Item.Tag as JobWrapper;
            BitsJob bitsJob = tag.BitsJob;
            Color windowText = SystemColors.WindowText;
            Font prototype = this.lvJobList.Font;
            int num = 0;
            if (e.SubItem.Name == bitsJob.JobId.ToString())
            {
                num = 0;
            }
            TextFormatFlags flags = TextFormatFlags.LeftAndRightPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter;
            if ((bitsJob.State == JobState.Suspended) || (bitsJob.State == JobState.Queued))
            {
                if (e.Item.Selected)
                {
                    windowText = SystemColors.ActiveCaptionText;
                }
                else
                {
                    windowText = SystemColors.InactiveCaptionText;
                }
                prototype = new Font(prototype, FontStyle.Italic);
            }
            else if ((bitsJob.State == JobState.Error) || (bitsJob.State == JobState.TransientError))
            {
                windowText = Color.Red;
            }
            else if ((bitsJob.State == JobState.Transferred) || (bitsJob.State == JobState.Acknowledged))
            {
                windowText = Settings.Default.ProgressCompletedColor;
                prototype = new Font(prototype, FontStyle.Strikeout);
            }
            else if (bitsJob.State == JobState.Canceled)
            {
                if (e.Item.Selected)
                {
                    windowText = SystemColors.ActiveCaptionText;
                }
                else
                {
                    windowText = SystemColors.InactiveCaptionText;
                }
                prototype = new Font(prototype, FontStyle.Strikeout);
            }
            else if (bitsJob.State == JobState.Connecting)
            {
                windowText = Color.YellowGreen;
            }
            else if (bitsJob.State == JobState.Transferring)
            {
                windowText = Settings.Default.ProgressDoneColor;
            }
            if (e.Item.Selected)
            {
                int num2 = (e.ColumnIndex == 0) ? 1 : 0;
                rectangle = new Rectangle((e.Bounds.X + num) + num2, e.Bounds.Y + 1, (e.Bounds.Width - num) - num2, e.Bounds.Height - 2);
                if (this.lvJobList.Focused)
                {
                    gradientActiveCaption = SystemBrushes.GradientActiveCaption;
                }
                else
                {
                    gradientActiveCaption = SystemBrushes.GradientInactiveCaption;
                }
                e.Graphics.FillRectangle(gradientActiveCaption, rectangle);
            }
            if (e.SubItem.Name == ColumnNames.Progress)
            {
                StringFormat format = new StringFormat();
                format.Alignment = StringAlignment.Center;
                gradientActiveCaption = new SolidBrush(Settings.Default.ProgressLeftColor);
                rectangle = new Rectangle(e.Bounds.X, e.Bounds.Y, e.Bounds.Width, e.Bounds.Height);
                e.Graphics.FillRectangle(gradientActiveCaption, rectangle);
                if (bitsJob.Progress.BytesTransferred > 0L)
                {
                    if ((bitsJob.State == JobState.Transferred) || (bitsJob.State == JobState.Acknowledged))
                    {
                        gradientActiveCaption = new SolidBrush(Settings.Default.ProgressCompletedColor);
                    }
                    else
                    {
                        gradientActiveCaption = new LinearGradientBrush(rectangle, Settings.Default.ProgressDoneColor, Settings.Default.ProgressCompletedColor, LinearGradientMode.Horizontal);
                    }
                    Rectangle rect = Utils.CalculateProgress(rectangle, bitsJob.Progress.BytesTotal, bitsJob.Progress.BytesTransferred);
                    e.Graphics.FillRectangle(gradientActiveCaption, rect);
                }
                ControlPaint.DrawBorder(e.Graphics, rectangle, SystemColors.ActiveBorder, ButtonBorderStyle.Solid);
                windowText = SystemColors.InactiveCaptionText;
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, prototype, rectangle, windowText, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            }
            else if (e.SubItem.Name == bitsJob.JobId.ToString())
            {
                Image image = this.imglDirection.Images[e.Item.ImageIndex];
                rectangle = new Rectangle(e.Bounds.X + 2, e.Bounds.Y + 1, image.Width, image.Height);
                e.Graphics.DrawImage(image, rectangle);
                rectangle = new Rectangle((e.Bounds.X + 2) + this.textDelta, e.Bounds.Y + 1, (e.Bounds.Width - 2) - this.textDelta, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, prototype, rectangle, windowText, flags);
            }
            else if (e.SubItem.Name == ColumnNames.JobState)
            {
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, prototype, e.Bounds, windowText, flags);
            }
            else
            {
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, prototype, e.Bounds, windowText, flags | TextFormatFlags.Right);
            }
        }

        private void lvJobList_KeyUp(object sender, KeyEventArgs e)
        {
            Keys keyCode = e.KeyCode;
            if (keyCode != Keys.Return)
            {
                if (keyCode == Keys.Delete)
                {
                    this.ctxmiCancel_Click(sender, e);
                    return;
                }
                if (keyCode != Keys.V)
                {
                    e.Handled = false;
                    return;
                }
            }
            else
            {
                this.ctxmiJobProperties_Click(sender, e);
                return;
            }
            if (e.Control)
            {
                this.PasteUrl();
            }
        }

        private void lvJobList_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.ControlsFromJobState();
        }

        private void manager_OnJobAdded(object sender, NotificationEventArgs e)
        {
            if (base.InvokeRequired)
            {
                EventHandler<NotificationEventArgs> method = new EventHandler<NotificationEventArgs>(this.manager_OnJobAdded);
                base.Invoke(method, new object[] { sender, e });
            }
            else
            {
                if (!this.jobWrappers.ContainsKey(e.Job.JobId))
                {
                    this.jobWrappers[e.Job.JobId] = new JobWrapper(this.bitsManager, e.Job.JobId);
                    if (e.Job.NotificationFlags != (NotificationFlags.JobModified | NotificationFlags.JobErrorOccured | NotificationFlags.JobTransferred))
                    {
                        e.Job.NotificationFlags = NotificationFlags.JobModified | NotificationFlags.JobErrorOccured | NotificationFlags.JobTransferred;
                    }
                }
                if (this.jobWrappers.ContainsKey(e.Job.JobId))
                {
                    this.UpdateControl(this.jobWrappers[e.Job.JobId]);
                }
            }
        }

        private void manager_OnJobErrorEvent(object sender, ErrorNotificationEventArgs e)
        {
            if (base.InvokeRequired)
            {
                EventHandler<ErrorNotificationEventArgs> method = new EventHandler<ErrorNotificationEventArgs>(this.manager_OnJobErrorEvent);
                base.Invoke(method, new object[] { sender, e });
            }
            else if (this.jobWrappers.ContainsKey(e.Job.JobId))
            {
                this.UpdateControl(this.jobWrappers[e.Job.JobId]);
            }
        }

        private void manager_OnJobModifiedEvent(object sender, NotificationEventArgs e)
        {
            if (base.InvokeRequired)
            {
                EventHandler<NotificationEventArgs> method = new EventHandler<NotificationEventArgs>(this.manager_OnJobModifiedEvent);
                base.Invoke(method, new object[] { sender, e });
            }
            else if (this.jobWrappers.ContainsKey(e.Job.JobId))
            {
                this.UpdateControl(this.jobWrappers[e.Job.JobId]);
            }
        }

        private void manager_OnJobTransferredEvent(object sender, NotificationEventArgs e)
        {
            if (base.InvokeRequired)
            {
                EventHandler<NotificationEventArgs> method = new EventHandler<NotificationEventArgs>(this.manager_OnJobTransferredEvent);
                base.Invoke(method, new object[] { sender, e });
            }
            else if (this.jobWrappers.ContainsKey(e.Job.JobId))
            {
                JobWrapper wrapper = this.jobWrappers[e.Job.JobId];
                if (wrapper.AutoComplete)
                {
                    wrapper.BitsJob.Complete();
                }
                if ((wrapper.BitsJob.JobType == JobType.Upload) && wrapper.DeleteLocalFile)
                {
                    try
                    {
                        wrapper.BitsJob.EnumFiles();
                        string localName = wrapper.BitsJob.Files[0].LocalName;
                        File.SetAttributes(localName, FileAttributes.Normal);
                        File.Delete(localName);
                    }
                    catch (Exception exception)
                    {
                        if (this.notificationEvent != null)
                        {
                            this.notificationEvent(exception.Message, MessageLevel.Error);
                        }
                    }
                }
            }
        }

        public void ShowJobDetails(JobWrapper wrapper, bool newJob)
        {
            if (wrapper != null)
            {
                JobDetailsForm form = new JobDetailsForm(wrapper);
                if (form.ShowDialog(this, newJob) == DialogResult.Abort)
                {
                    wrapper.BitsJob.Cancel();
                    Thread.Sleep(0);
                    this.UpdateControl();
                }
            }
        }

        private void ShowJobs(bool allJobs)
        {
            Settings.Default.ShowAllJobs = allJobs;
            Settings.Default.Save();
            if (allJobs)
            {
                this.jobOwnerImage = Resources.AllUsers;
                this.jobOwnerTooltip = "Display Jobs for current user";
                this.jobOwnerText = "Show Current User Jobs";
            }
            else
            {
                this.jobOwnerImage = Resources.CurrentUser;
                this.jobOwnerTooltip = "Display Jobs for all users";
                this.jobOwnerText = "Show All User Jobs";
            }
            this.ctxmiJobOwnerSettings.Image = this.jobOwnerImage;
            this.ctxmiJobOwnerSettings.ToolTipText = this.jobOwnerTooltip;
            this.ctxmiJobOwnerSettings.Text = this.jobOwnerText;

            if (this.onJobListOwnerChanged != null)
            {
                this.onJobListOwnerChanged(this, new EventArgs());
            }
        }

        public void UpdateControl()
        {
            object tag = null;
            if (this.bitsManager != null)
            {
                this.bitsManager.EnumJobs(Settings.Default.ShowAllJobs ? JobOwner.AllUsers : JobOwner.CurrentUser);
                foreach (ListViewItem item in this.lvJobList.Items)
                {
                    if (item.Selected)
                    {
                        tag = item.Tag;
                        item.Selected = false;
                        break;
                    }
                }
                this.lvJobList.BeginUpdate();
                this.lvJobList.Items.Clear();
                this.UpdateJobList();
                foreach (JobWrapper wrapper in this.jobWrappers.Values)
                {
                    ListViewItem listItem = new ListViewItem();
                    this.FillListViewItem(listItem, wrapper);
                    this.lvJobList.Items.Add(listItem);
                }
                this.lvJobList.EndUpdate();
                this.ControlsFromJobState();
                if (tag != null)
                {
                    foreach (ListViewItem item3 in this.lvJobList.Items)
                    {
                        if (item3.Tag == tag)
                        {
                            item3.Selected = true;
                            break;
                        }
                    }
                }
            }
        }

        private void UpdateControl(JobWrapper wrapper)
        {
            ListViewItem item;
            if (!this.lvJobList.Items.ContainsKey(wrapper.JobId.ToString()))
            {
                item = new ListViewItem();
                this.FillListViewItem(item, wrapper);
                this.lvJobList.Items.Add(item);
            }
            else
            {
                item = this.lvJobList.Items[wrapper.JobId.ToString()];
                this.FillListViewItem(item, wrapper);
            }
        }

        private void UpdateJobList()
        {
            JobWrapperCollection wrappers = new JobWrapperCollection();
            Settings.Default.Reload();
            if (this.jobWrappers.Count == 0)
            {
                JobWrapperCache jobCache = Settings.Default.JobCache;
                if (jobCache != null)
                {
                    foreach (JobWrapper wrapper in jobCache)
                    {
                        wrapper.manager = this.bitsManager;
                        wrappers[wrapper.JobId] = wrapper;
                    }
                }
                else
                {
                    Settings.Default.JobCache = new JobWrapperCache();
                }
            }
            else
            {
                foreach (KeyValuePair<Guid, JobWrapper> pair in this.jobWrappers)
                {
                    wrappers[pair.Key] = pair.Value;
                }
            }
            this.jobWrappers.Clear();
            foreach (KeyValuePair<Guid, BitsJob> pair2 in this.bitsManager.Jobs)
            {
                if (wrappers.ContainsKey(pair2.Key))
                {
                    this.jobWrappers[pair2.Key] = wrappers[pair2.Key];
                }
                else
                {
                    this.jobWrappers[pair2.Key] = new JobWrapper(this.bitsManager, pair2.Key);
                }
                if (pair2.Value.NotificationFlags != (NotificationFlags.JobModified | NotificationFlags.JobErrorOccured | NotificationFlags.JobTransferred))
                {
                    pair2.Value.NotificationFlags = NotificationFlags.JobModified | NotificationFlags.JobErrorOccured | NotificationFlags.JobTransferred;
                }
            }
            Settings.Default.JobCache.Clear();
            foreach (JobWrapper wrapper2 in this.jobWrappers.Values)
            {
                Settings.Default.JobCache.Add(wrapper2);
            }
            Settings.Default.Save();
        }

        // Properties
        public Image JobListOwnerImage
        {
            get { return this.jobOwnerImage; }
        }

        public string JobListOwnerText
        {
            get { return this.jobOwnerText; }
        }


        private void ctxmiJobProperties_Click(object sender, EventArgs e)
        {
            if (this.lvJobList.SelectedItems.Count == 1)
            {
                this.ShowJobDetails(this.lvJobList.SelectedItems[0].Tag as JobWrapper, false);
            }
        }

        private void ctxmiComplete_Click(object sender, EventArgs e)
        {
            if (this.lvJobList.SelectedItems.Count == 1)
            {
                JobWrapper tag = this.lvJobList.SelectedItems[0].Tag as JobWrapper;
                BitsJob bitsJob = tag.BitsJob;
                if ((((bitsJob.JobType == JobType.Download) && (bitsJob.State != JobState.Acknowledged)) && (bitsJob.State != JobState.Canceled)) || ((bitsJob.JobType == JobType.Upload) && (bitsJob.State == JobState.Transferred)))
                {
                    bitsJob.Complete();
                }
                this.ControlsFromJobState();
            }
        }

        private void ctxmiSuspend_Click(object sender, EventArgs e)
        {
            if (this.lvJobList.SelectedItems.Count == 1)
            {
                JobWrapper tag = this.lvJobList.SelectedItems[0].Tag as JobWrapper;
                BitsJob bitsJob = tag.BitsJob;
                if ((bitsJob.State != JobState.Acknowledged) && (bitsJob.State != JobState.Canceled))
                {
                    bitsJob.Suspend();
                }
                this.ControlsFromJobState();
            }
        }

        private void ctxmiCancel_Click(object sender, EventArgs e)
        {
            if (this.lvJobList.SelectedItems.Count == 1)
            {
                JobWrapper tag = this.lvJobList.SelectedItems[0].Tag as JobWrapper;
                BitsJob bitsJob = tag.BitsJob;
                if (((MessageBox.Show(string.Format("Are you sure to delete job '{0}''", bitsJob.DisplayName), "Confirmation", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK) && (bitsJob.State != JobState.Acknowledged)) && (bitsJob.State != JobState.Canceled))
                {
                    bitsJob.Cancel();
                }
                this.ControlsFromJobState();
            }
        }

        private void ctxmiResume_Click(object sender, EventArgs e)
        {
            if (this.lvJobList.SelectedItems.Count == 1)
            {
                JobWrapper tag = this.lvJobList.SelectedItems[0].Tag as JobWrapper;
                BitsJob bitsJob = tag.BitsJob;
                if ((bitsJob.State != JobState.Acknowledged) && (bitsJob.State != JobState.Canceled))
                {
                    bitsJob.Resume();
                }
                this.ControlsFromJobState();
            }
        }

        private void ctxmiaddDownload_Click(object sender, EventArgs e)
        {
            this.CreateNewJob(JobType.Download, null);
        }

        private void ctxmiAddUpload_Click(object sender, EventArgs e)
        {
            this.CreateNewJob(JobType.Upload, null);
        }

        public void ShowAllJobs()
        {
            bool allJobs = !Settings.Default.ShowAllJobs;
            try
            {
                this.ShowJobs(allJobs);
                this.UpdateControl();
            }
            catch
            {
                allJobs = false;
                this.ShowJobs(allJobs);
                this.UpdateControl();
            }
        }

        private void ctxmiJobOwnerSettings_Click(object sender, EventArgs e)
        {
            this.ShowAllJobs();
        }

        private void ctxmiRefresh_Click(object sender, EventArgs e)
        {
            this.UpdateControl();
        }
    }

}
