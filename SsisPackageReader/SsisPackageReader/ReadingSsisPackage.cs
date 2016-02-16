using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.SqlServer.Dts.Pipeline.Wrapper;
using Microsoft.SqlServer.Dts.Runtime;
using Microsoft.SqlServer.Dts.Tasks.ExecuteSQLTask;

namespace SsisPackageReader {
    public sealed class ReadingSsisPackage {
        private const string OPEN_ROWSET = "OpenRowset",
                             SQL_COMMAND = "SqlCommand",
                             SQL_STATEMENT_SOURCE = "SqlStatementSource";
        public const string CONTAINER_NAME_KEY = "CntnrNm",
                            H_TAB = "\t",
                            NEW_LINE = "\n";

        private bool _isAllResultsRequested = false;
        private bool _isTermExist = false;
        private int _multipleTasksContainerCounter = 0,
                    _taskCounter = 0;
        private Dictionary<string, List<string>> _allResults = new Dictionary<string, List<string>>(),          // all information based on the supported tasks
                                                 _searchResults = new Dictionary<string, List<string>>();       // search results

        private static readonly ReadingSsisPackage _instance = new ReadingSsisPackage();

        public int MultipleTasksContainerCounter {
            get { return _multipleTasksContainerCounter; }
        }

        public int TaskCounter {
            get { return _taskCounter; }
        }

        public Dictionary<string, List<string>> ContentList {
            get {
                if(_isAllResultsRequested) {
                    return _allResults;
                } else {
                    return _searchResults;
                }
            }
        }

        public static ReadingSsisPackage Instance {
            get { return _instance; }
        }

        // Reference: "Implementing the Singleton Pattern in C#" (http://csharpindepth.com/Articles/General/Singleton.aspx)
        private ReadingSsisPackage() { }

        static ReadingSsisPackage() { }

        public void ClearExtractedData() {
            _allResults.Clear();
            _isAllResultsRequested = false;
            _isTermExist = false;
            _multipleTasksContainerCounter = 0;
            _searchResults.Clear();
            _taskCounter = 0;
        }

        public void ExtractPackageData(string packageFile) {
            Microsoft.SqlServer.Dts.Runtime.Application sqlApp = new Microsoft.SqlServer.Dts.Runtime.Application();

            Executables executables = sqlApp.LoadPackage(packageFile, null).Executables;
            Stack<Executable> executablesStack = new Stack<Executable>();                       // temporary storage for nested containers

            foreach(Executable executable in executables) {
                ContainerType containerType;
                Executable exe = executable;

                do {
                    if(executablesStack.Count != 0) {
                        exe = executablesStack.Pop();
                    }

                    if(Enum.TryParse(exe.GetType().Name, out containerType)) {
                        switch(containerType) {
                            case ContainerType.ForEachLoop:
                                var forEachLoop = exe as ForEachLoop;
                                var keyArray = forEachLoop.Executables.Cast<dynamic>().ToDictionary(x => x.Name).Keys.ToArray();
                                ProcessMultipleTasksContainer(forEachLoop, keyArray, executablesStack, packageFile);
                                break;
                            case ContainerType.ForLoop:
                                var forLoop = exe as ForLoop;
                                keyArray = forLoop.Executables.Cast<dynamic>().ToDictionary(x => x.Name).Keys.ToArray();
                                ProcessMultipleTasksContainer(forLoop, keyArray, executablesStack, packageFile);
                                break;
                            case ContainerType.Sequence:
                                var sequence = exe as Sequence;
                                keyArray = sequence.Executables.Cast<dynamic>().ToDictionary(x => x.Name).Keys.ToArray();
                                ProcessMultipleTasksContainer(sequence, keyArray, executablesStack, packageFile);
                                break;
                            case ContainerType.TaskHost:
                                ProcessSingleTaskContainer(exe, packageFile);
                                break;
                        }
                    }
                } while(executablesStack.Count != 0);
            }

            if(_searchResults.Count > 0) {
                // Reference: "Merge two dictionaries and remove duplicate keys and sort by the value" (http://stackoverflow.com/questions/18123538/merge-two-dictionaries-and-remove-duplicate-keys-and-sort-by-the-value)
                _searchResults = _searchResults.Concat(_allResults.Where(x => !_searchResults.ContainsKey(x.Key))).ToDictionary(y => y.Key, y => y.Value);
            } else {
                // Create a deep copy
                foreach(KeyValuePair<string, List<string>> keyValue in _allResults) {
                    _searchResults.Add(keyValue.Key, keyValue.Value);
                }
            }
        }

        public bool GetSearchResults(string searchTerm) {
            _isAllResultsRequested = false;
            _isTermExist = false;
            _searchResults.Clear();

            if((_allResults.Count > 0) && (!String.IsNullOrWhiteSpace(searchTerm))) {
                foreach(KeyValuePair<string, List<string>> keyValue in _allResults) {

                    if(keyValue.Value.Any(x => x.ToLower().Contains(searchTerm.ToLower()))) {
                        _searchResults.Add(keyValue.Key, keyValue.Value);
                        _isTermExist = true;
                    }
                }
            }

            return _isTermExist;
        }

        // Process ForEachLoop, ForLoop, and Sequence containers
        private void ProcessMultipleTasksContainer(dynamic container, dynamic[] keyArray, Stack<Executable> executablesStack, string packageFile) {
            foreach(string key in keyArray) {
                ContainerType containerType;

                if(Enum.TryParse(container.Executables[key].GetType().Name, out containerType) && containerType == ContainerType.TaskHost) {
                    ProcessSingleTaskContainer(container.Executables[key], packageFile, container.Name);
                } else {
                    executablesStack.Push(container.Executables[key]);
                }
            }
        }

        // Process TaskHost container
        private void ProcessSingleTaskContainer(Executable executable, string packageFile, string containerName = null) {
            var taskHost = executable as TaskHost;

            if(containerName == null) {
                PrepareResults(packageFile, taskHost);
            } else {
                PrepareResults(packageFile, taskHost, containerName);
            }
        }

        private void PrepareResults(string packageFile, TaskHost taskHost, string containerName = null) {
            if(taskHost != null) {
                if((containerName != null) && ((taskHost.InnerObject is MainPipe) || (taskHost.InnerObject is ExecuteSQLTask))) {
                    List<string> containerNameList = new List<string>();
                    containerNameList.Add(containerName + NEW_LINE);
                    _allResults.Add(CONTAINER_NAME_KEY + _multipleTasksContainerCounter.ToString(), containerNameList);
                    _multipleTasksContainerCounter++;
                }

                if(taskHost.InnerObject is MainPipe) {
                    AddDataFlowTaskInfo(packageFile, taskHost.Name, taskHost, containerName);
                }

                if(taskHost.InnerObject is ExecuteSQLTask) {
                    AddExecuteSQLTaskInfo(packageFile, taskHost.Name, taskHost, containerName);
                }
            }
        }

        private void AddDataFlowTaskInfo(string packageFile, string taskHostName, TaskHost taskHost, string containerName = null) {
            foreach(IDTSComponentMetaData100 item in ((MainPipe)taskHost.InnerObject).ComponentMetaDataCollection) {
                List<string> taskContent = new List<string>();

                if(containerName != null) {
                    taskContent.Add(containerName);
                }

                taskContent.Add(packageFile);
                taskContent.Add(taskHost.Description + H_TAB + taskHostName);      // taskHostName == object name
                taskContent.Add(item.Description + H_TAB + item.Name);             // item.Name == component name

                // Reference: "How to use LINQ with dynamic collections" (http://stackoverflow.com/questions/18734996/how-to-use-linq-with-dynamic-collections)
                if(item.CustomPropertyCollection.Cast<dynamic>().ToDictionary(x => x.Name).ContainsKey(OPEN_ROWSET) && !String.IsNullOrWhiteSpace(item.CustomPropertyCollection[OPEN_ROWSET].Value)) {
                    taskContent.Add(OPEN_ROWSET + H_TAB + item.CustomPropertyCollection[OPEN_ROWSET].Value);
                }

                if(item.CustomPropertyCollection.Cast<dynamic>().ToDictionary(x => x.Name).ContainsKey(SQL_COMMAND) && !String.IsNullOrWhiteSpace(item.CustomPropertyCollection[SQL_COMMAND].Value)) {
                    taskContent.Add(SQL_COMMAND + H_TAB + item.CustomPropertyCollection[SQL_COMMAND].Value);
                }

                taskContent.Add(NEW_LINE);
                _allResults.Add(_taskCounter.ToString(), taskContent);
                _taskCounter++;
            }
        }

        private void AddExecuteSQLTaskInfo(string packageFile, string taskHostName, TaskHost taskHost, string containerName = null) {
            List<string> taskContent = new List<string>();

            if(containerName != null) {
                taskContent.Add(containerName);
            }

            taskContent.Add(packageFile);
            taskContent.Add(taskHost.Description + H_TAB + taskHostName);                                   // taskHostName == table name
            taskContent.Add(taskHost.Properties[SQL_STATEMENT_SOURCE].GetValue(taskHost).ToString());       // PropertyExpression
            taskContent.Add(NEW_LINE);

            _allResults.Add(_taskCounter.ToString(), taskContent);
            _taskCounter++;
        }

        public void RequestAllResults() {
            _isAllResultsRequested = true;
        }
    }
}
