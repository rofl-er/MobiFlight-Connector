﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MobiFlight;
using ArcazeUSB.Panels;

namespace ArcazeUSB
{
    public partial class InputConfigWizard : Form
    {
        public event EventHandler PreconditionTreeNodeChanged;

        static int lastTabActive = 0;

        ExecutionManager _execManager = null;
        int displayPanelHeight = -1;
        List<UserControl> displayPanels = new List<UserControl>();
        InputConfigItem config = null;
        ErrorProvider errorProvider = new ErrorProvider();
        Dictionary<String, String> arcazeFirmware = new Dictionary<String, String>();
        DataSet _dataSetConfig = null;

        Dictionary<string, ArcazeModuleSettings> moduleSettings;

        Panels.DisplayPinPanel displayPinPanel = new Panels.DisplayPinPanel();
        Panels.DisplayBcdPanel displayBcdPanel = new Panels.DisplayBcdPanel();
        Panels.DisplayLedDisplayPanel displayLedDisplayPanel = new Panels.DisplayLedDisplayPanel();
        Panels.DisplayNothingSelectedPanel displayNothingSelectedPanel = new Panels.DisplayNothingSelectedPanel();
        Panels.ServoPanel servoPanel = new Panels.ServoPanel();

        public InputConfigWizard(ExecutionManager mainForm, 
                             InputConfigItem cfg, 
                             ArcazeCache arcazeCache, 
                             Dictionary<string, ArcazeModuleSettings> moduleSettings, 
                             DataSet dataSetConfig, 
                             String filterGuid)
        {
            this.moduleSettings = moduleSettings;
            Init(mainForm, cfg);            
            initWithArcazeCache(arcazeCache);
            preparePreconditionPanel(dataSetConfig, filterGuid);            
        }

        protected void Init(ExecutionManager mainForm, InputConfigItem cfg)
        {
            this._execManager = mainForm;
            config = cfg;
            InitializeComponent();
            
            // if one opens the dialog for a new config
            // ensure that always the first tab is shown
            //if (cfg.FSUIPCOffset == InputConfigItem.FSUIPCOffsetNull)
            //{
            //    lastTabActive = 0;
            //}
            tabControlFsuipc.SelectedIndex = lastTabActive;

            _initPreconditionPanel();
            _loadPresets();
            // displayLedDisplayComboBox.Items.Clear(); 
        }

        private void _initPreconditionPanel()
        {
            preConditionTypeComboBox.Items.Clear();
            List<ListItem> preconTypes = new List<ListItem>() {
                new ListItem() { Value = "none",    Label = MainForm._tr("LabelPrecondition_None") },
                new ListItem() { Value = "config",  Label = MainForm._tr("LabelPrecondition_ConfigItem") },
                new ListItem() { Value = "pin",     Label = MainForm._tr("LabelPrecondition_ArcazePin") }
            };
            preConditionTypeComboBox.DataSource = preconTypes;
            preConditionTypeComboBox.DisplayMember = "Label";
            preConditionTypeComboBox.ValueMember = "Value";
            preConditionTypeComboBox.SelectedIndex = 0;

            preconditionConfigComboBox.SelectedIndex = 0;
            preconditionRefOperandComboBox.SelectedIndex = 0;

            // init the pin-type config panel
            List<ListItem> preconPinValues = new List<ListItem>() {
                new ListItem() { Value = "0", Label = "Off" },
                new ListItem() { Value = "1", Label = "On" },                
            };

            preconditionPinValueComboBox.DataSource = preconPinValues;
            preconditionPinValueComboBox.DisplayMember = "Label";
            preconditionPinValueComboBox.ValueMember = "Value";
            preconditionPinValueComboBox.SelectedIndex = 0;

            preconditionSettingsPanel.Enabled = false;
            preconditionApplyButton.Visible = false;
        }

        private void _loadPresets()
        {
            bool isLoaded = true;

            if (!System.IO.File.Exists(Properties.Settings.Default.PresetFile))
            {
                isLoaded = false;
                MessageBox.Show(MainForm._tr("uiMessageConfigWizard_PresetsNotFound"), MainForm._tr("Hint"));             
            }
            else
            {

                try
                {
                    presetsDataSet.Clear();
                    presetsDataSet.ReadXml(Properties.Settings.Default.PresetFile);
                    DataRow[] rows = presetDataTable.Select("", "description");
                }
                catch (Exception e)
                {
                    isLoaded = false;
                    MessageBox.Show(MainForm._tr("uiMessageConfigWizard_ErrorLoadingPresets"), MainForm._tr("Hint"));                    
                }
            }
        }

        private void preparePreconditionPanel(DataSet dataSetConfig, String filterGuid)
        {
            _dataSetConfig = dataSetConfig;
            DataRow[] rows = dataSetConfig.Tables["config"].Select("guid <> '" + filterGuid +"'");         
   
            // this filters the current config
            DataView dv = new DataView (dataSetConfig.Tables["config"]);
            dv.RowFilter = "guid <> '" + filterGuid + "'";
            preconditionConfigComboBox.DataSource = dv;
            preconditionConfigComboBox.ValueMember = "guid";
            preconditionConfigComboBox.DisplayMember = "description";
        }

        /// <summary>
        /// sync the config wizard with the provided settings from arcaze cache such as available modules, ports, etc.
        /// </summary>
        /// <param name="arcazeCache"></param>
        public void initWithArcazeCache (ArcazeCache arcazeCache)
        {
            
            // update the display box with
            // modules
            inputModuleNameComboBox.Items.Clear();
            preconditionPinSerialComboBox.Items.Clear();
            inputModuleNameComboBox.Items.Add("-");
            preconditionPinSerialComboBox.Items.Add("-");

            foreach (IModuleInfo module in arcazeCache.getModuleInfo())
            {
                arcazeFirmware[module.Serial] = module.Version;
                //displayModuleNameComboBox.Items.Add(module.Name + "/ " + module.Serial);
                preconditionPinSerialComboBox.Items.Add(module.Name + "/ " + module.Serial);
            }
#if MOBIFLIGHT
            foreach (IModuleInfo module in _execManager.getMobiFlightModuleCache().getModuleInfo())
            {
                inputModuleNameComboBox.Items.Add(module.Name + "/ " + module.Serial);
                preconditionPinSerialComboBox.Items.Add(module.Name + "/ " + module.Serial);
            }
#endif
            inputModuleNameComboBox.SelectedIndex = 0;
            preconditionPinSerialComboBox.SelectedIndex = 0;            
        }

        protected string _extractSerial(String ModuleSerial)
        {
            string serial = null;
            if (config == null) throw new Exception(MainForm._tr("uiException_ConfigItemNotFound"));
            // first tab                        

            if (ModuleSerial != null && ModuleSerial != "")
            {
                serial = ModuleSerial;
                if (serial.Contains('/'))
                {
                    serial = serial.Split('/')[1].Trim();
                }
            }

            return serial;
        }

        /// <summary>
        /// sync the values from config with the config wizard form
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        protected bool _syncConfigToForm(InputConfigItem config)
        {
            string serial = null;
            if (config == null) throw new Exception(MainForm._tr("uiException_ConfigItemNotFound"));
            // first tab                        
            serial = _extractSerial(config.ModuleSerial);
            if (serial != null)
            {
                if (!ComboBoxHelper.SetSelectedItemByPart(inputModuleNameComboBox, serial))
                {
                    // TODO: provide error message
                }
            }    

            // second tab
            if (!ComboBoxHelper.SetSelectedItem(inputTypeComboBox, config.Name))
            {
                // TODO: provide error message
                Log.Instance.log("_syncConfigToForm : Exception on selecting item in Display Type ComboBox", LogSeverity.Debug);
            }
                        
            preconditionListTreeView.Nodes.Clear();
            foreach (Precondition p in config.Preconditions)
            {
                TreeNode tmpNode = new TreeNode();
                tmpNode.Text = p.ToString();
                tmpNode.Tag = p;
                tmpNode.Checked = p.PreconditionActive;
                _updateNodeWithPrecondition(tmpNode, p);
                preconditionListTreeView.Nodes.Add(tmpNode);   
            }

            if (preconditionListTreeView.Nodes.Count == 0)
            {
                _addEmptyNodeToTreeView();
            }

            return true;
        }

        private void _addEmptyNodeToTreeView()
        {
            TreeNode tmpNode = new TreeNode();
            Precondition p = new Precondition();

            tmpNode.Text = p.ToString();
            tmpNode.Tag = p;
            tmpNode.Checked = p.PreconditionActive;            
            _updateNodeWithPrecondition(tmpNode, p);
            config.Preconditions.Add(p);
            preconditionListTreeView.Nodes.Add(tmpNode);
        }

        /*
        protected bool _setSelectedItem (ComboBox comboBox, string value) {
            if (comboBox.FindStringExact(value) != -1)
            {
                comboBox.SelectedIndex = comboBox.FindStringExact(value);
                return true;
            }
            return false;
        }        

        protected bool _setSelectedItemByPart (ComboBox comboBox, string value)
        {
            foreach (string item in comboBox.Items)
            {
                if (item.Contains(value))
                {
                    comboBox.SelectedIndex = comboBox.FindStringExact(item);
                    return true;
                }
            }

            return false;
        }
         * */

        /// <summary>
        /// sync current status of form values to config
        /// </summary>
        /// <returns></returns>
        protected bool _syncFormToConfig()
        {
            // display panel
            config.ModuleSerial = inputModuleNameComboBox.Text;
            config.Name = inputTypeComboBox.Text;
            DeviceType currentInputType = determineCurrentDeviceType(_extractSerial(config.ModuleSerial));

            switch (currentInputType)
            {
                case DeviceType.Button:
                    if (config.button == null) config.button = new MobiFlight.InputConfig.ButtonInputConfig();
                    (groupBoxInputSettings.Controls[0] as ButtonPanel).ToConfig(config.button);
                    break;

                case DeviceType.Encoder:
                    if (config.encoder == null) config.encoder = new MobiFlight.InputConfig.EncoderInputConfig();
                    (groupBoxInputSettings.Controls[0] as EncoderPanel).ToConfig(config.encoder);
                    break;
            }
            // depending on the current type, choose the appropriate
            // input config object
            
            return true;
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            if (!ValidateChildren())
            {              
                return;
            }
            _syncFormToConfig();
            DialogResult = DialogResult.OK;
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void ModuleSerialComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            // check which extension type is available to current serial
            ComboBox cb = (sender as ComboBox);
            try
            {
                // disable test button
                // in case that no display is selected                
                String serial = ArcazeModuleSettings.ExtractSerial(cb.SelectedItem.ToString());

                inputTypeComboBox.Enabled = groupBoxInputSettings.Enabled = (serial != "");
                // serial is empty if no module is selected (on init of form)
                //if (serial == "") return;                

                // update the available types depending on the 
                // type of module
                
                inputTypeComboBox.Items.Clear();
                MobiFlightModule module = _execManager.getMobiFlightModuleCache().GetModuleBySerial(serial);
                foreach (MobiFlight.Config.BaseDevice device in module.GetConnectedInputDevices())
                {
                    switch (device.Type)
                    {
                        case DeviceType.Button:
                            inputTypeComboBox.Items.Add(device.Name);
                            break;

                        case DeviceType.Encoder:
                            inputTypeComboBox.Items.Add(device.Name);
                            break;
                    }
                }
                
                // third tab
                if (!ComboBoxHelper.SetSelectedItem(inputTypeComboBox, config.Name))
                {
                    // TODO: provide error message
                    Log.Instance.log("displayArcazeSerialComboBox_SelectedIndexChanged : Problem setting Display Type ComboBox", LogSeverity.Debug);
                }

            }
            catch (Exception ex)
            {
                Log.Instance.log("displayArcazeSerialComboBox_SelectedIndexChanged : Some Exception occurred" + ex.Message, LogSeverity.Debug);
            }
        }

        private DeviceType determineCurrentDeviceType(String serial)
        {
            DeviceType currentInputType = DeviceType.Button;

            MobiFlightModule module = _execManager.getMobiFlightModuleCache().GetModuleBySerial(serial);

            // find the correct input type based on the name
            foreach (MobiFlight.Config.BaseDevice device in module.GetConnectedInputDevices())
            {
                if (device.Name != inputTypeComboBox.SelectedItem.ToString()) continue;

                currentInputType = device.Type;
                break;
            }

            return currentInputType;
        }

        private void inputTypeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            Control panel = null;
            groupBoxInputSettings.Controls.Clear();

            try
            {
                bool panelEnabled = true;
                // get the deviceinfo for the current arcaze
                ComboBox cb = inputModuleNameComboBox;                
                String serial = ArcazeModuleSettings.ExtractSerial(cb.SelectedItem.ToString());

                // we remove the callback method to ensure, that it is not added more than once
                // displayLedDisplayPanel.displayLedAddressComboBox.SelectedIndexChanged -= displayLedAddressComboBox_SelectedIndexChanged;

                DeviceType currentInputType = determineCurrentDeviceType(serial);

                switch (currentInputType)
                {
                    case DeviceType.Button:
                        panel = new Panels.ButtonPanel();
                        (panel as Panels.ButtonPanel).syncFromConfig(config.button);
                        break;

                    case DeviceType.Encoder:
                        panel = new Panels.EncoderPanel();
                        (panel as Panels.EncoderPanel).syncFromConfig(config.encoder);
                        break;
                }

                if (panel != null)
                {
                    panel.Padding = new Padding(2, 0, 0, 0);
                    groupBoxInputSettings.Controls.Add(panel);
                    panel.Dock = DockStyle.Top;
                }
            }
            catch (Exception)
            {
                MessageBox.Show(MainForm._tr("uiMessageNotImplementedYet"), 
                                MainForm._tr("Hint"), 
                                MessageBoxButtons.OK, 
                                MessageBoxIcon.Warning);
            }
        }

        void displayLedAddressComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComboBox cb = inputModuleNameComboBox;                
            String serial = ArcazeModuleSettings.ExtractSerial(cb.SelectedItem.ToString());
            MobiFlightModule module = _execManager.getMobiFlightModuleCache().GetModuleBySerial(serial);

            List<ListItem> connectors = new List<ListItem>();

            foreach (IConnectedDevice device in module.GetConnectedDevices())
            {
                if (device.Type != DeviceType.LedModule) continue;
                if (device.Name != ((sender as ComboBox).SelectedItem as ListItem).Value) continue;
                for (int i = 0; i< (device as MobiFlightLedModule).SubModules; i++) {
                    connectors.Add(new ListItem() { Label = (i + 1).ToString(), Value = (i + 1).ToString() });
                }
            }
            displayLedDisplayPanel.SetConnectors(connectors);
        }

        private void ConfigWizard_Load(object sender, EventArgs e)
        {
            _syncConfigToForm(config);
        }

        private void displayError(Control control, String message)
        {
            errorProvider.SetError(
                    control,
                    message);
            MessageBox.Show(message, MainForm._tr("Hint"));
        }

        private void removeError(Control control)
        {
            errorProvider.SetError(
                    control,
                    "");
        }

        private void displayArcazeSerialComboBox_Validating(object sender, CancelEventArgs e)
        {
            /* disabled this validation to permit configs even without module or
             * as precondition only
             
            if (displayArcazeSerialComboBox.Text.Trim() == "-")
            {
                e.Cancel = true;
                tabControlFsuipc.SelectedTab = displayTabPage;
                displayArcazeSerialComboBox.Focus();
                displayError(displayArcazeSerialComboBox, MainForm._tr("uiMessageConfigWizard_SelectArcaze"));                
            }
            else
            {
               removeError(displayArcazeSerialComboBox);             
            }
             */
        }

        private void portComboBox_Validating(object sender, CancelEventArgs e)
        {
            ComboBox cb = (sender as ComboBox);
            if (!cb.Parent.Visible) return;
            if (null == cb.SelectedItem) return;
            if (cb.SelectedItem.ToString() == "-----")
            {
                e.Cancel = true;
                tabControlFsuipc.SelectedTab = displayTabPage;
                cb.Focus();
                displayError(cb, MainForm._tr("Please_select_a_port"));
            }
            else
            {
                removeError(cb);
            }
        }

        private void displayLedDisplayComboBox_Validating(object sender, CancelEventArgs e)
        {
            if (inputTypeComboBox.Text == ArcazeLedDigit.TYPE)                
            {                
                try
                {
                    int.Parse(displayLedDisplayPanel.displayLedAddressComboBox.Text);
                    removeError(displayLedDisplayPanel.displayLedAddressComboBox);
                }
                catch (Exception exc)
                {
                    Log.Instance.log("displayLedDisplayComboBox_Validating : Parsing problem, " + exc.Message, LogSeverity.Debug);
                    e.Cancel = true;
                    tabControlFsuipc.SelectedTab = displayTabPage;
                    displayLedDisplayPanel.displayLedAddressComboBox.Focus();
                    displayError(displayLedDisplayPanel.displayLedAddressComboBox, MainForm._tr("uiMessageConfigWizard_ProvideLedDisplayAddress"));
                }                
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int value = Int16.Parse((sender as ComboBox).Text);
            for (int i = 0; i < 8; i++)
            {
                displayLedDisplayPanel.displayLedDigitFlowLayoutPanel.Controls["displayLedDigit" + i + "CheckBox"].Visible = i < value;
                displayLedDisplayPanel.displayLedDecimalPointFlowLayoutPanel.Controls["displayLedDecimalPoint" + i + "CheckBox"].Visible = i < value;
                displayLedDisplayPanel.Controls["displayLedDisplayLabel" + i].Visible = i < value;

                // uncheck all invisible checkboxes to ensure correct mask
                if (i >= value)
                {
                    (displayLedDisplayPanel.displayLedDigitFlowLayoutPanel.Controls["displayLedDigit" + i + "CheckBox"] as CheckBox).Checked = false;
                    (displayLedDisplayPanel.displayLedDecimalPointFlowLayoutPanel.Controls["displayLedDecimalPoint" + i + "CheckBox"] as CheckBox).Checked = false;
                }
            }
        }

        private void preConditionTypeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selected = ((sender as ComboBox).SelectedItem as ListItem).Value;
            preconditionSettingsGroupBox.Visible = selected != "none";
            preconditionRuleConfigPanel.Visible = false;
            preconditionRuleConfigPanel.Visible = selected == "config";
            preconditionPinPanel.Visible = selected == "pin";
        }

        private void preconditionRuleConfigPanel_Validating(object sender, CancelEventArgs e)
        {            
        }
        
        private void preconditionRefValueTextBox_Validating(object sender, CancelEventArgs e)
        {
            if (!(preconditionRuleConfigPanel).Visible)
            {
                removeError(preconditionRefValueTextBox);
                return;
            }

            if (preconditionRefValueTextBox.Text.Trim() == "")
            {
                e.Cancel = true;
                tabControlFsuipc.SelectedTab = preconditionTabPage;
                displayError(preconditionRefValueTextBox, MainForm._tr("uiMessageConfigWizard_SelectComparison"));
            }
            else
            {
                removeError(preconditionRefValueTextBox);
            }
        }

        private void preconditionPinSerialComboBox_Validating(object sender, CancelEventArgs e)
        {
            if (!(preconditionPinPanel).Visible)
            {
                removeError(preconditionRefValueTextBox);
                return;
            }

            if (preconditionPinSerialComboBox.Text.Trim() == "-")
            {
                e.Cancel = true;
                tabControlFsuipc.SelectedTab = preconditionTabPage;
                preconditionPinSerialComboBox.Focus();
                displayError(preconditionPinSerialComboBox, MainForm._tr("uiMessageConfigWizard_SelectArcaze"));
            }
            else
            {
                removeError(preconditionPinSerialComboBox);
            }

        }

        private void preconditionPinComboBox_Validating(object sender, CancelEventArgs e)
        {
            if (!(preconditionPinPanel).Visible)
            {
                removeError(preconditionPinComboBox);
                return;
            }

            if (preconditionPinComboBox.SelectedIndex == -1)
            {
                e.Cancel = true;
                tabControlFsuipc.SelectedTab = preconditionTabPage;
                displayError(preconditionPinComboBox, MainForm._tr("Please_select_a_pin"));
            }
            else
            {
                removeError(preconditionPinComboBox);
            }
        }

        private void preconditionPortComboBox_Validating(object sender, CancelEventArgs e) {
            if (!(preconditionPinPanel).Visible)
            {
                removeError(preconditionPortComboBox);
                return;
            }

            if (preconditionPortComboBox.SelectedIndex == -1)
            {
                e.Cancel = true;
                tabControlFsuipc.SelectedTab = preconditionTabPage;
                displayError(preconditionPortComboBox, MainForm._tr("Please_select_a_port"));
            }
            else
            {
                removeError(preconditionPortComboBox);
            }
        }

                
        private void tabControlFsuipc_SelectedIndexChanged(object sender, EventArgs e)
        {
            // check if running in test mode
            lastTabActive = (sender as TabControl).SelectedIndex;
        }

        private void preconditionListTreeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            preconditionListTreeView.SelectedNode = e.Node;
            if (e.Button != System.Windows.Forms.MouseButtons.Left) return;

            Precondition config = (e.Node.Tag as Precondition);
            preConditionTypeComboBox.SelectedValue = config.PreconditionType;
            preconditionSettingsPanel.Enabled = true;
            preconditionApplyButton.Visible = true;
            config.PreconditionActive = e.Node.Checked;

            switch (config.PreconditionType)
            {
                case "config":
                    try
                    {                        
                        preconditionConfigComboBox.SelectedValue = config.PreconditionRef;
                    }
                    catch (Exception exc)
                    {
                        // precondition could not be loaded
                        Log.Instance.log("preconditionListTreeView_NodeMouseClick : Precondition could not be loaded, " + exc.Message, LogSeverity.Debug);
                    }

                    ComboBoxHelper.SetSelectedItem(preconditionRefOperandComboBox, config.PreconditionOperand);
                    preconditionRefValueTextBox.Text = config.PreconditionValue;
                    break;

                case "pin":
                    ArcazeIoBasic io = new ArcazeIoBasic(config.PreconditionPin);                    
                    ComboBoxHelper.SetSelectedItemByPart(preconditionPinSerialComboBox, config.PreconditionSerial);
                    preconditionPinValueComboBox.SelectedValue = config.PreconditionValue;
                    preconditionPortComboBox.SelectedIndex = io.Port;
                    preconditionPinComboBox.SelectedIndex = io.Pin;
                    break;
            }  
        }

        private void preconditionApplyButton_Click(object sender, EventArgs e)
        {
            // sync the selected node with the current settings from the panels
            TreeNode selectedNode = preconditionListTreeView.SelectedNode;
            Precondition c = selectedNode.Tag as Precondition;
            
            c.PreconditionType = (preConditionTypeComboBox.SelectedItem as ListItem).Value;
            switch (c.PreconditionType)
            {
                case "config":
                    c.PreconditionRef = preconditionConfigComboBox.SelectedValue.ToString();
                    c.PreconditionOperand = preconditionRefOperandComboBox.Text;
                    c.PreconditionValue = preconditionRefValueTextBox.Text;
                    c.PreconditionActive = true;
                    break;
                
                case "pin":                    
                    c.PreconditionSerial = preconditionPinSerialComboBox.Text;
                    c.PreconditionValue = preconditionPinValueComboBox.SelectedValue.ToString();
                    c.PreconditionPin = preconditionPortComboBox.Text + preconditionPinComboBox.Text;
                    c.PreconditionActive = true;
                    break;                    
            }

            _updateNodeWithPrecondition(selectedNode, c);
        }    
    
        private void _updateNodeWithPrecondition (TreeNode node, Precondition p) 
        {
            String label = p.PreconditionLabel;
            if (p.PreconditionType == "config")
            {
                String replaceString = "[unknown]";
                if (_dataSetConfig != null)
                {
                    DataRow[] rows = _dataSetConfig.Tables["config"].Select("guid = '" + p.PreconditionRef + "'");
                    replaceString = rows[0]["description"] as String;
                }
                label = label.Replace("<Ref:" + p.PreconditionRef  + ">", replaceString);
            }
            else if (p.PreconditionType == "pin")
            {
                label = label.Replace("<Serial:" + p.PreconditionSerial + ">", p.PreconditionSerial.Split('/')[0]);
            }
            
            label = label.Replace("<Logic:and>", " (AND)").Replace("<Logic:or>", " (OR)");
            node.Checked = p.PreconditionActive;
            node.Tag = p;
            node.Text = label;
            aNDToolStripMenuItem.Checked = p.PreconditionLogic == "and";
            oRToolStripMenuItem.Checked = p.PreconditionLogic == "or";
        }

        private void addPreconditionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Precondition p = new Precondition();
            TreeNode n = new TreeNode();
            n.Tag = p;
            config.Preconditions.Add(p);
            preconditionListTreeView.Nodes.Add(n);
            _updateNodeWithPrecondition(n, p);
        }

        private void andOrToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode selectedNode = preconditionListTreeView.SelectedNode;
            Precondition p = selectedNode.Tag as Precondition;
            if ((sender as ToolStripMenuItem).Text == "AND")
                p.PreconditionLogic = "and";
            else
                p.PreconditionLogic = "or";

            _updateNodeWithPrecondition(selectedNode, p);            
        }

        private void removePreconditionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode selectedNode = preconditionListTreeView.SelectedNode;
            Precondition p = selectedNode.Tag as Precondition;
            config.Preconditions.Remove(p);
            preconditionListTreeView.Nodes.Remove(selectedNode);
        }

        private void preconditionPinSerialComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            // get the deviceinfo for the current arcaze
            ComboBox cb = preconditionPinSerialComboBox;
            String serial = ArcazeModuleSettings.ExtractSerial(cb.SelectedItem.ToString());
            // if (serial == "" && config.ModuleSerial != null) serial = ArcazeModuleSettings.ExtractSerial(config.ModuleSerial);

            if (serial.IndexOf("SN") != 0)
            {
                preconditionPortComboBox.Items.Clear();
                preconditionPinComboBox.Items.Clear();

                List<ListItem> ports = new List<ListItem>();

                foreach (String v in ArcazeModule.getPorts())
                {
                    ports.Add(new ListItem() { Label = v, Value = v });
                    if (v == "B" || v == "E" || v == "H" || v == "K")
                    {
                        ports.Add(new ListItem() { Label = "-----", Value = "-----" });
                    }

                    if (v == "A" || v == "B")
                    {
                        preconditionPortComboBox.Items.Add(v);
                    }
                }

                List<ListItem> pins = new List<ListItem>();
                foreach (String v in ArcazeModule.getPins())
                {
                    pins.Add(new ListItem() { Label = v, Value = v });
                    preconditionPinComboBox.Items.Add(v);
                }
            }
        }
    }
}
