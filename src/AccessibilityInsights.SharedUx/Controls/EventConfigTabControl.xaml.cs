// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using AccessibilityInsights.SharedUx.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Axe.Windows.Core.Bases;
using AccessibilityInsights.SharedUx.Dialogs;
using AccessibilityInsights.SharedUx.Settings;
using System.Windows.Automation.Peers;
using System.Windows.Automation;
using Axe.Windows.Desktop.Settings;
using Axe.Windows.Desktop.Types;
using Axe.Windows.Desktop.Utility;
using AccessibilityInsights.SharedUx.Enums;
using System.Windows.Input;
using AccessibilityInsights.SharedUx.Controls.CustomControls;
using System.Globalization;

namespace AccessibilityInsights.SharedUx.Controls
{
    /// <summary>
    /// Interaction logic for EventConfigTabControl.xaml
    /// </summary>
    public partial class EventConfigTabControl : UserControl
    {
        /// <summary>
        /// List of root nodes in treeview
        /// </summary>
        List<EventConfigNodeViewModel> RootNodes;

        /// <summary>
        /// Root node for suggested events
        /// </summary>
        EventConfigNodeViewModel SuggestedNode;

        /// <summary>
        /// Root node for custom events
        /// </summary>
        EventConfigNodeViewModel CustomNode;

        /// <summary>
        /// Node with edit button
        /// </summary>
        EventConfigNodeViewModel EditBtnNode;

        /// <summary>
        /// Root node for properties under custom events
        /// </summary>
        EventConfigNodeViewModel CustomPropertiesNode;

        /// <summary>
        /// Constructor
        /// </summary>
        public EventConfigTabControl()
        {
            InitializeComponent();
            var help = string.Format(CultureInfo.InvariantCulture, Properties.Resources.EventConfigTabControl_EventConfigTabControl_Select_events_to_listen_to_then_start_recording_with__0__or_by_pressing_the_record_button, ConfigurationManager.GetDefaultInstance().AppConfig.HotKeyForRecord);
            this.trviewConfigEvents.SetValue(AutomationProperties.HelpTextProperty, help);
        }

        /// <summary>
        /// Populate treeview based on selected element
        /// </summary>
        /// <param name="el"></param>
        public void SetElement(A11yElement el)
        {
            this.tbElement.Text = el.Glimpse;

            RootNodes = new List<EventConfigNodeViewModel>();
            trviewConfigEvents.ItemsSource = RootNodes;
            var ids = SupportedEvents.GetEventsForControl(el.ControlTypeId, el.Patterns);
            SuggestedNode = new EventConfigNodeViewModel(string.Format(CultureInfo.InvariantCulture, Properties.Resources.EventConfigTabControl_SetElement_Expected_Events_based_on_the_0_control_type, Axe.Windows.Core.Types.ControlType.GetInstance().GetNameById(el.ControlTypeId))) { IsExpanded = true };

            if (ids.Any())
            {
                SuggestedNode.AddChildren(ids, EventConfigNodeType.Event, true);
                SuggestedNode.SortChildren();
            }

            var properties = new EventConfigNodeViewModel("Properties") { Depth = 1 };
            properties.AddChildren(el.Properties.Values);
            
            if (properties.Children.Any())
            {
                properties.SortChildren();
                SuggestedNode.AddChild(properties);
            }

            if (SuggestedNode.Children.Any())
            {
                RootNodes.Add(SuggestedNode);
            }

            CustomNode = new EventConfigNodeViewModel("My Events");

            CustomPropertiesNode = new EventConfigNodeViewModel("Properties") { Depth = 1 };
            EditBtnNode = new EventConfigNodeViewModel("", Visibility.Visible, Properties.Resources.EventConfigTabControl_SetElement_Edit_My_Events) { Depth = 1, TextVisibility = Visibility.Collapsed };

            UpdateCustomNode();
            RootNodes.Add(CustomNode);

            trviewConfigEvents.Items.Refresh();
            trviewConfigEvents.UpdateLayout();
        }

        /// <summary>
        /// Call control a pane
        /// </summary>
        /// <returns></returns>
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new CustomControlOverridingAutomationPeer(this, "pane");
        }

        /// <summary>
        /// Update the custom events node based on user's selections
        /// </summary>
        private void UpdateCustomNode()
        {
            CustomNode.Children.Remove(CustomPropertiesNode);
            CustomNode.Children.Remove(EditBtnNode);
            var custom = (from e in ConfigurationManager.GetDefaultInstance().EventConfig.Events
                          where e.IsRecorded
                          select e.Id).ToList();

            CustomNode.Children.Where(c => c.Type != EventConfigNodeType.Group && !custom.Contains(c.Id)).ToList().ForEach(c => CustomNode.RemoveChild(c));

            var add = custom.Where(id => !CustomNode.Children.Select(c => c.Id).Contains(id)).ToList();
            CustomNode.AddChildren(add, EventConfigNodeType.Event);
            CustomNode.SortChildren();
            CustomNode.Children.Insert(0, EditBtnNode);

            custom = (from e in ConfigurationManager.GetDefaultInstance().EventConfig.Properties
                          where e.IsRecorded
                          select e.Id).ToList();

            CustomPropertiesNode.Children.Where(c => c.Type != EventConfigNodeType.Group && !custom.Contains(c.Id)).ToList().ForEach(c => CustomPropertiesNode.RemoveChild(c));

            add = custom.Where(id => !CustomPropertiesNode.Children.Select(c => c.Id).Contains(id)).ToList();
            CustomPropertiesNode.AddChildren(add, EventConfigNodeType.Property);
            CustomPropertiesNode.SortChildren();
         
            if (CustomPropertiesNode.Children.Count > 0)
            {
                CustomNode.Children.Add(CustomPropertiesNode);
            }
        }

        /// <summary>
        /// Handles click on event config button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnConfig_Click(object sender, RoutedEventArgs e)
        {
            OpenConfigDialog();
        }

        /// <summary>
        /// Updates the focus changed checkbox based on current config setting
        /// </summary>
        public void UpdateGlobalFocusEventCheckbox()
        {
            var node = SuggestedNode?.Children.Where(c => c.Id == EventType.UIA_AutomationFocusChangedEventId).FirstOrDefault();
            if (node != null)
            {
                node.IsChecked = ConfigurationManager.GetDefaultInstance().EventConfig.IsListeningFocusChangedEvent;
            }
        }

        /// <summary>
        /// Opens event config dialog
        /// </summary>
        private void OpenConfigDialog()
        {
            var window = new EventConfigWindow();
            window.Owner = Application.Current.MainWindow;

            window.ShowDialog();

            if (window.DialogResult.HasValue && window.DialogResult.Value)
            {
                ConfigurationManager.GetDefaultInstance().SaveEventsConfiguration();
                UpdateCustomNode();
                UpdateGlobalFocusEventCheckbox();
                CustomNode.IsChecked = true;
            }
            else
            {
                ConfigurationManager.GetDefaultInstance().PopulateEventConfiguration();
            }
        }

        /// <summary>
        /// Create a property bag based on RecorderSetting changes.
        /// </summary>
        /// <param name="cfg"></param>
        /// <returns></returns>
        private static IDictionary<string, string> GetPropertyBag(RecorderSetting cfg)
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();

            dic.Add("Scope", cfg.ListenScope.ToString());

            if (cfg.IsListeningFocusChangedEvent)
            {
                dic.Add(EventType.UIA_AutomationFocusChangedEventId.ToString(CultureInfo.InvariantCulture), EventType.GetInstance().GetNameById(EventType.UIA_AutomationFocusChangedEventId));
            }

            foreach (var e in cfg.Events)
            {
                if (e.IsRecorded)
                {
                    dic.Add(e.Id.ToString(CultureInfo.InvariantCulture), e.Name);
                }
            }

            foreach (var p in cfg.Properties)
            {
                if (p.IsRecorded)
                {
                    dic.Add(p.Id.ToString(CultureInfo.InvariantCulture), p.Name);
                }
            }

            return dic;
        }

        /// <summary>
        /// Disable changing config while recording
        /// </summary>
        /// <param name="isRecording"></param>
        public void SetEditEnabled(bool isRecording)
        {
            this.RootNodes?.ForEach(n => n.IsEditEnabled = !isRecording);
        }

        /// <summary>
        /// Custom keyboard nav behavior
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TreeViewItem_KeyDown(object sender, KeyEventArgs e)
        {
            var evm = (sender as TreeViewItem).DataContext as EventConfigNodeViewModel;

            if (e.Key == Key.Space)
            {
                if (evm.IsEditEnabled)
                {
                    evm.IsChecked = !evm.IsChecked;
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (evm.IsEditEnabled && evm.ButtonVisibility == Visibility.Visible)
                {
                    OpenConfigDialog();
                }
                e.Handled = true;
            }
        }

        public void Clear()
        {
            this.CustomNode = null;
            this.CustomPropertiesNode = null;
            this.SuggestedNode = null;
            this.RootNodes?.Clear();
            this.trviewConfigEvents.ItemsSource = null;
            this.RootNodes = null;
            ConfigurationManager.GetDefaultInstance().EventConfig.Events.ForEach(e => e.CheckedCount = 0);
            ConfigurationManager.GetDefaultInstance().EventConfig.Properties.ForEach(e => e.CheckedCount = 0);
            this.ckbxAllEvents.IsChecked = false;
            ConfigurationManager.GetDefaultInstance().EventConfig.IsListeningAllEvents = false;
            this.tbElement.Text = "";
        }

        /// <summary>
        /// Focus on treeview
        /// </summary>
        public new void Focus()
        {
            this.trviewConfigEvents.Focus();
        }

        /// <summary>
        /// Listen to all events
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ckbxAllEvents_Checked(object sender, RoutedEventArgs e)
        {
            ConfigurationManager.GetDefaultInstance().EventConfig.IsListeningAllEvents = true;
        }

        /// <summary>
        /// Don't listen to all events
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ckbxAllEvents_Unchecked(object sender, RoutedEventArgs e)
        {
            ConfigurationManager.GetDefaultInstance().EventConfig.IsListeningAllEvents = false;
        }
    }
}
