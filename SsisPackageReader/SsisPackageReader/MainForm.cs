using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.IO;

// Summary: Display specific information stored in packages.
namespace SsisPackageReader {
    public partial class MainForm: Form {
        private const string EXCEPTION_LOG_MESSAGE = "Error occurred. Details are logged in " + LOG_FILE_NAME + ".",
                             FOLDER_DIALOG_DESCRIPTION = "Choose the directory that contains SSIS package files.",
                             HOWTO_USE_TEXT = "• On the File menu, click Open to choose a path that contains SSIS package files.\n\n" +
                                              "• To search for a specific word, after choosing a path of the files, enter it in the Search Term box and click Search.\n\n" +
                                              "• To show all information based on the supported tasks, click Search All.",
                             LOG_DATE_FORMAT = "yyyy-MM-dd HH:mm:ss zzz",
                             LOG_FILE_NAME = "SsisPackageReaderErrorLog.txt",
                             NO_DATA_FOR_TERM = "No tasks found that relate to the specified word.",
                             NO_DATA_IN_PATH = "No tasks found in the specified path.",
                             NO_FILE_IN_PATH = "No SSIS package files found in the specified path.",
                             PACKAGE_FILE_EXTENSION = "*.dtsx",
                             STATUS_LABEL_INPROGRESS = "Processing...",
                             STATUS_LABEL_FINISHED = "Finshed!",
                             TEXTBOX_DEFAULT_TEXT = "Enter search term";
        private bool _isFileExist = false;
        private string _selectedPath = "";

        ReadingSsisPackage _readingSsisPackage = ReadingSsisPackage.Instance;

        public MainForm() {
            InitializeComponent();

            folderBrowserDialog.Description = FOLDER_DIALOG_DESCRIPTION;
            folderBrowserDialog.RootFolder = Environment.SpecialFolder.MyComputer;
            folderBrowserDialog.ShowNewFolderButton = false;
            resultsRichTextBox.Text = HOWTO_USE_TEXT;
            searchAllButton.Enabled = false;
            searchButton.Enabled = false;
            searchTermTextBox.ForeColor = Color.Silver;
            searchTermTextBox.Text = TEXTBOX_DEFAULT_TEXT;
        }

        private void MainForm_Shown(object sender, EventArgs e) {
            exitToolStripMenuItem.Click += (obj, ea) => this.Close();

            openToolStripMenuItem.Click += (obj, ea) => {
                if(folderBrowserDialog.ShowDialog() == DialogResult.OK) {
                    ResetData();
                    resultsRichTextBox.Clear();

                    _selectedPath = folderBrowserDialog.SelectedPath;
                    progressToolStripStatusLabel.Text = STATUS_LABEL_INPROGRESS;

                    GetData();

                    if(!_isFileExist) {
                        resultsRichTextBox.Text = NO_FILE_IN_PATH;
                    } else if(!(_readingSsisPackage.ContentList.Count > 0)) {
                        resultsRichTextBox.Text = NO_DATA_IN_PATH;
                    } else {
                        DisplayData();
                        searchAllButton.Enabled = true;
                    }

                    progressToolStripStatusLabel.Text = STATUS_LABEL_FINISHED;
                }
            };

            resetToolStripMenuItem.Click += (obj, ea) => ResetData();

            searchAllButton.Click += (obj, ea) => {
                _readingSsisPackage.RequestAllResults();

                if(_readingSsisPackage.ContentList.Count > 0) {
                    progressToolStripStatusLabel.Text = STATUS_LABEL_INPROGRESS;

                    resultsRichTextBox.Clear();
                    DisplayData();

                    progressToolStripStatusLabel.Text = STATUS_LABEL_FINISHED;
                } else {
                    resultsRichTextBox.Clear();
                    resultsRichTextBox.Text = "";
                }
            };

            searchButton.Click += (obj, ea) => {
                string searchTerm = searchTermTextBox.Text;

                if(_readingSsisPackage.GetSearchResults(searchTerm)) {
                    progressToolStripStatusLabel.Text = STATUS_LABEL_INPROGRESS;

                    resultsRichTextBox.Clear();
                    DisplayData();

                    progressToolStripStatusLabel.Text = STATUS_LABEL_FINISHED;
                } else {
                    resultsRichTextBox.ForeColor = Color.Black;
                    resultsRichTextBox.Text = NO_DATA_FOR_TERM;
                }
            };

            searchTermTextBox.Enter += (obj, ea) => {
                if(searchTermTextBox.Text == TEXTBOX_DEFAULT_TEXT) {
                    searchTermTextBox.Text = "";
                }

                searchTermTextBox.ForeColor = Color.Black;
            };

            searchTermTextBox.Leave += (obj, ea) => {
                if(String.IsNullOrWhiteSpace(searchTermTextBox.Text)) {
                    searchButton.Enabled = false;
                    searchTermTextBox.ForeColor = Color.Silver;
                    searchTermTextBox.Text = TEXTBOX_DEFAULT_TEXT;
                }
            };

            searchTermTextBox.TextChanged += (obj, ea) => {
                if((!String.IsNullOrWhiteSpace(searchTermTextBox.Text)) && (searchTermTextBox.Text != TEXTBOX_DEFAULT_TEXT) && (Directory.Exists(_selectedPath))) {
                    searchButton.Enabled = true;
                } else {
                    searchButton.Enabled = false;
                }
            };
        }

        private void DisplayData(string searchTerm = null) {
            string currentContainerName = ReadingSsisPackage.CONTAINER_NAME_KEY;

            foreach(KeyValuePair<string, List<string>> keyValue in _readingSsisPackage.ContentList) {
                if(keyValue.Key.Contains(ReadingSsisPackage.CONTAINER_NAME_KEY)) {
                    currentContainerName = keyValue.Value[0].Replace(ReadingSsisPackage.NEW_LINE, "");
                    resultsRichTextBox.Text += keyValue.Value[0] + ReadingSsisPackage.NEW_LINE;
                } else if(keyValue.Value[0].Contains(currentContainerName)) {
                    for(int i = 1; i < keyValue.Value.Count; i++) {
                        resultsRichTextBox.Text += keyValue.Value[i] + ReadingSsisPackage.NEW_LINE;
                    }
                } else {
                    resultsRichTextBox.Text += String.Join(ReadingSsisPackage.NEW_LINE, keyValue.Value);
                }
            }
        }

        private void GetData() {
            try {
                // packageFile == a package file full path
                foreach(string packageFile in Directory.EnumerateFiles(folderBrowserDialog.SelectedPath, PACKAGE_FILE_EXTENSION, SearchOption.AllDirectories)) {
                    _isFileExist = true;
                    _readingSsisPackage.ExtractPackageData(packageFile);
                }
            } catch(Exception ex) {
                resultsRichTextBox.ForeColor = Color.Firebrick;
                resultsRichTextBox.Text = EXCEPTION_LOG_MESSAGE;

                using(StreamWriter streamWriter = new StreamWriter(LOG_FILE_NAME, true)) {
                    streamWriter.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
                    streamWriter.WriteLine(string.Join(ReadingSsisPackage.NEW_LINE, ex) + ReadingSsisPackage.NEW_LINE);
                }
            }
        }

        private void ResetData() {
            _isFileExist = false;
            progressToolStripStatusLabel.Text = "";
            resultsRichTextBox.Focus();
            resultsRichTextBox.ForeColor = Color.Black;
            resultsRichTextBox.Text = HOWTO_USE_TEXT;
            searchAllButton.Enabled = false;
            searchButton.Enabled = false;
            _selectedPath = "";
            searchTermTextBox.ForeColor = Color.Silver;
            searchTermTextBox.Text = TEXTBOX_DEFAULT_TEXT;

            _readingSsisPackage.ClearExtractedData();
            searchTermTextBox.Select(0, 0);
        }
    }
}
