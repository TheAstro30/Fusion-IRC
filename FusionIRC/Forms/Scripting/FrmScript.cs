﻿/* FusionIRC IRC Client
 * Written by Jason James Newland
 * Copyright (C) 2016 - 2019
 * Provided AS-IS with no warranty expressed or implied
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FusionIRC.Helpers;
using FusionIRC.Properties;
using ircCore.Controls.Rendering;
using ircCore.Settings;
using ircCore.Settings.SettingsBase.Structures;
using ircCore.Settings.SettingsBase.Structures.Misc;
using ircCore.Utils;
using ircScript;
using ircScript.Classes.Structures;
using libolv;

namespace FusionIRC.Forms.Scripting
{
    public sealed class FrmScript : Form
    {
        private readonly MenuStrip _menu;
        private readonly ToolStripMenuItem _mnuFile;
        private readonly ToolStripMenuItem _mnuEdit;
        private readonly ToolStripMenuItem _mnuView;
        private readonly ToolStripMenuItem _mnuHelp;

        private readonly TreeListView _tvFiles;
        private readonly OlvColumn _colFiles;
        private readonly ircScript.Controls.ScriptEditor _txtEdit;

        private readonly SplitContainer _splitter;
        private readonly StatusStrip _status;
        private readonly ToolStripStatusLabel _label;
        private readonly ToolStripStatusLabel _fileName;
        private readonly ToolStripStatusLabel _lineInfo;
        private readonly ToolStripStatusLabel _editMode;

        private readonly List<ScriptData> _aliases = new List<ScriptData>();
        private readonly List<ScriptData> _events = new List<ScriptData>();
        private readonly List<ScriptData> _popups = new List<ScriptData>();
        
        private readonly List<ScriptFileNode> _files = new List<ScriptFileNode>();
        private readonly bool _initialize;
        private readonly ScriptFileNode _varNode;
        private readonly ScriptData _variables;                
        private ScriptData _currentEditingScript;

        private readonly Timer _timer;

        private bool _fileChanged;

        public FrmScript()
        {
            _initialize = true;
            /* Set window position and size */
            var w = SettingsManager.GetWindowByName("editor");
            Size = w.Size;
            Location = w.Position;
            WindowState = w.Maximized ? FormWindowState.Maximized : FormWindowState.Normal;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, ((0)));
            Icon = Resources.aliasEditor;            
            MinimumSize = new Size(512, 395);
            StartPosition = FormStartPosition.Manual;
            Text = @"FusionIRC - Script Editor";
            /* Main menu bar */
            var renderer = new CustomRenderer(new Renderer());
            _menu = new MenuStrip
                       {
                           Location = new Point(0, 0),
                           RenderMode = ToolStripRenderMode.Professional,
                           Renderer = renderer,
                           Size = new Size(496, 24),
                           GripStyle = ToolStripGripStyle.Visible,
                           ShowItemToolTips = true,
                           TabIndex = 3
                       };
            /* File menu */
            _mnuFile = new ToolStripMenuItem {Size = new Size(37, 20), Text = @"&File"};
            _mnuFile.DropDownItems.AddRange(new ToolStripItem[]
                                                {
                                                    new ToolStripMenuItem("New...", null, MenuItemOnClick, Keys.None),
                                                    new ToolStripSeparator(),
                                                    new ToolStripMenuItem("Load", null, MenuItemOnClick,
                                                                          Keys.Control | Keys.L),
                                                    new ToolStripMenuItem("Unload", null, MenuItemOnClick,
                                                                          Keys.Control | Keys.Shift | Keys.U),
                                                    new ToolStripSeparator(),                                                    
                                                    new ToolStripMenuItem("Save", null, MenuItemOnClick,
                                                                          Keys.Control | Keys.S),
                                                    new ToolStripMenuItem("Save As...", null, MenuItemOnClick, Keys.None),
                                                    new ToolStripMenuItem("Save All", null, MenuItemOnClick,
                                                                          Keys.Control | Keys.Shift | Keys.S),
                                                    new ToolStripSeparator(),
                                                    new ToolStripMenuItem("Move Up", null, MenuItemOnClick,
                                                                          Keys.Control | Keys.PageUp),
                                                    new ToolStripMenuItem("Move Down", null, MenuItemOnClick,
                                                                          Keys.Control | Keys.PageDown),
                                                    new ToolStripSeparator(),
                                                    new ToolStripMenuItem("Close", null, MenuItemOnClick,
                                                                          Keys.Alt | Keys.F4)
                                                });
            /* Edit menu */
            _mnuEdit = new ToolStripMenuItem { Size = new Size(39, 20), Text = @"&Edit" };
            _mnuEdit.DropDownItems.AddRange(new ToolStripItem[]
                                                {
                                                    new ToolStripMenuItem("Undo", null, MenuItemOnClick,
                                                                          Keys.Control | Keys.Z),
                                                    new ToolStripMenuItem("Redo", null, MenuItemOnClick,
                                                                          Keys.Control | Keys.Y),
                                                    new ToolStripSeparator(),
                                                    new ToolStripMenuItem("Cut", null, MenuItemOnClick,
                                                                          Keys.Control | Keys.X),
                                                    new ToolStripMenuItem("Copy", null, MenuItemOnClick,
                                                                          Keys.Control | Keys.C),
                                                    new ToolStripMenuItem("Paste", null, MenuItemOnClick,
                                                                          Keys.Control | Keys.V),
                                                    new ToolStripMenuItem("Delete", null, MenuItemOnClick, Keys.None),
                                                    new ToolStripSeparator(),
                                                    new ToolStripMenuItem("Find", null, MenuItemOnClick,
                                                                          Keys.Control | Keys.F),
                                                    new ToolStripMenuItem("Replace", null, MenuItemOnClick,
                                                                          Keys.Control | Keys.H),
                                                    new ToolStripMenuItem("Go To Line", null, MenuItemOnClick,
                                                                          Keys.Control | Keys.G)
                                                });
            /* View menu */
            _mnuView = new ToolStripMenuItem { Size = new Size(39, 20), Text = @"&View" };
            _mnuView.DropDownItems.AddRange(new ToolStripItem[]
                                                {
                                                    new ToolStripMenuItem("Syntax Highlight", null, MenuItemOnClick,
                                                                          Keys.None),
                                                    new ToolStripMenuItem("Line Numbering", null, MenuItemOnClick,
                                                                          Keys.None),
                                                    new ToolStripSeparator(),
                                                    new ToolStripMenuItem("Highlight Colors", null, MenuItemOnClick,
                                                                          Keys.None)
                                                });
            /* Help menu */
            _mnuHelp = new ToolStripMenuItem {Size = new Size(39, 20), Text = @"&Help"};
            _mnuHelp.DropDownItems.Add(new ToolStripMenuItem("Show Help", null, MenuItemOnClick, Keys.F1));

            _menu.Items.AddRange(new ToolStripItem[]
                                     {
                                         _mnuFile,
                                         _mnuEdit,
                                         _mnuView,
                                         _mnuHelp
                                     });
            /* Status bar - order components are added to the control array is IMPORTANT */
            _status = new StatusStrip
                          {
                              Dock = DockStyle.Bottom,
                              GripStyle = ToolStripGripStyle.Visible,
                              RenderMode = ToolStripRenderMode.Professional,
                              Renderer = renderer                              
                          };
            _label = new ToolStripStatusLabel
                         {
                             Alignment = ToolStripItemAlignment.Left,
                             TextAlign = ContentAlignment.MiddleLeft,
                             Image = Resources.editFile.ToBitmap(),
                             TextImageRelation = TextImageRelation.ImageBeforeText,
                             Text = @"File:"
                         };
            _fileName = new ToolStripStatusLabel
                            {
                                Spring = true,
                                Alignment = ToolStripItemAlignment.Left,
                                TextAlign = ContentAlignment.MiddleLeft,
                                AutoSize = true
                            };
            _lineInfo = new ToolStripStatusLabel
                            {
                                Alignment = ToolStripItemAlignment.Left,
                                TextAlign = ContentAlignment.MiddleLeft,
                                Image = Resources.editLines.ToBitmap(),
                                TextImageRelation = TextImageRelation.ImageBeforeText
                            };
            _editMode = new ToolStripStatusLabel
                            {
                                TextAlign = ContentAlignment.MiddleLeft,
                                Image = Resources.editReplace.ToBitmap(),
                                TextImageRelation = TextImageRelation.ImageBeforeText
                            };
            _status.Items.AddRange(new ToolStripItem[]
                                       {
                                           _label,
                                           _fileName, new ToolStripSeparator(),
                                           _lineInfo, new ToolStripSeparator(),
                                           _editMode
                                       });
            /* Treeview */
            _tvFiles = new TreeListView
                          {
                              BorderStyle = BorderStyle.None,
                              Dock = DockStyle.Fill,
                              FullRowSelect = true,
                              HideSelection = false,
                              HeaderStyle = ColumnHeaderStyle.Nonclickable,
                              Location = new Point(3, 3),
                              OwnerDraw = true,
                              ShowGroups = false,
                              MultiSelect = false,
                              Size = new Size(134, 290),
                              TabIndex = 1,
                              UseCompatibleStateImageBehavior = false,
                              View = View.Details,
                              VirtualMode = true
                          };

            /* Build treelist */
            _colFiles = new OlvColumn(@"Script Files:", "Name")
                            {
                                CellPadding = null,
                                IsEditable = false,
                                Sortable = false,
                                Width = 160,
                                FillsFreeSpace = true
                            };

            _tvFiles.AllColumns.Add(_colFiles);
            _tvFiles.Columns.Add(_colFiles);
            _tvFiles.TreeColumnRenderer.LinePen = new Pen(Color.FromArgb(190, 190, 190), 0.5F)
                                                      {
                                                          DashStyle = DashStyle.Dot
                                                      };
            /* Root item (network name) */
            _tvFiles.CanExpandGetter = x => x is ScriptFileNode;
            /* Children of each root item (script data) */
            _tvFiles.ChildrenGetter = delegate(object x)
                                          {
                                              var sd = (ScriptFileNode) x;
                                              return sd.Data;
                                          };            

            _colFiles.ImageGetter = x => x is ScriptFileNode
                                             ? Resources.codeHeader.ToBitmap()
                                             : Resources.codeFile.ToBitmap();

            _txtEdit = new ircScript.Controls.ScriptEditor
                           {
                               BackColor = SystemColors.Window,
                               BorderStyle = BorderStyle.None,
                               Dock = DockStyle.Fill,
                               Location = new Point(143, 3),
                               Size = new Size(350, 290),
                               EnableSyntaxHighlight = SettingsManager.Settings.Editor.SyntaxHighlight,
                               ShowLineNumbers = SettingsManager.Settings.Editor.LineNumbering,
                               Zoom = SettingsManager.Settings.Editor.Zoom,
                               CommentColor = SettingsManager.Settings.Editor.Colors.CommentColor,
                               CommandColor = SettingsManager.Settings.Editor.Colors.CommandColor,
                               KeywordColor = SettingsManager.Settings.Editor.Colors.KeyWordColor,
                               VariableColor = SettingsManager.Settings.Editor.Colors.VariableColor,
                               IdentifierColor = SettingsManager.Settings.Editor.Colors.IdentifierColor,
                               CustomIdentifierColor = SettingsManager.Settings.Editor.Colors.CustomIdentifierColor,
                               MiscColor = SettingsManager.Settings.Editor.Colors.MiscColor,
                               TabIndex = 0
                           };

            /* Splitter */
            _splitter = new SplitContainer
                            {
                                Dock = DockStyle.Fill,
                                BorderStyle = BorderStyle.FixedSingle,                               
                                Panel1MinSize = 120,
                                SplitterWidth = 2,                                
                                FixedPanel = FixedPanel.Panel1
                            };
            _splitter.SplitterMoved += SplitterMoved;

            _splitter.Panel1.Controls.Add(_tvFiles);
            _splitter.Panel2.Controls.Add(_txtEdit);

            Controls.AddRange(new Control[] {_splitter, _status, _menu});

            _timer = new Timer { Interval = 10 };

            MainMenuStrip = _menu;            
            /* Copy scripts to temporary arrays - create at least one of each if none */
            _aliases = ScriptManager.AliasData.Clone();
            if (_aliases.Count == 0)
            {
                NewScript(ScriptType.Aliases);
            }
            _events = ScriptManager.EventData.Clone();
            if (_events.Count == 0)
            {
                NewScript(ScriptType.Events);
            }
            /* Popups */
            _popups = PopupManager.Popups.Clone();
            /* Here we can cheat with displaying variables by creating them as new script file -
             * we leave the data blank as this gets "imported" when the viewing file is switched to %variables */
            _variables = new ScriptData
                             {
                                 Name = "%variables"                                 
                             };
            _varNode = new ScriptFileNode
                           {
                               Name = "Variables",
                               Type = ScriptType.Variables
                           };
            _varNode.Data.Add(_variables);
            _files.AddRange(new[]
                                {
                                    new ScriptFileNode {Name = "Aliases", Data = _aliases, Type = ScriptType.Aliases},
                                    new ScriptFileNode {Name = "Events", Data = _events, Type = ScriptType.Events},
                                    new ScriptFileNode {Name = "Popups", Data = _popups, Type = ScriptType.Popups},
                                    _varNode
                                });

            _tvFiles.AddObjects(_files);
            /* Attempt to get last script file and display it (if it's null, or aliases is 0, display variables
             * file, as it's always there, blank or not */
            var s = GetScriptFileByName(SettingsManager.Settings.Editor.Last);
            _currentEditingScript = s ?? _aliases[0];
            _tvFiles.ExpandAll();
            _tvFiles.SelectObject(_currentEditingScript);
            /* Set current editing script file */
            SwitchEditingFile(_currentEditingScript);
            /* Script formatting "corrector" */
            _menu.Items.Add(new ToolStripButton("{ }", null, MenuButtonClick)
                               {
                                   Alignment = ToolStripItemAlignment.Right,
                                   ToolTipText = @"Fix formatting"
                               });

            _mnuFile.DropDownOpening += MenuDropDownOpening;
            _mnuEdit.DropDownOpening += MenuDropDownOpening;
            _mnuView.DropDownOpening += MenuDropDownOpening;

            _tvFiles.SelectionChanged += FileSelectionChanged;
            _tvFiles.SelectedIndexChanged += FilesSelectedIndexChanged;
            _txtEdit.TextChanged += TextEditTextChanged;
            _txtEdit.KeyUp += TextEditKeyUp;
            _txtEdit.MouseUp += TextEditMouseUp;
            _txtEdit.ZoomChanged += TextEditZoomChanged;
            _timer.Tick += TimerFocusTick;
            
            _initialize = false;
        }

        /* Overrides */
        protected override void OnLoad(EventArgs e)
        {
            _splitter.SplitterDistance = SettingsManager.Settings.Editor.SplitSize;
            base.OnLoad(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {            
            /* Check if any files need saving */
            if (_files.SelectMany(file => file.Data).Any(s => s.ContentsChanged))
            {
                var r = MessageBox.Show(@"Some files have changed. Do you wish to save changes?",
                                        @"Save Changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
                if (r == DialogResult.Yes)
                {
                    SaveAll();
                }
            }
            base.OnFormClosing(e);
        }

        protected override void OnMove(EventArgs e)
        {
            if (!_initialize)
            {
                var w = SettingsManager.GetWindowByName("editor");
                if (WindowState == FormWindowState.Normal)
                {
                    w.Position = Location;
                }
            }
            base.OnMove(e);
        }

        protected override void OnResize(EventArgs e)
        {
            if (!_initialize)
            {
                var w = SettingsManager.GetWindowByName("editor");
                if (WindowState == FormWindowState.Normal)
                {
                    w.Size = Size;
                    w.Position = Location;
                }
                w.Maximized = WindowState == FormWindowState.Maximized;
            }
            base.OnResize(e);
        }

        /* Handler callbacks */
        private void SplitterMoved(object sender, SplitterEventArgs e)
        { 
            SettingsManager.Settings.Editor.SplitSize = _splitter.SplitterDistance;
        }

        private void TimerFocusTick(object sender, EventArgs e)
        {
            _timer.Enabled = false;
            _txtEdit.Focus();
            UpdateStatusInfo();
        }

        private void FileSelectionChanged(object sender, EventArgs e)
        {
            _timer.Enabled = true;
        }

        private void FilesSelectedIndexChanged(object sender, EventArgs e)
        {
            var node = _tvFiles.SelectedObject;
            if (node == null || node.GetType() == typeof (ScriptFileNode))
            {
                return;
            }
            var s = (ScriptData) node;
            if (s == _currentEditingScript)
            {                
                return;
            }
            SwitchEditingFile(s);
        }

        private void TextEditTextChanged(object sender, EventArgs e)
        {            
            if (_fileChanged)
            {
                return;
            }
            _currentEditingScript.ContentsChanged = true;
        }

        private void TextEditKeyUp(object sender, KeyEventArgs e)
        {
            UpdateStatusInfo();
        }

        private void TextEditMouseUp(object sender, MouseEventArgs e)
        {
            UpdateStatusInfo();
        }

        private void TextEditZoomChanged(object sender, EventArgs e)
        {
            SettingsManager.Settings.Editor.Zoom = _txtEdit.Zoom;
        }

        private void MenuDropDownOpening(object sender, EventArgs e)
        {
            var dd = (ToolStripMenuItem) sender;
            if (dd == null)
            {
                return;
            }
            switch (dd.Text.ToUpper())
            {
                case "&FILE":
                    var nodeType = GetNodeType();
                    var node = nodeType != ScriptType.Variables && nodeType != ScriptType.Popups;
                    var type = _tvFiles.SelectedObject != null && _tvFiles.SelectedObject.GetType() != typeof(ScriptFileNode);
                    _mnuFile.DropDownItems[0].Enabled = node;
                    _mnuFile.DropDownItems[2].Enabled = node;
                    /* Cannot unload/save as the root item! */
                    _mnuFile.DropDownItems[3].Enabled = node && type;
                    _mnuFile.DropDownItems[6].Enabled = node && type;                    
                    /* Cannot move variables/popups or root items */
                    _mnuFile.DropDownItems[9].Enabled = node && type;
                    _mnuFile.DropDownItems[10].Enabled = node && type;
                    break;

                case "&EDIT":
                    /* Check ability to undo/redo */
                    _mnuEdit.DropDownItems[0].Enabled = _txtEdit.UndoEnabled;
                    _mnuEdit.DropDownItems[1].Enabled = _txtEdit.RedoEnabled;
                    _mnuEdit.DropDownItems[5].Enabled = Clipboard.ContainsText();                    
                    break;

                case "&VIEW":
                    ((ToolStripMenuItem) _mnuView.DropDownItems[0]).Checked =
                        SettingsManager.Settings.Editor.SyntaxHighlight;
                    ((ToolStripMenuItem) _mnuView.DropDownItems[1]).Checked =
                        SettingsManager.Settings.Editor.LineNumbering;
                    break;                    
            }
        }

        private void MenuButtonClick(object sender, EventArgs e)
        {
            var btn = (ToolStripButton) sender;
            if (btn == null || _currentEditingScript == null)
            {
                return;
            }
            /* Reformat text */
            _currentEditingScript.SelectionStart = _txtEdit.SelectionStart;
            _txtEdit.Indent();
            _txtEdit.SelectionStart = _currentEditingScript.SelectionStart;
            _txtEdit.DoSelectionVisible();
        }

        private void MenuItemOnClick(object sender, EventArgs e)
        {
            var di = (ToolStripMenuItem) sender;
            if (di == null)
            {
                return;
            }
            bool enable;
            switch (di.Text.ToUpper())
            {
                case "NEW...":
                    NewScript();
                    break;

                case "LOAD":
                    LoadScript();
                    break;

                case "UNLOAD":
                    UnloadScript();
                    break;

                case "SAVE":
                    Save();
                    break;

                case "SAVE AS...":
                    SaveAs();
                    break;

                case "SAVE ALL":
                    SaveAll();
                    break;

                case "MOVE UP":
                    /* Pretty simple to do */
                    MoveScript(false);                    
                    break;

                case "MOVE DOWN":
                    /* Again pretty simple to do */
                    MoveScript(true);
                    break;

                case "CLOSE":
                    Close();
                    break;

                case "UNDO":
                    _txtEdit.Undo();
                    break;

                case "REDO":
                    _txtEdit.Redo();
                    break;

                case "CUT":
                    _txtEdit.Cut();
                    break;

                case "COPY":
                    _txtEdit.Copy();
                    break;

                case "PASTE":
                    _txtEdit.Paste();
                    break;

                case "DELETE":
                    /* Copy contents of clipboard */
                    var clipText = Clipboard.GetText();
                    /* Remove selected text */
                    _txtEdit.Cut();
                    /* Reset clipboard contents */
                    if (!string.IsNullOrEmpty(clipText))
                    {
                        Clipboard.SetText(clipText);
                    }
                    else
                    {
                        Clipboard.Clear();
                    }
                    break;

                case "FIND":
                    _txtEdit.ShowFindDialog();
                    break;

                case "REPLACE":
                    _txtEdit.ShowReplaceDialog();
                    break;

                case "GO TO LINE":
                    _txtEdit.ShowGoToDialog();
                    break;

                case "SYNTAX HIGHLIGHT":
                    enable = !SettingsManager.Settings.Editor.SyntaxHighlight;
                    SettingsManager.Settings.Editor.SyntaxHighlight = enable;
                    _txtEdit.EnableSyntaxHighlight = enable;
                    break;

                case "LINE NUMBERING":
                    enable = !SettingsManager.Settings.Editor.LineNumbering;
                    SettingsManager.Settings.Editor.LineNumbering = enable;
                    _txtEdit.ShowLineNumbers = enable;
                    break;

                case "HIGHLIGHT COLORS":
                    using (var d = new FrmColors())
                    {
                        if (d.ShowDialog(this) == DialogResult.OK)
                        {
                            _txtEdit.CommentColor = SettingsManager.Settings.Editor.Colors.CommentColor;
                            _txtEdit.CommandColor = SettingsManager.Settings.Editor.Colors.CommandColor;
                            _txtEdit.KeywordColor = SettingsManager.Settings.Editor.Colors.KeyWordColor;
                            _txtEdit.VariableColor = SettingsManager.Settings.Editor.Colors.VariableColor;
                            _txtEdit.IdentifierColor = SettingsManager.Settings.Editor.Colors.IdentifierColor;
                            _txtEdit.CustomIdentifierColor = SettingsManager.Settings.Editor.Colors.CustomIdentifierColor;
                            _txtEdit.MiscColor = SettingsManager.Settings.Editor.Colors.MiscColor;
                            _txtEdit.SetStyles();
                        }
                    }
                    break;

                case "SHOW HELP":
                    try
                    {
                        var path = Functions.MainDir(@"\FusionIRC Help.chm", true);
                        if (File.Exists(path))
                        {
                            Process.Start("hh", string.Format(@"{0}::/Scripting.htm", path));
                        }
                    }
                    catch
                    {
                        Debug.Assert(true);
                    }
                    break;
            }
        }

        /* Private methods - script adding/deleting/renaming */
        private void NewScript()
        {
            /* Create a new script file - we need to know the type of file to add and we can't rely on tvFiles
             * to always have a selection */
            var type = GetNodeType();            
            NewScript(type);
        }

        private void NewScript(ScriptType type)
        {
            /* We generate a random file name based on type (aliases01) */
            var list = GetScriptListByType(type);
            string name;
            switch (type)
            {
                case ScriptType.Aliases:
                    name = "aliases";
                    break;

                default:
                    name = "events";
                    break;
            }
            var scriptName = string.Format("{0}{1}", name, list.Count + 1);
            var script = new ScriptData { Name = scriptName, ContentsChanged = true };
            /* Don't need to find what FileNode this script belongs to, as when added to either list below, _files
             * is referenced to that list - any changes made on the lists is reflected by _files */
            list.Add(script);
            /* Update file list */
            var path = Functions.MainDir(string.Format(@"\scripts\{0}.xml", scriptName));
            /* Save script */
            AddNewFilePath(type, path);
            ScriptManager.SaveScript(script, path);
            /* Update treeview */
            _tvFiles.RefreshObjects(_files);
            _tvFiles.Expand(script);
            _tvFiles.SelectObject(script);
        }

        private void LoadScript()
        {
            var nodeType = GetNodeType();
            if (nodeType == ScriptType.Variables)
            {
                return; /* Don't do anything! */
            }
            string fileName;
            using (var fd = new OpenFileDialog
                                {
                                    Title = @"Load script", Filter = @"Script files (*.XML)|*.XML"
                                })
            {
                if (fd.ShowDialog(this) == DialogResult.Cancel)
                {
                    return;
                }
                fileName = fd.FileName;
            }                       
            var script = ScriptManager.LoadScript(fileName);
            if (script == null)
            {
                return;
            }
            /* Check it's not already loaded */
            var s = GetScriptFileByName(script.Name);
            if (s != null)
            {
                _tvFiles.SelectObject(s);
                return;
            }
            /* We have a script file, add it to file list and treeview */
            var list = GetScriptListByType(nodeType);
            list.Add(script);
            /* Update file list */
            AddNewFilePath(nodeType, fileName);
            /* Save all scripts */
            SaveAll();
            _tvFiles.RefreshObjects(_files);
            _tvFiles.Expand(script);
            _tvFiles.SelectObject(script);            
        }

        private void UnloadScript()
        {
            var nodeType = GetNodeType();
            var script = _currentEditingScript;
            if (script == null)
            {
                /* It shouldn't... */
                return;
            }
            /* Make sure to dump contents of editbox! */
            if (_currentEditingScript.ContentsChanged)
            {
                _currentEditingScript.RawScriptData = new List<string>(_txtEdit.Lines);
            }
            /* Check it doesn't need saving */
            if (script.ContentsChanged)
            {
                if (MessageBox.Show(@"Current file's contents have changed. Do you wish to save the changes before unloading the selected script?", @"Save script", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    var file = nodeType == ScriptType.Aliases
                                   ? ScriptManager.GetScriptFilePath(SettingsManager.Settings.Scripts.Aliases, script)
                                   : ScriptManager.GetScriptFilePath(SettingsManager.Settings.Scripts.Events, script);
                    ScriptManager.SaveScript(script, Functions.MainDir(file.Path));
                }
                else
                {
                    return;
                }
            }
            var list = GetScriptListByType(nodeType);
            list.Remove(script);
            var nextIndex = list.Count - 1;
            script = nextIndex >= 0 ? list[nextIndex] : null;   
            /* Remove from file list */
            switch (nodeType)
            {
                case ScriptType.Aliases:
                    ScriptManager.RemoveScriptFilePath(SettingsManager.Settings.Scripts.Aliases, _currentEditingScript);
                    break;

                case ScriptType.Events:
                    ScriptManager.RemoveScriptFilePath(SettingsManager.Settings.Scripts.Events, _currentEditingScript);
                    break;
            }
            _tvFiles.RefreshObjects(_files);
            if (script != null)
            {
                _tvFiles.Expand(script);
                _tvFiles.SelectObject(script);
            }
            else
            {
                _txtEdit.Clear();
            }
        }

        private void Save()
        {            
            if (_currentEditingScript == null || !_currentEditingScript.ContentsChanged)
            {
                return;
            }
            /* Make sure to dump contents of edit window text */
            _currentEditingScript.RawScriptData = new List<string>(_txtEdit.Lines);
            /* Save current editing file */
            var type = GetNodeType();
            SettingsScripts.SettingsScriptPath file;
            switch (type)
            {
                case ScriptType.Variables:
                    RebuildVariables(_currentEditingScript);
                    break;

                case ScriptType.Popups:
                    file = ScriptManager.GetScriptFilePath(SettingsManager.Settings.Scripts.Popups,
                                                           _currentEditingScript);
                    PopupManager.SavePopup(_currentEditingScript, Functions.MainDir(file.Path));
                    if (file.Type == PopupType.Commands)
                    {
                        var menu = ((FrmClientWindow) WindowManager.MainForm).MenuBar.MenuCommands;
                        menu.Visible = PopupManager.BuildPopups(PopupType.Commands, _currentEditingScript, menu.DropDownItems);
                    }
                    break;

                default:
                    /* Renaming of scripts happens elsewhere and does not need this flag set to true
                     * nor the file deleted - just rename file and change the Name flag */
                    file = type == ScriptType.Aliases
                               ? ScriptManager.GetScriptFilePath(SettingsManager.Settings.Scripts.Aliases,
                                                                 _currentEditingScript)
                               : ScriptManager.GetScriptFilePath(SettingsManager.Settings.Scripts.Events,
                                                                 _currentEditingScript);
                    //ScriptManager.SaveScript(_currentEditingScript, Functions.MainDir(string.Format(@"\scripts\{0}.xml", _currentEditingScript.Name)));                    
                    ScriptManager.SaveScript(_currentEditingScript, Functions.MainDir(file.Path));
                    break;
            }
            _currentEditingScript.ContentsChanged = false;
            RebuildAllScripts();
        }

        private void SaveAs()
        {
            /* Make sure to dump contents of editbox! */
            if (_currentEditingScript.ContentsChanged)
            {
                _currentEditingScript.RawScriptData = new List<string>(_txtEdit.Lines);
            }
            var type = GetNodeType();
            var oldFile = type == ScriptType.Aliases
                              ? ScriptManager.GetScriptFilePath(SettingsManager.Settings.Scripts.Aliases, _currentEditingScript)
                              : ScriptManager.GetScriptFilePath(SettingsManager.Settings.Scripts.Events, _currentEditingScript);
            
            string fileName;
            using (
                var sfd = new SaveFileDialog
                              {
                                  InitialDirectory = Functions.MainDir(@"\scripts"),
                                  Title = @"Save script as",
                                  Filter = @"Script files (*.XML)|*.XML"
                              })
            {
                if (sfd.ShowDialog(this) == DialogResult.Cancel)
                {
                    return;
                }
                fileName = sfd.FileName;
            }
            var name = Path.GetFileNameWithoutExtension(fileName);            
            if (string.IsNullOrEmpty(name))
            {
                return; /* Shouldn't be - but... */
            }
            _currentEditingScript.Name = name;
            ScriptManager.SaveScript(_currentEditingScript, fileName);
            oldFile.Path = Functions.MainDir(fileName);
            _tvFiles.RefreshObjects(_files);
        }

        private void SaveAll()
        {
            /* Make sure to dump contents of edit window text */
            if (_currentEditingScript != null && _currentEditingScript.ContentsChanged)
            {
                _currentEditingScript.RawScriptData = new List<string>(_txtEdit.Lines);
            }
            foreach (var file in _files)
            {
                foreach (var s in file.Data.Where(s => s.ContentsChanged))
                {
                    SettingsScripts.SettingsScriptPath f;
                    switch (file.Type)
                    {
                        case ScriptType.Variables:
                            RebuildVariables(s);
                            break;

                        case ScriptType.Popups:
                            f = ScriptManager.GetScriptFilePath(SettingsManager.Settings.Scripts.Popups, s);
                            PopupManager.SavePopup(s, Functions.MainDir(f.Path));
                            if (f.Type == PopupType.Commands)
                            {
                                var menu = ((FrmClientWindow) WindowManager.MainForm).MenuBar.MenuCommands;
                                menu.Visible = PopupManager.BuildPopups(PopupType.Commands, _currentEditingScript, menu.DropDownItems);
                            }
                            break;

                        default:
                            /* Renaming of scripts happens elsewhere and does not need this flag set to true
                             * nor the file deleted - just rename file and change the Name flag */
                            f = file.Type == ScriptType.Aliases
                                    ? ScriptManager.GetScriptFilePath(SettingsManager.Settings.Scripts.Aliases, s)
                                    : ScriptManager.GetScriptFilePath(SettingsManager.Settings.Scripts.Events, s);
                            //ScriptManager.SaveScript(s, Functions.MainDir(string.Format(@"\scripts\{0}.xml", s.Name)));
                            ScriptManager.SaveScript(s, Functions.MainDir(f.Path));
                            break;
                    }
                    s.ContentsChanged = false;
                }
            }            
            RebuildAllScripts();
        }

        private void MoveScript(bool moveDown)
        {            
            if (_currentEditingScript == null)
            {
                return;
            }
            /* Get file node script belongs to and the swap indexes */
            int index1;
            int index2;
            var node = GetScriptNodeListData(_currentEditingScript);
            if (node == null || !GetScriptFileIndexes(node, _currentEditingScript, moveDown, out index1, out index2))
            {
                /* Can't move */
                return;
            }
            /* Dump contents of text box */
            if (_currentEditingScript.ContentsChanged)
            {
                _currentEditingScript.RawScriptData = new List<string>(_txtEdit.Lines);
            }
            /* Done! */
            node.Swap(index1, index2);
            RebuildAllScripts();
            _tvFiles.RefreshObjects(_files);
            _tvFiles.SelectObject(_currentEditingScript);
        }

        private bool GetScriptFileIndexes(IList<ScriptData> list, ScriptData script, bool moveDown, out int index1, out int index2)
        {
            var index = list.IndexOf(script);
            var i = index + (moveDown ? +1 : -1);
            if (i < 0 || i > _aliases.Count - 1)
            {
                /* Cannot move */
                index1 = -1;
                index2 = -1;
                return false;
            }
            index1 = index;
            index2 = i;
            return true;
        }

        /* Script rebuilding */
        private static void AddNewFilePath(ScriptType type, string fileName)
        {
            var p = new SettingsScripts.SettingsScriptPath {Path = Functions.MainDir(fileName)};
            switch (type)
            {
                case ScriptType.Aliases:
                    SettingsManager.Settings.Scripts.Aliases.Add(p);
                    break;

                case ScriptType.Events:
                    SettingsManager.Settings.Scripts.Events.Add(p);
                    break;
            }
        }

        private void RebuildAllScripts()
        {
            /* Make sure to clone these lists back to master lists (adding of new files/renaming) */
            ScriptManager.AliasData = _aliases.Clone();
            ScriptManager.EventData = _events.Clone();
            PopupManager.Popups = _popups.Clone();
            /* Build script data */
            ScriptManager.BuildScripts(ScriptType.Aliases, ScriptManager.AliasData, ScriptManager.Aliases);
            ScriptManager.BuildScripts(ScriptType.Events, ScriptManager.EventData, ScriptManager.Events);
            PopupManager.ReBuildAllPopups();
            UpdateStatusInfo();
        }

        private static void RebuildVariables(ScriptData s)
        {
            /* A little more involved... */
            ScriptManager.Variables.Variable.Clear();
            foreach (var v in s.RawScriptData)
            {               
                var globalVar = new ScriptVariable();
                var i = v.IndexOf('=');
                if (i != -1)
                {
                    globalVar.Name = Functions.GetFirstWord(v.Substring(0, i));
                    globalVar.Value = v.Substring(i + 1);
                }
                else
                {
                    globalVar.Name = v;
                }
                ScriptManager.Variables.Variable.Add(globalVar);
            }
            ScriptManager.SaveVariables(Functions.MainDir(@"\scripts\variables.xml"));
        }

        /* Helper methods */
        private void UpdateStatusInfo()
        {            
            var r = _txtEdit.Selection;
            //var path = Functions.MainDir(string.Format(@"\scripts\{0}.xml", _currentEditingScript.Name));
            var type = GetNodeType();
            SettingsScripts.SettingsScriptPath file;
            switch (type)
            {
                case ScriptType.Aliases:
                    file = ScriptManager.GetScriptFilePath(SettingsManager.Settings.Scripts.Aliases, _currentEditingScript);
                    break;

                case ScriptType.Events:
                    file = ScriptManager.GetScriptFilePath(SettingsManager.Settings.Scripts.Events, _currentEditingScript);
                    break;

                case ScriptType.Popups:
                    file = ScriptManager.GetScriptFilePath(SettingsManager.Settings.Scripts.Popups, _currentEditingScript);
                    break;

                default:
                    file = new SettingsScripts.SettingsScriptPath {Path = @"\scripts\variables.xml"};
                    break;
            }
            if (file == null)
            {
                return;
            }
            var path = Functions.MainDir(file.Path);
            _fileName.Text = string.Format("{0}", _currentEditingScript.ContentsChanged
                                                      ? string.Format("* {0}", path)
                                                      : string.Format("{0}", path));
            _lineInfo.Text = string.Format("Line: {0}/{1}    Col: {2}", r.Start.Line + 1, _txtEdit.LinesCount, r.Start.Char);            
            _editMode.Text = _txtEdit.IsReplaceMode ? "Overwrite" : "Insert";
        }

        private List<ScriptData> GetScriptListByType(ScriptType type)
        {
            switch (type)
            {
                case ScriptType.Aliases:
                    return _aliases;

                default:
                    return _events;
            }
        }

        private void SwitchEditingFile(ScriptData file)
        {
            if (!_initialize && _currentEditingScript != null)
            {
                if (_currentEditingScript.ContentsChanged)
                {
                    /* Make sure to dump contents of edit window text */
                    _currentEditingScript.RawScriptData = new List<string>(_txtEdit.Lines);
                    if (_currentEditingScript.Name == "%variables")
                    {
                        /* Dump out variables list to main file */
                        RebuildVariables(_currentEditingScript);
                        _currentEditingScript.ContentsChanged = false;
                    }
                }                
                _currentEditingScript.SelectionStart = _txtEdit.SelectionStart;
            }
            if (file == null)
            {
                return;
            }
            _fileChanged = true;
            _currentEditingScript = file;
            if (_currentEditingScript.Name == "%variables")
            {
                /* Make sure to get an updated version of this list */
                _currentEditingScript.RawScriptData =
                    ScriptManager.Variables.Variable.Select(data => data.ToString()).ToList();
            }
            _txtEdit.Clear();
            _txtEdit.Text = string.Join(Environment.NewLine, file.RawScriptData);                        
            _txtEdit.Indent();
            _txtEdit.SelectionStart = _currentEditingScript.SelectionStart;
            _txtEdit.DoSelectionVisible();
            _txtEdit.ClearUndo();
            SettingsManager.Settings.Editor.Last = file.Name;
            _fileChanged = false;

        }

        private ScriptType GetNodeType()
        {
            var type = ScriptType.Aliases;
            var node = _tvFiles.SelectedObject;
            if (node != null)
            {
                if (node.GetType() == typeof(ScriptFileNode))
                {
                    type = ((ScriptFileNode)node).Type;
                }
                else if (node.GetType() == typeof(ScriptData))
                {
                    type = GetNodeTypeFromScript((ScriptData)node);
                }
            }
            else
            {                
                type = GetNodeTypeFromScript(_currentEditingScript);
            }
            return type;
        }

        private ScriptType GetNodeTypeFromScript(ScriptData script)
        {
            return (from file in _files from s in file.Data where s == script select file.Type).FirstOrDefault();
        }

        private ScriptData GetScriptFileByName(string name)
        {
            return _files.SelectMany(f => f.Data).FirstOrDefault(s => !string.IsNullOrEmpty(s.Name) && s.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));            
        }

        private List<ScriptData> GetScriptNodeListData(ScriptData data)
        {
            return (from f in _files let d = f.Data.IndexOf(data) where d != -1 select f.Data).FirstOrDefault();
        }
    }
}