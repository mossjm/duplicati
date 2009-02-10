using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using Duplicati.Datamodel;
using System.Collections;

namespace Duplicati.GUI.HelperControls
{
    public partial class BackupFileList : UserControl
    {
        private DateTime m_when;
        private List<string> m_files;
        private Exception m_exception;
        private Schedule m_schedule;
        private bool m_isInCheck = false;

        public BackupFileList()
        {
            InitializeComponent();
        }

        public void LoadFileList(Schedule schedule, DateTime when, List<string> filelist)
        {
            //backgroundWorker.CancelAsync();
            LoadingIndicator.Visible = true;
            progressBar.Visible = true;
            treeView.Visible = false;
            treeView.TreeViewNodeSorter = new NodeSorter();
            LoadingIndicator.Text = "Loading filelist, please wait ...";

            m_files = filelist;
            m_when = when;
            m_schedule = schedule;

            if (m_files != null && m_files.Count != 0)
                backgroundWorker_RunWorkerCompleted(null, null);
            else if (!backgroundWorker.IsBusy)
                backgroundWorker.RunWorkerAsync();
        }

        private void backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                m_exception = null;
                DuplicatiRunner r = new DuplicatiRunner();
                IList<string> files = r.ListFiles (m_schedule, m_when);
                if (backgroundWorker.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }
                if (m_files != null)
                {
                    m_files.Clear();
                    m_files.AddRange(files);
                }
                else
                    m_files = new List<string>(files);
            }
            catch (Exception ex)
            {
                m_exception = ex;
            }
        }

        private void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            treeView.Nodes.Clear();
            if (m_exception != null)
            {
                LoadingIndicator.Visible = true;
                treeView.Visible = false;
                progressBar.Visible = false;
                LoadingIndicator.Text = m_exception.Message;
            }

            if (e != null && e.Cancelled)
                return;

            try
            {
                treeView.BeginUpdate();

                bool supported = OSGeo.MapGuide.Maestro.ResourceEditors.ShellIcons.Supported;
                if (supported)
                    treeView.ImageList = OSGeo.MapGuide.Maestro.ResourceEditors.ShellIcons.ImageList;

                foreach (string s in m_files)
                {
                    TreeNodeCollection c = treeView.Nodes;
                    string[] parts = s.Split('/');
                    for(int i = 0; i < parts.Length; i++)
                    {
                        if (parts[i] != "")
                        {
                            TreeNode t = FindNode(parts[i], c);
                            if (t == null)
                            {
                                t = new TreeNode(parts[i]);
                                if (supported)
                                {
                                    if (i == parts.Length - 1)
                                        t.ImageIndex = t.SelectedImageIndex = OSGeo.MapGuide.Maestro.ResourceEditors.ShellIcons.GetShellIcon(s);

                                    else
                                        t.ImageIndex = t.SelectedImageIndex = OSGeo.MapGuide.Maestro.ResourceEditors.ShellIcons.GetFolderIcon(false);
                                }

                                //tag = IsFolder
                                t.Tag = !(i == parts.Length - 1);
                                c.Add(t);
                            }
                            c = t.Nodes;
                        }
                    }
                }

                treeView.Sort();
            }
            finally
            {
                treeView.EndUpdate();
            }


            LoadingIndicator.Visible = false;
            treeView.Visible = true;
        }

        private TreeNode FindNode(string name, TreeNodeCollection items)
        {
            foreach(TreeNode t in items)
                if (t.Text == name)
                    return t;

            return null;
        }

        private class NodeSorter : IComparer
        {
            #region IComparer Members

            public int Compare(object x, object y)
            {
                if (!(x is TreeNode) || !(y is TreeNode))
                    return 0;

                if ((bool)((TreeNode)x).Tag == (bool)((TreeNode)y).Tag)
                    return string.Compare(((TreeNode)x).Text, ((TreeNode)y).Text);
                else
                    return (bool)((TreeNode)x).Tag ? -1 : 1;
            }

            #endregion
        }

        private void treeView_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (!m_isInCheck)
            {
                try
                {
                    m_isInCheck = true;
                    Queue<TreeNode> nodes = new Queue<TreeNode>();
                    nodes.Enqueue(e.Node);
                    while (nodes.Count > 0)
                    {
                        TreeNode n = nodes.Dequeue();
                        foreach (TreeNode nx in n.Nodes)
                            nodes.Enqueue(nx);

                        n.Checked = e.Node.Checked;

                    }

                    /*TreeNode nf = e.Node;

                    while (nf.Parent != null)
                    {
                        bool oneChecked = false;
                        bool noneChecked = true;

                        foreach (TreeNode nx in nf.Parent.Nodes)
                        {
                            oneChecked |= nx.Checked;
                            noneChecked &= !nx.Checked;
                        }

                        if (oneChecked && !nf.Parent.Checked)
                            nf.Parent.Checked = true;

                        if (noneChecked && nf.Parent.Checked)
                            nf.Parent.Checked = false;

                        nf = nf.Parent;
                    }*/


                }
                finally
                {
                    m_isInCheck = false;
                }
            }
        }

        private void treeView_AfterCollapse(object sender, TreeViewEventArgs e)
        {
            if (e.Node != null && e.Node.Tag != null && e.Node.Tag is bool)
                e.Node.ImageIndex = e.Node.SelectedImageIndex = OSGeo.MapGuide.Maestro.ResourceEditors.ShellIcons.GetFolderIcon(false);
        }

        private void treeView_AfterExpand(object sender, TreeViewEventArgs e)
        {
            if (e.Node != null && e.Node.Tag != null && e.Node.Tag is bool)
                e.Node.ImageIndex = e.Node.SelectedImageIndex = OSGeo.MapGuide.Maestro.ResourceEditors.ShellIcons.GetFolderIcon(true);
        }

        public string CheckedAsFilter
        {
            get
            {
                List<KeyValuePair<bool, string>> filter = new List<KeyValuePair<bool, string>>();
                filter.Add(new KeyValuePair<bool, string>(false, ".*"));

                Queue<TreeNode> items = new Queue<TreeNode>();
                foreach (TreeNode t in treeView.Nodes)
                    if (t.Checked)
                        items.Enqueue(t);

                treeView.PathSeparator = System.IO.Path.DirectorySeparatorChar.ToString();

                while (items.Count > 0)
                {
                    TreeNode t = items.Dequeue();

                    foreach (TreeNode tn in t.Nodes)
                        if (tn.Checked)
                            items.Enqueue(tn);

                    if (t.Tag == null)
                        filter.Add(new KeyValuePair<bool, string>(true, Library.Core.FilenameFilter.ConvertGlobbingToRegExp(treeView.PathSeparator + t.FullPath)));
                }

                return Library.Core.FilenameFilter.EncodeAsFilter(filter);
            }
        }

        public int CheckedCount
        {
            get
            {
                int count = 0;
                Queue<TreeNode> items = new Queue<TreeNode>();
                foreach (TreeNode t in treeView.Nodes)
                    if (t.Checked)
                    {
                        items.Enqueue(t);
                        if (t.Tag == null)
                            count++;
                    }

                while (items.Count > 0)
                {
                    TreeNode t = items.Dequeue();

                    foreach (TreeNode tn in t.Nodes)
                        if (tn.Checked)
                        {
                            items.Enqueue(tn);
                            if (tn.Tag == null)
                                count++;
                        }
                }

                return count;
            }
        }
    }
}