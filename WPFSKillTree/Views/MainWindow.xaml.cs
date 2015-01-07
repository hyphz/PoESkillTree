using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MahApps.Metro;
using POESKillTree.Controls;
using POESKillTree.Model;
using POESKillTree.SkillTreeFiles;
using POESKillTree.Utils;
using POESKillTree.ViewModels;
using POESKillTree.ViewModels.ItemAttribute;
using Attribute = POESKillTree.ViewModels.Attribute;
using MessageBox = POESKillTree.Views.MetroMessageBox;

namespace POESKillTree.Views
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private const string TreeAddress = "http://www.pathofexile.com/passive-skill-tree/";
        private const int TrivialMouseMoveThreshold = 25;
        private static readonly Action EmptyDelegate = delegate { };
        private readonly List<Attribute> _allAttributesList = new List<Attribute>();
        private readonly List<Attribute> _attiblist = new List<Attribute>();
        private readonly Regex _backreplace = new Regex("#");
        private readonly List<ListGroupItem> _defenceList = new List<ListGroupItem>();

        private readonly Dictionary<string, AttributeGroup> _defenceListGroups =
            new Dictionary<string, AttributeGroup>();

        private readonly ToolTip _noteTip = new ToolTip();
        private readonly List<ListGroupItem> _offenceList = new List<ListGroupItem>();

        private readonly Dictionary<string, AttributeGroup> _offenceListGroups =
            new Dictionary<string, AttributeGroup>();

        private readonly PersistentData _persistentData = new PersistentData();
        private readonly Stack<string> _redoList = new Stack<string>();
        private readonly ToolTip _sToolTip = new ToolTip();
        private readonly Stack<string> _undoList = new Stack<string>();
        private Vector2D _addtransform;
        private DragAdorner _adorner;
        private ListCollectionView _allAttributeCollection;
        private ListCollectionView _attibuteCollection;
        private RenderTargetBitmap _clipboardBmp;
        private SkillNode _currentNode;
        private ListCollectionView _defenceCollection;
        private Point _dragAndDropStartPoint;
        private ItemAttributes _itemAttributes;
        private bool _justLoaded;
        private Vector2D _lastpos;
        private string _lasttooltip;
        private AdornerLayer _layer;
        private LoadingWindow _loadingWindow;
        private Vector2D _multransform;
        private ListCollectionView _offenceCollection;
        private List<ushort> _prePath;
        private HashSet<ushort> _toRemove;
        protected SkillTree Tree;

        public MainWindow()
        {
            Thread.CurrentThread.SetApartmentState(ApartmentState.STA);
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            InitializeComponent();
        }

        #region Window methods

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            ItemDB.Load("Items.xml");
            if (File.Exists("ItemsLocal.xml"))
                ItemDB.Merge("ItemsLocal.xml");
            ItemDB.Index();

            _attibuteCollection = new ListCollectionView(_attiblist);
            listBox1.ItemsSource = _attibuteCollection;
            Debug.Assert(_attibuteCollection.GroupDescriptions != null, "_attibuteCollection.GroupDescriptions != null");
            _attibuteCollection.GroupDescriptions.Add(new PropertyGroupDescription("Text")
                                                      {
                                                          Converter = new GroupStringConverter()
                                                      });

            _allAttributeCollection = new ListCollectionView(_allAttributesList);
            Debug.Assert(_allAttributeCollection.GroupDescriptions != null, "_allAttributeCollection.GroupDescriptions != null");
            _allAttributeCollection.GroupDescriptions.Add(new PropertyGroupDescription("Text")
                                                          {
                                                              Converter = new GroupStringConverter()
                                                          });
            lbAllAttr.ItemsSource = _allAttributeCollection;

            _defenceCollection = new ListCollectionView(_defenceList);
            Debug.Assert(_defenceCollection.GroupDescriptions != null, "_defenceCollection.GroupDescriptions != null");
            _defenceCollection.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
            listBoxDefence.ItemsSource = _defenceCollection;

            _offenceCollection = new ListCollectionView(_offenceList);
            Debug.Assert(_offenceCollection.GroupDescriptions != null, "_offenceCollection.GroupDescriptions != null");
            _offenceCollection.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
            listBoxOffence.ItemsSource = _offenceCollection;

            //Load Persistent Data and set theme
            _persistentData.LoadPersistentDataFromFile();
            SetTheme(_persistentData.Options.Theme);
            SetAccent(_persistentData.Options.Accent);

            Tree = SkillTree.CreateSkillTree(StartLoadingWindow, UpdateLoadingWindow, CloseLoadingWindow);
            recSkillTree.Width = Tree.TRect.Width/Tree.TRect.Height*500;
            recSkillTree.UpdateLayout();
            recSkillTree.Fill = new VisualBrush(Tree.SkillTreeVisual);

            Tree.Chartype =
                Tree.CharName.IndexOf(((string) ((ComboBoxItem) cbCharType.SelectedItem).Content).ToUpper());
            Tree.UpdateAvailNodes();
            UpdateAllAttributeList();

            _multransform = Tree.TRect.Size/new Vector2D(recSkillTree.RenderSize.Width, recSkillTree.RenderSize.Height);
            _addtransform = Tree.TRect.TopLeft;

            expAttributes.IsExpanded = _persistentData.Options.AttributesBarOpened;
            expSavedBuilds.IsExpanded = _persistentData.Options.BuildsBarOpened;

            // loading last build
            if (_persistentData.CurrentBuild != null)
                SetCurrentBuild(_persistentData.CurrentBuild);

            BtnLoadBuildClick(this, new RoutedEventArgs());
            _justLoaded = false;

            // loading saved build
            lvSavedBuilds.Items.Clear();
            foreach (var build in _persistentData.Builds)
            {
                lvSavedBuilds.Items.Add(build);
            }

            ImportLegacySavedBuilds();
        }

        private void WindowPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.Q:
                        ToggleAttributes();
                        break;
                    case Key.B:
                        ToggleBuilds();
                        break;
                    case Key.R:
                        BtnResetClick(sender, e);
                        break;
                    case Key.E:
                        BtnPoeUrlClick(sender, e);
                        break;
                    case Key.D1:
                        cbCharType.SelectedIndex = 0;
                        break;
                    case Key.D2:
                        cbCharType.SelectedIndex = 1;
                        break;
                    case Key.D3:
                        cbCharType.SelectedIndex = 2;
                        break;
                    case Key.D4:
                        cbCharType.SelectedIndex = 3;
                        break;
                    case Key.D5:
                        cbCharType.SelectedIndex = 4;
                        break;
                    case Key.D6:
                        cbCharType.SelectedIndex = 5;
                        break;
                    case Key.D7:
                        cbCharType.SelectedIndex = 6;
                        break;
                    case Key.Z:
                        TbSkillUrlUndo();
                        break;
                    case Key.Y:
                        TbSkillUrlRedo();
                        break;
                    case Key.S:
                        SaveNewBuild();
                        break;
                }
            }
        }

        private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
        }

        private void WindowClosing(object sender, CancelEventArgs e)
        {
            _persistentData.CurrentBuild.Url = tbSkillURL.Text;
            _persistentData.CurrentBuild.Level = tbLevel.Text;
            _persistentData.Options.AttributesBarOpened = expAttributes.IsExpanded;
            _persistentData.Options.BuildsBarOpened = expSavedBuilds.IsExpanded;
            _persistentData.SavePersistentDataToFile();

            if (lvSavedBuilds.Items.Count > 0)
            {
                SaveBuildsToFile();
            }
        }

        #endregion

        #region LoadingWindow

        private void StartLoadingWindow()
        {
            _loadingWindow = new LoadingWindow();
            _loadingWindow.Show();
            Thread.Sleep(400);
            _loadingWindow.Dispatcher.Invoke(DispatcherPriority.Render, EmptyDelegate);
        }

        private void UpdateLoadingWindow(double c, double max)
        {
            _loadingWindow.progressBar1.Maximum = max;
            _loadingWindow.progressBar1.Value = c;
            _loadingWindow.Dispatcher.Invoke(DispatcherPriority.Render, EmptyDelegate);
            if (Equals(c, max))
                Thread.Sleep(100);
        }

        private void CloseLoadingWindow()
        {
            _loadingWindow.Close();
        }

        #endregion

        #region Menu

        private void MenuSkillHighlightedNodes(object sender, RoutedEventArgs e)
        {
            var currentCursor = Cursor;
            try
            {
                Cursor = Cursors.Wait;
                Tree.SkillAllHighligtedNodes();
                UpdateAllAttributeList();
                tbSkillURL.Text = Tree.SaveToURL();
            }
            finally
            {
                Cursor = currentCursor;
            }
        }

        private void MenuScreenShot(object sender, RoutedEventArgs e)
        {
            const int maxsize = 3000;
            Rect2D contentBounds = Tree.PicActiveLinks.ContentBounds;
            contentBounds *= 1.2;
            if (!double.IsNaN(contentBounds.Width) && !double.IsNaN(contentBounds.Height))
            {
                var aspect = contentBounds.Width/contentBounds.Height;
                var xmax = contentBounds.Width;
                var ymax = contentBounds.Height;
                if (aspect > 1 && xmax > maxsize)
                {
                    xmax = maxsize;
                    ymax = xmax/aspect;
                }
                if (aspect < 1 & ymax > maxsize)
                {
                    ymax = maxsize;
                    xmax = ymax*aspect;
                }

                _clipboardBmp = new RenderTargetBitmap((int) xmax, (int) ymax, 96, 96, PixelFormats.Pbgra32);
                var db = new VisualBrush(Tree.SkillTreeVisual)
                         {
                             ViewboxUnits = BrushMappingMode.Absolute,
                             Viewbox = contentBounds
                         };
                var dw = new DrawingVisual();

                using (var dc = dw.RenderOpen())
                {
                    dc.DrawRectangle(db, null, new Rect(0, 0, xmax, ymax));
                }
                _clipboardBmp.Render(dw);
                _clipboardBmp.Freeze();

                Clipboard.SetImage(_clipboardBmp);

                recSkillTree.Fill = new VisualBrush(Tree.SkillTreeVisual);
            }
        }

        private void MenuImportItems(object sender, RoutedEventArgs e)
        {
            var diw = new DownloadItemsWindow(_persistentData.CurrentBuild.CharacterName) {Owner = this};
            diw.ShowDialog();
            _persistentData.CurrentBuild.CharacterName = diw.GetCharacterName();
        }

        private void MenuClearItems(object sender, RoutedEventArgs e)
        {
            ClearCurrentItemData();
        }

        private void MenuCopyStats(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            foreach (var at in _attiblist)
            {
                sb.AppendLine(at.ToString());
            }
            try
            {
                System.Windows.Forms.Clipboard.SetText(sb.ToString());
            }
            catch (Exception)
            {
                MessageBox.Show("Clipboard could not be copied to. Please try again.", "Failed Copy!",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuRedownloadTreeAssets(object sender, RoutedEventArgs e)
        {
            const string sMessageBoxText =
                "This will delete your data folder and Redownload all the SkillTree assets.\nThis requires an internet connection!\n\nDo you want to proced?";
            const string sCaption = "Redownload SkillTree Assets - Warning";

            var rsltMessageBox = MessageBox.Show(sMessageBoxText, sCaption, MessageBoxButton.YesNo,
                MessageBoxImage.Warning, MessageBoxResult.No);
            switch (rsltMessageBox)
            {
                case MessageBoxResult.Yes:
                    if (Directory.Exists("Data"))
                    {
                        try
                        {
                            if (Directory.Exists("DataBackup"))
                                Directory.Delete("DataBackup", true);
                            Directory.Move("Data", "DataBackup");

                            Tree = SkillTree.CreateSkillTree(StartLoadingWindow, UpdateLoadingWindow, CloseLoadingWindow);
                            recSkillTree.Fill = new VisualBrush(Tree.SkillTreeVisual);

                            BtnLoadBuildClick(this, new RoutedEventArgs());
                            _justLoaded = false;

                            if (Directory.Exists("DataBackup"))
                                Directory.Delete("DataBackup", true);
                        }
                        catch (Exception ex)
                        {
                            if (Directory.Exists("Data"))
                                Directory.Delete("Data", true);
                            try
                            {
                                CloseLoadingWindow();
                            }
                            catch (Exception)
                            {
                                //Nothing
                            }
                            Directory.Move("DataBackup", "Data");
                            MessageBox.Show(ex.Message, "Error while downloading assets", MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                    }
                    break;

                case MessageBoxResult.No:
                    //Do nothing
                    break;
            }
        }

        private void MenuExit(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MenuOpenPoEWebsite(object sender, RoutedEventArgs e)
        {
            Process.Start("https://www.pathofexile.com/");
        }

        private void MenuOpenWiki(object sender, RoutedEventArgs e)
        {
            Process.Start("http://pathofexile.gamepedia.com/");
        }

        private void MenuOpenHelp(object sender, RoutedEventArgs e)
        {
            var helpWindow = new HelpWindow();
            helpWindow.ShowDialog();
        }

        private void MenuOpenHotkeys(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new HotkeysWindow();
            aboutWindow.ShowDialog();
        }

        private void MenuOpenAbout(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new AboutWindow();
            aboutWindow.ShowDialog();
        }

        // Checks for updates.
        private void MenuCheckForUpdates(object sender, RoutedEventArgs e)
        {
            try
            {
                var release = Updater.CheckForUpdates();
                if (release == null)
                {
                    MessageBox.Show("You have the lastest version!", "No update found.");
                }
                else
                {
                    var message = "Would you like to install " + release.Version + "?";
                    MessageBoxResult download;
                    if (release.Version.ToLower().Contains("pre"))
                    {
                        download =
                            MessageBox.Show(message + "\nThis is a pre-release, meaning there could be some bugs!",
                                "Pre-release Found!", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    }
                    else
                        download = MessageBox.Show(message, "Release Found!", MessageBoxButton.YesNo);

                    if (download == MessageBoxResult.Yes)
                        BtnUpdateInstall();
                    else
                        btnUpdateCancel();
                    // Show dialog with release informations and "Install & Restart" button.
                }
            }
            catch (UpdaterException ex)
            {
                // Display error message: ex.Message.
                MessageBox.Show(ex.Message, "Error while checking for updates", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // Starts update process.
        private void BtnUpdateInstall()
        {
            try
            {
                // Show download progress bar and Cancel button.
                // Start downloading.
                StartLoadingWindow();
                Updater.Download(UpdateDownloadCompleted, UpdateDownloadProgressChanged);
            }
            catch (UpdaterException ex)
            {
                // Display error message: ex.Message.
                MessageBox.Show(ex.Message, "Failed to install update!");
            }
        }

        // Cancels update download (also invoked when download progress dialog is closed).
        private void btnUpdateCancel()
        {
            if (Updater.IsDownloading)
                Updater.Cancel();
            else
            {
                Updater.Dispose();
                // Close dialog.
            }
        }

        // Invoked when update download completes, aborts or fails.
        private void UpdateDownloadCompleted(Object sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled) // Check whether download was cancelled.
            {
                Updater.Dispose();
                // Close dialog.
            }
            else if (e.Error != null) // Check whether error occured.
            {
                // Display error message: e.Error.Message.
                MessageBox.Show(e.Error.Message, "Failed to install update!");
            }
            else // Download completed.
            {
                try
                {
                    Updater.Install();
                    Updater.RestartApplication();
                }
                catch (UpdaterException ex)
                {
                    Updater.Dispose();
                    // Display error message: ex.Message.
                    MessageBox.Show(ex.Message, "Failed to install update!");
                }
            }
            CloseLoadingWindow();
        }

        // Invoked when update download progress changes.
        private void UpdateDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            // Update download progres bar.
            UpdateLoadingWindow(e.BytesReceived, e.TotalBytesToReceive);
        }

        #endregion

        #region  Character Selection

        private void CbCharTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_justLoaded)
            {
                _justLoaded = false;
                return;
            }

            if (Tree == null)
                return;
            var comboItem = (ComboBoxItem) cbCharType.SelectedItem;
            var className = comboItem.Name;

            if (Tree.CanSwitchClass(className))
            {
                var currentClassArray = GetCurrentClass();
                var changeClassArray = getAnyClass(className);

                if (currentClassArray[0] == "ERROR")
                    return;
                if (changeClassArray[0] == "ERROR")
                    return;
                var usedPoints = tbUsedPoints.Text;
                cbCharType.Text = changeClassArray[0];

                Tree.LoadFromURL(tbSkillURL.Text.Replace(currentClassArray[1], changeClassArray[1]));
                tbUsedPoints.Text = usedPoints;
            }
            else
            {
                var startnode =
                    Tree.Skillnodes.First(
                        nd => nd.Value.Name.ToUpper() == (Tree.CharName[cbCharType.SelectedIndex]).ToUpper()).Value;
                Tree.SkilledNodes.Clear();
                Tree.SkilledNodes.Add(startnode.Id);
                Tree.Chartype = Tree.CharName.IndexOf((Tree.CharName[cbCharType.SelectedIndex]).ToUpper());
            }
            Tree.UpdateAvailNodes();
            UpdateAllAttributeList();
            tbSkillURL.Text = Tree.SaveToURL();
        }

        private void TbLevelTextChanged(object sender, TextChangedEventArgs e)
        {
            int lvl;
            if (!int.TryParse(tbLevel.Text, out lvl)) return;
            Tree.Level = lvl;
            UpdateAllAttributeList();
        }

        private void BtnResetClick(object sender, RoutedEventArgs e)
        {
            if (Tree == null)
                return;
            Tree.Reset();
            UpdateAllAttributeList();
            tbSkillURL.Text = Tree.SaveToURL();
        }

        #endregion

        #region Update Attribute and Character lists

        public void UpdateAllAttributeList()
        {
            _allAttributesList.Clear();

            if (_itemAttributes != null)
            {
                var attritemp = Tree.SelectedAttributesWithoutImplicit;
                foreach (var mod in _itemAttributes.NonLocalMods)
                {
                    if (attritemp.ContainsKey(mod.TextAttribute))
                    {
                        for (var i = 0; i < mod.Value.Count; i++)
                        {
                            attritemp[mod.TextAttribute][i] += mod.Value[i];
                        }
                    }
                    else
                    {
                        attritemp[mod.TextAttribute] = mod.Value;
                    }
                }

                foreach (var a in Tree.ImplicitAttributes(attritemp))
                {
                    var key = SkillTree.RenameImplicitAttributes.ContainsKey(a.Key)
                        ? SkillTree.RenameImplicitAttributes[a.Key]
                        : a.Key;

                    if (!attritemp.ContainsKey(key))
                        attritemp[key] = new List<float>();
                    for (var i = 0; i < a.Value.Count; i++)
                    {
                        if (attritemp.ContainsKey(key) && attritemp[key].Count > i)
                            attritemp[key][i] += a.Value[i];
                        else
                        {
                            attritemp[key].Add(a.Value[i]);
                        }
                    }
                }

                foreach (var item in (attritemp.Select(InsertNumbersInAttributes)))
                {
                    var a = new Attribute(item);
                    _allAttributesList.Add(a);
                }
            }

            _allAttributeCollection.Refresh();

            UpdateStatistics();

            UpdateAttributeList();
        }

        public void UpdateAttributeList()
        {
            _attiblist.Clear();
            foreach (var item in (Tree.SelectedAttributes.Select(InsertNumbersInAttributes)))
            {
                var a = new Attribute(item);
                _attiblist.Add(a);
            }
            _attibuteCollection.Refresh();
            tbUsedPoints.Text = "" + (Tree.SkilledNodes.Count - 1);
        }

        public void UpdateStatistics()
        {
            _defenceList.Clear();
            _offenceList.Clear();

            if (_itemAttributes != null)
            {
                Compute.Initialize(Tree, _itemAttributes);

                foreach (var group in Compute.Defense())
                {
                    foreach (var item in group.Properties.Select(InsertNumbersInAttributes))
                    {
                        AttributeGroup attributeGroup;
                        if (!_defenceListGroups.TryGetValue(group.Name, out attributeGroup))
                        {
                            attributeGroup = new AttributeGroup(group.Name);
                            _defenceListGroups.Add(group.Name, attributeGroup);
                        }
                        _defenceList.Add(new ListGroupItem(item, attributeGroup));
                    }
                }

                foreach (var group in Compute.Offense())
                {
                    foreach (var item in group.Properties.Select(InsertNumbersInAttributes))
                    {
                        AttributeGroup attributeGroup;
                        if (!_offenceListGroups.TryGetValue(group.Name, out attributeGroup))
                        {
                            attributeGroup = new AttributeGroup(group.Name);
                            _offenceListGroups.Add(group.Name, attributeGroup);
                        }
                        _offenceList.Add(new ListGroupItem(item, attributeGroup));
                    }
                }
            }

            _defenceCollection.Refresh();
            _offenceCollection.Refresh();
        }

        private string InsertNumbersInAttributes(KeyValuePair<string, List<float>> attrib)
        {
            return attrib.Value.Aggregate(attrib.Key, (current, f) => _backreplace.Replace(current, f + "", 1));
        }

        #endregion

        #region Attribute and Character lists - Event Handlers

        private void ToggleAttributes()
        {
            mnuViewAttributes.IsChecked = !mnuViewAttributes.IsChecked;
            expAttributes.IsExpanded = !expAttributes.IsExpanded;
        }

        private void ToggleAttributesClick(object sender, RoutedEventArgs e)
        {
            ToggleAttributes();
        }

        private void ExpAttributesCollapsed(object sender, RoutedEventArgs e)
        {
            if (sender == e.Source) // Ignore contained ListBox group collapsion events.
            {
                mnuViewAttributes.IsChecked = false;
            }
        }

        private void ExpAttributesExpanded(object sender, RoutedEventArgs e)
        {
            if (sender == e.Source) // Ignore contained ListBox group collapsion events.
            {
                mnuViewAttributes.IsChecked = true;

                if (expSheet.IsExpanded) expSheet.IsExpanded = false;
            }
        }

        private void TextBlockMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var selectedAttr =
                Regex.Replace(
                    Regex.Match(listBox1.SelectedItem.ToString(), @"(?!\d)\w.*\w")
                        .Value.Replace(@"+", @"\+")
                        .Replace(@"-", @"\-")
                        .Replace(@"%", @"\%"), @"\d+", @"\d+");
            Tree.HighlightNodes(selectedAttr, true, Brushes.Azure);
        }

        private void ExpAttributesMouseLeave(object sender, MouseEventArgs e)
        {
            SearchUpdate();
        }

        private void ExpSheetExpanded(object sender, RoutedEventArgs e)
        {
            if (sender == e.Source) // Ignore contained ListBox group expansion events.
            {
                if (expAttributes.IsExpanded) ToggleAttributes();
            }
        }

        private void ToggleBuilds()
        {
            mnuViewBuilds.IsChecked = !mnuViewBuilds.IsChecked;
            expSavedBuilds.IsExpanded = !expSavedBuilds.IsExpanded;
        }

        private void ToggleBuildsClick(object sender, RoutedEventArgs e)
        {
            ToggleBuilds();
        }

        private void ExpSavedBuildsCollapsed(object sender, RoutedEventArgs e)
        {
            mnuViewBuilds.IsChecked = false;
        }

        private void ExpSavedBuildsExpanded(object sender, RoutedEventArgs e)
        {
            mnuViewBuilds.IsChecked = true;
        }

        #endregion

        #region zbSkillTreeBackground

        private void ZbSkillTreeBackgroundClick(object sender, RoutedEventArgs e)
        {
            var p = ((MouseEventArgs) e.OriginalSource).GetPosition(zbSkillTreeBackground.Child);
            var v = new Vector2D(p.X, p.Y);

            v = v*_multransform + _addtransform;

            IEnumerable<KeyValuePair<ushort, SkillNode>> nodes =
                Tree.Skillnodes.Where(n => ((n.Value.Position - v).Length < 50)).ToList();
            if (nodes.Count() != 0)
            {
                var node = nodes.First().Value;

                if (node.Spc == null)
                {
                    if (Tree.SkilledNodes.Contains(node.Id))
                    {
                        Tree.ForceRefundNode(node.Id);
                        UpdateAllAttributeList();

                        _prePath = Tree.GetShortestPathTo(node.Id);
                        Tree.DrawPath(_prePath);
                    }
                    else if (_prePath != null)
                    {
                        foreach (var i in _prePath)
                        {
                            Tree.SkilledNodes.Add(i);
                        }
                        UpdateAllAttributeList();
                        Tree.UpdateAvailNodes();

                        _toRemove = Tree.ForceRefundNodePreview(node.Id);
                        if (_toRemove != null)
                            Tree.DrawRefundPreview(_toRemove);
                    }
                }
            }
            tbSkillURL.Text = Tree.SaveToURL();
        }

        private void ZbSkillTreeBackgroundMouseLeave(object sender, MouseEventArgs e)
        {
            // We might have popped up a tooltip while the window didn't have focus,
            // so we should close tooltips whenever the mouse leaves the canvas in addition to
            // whenever we lose focus.
            _sToolTip.IsOpen = false;
        }

        private void ZbSkillTreeBackgroundMouseMove(object sender, MouseEventArgs e)
        {
            var p = e.GetPosition(zbSkillTreeBackground.Child);
       
            var v = (Vector2D)p;
            v = v*_multransform + _addtransform;
            textBox1.Text = "" + v.X;
            textBox2.Text = "" + v.Y;
            if ((_lastpos - v).Length < TrivialMouseMoveThreshold) return;
            _lastpos = v;

            SkillNode node = null;

            IEnumerable<KeyValuePair<ushort, SkillNode>> nodes =
                Tree.Skillnodes.Where(n => ((n.Value.Position - v).Length < 50)).ToList();
            if (nodes.Count() != 0)
                node = nodes.First().Value;

            if (node != null && node.Attributes.Count != 0)
            {
                if (_currentNode != node)
                {
                    var tooltip = node.Name + "\n" + node.attributes.Aggregate((s1, s2) => s1 + "\n" + s2);
                    if (!(_sToolTip.IsOpen && _lasttooltip == tooltip))
                    {
                        _sToolTip.Content = tooltip;
                        _sToolTip.IsOpen = true;
                        _lasttooltip = tooltip;
                    }
                    if (Tree.SkilledNodes.Contains(node.Id))
                    {
                        _toRemove = Tree.ForceRefundNodePreview(node.Id);
                        if (_toRemove != null)
                            Tree.DrawRefundPreview(_toRemove);
                    }
                    else
                    {
                        _prePath = Tree.GetShortestPathTo(node.Id);
                        Tree.DrawPath(_prePath);
                    }
                    _currentNode = node;
                }
            }
            else
            {
                if (_currentNode != null)
                {
                    _sToolTip.Tag = false;
                    _sToolTip.IsOpen = false;
                    _prePath = null;
                    _toRemove = null;
                    if (Tree != null)
                    {
                        Tree.ClearPath();
                    }
                    _currentNode = null;
                }
            }
        }

        private void ZbSkillTreeBackgroundPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            zbSkillTreeBackground.Child.RaiseEvent(e);
        }

        #endregion

        #region Items

        public void LoadItemData(string itemData)
        {
            if (!string.IsNullOrEmpty(itemData))
            {
                try
                {
                    _persistentData.CurrentBuild.ItemData = itemData;
                    _itemAttributes = new ItemAttributes(itemData);
                    lbItemAttr.ItemsSource = _itemAttributes.Attributes;
                    mnuClearItems.IsEnabled = true;
                }
                catch (Exception)
                {
                    MessageBox.Show("Item data currupted!");
                    _persistentData.CurrentBuild.ItemData = "";
                    _itemAttributes = null;
                    lbItemAttr.ItemsSource = null;
                    ClearCurrentItemData();
                }
            }
            else
            {
                ClearCurrentItemData();
            }

            UpdateAllAttributeList();
        }

        public void ClearCurrentItemData()
        {
            _persistentData.CurrentBuild.ItemData = "";
            _itemAttributes = null;
            lbItemAttr.ItemsSource = null;
            UpdateAllAttributeList();
            mnuClearItems.IsEnabled = false;
        }

        #endregion

        #region Builds - Event Handlers

        private void LviMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var lvi = ((ListView) sender).SelectedItem;
            if (lvi == null) return;
            var build = ((PoEBuild) lvi);
            SetCurrentBuild(build);
            BtnLoadBuildClick(this, null); // loading the build
        }

        private void LviMouseLeave(object sender, MouseEventArgs e)
        {
            _noteTip.IsOpen = false;
        }

        private void LviMouseEnter(object sender, MouseEventArgs e)
        {
            var highlightedItem = FindListViewItem(e);
            if (highlightedItem != null)
            {
                var build = (PoEBuild) highlightedItem.Content;
                _noteTip.Content = build.Note == @"" ? @"Right Click To Edit" : build.Note;
                _noteTip.IsOpen = true;
            }
        }

        private void LviMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var selectedBuild = (PoEBuild) lvSavedBuilds.SelectedItem;
            var formBuildName = new FormChooseBuildName(selectedBuild.Name, selectedBuild.Note,
                selectedBuild.CharacterName, selectedBuild.ItemData);
            var showDialog = formBuildName.ShowDialog();
            if (showDialog != null && (bool) showDialog)
            {
                selectedBuild.Name = formBuildName.GetBuildName();
                selectedBuild.Note = formBuildName.GetNote();
                selectedBuild.CharacterName = formBuildName.GetCharacterName();
                selectedBuild.ItemData = formBuildName.GetItemData();
                lvSavedBuilds.Items.Refresh();
            }
            SaveBuildsToFile();
        }

        private ListViewItem FindListViewItem(MouseEventArgs e)
        {
            var visualHitTest = VisualTreeHelper.HitTest(lvSavedBuilds, e.GetPosition(lvSavedBuilds)).VisualHit;

            ListViewItem listViewItem = null;

            while (visualHitTest != null)
            {
                if (visualHitTest is ListViewItem)
                {
                    listViewItem = visualHitTest as ListViewItem;

                    break;
                }
                if (Equals(visualHitTest, lvSavedBuilds))
                {
                    return null;
                }

                visualHitTest = VisualTreeHelper.GetParent(visualHitTest);
            }

            return listViewItem;
        }

        private void BtnSaveNewBuildClick(object sender, RoutedEventArgs e)
        {
            SaveNewBuild();
        }

        private void BtnOverwriteBuildClick(object sender, RoutedEventArgs e)
        {
            if (lvSavedBuilds.SelectedItems.Count > 0)
            {
                var selectedBuild = (PoEBuild) lvSavedBuilds.SelectedItem;
                selectedBuild.Class = cbCharType.Text;
                selectedBuild.CharacterName = _persistentData.CurrentBuild.CharacterName;
                selectedBuild.Level = tbLevel.Text;
                selectedBuild.PointsUsed = tbUsedPoints.Text;
                selectedBuild.Url = tbSkillURL.Text;
                selectedBuild.ItemData = _persistentData.CurrentBuild.ItemData;
                lvSavedBuilds.Items.Refresh();
                SaveBuildsToFile();
            }
            else
            {
                MessageBox.Show("Please select an existing build first.", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnDeleteClick(object sender, RoutedEventArgs e)
        {
            if (lvSavedBuilds.SelectedItems.Count > 0)
            {
                lvSavedBuilds.Items.Remove(lvSavedBuilds.SelectedItem);
                SaveBuildsToFile();
            }
        }

        private void LvSavedBuildsKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) &&
                lvSavedBuilds.SelectedIndex > 0)
            {
                MoveBuildInList(-1);
            }

            else if (e.Key == Key.Down && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) &&
                     lvSavedBuilds.SelectedIndex < lvSavedBuilds.Items.Count - 1)
            {
                MoveBuildInList(1);
            }
        }

        private void MoveBuildInList(int direction)
        {
            var obj = lvSavedBuilds.Items[lvSavedBuilds.SelectedIndex];
            var selectedIndex = lvSavedBuilds.SelectedIndex;
            lvSavedBuilds.Items.RemoveAt(selectedIndex);
            lvSavedBuilds.Items.Insert(selectedIndex + direction, obj);
            lvSavedBuilds.SelectedItem = lvSavedBuilds.Items[selectedIndex + direction];
            lvSavedBuilds.SelectedIndex = selectedIndex + direction;
            lvSavedBuilds.Items.Refresh();

            SaveBuildsToFile();
        }

        #endregion

        #region Builds - Services

        private void SetCurrentBuild(PoEBuild build)
        {
            _persistentData.CurrentBuild = PoEBuild.Copy(build);

            tbSkillURL.Text = build.Url;
            tbLevel.Text = build.Level;
            LoadItemData(build.ItemData);
        }

        private void SaveNewBuild()
        {
            var formBuildName = new FormChooseBuildName(_persistentData.CurrentBuild.CharacterName,
                _persistentData.CurrentBuild.ItemData);
            var showDialog = formBuildName.ShowDialog();
            if (showDialog != null && (bool) showDialog)
            {
                var newBuild = new PoEBuild
                               {
                                   Name = formBuildName.GetBuildName(),
                                   Level = tbLevel.Text,
                                   Class = cbCharType.Text,
                                   PointsUsed = tbUsedPoints.Text,
                                   Url = tbSkillURL.Text,
                                   Note = formBuildName.GetNote(),
                                   CharacterName = formBuildName.GetCharacterName(),
                                   ItemData = formBuildName.GetItemData()
                               };
                SetCurrentBuild(newBuild);
                lvSavedBuilds.Items.Add(newBuild);
            }

            if (lvSavedBuilds.Items.Count > 0)
            {
                SaveBuildsToFile();
            }
        }

        private void SaveBuildsToFile()
        {
            _persistentData.SaveBuilds(lvSavedBuilds.Items);
        }

        private void LoadBuildFromUrl()
        {
            try
            {
                if (tbSkillURL.Text.Contains("poezone.ru"))
                {
                    SkillTreeImporter.LoadBuildFromPoezone(Tree, tbSkillURL.Text);
                    tbSkillURL.Text = Tree.SaveToURL();
                }
                else if (tbSkillURL.Text.Contains("poebuilder.com/"))
                {
                    const string poebuilderTree = "https://poebuilder.com/character/";
                    const string poebuilderTreeWww = "https://www.poebuilder.com/character/";
                    const string poebuilderTreeOwww = "http://www.poebuilder.com/character/";
                    const string poebuilderTreeO = "http://poebuilder.com/character/";
                    var urlString = tbSkillURL.Text;
                    urlString = urlString.Replace(poebuilderTree, TreeAddress);
                    urlString = urlString.Replace(poebuilderTreeO, TreeAddress);
                    urlString = urlString.Replace(poebuilderTreeWww, TreeAddress);
                    urlString = urlString.Replace(poebuilderTreeOwww, TreeAddress);
                    tbSkillURL.Text = urlString;
                    Tree.LoadFromURL(urlString);
                }
                else if (tbSkillURL.Text.Contains("tinyurl.com/"))
                {
                    var request = (HttpWebRequest) WebRequest.Create(tbSkillURL.Text);
                    request.AllowAutoRedirect = false;
                    var response = (HttpWebResponse) request.GetResponse();
                    var redirUrl = response.Headers["Location"];
                    tbSkillURL.Text = redirUrl;
                    LoadBuildFromUrl();
                }
                else if (tbSkillURL.Text.Contains("poeurl.com/"))
                {
                    tbSkillURL.Text = tbSkillURL.Text.Replace("http://poeurl.com/",
                        "http://poeurl.com/redirect.php?url=");
                    var request = (HttpWebRequest) WebRequest.Create(tbSkillURL.Text);
                    request.AllowAutoRedirect = false;
                    var response = (HttpWebResponse) request.GetResponse();
                    var redirUrl = response.Headers["Location"];
                    tbSkillURL.Text = redirUrl;
                    LoadBuildFromUrl();
                }
                else
                    Tree.LoadFromURL(tbSkillURL.Text);

                _justLoaded = true;
                //cleans the default tree on load if 2
                if (_justLoaded)
                {
                    if (_undoList.Count > 1)
                    {
                        var holder = _undoList.Pop();
                        _undoList.Pop();
                        _undoList.Push(holder);
                    }
                }
                cbCharType.SelectedIndex = Tree.Chartype;
                UpdateAllAttributeList();
                _justLoaded = false;
            }
            catch (Exception)
            {
                MessageBox.Show("The Build you tried to load, is invalid");
            }
        }

        #endregion

        #region Builds - DragAndDrop

        private void ListViewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragAndDropStartPoint = e.GetPosition(lvSavedBuilds);
        }

        private void ListViewPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var position = e.GetPosition(lvSavedBuilds);

                if (Math.Abs(position.X - _dragAndDropStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(position.Y - _dragAndDropStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    BeginDrag(e);
                }
            }
        }

        private void BeginDrag(MouseEventArgs e)
        {
            var listView = lvSavedBuilds;
            var listViewItem = FindAnchestor<ListViewItem>((DependencyObject) e.OriginalSource);

            if (listViewItem == null)
                return;

            // get the data for the ListViewItem
            var item = listView.ItemContainerGenerator.ItemFromContainer(listViewItem);

            //setup the drag adorner.
            InitialiseAdorner(listViewItem);

            //add handles to update the adorner.
            listView.PreviewDragOver += ListViewDragOver;
            listView.DragLeave += ListViewDragLeave;
            listView.DragEnter += ListViewDragEnter;

            var data = new DataObject("myFormat", item);
            DragDrop.DoDragDrop(lvSavedBuilds, data, DragDropEffects.Move);

            //cleanup 
            listView.PreviewDragOver -= ListViewDragOver;
            listView.DragLeave -= ListViewDragLeave;
            listView.DragEnter -= ListViewDragEnter;

            if (_adorner != null)
            {
                AdornerLayer.GetAdornerLayer(listView).Remove(_adorner);
                _adorner = null;
                SaveBuildsToFile();
            }
        }

        private void ListViewDragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("myFormat") ||
                sender == e.Source)
            {
                e.Effects = DragDropEffects.None;
            }
        }


        private void ListViewDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("myFormat"))
            {
                var name = e.Data.GetData("myFormat");
                var listView = lvSavedBuilds;
                var listViewItem = FindAnchestor<ListViewItem>((DependencyObject) e.OriginalSource);

                if (listViewItem != null)
                {
                    var itemToReplace = listView.ItemContainerGenerator.ItemFromContainer(listViewItem);
                    var index = listView.Items.IndexOf(itemToReplace);

                    if (index >= 0)
                    {
                        listView.Items.Remove(name);
                        listView.Items.Insert(index, name);
                    }
                }
                else
                {
                    listView.Items.Remove(name);
                    listView.Items.Add(name);
                }
            }
        }

        private void InitialiseAdorner(UIElement listViewItem)
        {
            var brush = new VisualBrush(listViewItem);
            _adorner = new DragAdorner(listViewItem, listViewItem.RenderSize, brush) {Opacity = 0.5};
            _layer = AdornerLayer.GetAdornerLayer(lvSavedBuilds);
            _layer.Add(_adorner);
        }

        private void ListViewDragLeave(object sender, DragEventArgs e)
        {
            if (Equals(e.OriginalSource, lvSavedBuilds))
            {
                var p = e.GetPosition(lvSavedBuilds);
                var r = VisualTreeHelper.GetContentBounds(lvSavedBuilds);
                if (!r.Contains(p))
                {
                    e.Handled = true;
                }
            }
        }

        private void ListViewDragOver(object sender, DragEventArgs args)
        {
            if (_adorner != null)
            {
                _adorner.OffsetLeft = args.GetPosition(lvSavedBuilds).X - _dragAndDropStartPoint.X;
                _adorner.OffsetTop = args.GetPosition(lvSavedBuilds).Y - _dragAndDropStartPoint.Y;
            }
        }

        // Helper to search up the VisualTree
        private static T FindAnchestor<T>(DependencyObject current)
            where T : DependencyObject
        {
            do
            {
                var anchestor = current as T;
                if (anchestor != null)
                {
                    return anchestor;
                }
                current = VisualTreeHelper.GetParent(current);
            } while (current != null);
            return null;
        }

        #endregion

        #region Bottom Bar (Build URL etc)

        private void TbSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            SearchUpdate();
        }

        private void CbRegExClick(object sender, RoutedEventArgs e)
        {
            SearchUpdate();
        }

        private void SearchUpdate()
        {
            Tree.HighlightNodes(tbSearch.Text, cbRegEx.IsChecked != null && cbRegEx.IsChecked.Value);
        }

        private void TbSkillUrlKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                LoadBuildFromUrl();
        }

        private void TbSkillUrlMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            tbSkillURL.SelectAll();
        }

        private void TbSkillUrlTextChanged(object sender, TextChangedEventArgs e)
        {
            _undoList.Push(tbSkillURL.Text);
        }

        private void TbSkillUrlUndoClick(object sender, RoutedEventArgs e)
        {
            TbSkillUrlUndo();
        }

        private void TbSkillUrlUndo()
        {
            if (_undoList.Count <= 0) return;
            if (_undoList.Peek() == tbSkillURL.Text && _undoList.Count > 1)
            {
                _undoList.Pop();
                TbSkillUrlUndo();
            }
            else if (_undoList.Peek() != tbSkillURL.Text)
            {
                _redoList.Push(tbSkillURL.Text);
                tbSkillURL.Text = _undoList.Pop();
                Tree.LoadFromURL(tbSkillURL.Text);
                tbUsedPoints.Text = "" + (Tree.SkilledNodes.Count - 1);
            }
        }

        private void TbSkillUrlRedoClick(object sender, RoutedEventArgs e)
        {
            TbSkillUrlRedo();
        }

        private void TbSkillUrlRedo()
        {
            if (_redoList.Count <= 0) return;
            if (_redoList.Peek() == tbSkillURL.Text && _redoList.Count > 1)
            {
                _redoList.Pop();
                TbSkillUrlRedo();
            }
            else if (_redoList.Peek() != tbSkillURL.Text)
            {
                tbSkillURL.Text = _redoList.Pop();
                Tree.LoadFromURL(tbSkillURL.Text);
                tbUsedPoints.Text = "" + (Tree.SkilledNodes.Count - 1);
            }
        }

        private void BtnLoadBuildClick(object sender, RoutedEventArgs e)
        {
            LoadBuildFromUrl();
        }

        private void BtnPoeUrlClick(object sender, RoutedEventArgs e)
        {
            StartDownloadPoeUrl();
        }

        private void StartDownloadPoeUrl()
        {
            var regx =
                new Regex(
                    "http://([\\w+?\\.\\w+])+([a-zA-Z0-9\\~\\!\\@\\#\\$\\%\\^\\&amp;\\*\\(\\)_\\-\\=\\+\\\\\\/\\?\\.\\:\\;\\'\\,]*)?",
                    RegexOptions.IgnoreCase);

            var matches = regx.Matches(tbSkillURL.Text);

            if (matches.Count == 1)
            {
                try
                {
                    var url = matches[0].ToString();
                    if (url.Length <= 12)
                    {
                        ShowPoeUrlMessageAndAddToClipboard(url);
                    }
                    if (!url.ToLower().StartsWith("http") && !url.ToLower().StartsWith("ftp"))
                    {
                        url = "http://" + url;
                    }
                    var client = new WebClient();
                    client.DownloadStringCompleted += DownloadCompletedPoeUrl;
                    client.DownloadStringAsync(new Uri("http://poeurl.com/shrink.php?url=" + url));
                }
                catch (Exception)
                {
                    MessageBox.Show("Failed to create PoEURL", "poeurl error", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private static void DownloadCompletedPoeUrl(Object sender, DownloadStringCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                MessageBox.Show("Failed to create PoEURL", "poeurl error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            ShowPoeUrlMessageAndAddToClipboard("http://poeurl.com/" + e.Result.Trim());
        }

        private static void ShowPoeUrlMessageAndAddToClipboard(string poeurl)
        {
            System.Windows.Forms.Clipboard.SetDataObject(poeurl, true);
            MessageBox.Show("The URL below has been copied to you clipboard: \n" + poeurl, "poeurl Link");
        }

        #endregion

        #region Theme

        private void MnuSetThemeClick(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            SetTheme(menuItem.Header as string);
        }

        private void SetTheme(string sTheme)
        {
            var accent = ThemeManager.Accents.First(x => Equals(x.Name, _persistentData.Options.Accent));
            var theme = ThemeManager.GetAppTheme("Base" + sTheme);
            ThemeManager.ChangeAppStyle(Application.Current, accent, theme);
            ((MenuItem) NameScope.GetNameScope(this).FindName("mnuViewTheme" + sTheme)).IsChecked = true;
            _persistentData.Options.Theme = sTheme;
        }

        private void MnuSetAccentClick(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            SetAccent(menuItem.Header as string);
        }

        private void SetAccent(string sAccent)
        {
            var accent = ThemeManager.Accents.First(x => Equals(x.Name, sAccent));
            var theme = ThemeManager.GetAppTheme("Base" + _persistentData.Options.Theme);
            ThemeManager.ChangeAppStyle(Application.Current, accent, theme);
            ((MenuItem) NameScope.GetNameScope(this).FindName("mnuViewAccent" + sAccent)).IsChecked = true;
            _persistentData.Options.Accent = sAccent;
        }

        #endregion

        #region Change Class - No Reset

        /**
         * Will get the current class name and start string from the tree url
         * return: array[]
         *         index 0 containing the Class Name
         *         index 1 containing the Class Start String
         **/

        private string[] GetCurrentClass()
        {
            if (tbSkillURL.Text.IndexOf("AAAAAgAA", StringComparison.Ordinal) != -1)
            {
                return getAnyClass("Scion");
            }
            if (tbSkillURL.Text.IndexOf("AAAAAgEA", StringComparison.Ordinal) != -1)
            {
                return getAnyClass("Marauder");
            }
            if (tbSkillURL.Text.IndexOf("AAAAAgIA", StringComparison.Ordinal) != -1)
            {
                return getAnyClass("Ranger");
            }
            if (tbSkillURL.Text.IndexOf("AAAAAgMA", StringComparison.Ordinal) != -1)
            {
                return getAnyClass("Witch");
            }
            if (tbSkillURL.Text.IndexOf("AAAAAgQA", StringComparison.Ordinal) != -1)
            {
                return getAnyClass("Duelist");
            }
            if (tbSkillURL.Text.IndexOf("AAAAAgUA", StringComparison.Ordinal) != -1)
            {
                return getAnyClass("Templar");
            }
            if (tbSkillURL.Text.IndexOf("AAAAAgYA", StringComparison.Ordinal) != -1)
            {
                return getAnyClass("Shadow");
            }
            return getAnyClass("ERROR");
        }

        /**
         * parameters: className - any valid class name string
         * return: array[]
         *         index 0 containing the Class Name
         *         index 1 containing the Class Start String
         **/

        private string[] getAnyClass(string className)
        {
            var array = new string[2];
            if (className == "Scion")
            {
                array[0] = "Scion";
                array[1] = "AAAAAgAA";
                return array;
            }
            if (className == "Marauder")
            {
                array[0] = "Marauder";
                array[1] = "AAAAAgEA";
                return array;
            }
            if (className == "Ranger")
            {
                array[0] = "Ranger";
                array[1] = "AAAAAgIA";
                return array;
            }
            if (className == "Witch")
            {
                array[0] = "Witch";
                array[1] = "AAAAAgMA";
                return array;
            }
            if (className == "Duelist")
            {
                array[0] = "Duelist";
                array[1] = "AAAAAgQA";
                return array;
            }
            if (className == "Templar")
            {
                array[0] = "Templar";
                array[1] = "AAAAAgUA";
                return array;
            }
            if (className == "Shadow")
            {
                array[0] = "Shadow";
                array[1] = "AAAAAgYA";
                return array;
            }
            array[0] = "ERROR";
            array[1] = "ERROR";
            return array;
        }

        #endregion

        #region Legacy

        /// <summary>
        ///     Import builds from legacy build save file "savedBuilds" to PersistentData.xml.
        ///     Warning: This will remove the "savedBuilds"
        /// </summary>
        private void ImportLegacySavedBuilds()
        {
            try
            {
                if (File.Exists("savedBuilds"))
                {
                    var savedBuilds = new List<PoEBuild>();
                    var builds = File.ReadAllText("savedBuilds").Split('\n');
                    foreach (var b in builds)
                    {
                        var description = b.Split(';')[0].Split('|')[1];
                        var poeClass = description.Split(',')[0].Trim();
                        var pointsUsed = description.Split(',')[1].Trim().Split(' ')[0].Trim();

                        if (HasBuildNote(b))
                        {
                            savedBuilds.Add(new PoEBuild(b.Split(';')[0].Split('|')[0], poeClass, pointsUsed,
                                b.Split(';')[1].Split('|')[0], b.Split(';')[1].Split('|')[1]));
                        }
                        else
                        {
                            savedBuilds.Add(new PoEBuild(b.Split(';')[0].Split('|')[0], poeClass, pointsUsed,
                                b.Split(';')[1], ""));
                        }
                    }
                    lvSavedBuilds.Items.Clear();
                    foreach (var lvi in savedBuilds)
                    {
                        lvSavedBuilds.Items.Add(lvi);
                    }
                    File.Move("savedBuilds", "savedBuilds.old");
                    SaveBuildsToFile();
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Unable to load the saved builds.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool HasBuildNote(string b)
        {
            var buildNoteTest = b.Split(';')[1].Split('|');
            return buildNoteTest.Length > 1;
        }

        #endregion
    }
}