﻿using PEBakery.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PEBakery.WPF
{
    /// <summary>
    /// LogWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class LogWindow : Window
    {
        private LogViewModel model;

        public LogWindow()
        {
            InitializeComponent();
            this.model = this.DataContext as LogViewModel;            

            model.Logger.SystemLogUpdated += SystemLogUpdateEventHandler;
            model.Logger.BuildInfoUpdated += BuildInfoUpdateEventHandler;
            model.Logger.PluginUpdated += PluginUpdateEventHandler;
            model.Logger.BuildLogUpdated += BuildLogUpdateEventHandler;
            model.Logger.VariableUpdated += VariableUpdateEventHandler;

            SystemLogListView.UpdateLayout();
            SystemLogListView.ScrollIntoView(SystemLogListView.Items[SystemLogListView.Items.Count - 1]);
        }

       ~LogWindow()
        {
            model.Logger.SystemLogUpdated -= SystemLogUpdateEventHandler;
            model.Logger.BuildInfoUpdated -= BuildInfoUpdateEventHandler;
            model.Logger.PluginUpdated -= PluginUpdateEventHandler;
            model.Logger.BuildLogUpdated -= BuildLogUpdateEventHandler;
            model.Logger.VariableUpdated -= VariableUpdateEventHandler;
        }

        #region EventHandler
        public void SystemLogUpdateEventHandler(object sender, SystemLogUpdateEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                model.SystemLogListModel.Add(e.Log);
                model.SystemLogListSelectedIndex = model.SystemLogListModel.Count - 1;
                SystemLogListView.UpdateLayout();
                SystemLogListView.ScrollIntoView(SystemLogListView.Items[model.SystemLogListSelectedIndex]);
            });
            model.OnPropertyUpdate("SystemLogListModel");
        }

        public void BuildInfoUpdateEventHandler(object sender, BuildInfoUpdateEventArgs e)
        {
            model.RefreshBuildLog();
        }

        public void BuildLogUpdateEventHandler(object sender, BuildLogUpdateEventArgs e)
        {
            if (model.SelectBuildEntries != null &&
                model.SelectBuildEntries[model.SelectBuildIndex].Item2 == e.Log.BuildId)
            {
                Application.Current.Dispatcher.Invoke(() => 
                {
                    model.BuildLogListModel.Add(e.Log);
                    model.OnPropertyUpdate("BuildLogListModel");

                    if (0 < BuildLogSimpleListView.Items.Count)
                    {
                        BuildLogSimpleListView.UpdateLayout();
                        BuildLogSimpleListView.ScrollIntoView(BuildLogSimpleListView.Items[BuildLogSimpleListView.Items.Count - 1]);
                    }
                    
                    if (0 < BuildLogDetailListView.Items.Count)
                    {
                        BuildLogDetailListView.UpdateLayout();
                        BuildLogDetailListView.ScrollIntoView(BuildLogDetailListView.Items[BuildLogDetailListView.Items.Count - 1]);
                    }
                });
            }
        }

        public void PluginUpdateEventHandler(object sender, PluginUpdateEventArgs e)
        {
            model.RefreshPlugin(e.Log.BuildId);
        }

        public void VariableUpdateEventHandler(object sender, VariableUpdateEventArgs e)
        {
            if (model.SelectBuildEntries != null &&
                model.SelectBuildEntries[model.SelectBuildIndex].Item2 == e.Log.BuildId)
            {
                if (e.Log.Type != VarsType.Local)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        model.VariableListModel.Add(e.Log);
                        model.OnPropertyUpdate("VariableListModel");
                    });
                }
            }
        }
        #endregion

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            int idx = MainTab.SelectedIndex;
            switch (idx)
            {
                case 0: // System Log 
                    model.RefreshSystemLog();
                    break;
                case 1: // Build Log
                    model.RefreshBuildLog();
                    break;
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            int idx = MainTab.SelectedIndex;
            switch (idx)
            {
                case 0: // System Log 
                    model.LogDB.DeleteAll<DB_SystemLog>();
                    model.RefreshSystemLog();
                    break;
                case 1: // Build Log
                    model.LogDB.DeleteAll<DB_BuildInfo>();
                    model.LogDB.DeleteAll<DB_BuildLog>();
                    model.LogDB.DeleteAll<DB_Plugin>();
                    model.LogDB.DeleteAll<DB_Variable>();
                    model.RefreshBuildLog();
                    break;
            }
        }
    }

    #region LogListModel
    public class SystemLogListModel : ObservableCollection<DB_SystemLog> { }
    public class PluginListModel : ObservableCollection<DB_Plugin> { }
    public class VariableListModel : ObservableCollection<DB_Variable> { }
    public class BuildLogListModel : ObservableCollection<DB_BuildLog> { }
    #endregion

    #region LogViewModel
    public class LogViewModel : INotifyPropertyChanged
    {
        public Logger Logger { get; set; }
        public LogDB LogDB { get => Logger.DB; }

        public LogViewModel()
        {
            MainWindow w = Application.Current.MainWindow as MainWindow;
            Logger = w.Logger;

            RefreshSystemLog();
            RefreshBuildLog();
        }

        ~LogViewModel()
        {
            
        }

        #region Refresh 
        public void RefreshSystemLog()
        {
            SystemLogListModel list = new SystemLogListModel();
            foreach (DB_SystemLog log in LogDB.Table<DB_SystemLog>())
                list.Add(log);
            SystemLogListModel = list;

            SystemLogListSelectedIndex = SystemLogListModel.Count - 1;
        }

        public void RefreshBuildLog()
        {
            // Populate SelectBuildEntries
            List<Tuple<string, long>> list = new List<Tuple<string, long>>();
            foreach (DB_BuildInfo b in LogDB.Table<DB_BuildInfo>().OrderByDescending(x => x.StartTime))
            {
                string timeStr = b.StartTime.ToLocalTime().ToString("yyyy-MM-dd hh:mm:ss tt", CultureInfo.InvariantCulture);
                list.Add(new Tuple<string, long>($"[{timeStr}] {b.Name} ({b.Id})", b.Id));
            }
            SelectBuildEntries = list;
            SelectBuildIndex = 0;
        }

        public void RefreshPlugin(long? buildId)
        {
            if (buildId == null) // Clear
            {
                SelectPluginEntries = new List<Tuple<string, long, long>>();
                SelectPluginIndex = 0;
            }
            else
            {
                // Populate SelectPluginEntries
                List<Tuple<string, long, long>> list = new List<Tuple<string, long, long>>();
                var plugins = LogDB.Table<DB_Plugin>().Where(x => x.BuildId == buildId).OrderBy(x => x.Order).ToArray();
                foreach (DB_Plugin p in plugins)
                    list.Add(new Tuple<string, long, long>($"[{p.Order}/{plugins.Length}] {p.Name} ({p.Path})", p.Id, (long) buildId));
                SelectPluginEntries = list;
                SelectPluginIndex = 0;
            }
        }
        #endregion

        #region SystemLog
        private int systemLogListSelectedIndex;
        public int SystemLogListSelectedIndex
        {
            get => systemLogListSelectedIndex;
            set
            {
                systemLogListSelectedIndex = value;
                OnPropertyUpdate("SystemLogListSelectedIndex");
            }
        }

        private SystemLogListModel systemLogListModel = new SystemLogListModel();
        public SystemLogListModel SystemLogListModel
        {
            get => systemLogListModel;
            set
            {
                systemLogListModel = value;
                OnPropertyUpdate("SystemLogListModel");
            }
        }
        #endregion

        #region BuildLog
        private int selectBuildIndex;
        public int SelectBuildIndex
        {
            get => selectBuildIndex;
            set
            {
                selectBuildIndex = value;

                if (0 < selectBuildEntries.Count)
                {
                    long buildId = selectBuildEntries[value].Item2;

                    RefreshPlugin(SelectBuildEntries[value].Item2);

                    VariableListModel variableListModel = new VariableListModel();
                    foreach (DB_Variable v in LogDB.Table<DB_Variable>().Where(x => x.BuildId == buildId))
                        variableListModel.Add(v);
                    VariableListModel = variableListModel;
                }
                else
                {
                    RefreshPlugin(null);
                    VariableListModel = new VariableListModel();
                }

                OnPropertyUpdate("SelectBuildIndex");
            }
        }

        private List<Tuple<string, long>> selectBuildEntries;
        public List<Tuple<string, long>> SelectBuildEntries
        {
            get => selectBuildEntries;
            set
            {
                selectBuildEntries = value;
                OnPropertyUpdate("SelectBuildEntries");
            }
        }

        private int selectPluginIndex;
        public int SelectPluginIndex
        {
            get => selectPluginIndex;
            set
            {
                selectPluginIndex = value;
                if (value != -1 && 0 < selectPluginEntries.Count)
                {
                    long pluginId = selectPluginEntries[value].Item2;
                    long buildId = selectPluginEntries[value].Item3;

                    BuildLogListModel buildLogListModel = new BuildLogListModel();
                    foreach (DB_BuildLog b in LogDB.Table<DB_BuildLog>().Where(x => x.BuildId == buildId && x.PluginId == pluginId))
                        buildLogListModel.Add(b);
                    BuildLogListModel = buildLogListModel;
                }
                else
                {
                    BuildLogListModel = new BuildLogListModel();
                }

                OnPropertyUpdate("SelectPluginIndex");
            }
        }

        private List<Tuple<string, long, long>> selectPluginEntries; // Plugin Name, Plugin Id, Build Id
        public List<Tuple<string, long, long>> SelectPluginEntries
        {
            get => selectPluginEntries;
            set
            {
                selectPluginEntries = value;
                OnPropertyUpdate("SelectPluginEntries");
            }
        }

        private BuildLogListModel buildLogListModel;
        public BuildLogListModel BuildLogListModel
        {
            get => buildLogListModel;
            set
            {
                buildLogListModel = value;
                OnPropertyUpdate("BuildLogListModel");
            }
        }

        private int buildLogSimpleSelectedIndex;
        public int BuildLogSimpleSelectedIndex
        {
            get => buildLogSimpleSelectedIndex;
            set
            {
                buildLogSimpleSelectedIndex = value;
                OnPropertyUpdate("BuildLogListSimpleSelectedIndex");
            }
        }

        private int buildLogDetailSelectedIndex;
        public int BuildLogDetailSelectedIndex
        {
            get => buildLogDetailSelectedIndex;
            set
            {
                buildLogDetailSelectedIndex = value;
                OnPropertyUpdate("BuildLogListDetailSelectedIndex");
            }
        }

        private VariableListModel variableListModel;
        public VariableListModel VariableListModel
        {
            get => variableListModel;
            set
            {
                variableListModel = value;
                OnPropertyUpdate("VariableListModel");
            }
        }

        #endregion

        #region Utility
        private void ResizeGridViewColumn(GridViewColumn column)
        {
            if (double.IsNaN(column.Width))
                column.Width = column.ActualWidth;
            column.Width = double.NaN;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyUpdate(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
    #endregion
}