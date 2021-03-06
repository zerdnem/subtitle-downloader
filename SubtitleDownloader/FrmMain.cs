﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using SubtitleDownloader.Utilities;
using System.Net;
using System.Text.RegularExpressions;

namespace SubtitleDownloader
{
    public partial class FrmMain : Form
    {
        private bool SearchStarted = false;
        private bool Downloading = false;
        private string DownloadPath;
        private string FileName;
        private int TickCount = 0;
        private const string UserAgent = "test";


        public FrmMain(string[] args)
        {
            InitializeComponent();
            if (args.Length > 0)
            {
                string filename = Path.GetFileNameWithoutExtension(args[0]);
                FileName = filename;
                DownloadPath = Path.GetDirectoryName(args[0]);
            }
            else
            {
                DownloadPath = Path.GetDirectoryName(Application.ExecutablePath);
            }
        }

        private void FrmMain_Load(object sender, EventArgs e)
        {
            if (FileShellExtension.ContextMenuExists(".mkv", "DownloadSubtitle"))
                chkContextMenu.Checked = true;

            txtQuery.Text = FileName;
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtQuery.Text)) return;
            FileName = txtQuery.Text;
            Search(txtQuery.Text);
        }

        private void Search(string query)
        {
            if (SearchStarted) return;
            SetStatus("Searching ...");
            lstSubtitles.ClearItems();
            (new Thread(() =>
            {
            SearchStarted = true;
            BeginInvoke((MethodInvoker)delegate { btnSearch.Enabled = false; });

            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            var client = OSDBnet.Osdb.Create(UserAgent);
                var match = Regex.Match(query, "(.*?)[Ss]?(\\d+)[xXeE]+?(\\d+)(.*)");
                //var match = Regex.Match(query, "(\\w+$)*([1-9])");
                try
                {
                    if (match.Success)
                    {
                        ShowInfo fi = new ShowInfo(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, match.Groups[4].Value);
                        var results = client.SearchSubtitlesFromQuery("en", fi.name, Int32.Parse(fi.season), Int32.Parse(fi.episode)).Result;

                        if (results.Count > 0)
                        {
                            var subtitles = new List<Subtitle>();
                            foreach (var result in results)
                            {
                                subtitles.Add(new Subtitle(result.LanguageName, result.SubtitleFileName, "", result.SubTitleDownloadLink.ToString()));
                            }
                            InsertItems(subtitles);
                            SetStatus(results.Count.ToString() + " subtitles found.");
                        }
                        else
                        {
                            SetStatus("No subtitles found");

                        }
                    }
                    else
                    {
                        //ShowInfo fi = new ShowInfo(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, match.Groups[4].Value);
                        var results = client.SearchSubtitlesFromQuery("en", query, 0, 0).Result;
                        var subtitles = new List<Subtitle>();
                        foreach (var result in results)
                        {
                            subtitles.Add(new Subtitle(result.LanguageName, result.MovieName, "", result.SubTitleDownloadLink.ToString()));
                        }
                        if (results.Count > 0)
                        {
                            InsertItems(subtitles);
                            SetStatus(results.Count.ToString() + " subtitles found.");
                        }
                        else
                        {
                            SetStatus("No subtitles found");
                        }
                    }
                }
                catch(Exception e)
                {
                    SetStatus("Not found.");
                }


                //var pageContent = Web.GetSearch(query);
                //if (pageContent != null)
                //{
                //    var subtitles = Addic7edClass.Parse(pageContent);
                //    string episodeTitle = Addic7edClass.GetEpisodeTitle(pageContent);
                //    BeginInvoke((MethodInvoker)delegate { this.Text = Addic7edClass.GetEpisodeTitle(pageContent); });
                //    InsertItems(subtitles);
                //    if (subtitles.Count > 0)
                //        SetStatus(subtitles.Count.ToString() + " subtitles found.");
                //    else
                //        SetStatus("No subtitles found");
                //}
                //else
                //{
                //    SetStatus("Error.");
                //}
                SearchStarted = false;
                BeginInvoke((MethodInvoker)delegate{  btnSearch.Enabled = true; });
            })).Start();
        }

        private void InsertItems(List<Subtitle> subtitles)
        {
            this.BeginInvoke((MethodInvoker)delegate
            {
                int key = 0;
                var imageList = new ImageList();
                foreach (var subtitle in subtitles)
                {
                    imageList.Images.Add(Helper.GetResource(subtitle.Language.ToLower()));
                    var item = new ListViewItem(new[] { subtitle.Language, subtitle.Version, subtitle.Completed });
                    item.ImageIndex = key++;
                    lstSubtitles.Items.Add(item);
                    item.Tag = subtitle.Download;
                }
                lstSubtitles.SmallImageList = imageList;
            });
        }

        private void lstSubtitles_MouseDoubleClick(object sender, MouseEventArgs e)
        {

            var url = (string)lstSubtitles.SelectedItems[0].Tag;
            if (lstSubtitles.SelectedItems.Count == 0 || Downloading) return;
            (new Thread(() =>
            {
                Downloading = true;
                SetStatus("Downloading file...");
                Web.Download(url, Path.Combine(DownloadPath, FileName + ".srt"));
                this.BeginInvoke((MethodInvoker)delegate
                {
                    if (autoCloseTimer.Enabled)
                    {
                        TickCount = 0;
                        autoCloseTimer.Start();
                    }
                    else autoCloseTimer.Start();
                });
                SetStatus("Done");
                Downloading = false;
            })).Start();
        }

        private void SetStatus(string status)
        {
            this.BeginInvoke((MethodInvoker)delegate
            {
                lblStatusValue.Text = status;
            });
        }

        private void FrmMain_Shown(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(FileName))
            {
                this.Focus();
                Search(FileName);
            }
        }

        private void chkContextMenu_CheckedChanged(object sender, EventArgs e)
        {
            var menuDescription = "Download subtitle";
            var menuCommand = string.Format("\"{0}\" \"%L\"", Application.ExecutablePath);
            var menuName = "DownloadSubtitle";
            var extensions = new string[] { ".mkv", ".webm", ".flv", ".mp4", ".avi" };

            if (chkContextMenu.Checked)
            {
                chkContextMenu.Text = "Remove from context menu";
                foreach (var extension in extensions)
                {
                    FileShellExtension.AddContextMenuItem(extension, menuName, menuDescription, menuCommand);
                }
            }
            else
            {
                chkContextMenu.Text = "Add to context menu";
                foreach (var extension in extensions)
                {
                    FileShellExtension.RemoveContextMenuItem(extension, menuName);
                }
            }
        }

        private void FrmMain_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void FrmMain_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            string query = Path.GetFileNameWithoutExtension(files[0]);

            txtQuery.Text = query;
            FileName = query;
            DownloadPath = Path.GetDirectoryName(files[0]);

            Search(query);
        }

        private void autoCloseTimer_Tick(object sender, EventArgs e)
        {
            int autoCloseSeconds = 30;
            if (TickCount == autoCloseSeconds)
                Environment.Exit(0);
            Text = "SubtitleDownloader - " + (autoCloseSeconds - TickCount) + " s until closing";
            TickCount++;
        }

        private void lstSubtitles_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void lblStatusValue_Click(object sender, EventArgs e)
        {

        }
    }
}
