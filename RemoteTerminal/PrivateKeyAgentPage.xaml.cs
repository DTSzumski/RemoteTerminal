﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using RemoteTerminal.Model;
using Renci.SshNet;
using Renci.SshNet.Common;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using RemoteTerminal.Common;

// The Items Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234233

namespace RemoteTerminal
{
    /// <summary>
    /// A page that displays a collection of item previews.  In the Split Application this page
    /// is used to display and select one of the available groups.
    /// </summary>
    public sealed partial class PrivateKeyAgentPage : Page
    {
        private NavigationHelper navigationHelper;
        private ObservableDictionary defaultViewModel = new ObservableDictionary();

        /// <summary>
        /// This can be changed to a strongly typed view model.
        /// </summary>
        public ObservableDictionary DefaultViewModel
        {
            get { return this.defaultViewModel; }
        }

        /// <summary>
        /// NavigationHelper is used on each page to aid in navigation and 
        /// process lifetime management
        /// </summary>
        public NavigationHelper NavigationHelper
        {
            get { return this.navigationHelper; }
        }

        public PrivateKeyAgentPage()
        {
            this.InitializeComponent();
            this.navigationHelper = new NavigationHelper(this);
            this.navigationHelper.LoadState += this.navigationHelper_LoadState;
        }

        private ObservableCollection<PrivateKeyAgentKey> AgentKeys { get; set; }

        /// <summary>
        /// Populates the page with content passed during navigation. Any saved state is also
        /// provided when recreating a page from a prior session.
        /// </summary>
        /// <param name="sender">
        /// The source of the event; typically <see cref="NavigationHelper"/>
        /// </param>
        /// <param name="e">Event data that provides both the navigation parameter passed to
        /// <see cref="Frame.Navigate(Type, Object)"/> when this page was initially requested and
        /// a dictionary of state preserved by this page during an earlier
        /// session. The state will be null the first time a page is visited.</param>
        private void navigationHelper_LoadState(object sender, LoadStateEventArgs e)
        {
            // TODO: Assign a bindable collection of items to this.DefaultViewModel["Items"]
            PrivateKeysDataSource privateKeysDataSource = (PrivateKeysDataSource)App.Current.Resources["privateKeysDataSource"];
            if (privateKeysDataSource != null)
            {
                var keys = new ObservableCollection<PrivateKeyData>(privateKeysDataSource.PrivateKeys.OrderBy(f => f.FileName));
                this.DefaultViewModel["Keys"] = keys;
            }

            this.AgentKeys = new ObservableCollection<PrivateKeyAgentKey>(PrivateKeyAgentManager.PrivateKeyAgent.ListSsh2());
            this.DefaultViewModel["AgentKeys"] = this.AgentKeys;

            this.SetEmptyHintVisibilities();
        }

        private void SetEmptyHintVisibilities()
        {
            Visibility keysEmptyVisibility = this.keysGridView.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            this.keysGridEmptyHint.Visibility = keysEmptyVisibility;

            Visibility agentKeysEmptyVisibility = this.agentKeysGridView.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            this.agentKeysGridEmptyHint.Visibility = agentKeysEmptyVisibility;
        }

        private async void Keys_ItemClick(object sender, ItemClickEventArgs e)
        {
            PrivateKeyData privateKeyData = e.ClickedItem as PrivateKeyData;
            if (e.ClickedItem == null)
            {
                return;
            }

            MessageDialog dlg = null;
            try
            {
                PrivateKeyFile privateKey;
                using (var privateKeyStream = new MemoryStream(privateKeyData.Data))
                {
                    privateKey = new PrivateKeyFile(privateKeyStream);
                }

                var addedAgentKey = PrivateKeyAgentManager.PrivateKeyAgent.AddSsh2(privateKey.HostKey, privateKeyData.FileName);
                if (addedAgentKey != null)
                {
                    this.AgentKeys.Add(addedAgentKey);
                }
                else
                {
                    dlg = new MessageDialog("This private key is already loaded into the agent.", "Error loading private key");
                }
            }
            catch (SshPassPhraseNullOrEmptyException)
            {
                var coreWindow = Windows.UI.Core.CoreWindow.GetForCurrentThread();

                var clickedItem = ((ListViewBase)sender).ContainerFromItem(e.ClickedItem);
                this.loadKeyPasswordErrorTextBlock.Visibility = Visibility.Collapsed;
                this.loadKeyFileName.Text = privateKeyData.FileName;
                this.loadKeyPasswordContainer.Tag = e.ClickedItem;
                this.loadKeyPasswordContainer.Visibility = Visibility.Visible;
                this.loadKeyPasswordBox.Focus(FocusState.Programmatic);
            }
            catch (SshException ex)
            {
                dlg = new MessageDialog(ex.Message, "Error loading private key");
            }

            if (dlg != null)
            {
                await dlg.ShowAsync();
            }
        }

        private async void keyLoadButton_Click(object sender, RoutedEventArgs e)
        {
            PrivateKeyData privateKeyData = this.loadKeyPasswordContainer.Tag as PrivateKeyData;
            if (privateKeyData == null)
            {
                return;
            }

            try
            {
                PrivateKeyFile privateKey;
                using (var privateKeyStream = new MemoryStream(privateKeyData.Data))
                {
                    privateKey = new PrivateKeyFile(privateKeyStream, loadKeyPasswordBox.Password);
                }

                var addedAgentKey = PrivateKeyAgentManager.PrivateKeyAgent.AddSsh2(privateKey.HostKey, privateKeyData.FileName);
                if (addedAgentKey != null)
                {
                    this.AgentKeys.Add(addedAgentKey);
                }
                else
                {
                    MessageDialog dlg = new MessageDialog("This private key is already loaded into the private key agent.", "Error loading private key");
                    await dlg.ShowAsync();
                }

                this.loadKeyPasswordContainer.Visibility = Visibility.Collapsed;
                this.SetEmptyHintVisibilities();
            }
            catch (Exception ex)
            {
                this.loadKeyPasswordErrorTextBlock.Text = "Wrong password.";
                this.loadKeyPasswordErrorTextBlock.Visibility = Visibility.Visible;
            }

            this.loadKeyPasswordBox.Password = string.Empty;
        }

        private bool loadKeyPasswordContainerClose = true;
        private void loadKeyPasswordContainer_Tapped(object sender, RoutedEventArgs e)
        {
            if (this.loadKeyPasswordContainerClose == true)
            {
                this.loadKeyPasswordContainer.Visibility = Visibility.Collapsed;
            }

            this.loadKeyPasswordContainerClose = true;
        }

        private void loadKeyPasswordContainerChild_Tapped(object sender, RoutedEventArgs e)
        {
            this.loadKeyPasswordContainerClose=false;
        }

        private void AgentKeys_ItemClick(object sender, ItemClickEventArgs e)
        {
            PrivateKeyAgentKey agentKey = e.ClickedItem as PrivateKeyAgentKey;
            if (agentKey == null)
            {
                return;
            }

            var clickedItem = ((ListViewBase)sender).ContainerFromItem(e.ClickedItem);
            MenuFlyout menuFlyout = new MenuFlyout()
            {
                Placement = FlyoutPlacementMode.Bottom,
            };

            MenuFlyoutItem menuItemUnload = new MenuFlyoutItem();
            menuItemUnload.Text = "Unload";
            menuItemUnload.Tapped += (a, b) =>
            {
                PrivateKeyAgentManager.PrivateKeyAgent.RemoveSsh2(agentKey.Key.Data);
                this.AgentKeys.Remove(agentKey);
                this.SetEmptyHintVisibilities();
            };

            //MenuFlyoutItem menuItemImport = new MenuFlyoutItem();
            //menuItemImport.Text = "Import";
            //menuItemImport.Tapped += (a, b) =>
            //{
            //    PrivateKeysDataSource privateKeysDataSource = (PrivateKeysDataSource)App.Current.Resources["privateKeysDataSource"];
            //    if (privateKeysDataSource != null)
            //    {
            //        var privateKeysFolder = await PrivateKeysDataSource.GetPrivateKeysFolder();

            //        char[] fileNameChars = agentKey.Comment.ToCharArray();
            //        char[] invalidChars = Path.GetInvalidFileNameChars();
            //        for (int i = 0; i < fileNameChars.Length; i++)
            //        {
            //            if (invalidChars.Contains(fileNameChars[i]))
            //            {
            //                fileNameChars[i] = '_';
            //            }
            //        }

            //        string fileName = new string(fileNameChars);
            //        agentKey.Key.Key.

            //        var privateKeyFile = await file.CopyAsync(privateKeysFolder, file.Name, NameCollisionOption.GenerateUniqueName);

            //        var privateKeyData = new PrivateKeyData()
            //        {
            //            FileName = privateKeyFile.Name,
            //            Data = (await FileIO.ReadBufferAsync(privateKeyFile)).ToArray(),
            //        };

            //        privateKeysDataSource.PrivateKeys.Remove(PrivateKeysDataSource.GetPrivateKey(privateKeyData.FileName));
            //        privateKeysDataSource.PrivateKeys.Add(privateKeyData);

            //        this.SetEmptyHintVisibilities();
            //    }
            //};

            menuFlyout.Items.Add(menuItemUnload);
            //menu.Items.Add(menuItemImport);

            menuFlyout.ShowAt((FrameworkElement)clickedItem);

            //PopupMenu menu = new PopupMenu();
            //menu.Commands.Add(new UICommand("Unload"));
            //menu.Commands.Add(new UICommand("Save locally"));
            //var command = await menu.ShowAsync(new Point(0, 0));
            //if (command == null)
            //{
            //    return;
            //}
        }

        private void loadKeyPasswordBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                this.keyLoadButton_Click(sender, e);
                e.Handled = true;
            }
        }

        #region NavigationHelper registration

        /// The methods provided in this section are simply used to allow
        /// NavigationHelper to respond to the page's navigation methods.
        /// 
        /// Page specific logic should be placed in event handlers for the  
        /// <see cref="GridCS.Common.NavigationHelper.LoadState"/>
        /// and <see cref="GridCS.Common.NavigationHelper.SaveState"/>.
        /// The navigation parameter is available in the LoadState method 
        /// in addition to page state preserved during an earlier session.

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            this.navigationHelper.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            this.navigationHelper.OnNavigatedFrom(e);
        }

        #endregion
    }
}
