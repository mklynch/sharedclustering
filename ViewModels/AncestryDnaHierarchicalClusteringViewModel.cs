﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AncestryDnaClustering.Models;
using AncestryDnaClustering.Models.Anonymizers;
using AncestryDnaClustering.Models.HierarchicalClustering;
using AncestryDnaClustering.Models.HierarchicalClustering.CorrelationWriters;
using AncestryDnaClustering.Models.HierarchicalClustering.Distance;
using AncestryDnaClustering.Models.HierarchicalClustering.MatrixBuilders;
using AncestryDnaClustering.Models.HierarchicalClustering.PrimaryClusterFinders;
using AncestryDnaClustering.Models.SavedData;
using AncestryDnaClustering.Properties;
using Microsoft.Win32;

namespace AncestryDnaClustering.ViewModels
{
    /// <summary>
    /// A ViewModel that manages configuration for generating clusters from already-downloaded DNA data.
    /// </summary>
    internal class AncestryDnaHierarchicalClusteringViewModel : ObservableObject
    {
        public string Header { get; } = "Cluster";

        public ProgressData ProgressData { get; } = new ProgressData();

        private readonly IMatchesLoader _matchesLoader;
        private readonly IAnonymizer _anonymizer;

        public AncestryDnaHierarchicalClusteringViewModel(IMatchesLoader matchesLoader, IAnonymizer anonymizer)
        {
            _matchesLoader = matchesLoader;
            _anonymizer = anonymizer;

            SelectFileCommand = new RelayCommand(SelectFile);

            SelectCorrelationFileCommand = new RelayCommand(SelectCorrelationFile);

            ProcessSavedDataCommand = new RelayCommand(async () => await ProcessSavedDataAsync());

            MinClusterSize = Settings.Default.MinClusterSize;
            Filename = Settings.Default.Filename;
            MinCentimorgansInSharedMatches = Settings.Default.MinCentimorgansInSharedMatches;
            MinCentimorgansToCluster = Settings.Default.MinCentimorgansToCluster;
            MaxMatchesPerClusterFile = Settings.Default.MaxMatchesPerClusterFile;
            MaxGrayPercentage = Settings.Default.MaxGrayPercentage;
            FilterToGuids = Settings.Default.FilterToGuids;
            AncestryHostName = Settings.Default.AncestryHostName;
            ExcludeClustersGreaterThan = Settings.Default.ExcludeClustersGreaterThan > 0 ? Settings.Default.ExcludeClustersGreaterThan : (int?)null;
            CorrelationFilename = Settings.Default.CorrelationFilename;
            ShowAdvancedClusteringOptions = Settings.Default.ShowAdvancedClusteringOptions;
            ClusterTypeVeryClose = Settings.Default.ClusterTypeVeryClose;
            ClusterTypeOver20 = Settings.Default.ClusterTypeOver20;
            ClusterTypeComplete = Settings.Default.ClusterTypeComplete;
            OpenClusterFileWhenComplete = Settings.Default.OpenClusterFileWhenComplete;
        }

        public ICommand SelectFileCommand { get; set; }

        public ICommand SelectCorrelationFileCommand { get; set; }

        public ICommand ProcessSavedDataCommand { get; set; }

        // The name of the file (full path name) that contains the saved match data.
        private string _filename;
        public string Filename
        {
            get => _filename;
            set
            {
                if (SetFieldValue(ref _filename, value, nameof(Filename)))
                {
                    Settings.Default.Filename = Filename;
                    UpdateCanProcessSavedData();
                }
            }
        }

        // Present an Open File dialog to allow selecting the saved DNA data from disk
        private void SelectFile()
        {
            var fileName = _matchesLoader.SelectFile(Filename);
            if (fileName != null)
            {
                Filename = fileName;
                SetDefaultFileName(Filename);
            }
        }

        public void SetDefaultFileName(string fileName)
        {
            var trimmedFileName = _matchesLoader.GetTrimmedFileName(fileName);

            if (trimmedFileName != null)
            {
                CorrelationFilename = Path.Combine(Path.GetDirectoryName(Filename), trimmedFileName + "-clusters.xlsx");
            }
        }

        private bool _canProcessSavedData;
        public bool CanProcessSavedData
        {
            get => _canProcessSavedData;
            set => SetFieldValue(ref _canProcessSavedData, value, nameof(CanProcessSavedData));
        }

        private void UpdateCanProcessSavedData() => CanProcessSavedData = File.Exists(Filename) && MinCentimorgansToCluster > 0;

        // The size of the smallest valid cluster. This defaults to 3, which probably shouldn't be changed.
        private int _minClusterSize;
        public int MinClusterSize
        {
            get => _minClusterSize;
            set
            {
                if (SetFieldValue(ref _minClusterSize, value, nameof(MinClusterSize)))
                {
                    Settings.Default.MinClusterSize = MinClusterSize;
                }
            }
        }

        // The centimorgans value of the lowest match that will appear in the cluster diagram.
        // Typical values are 20 (the lowest value shown on the Ancestry website)
        // and 6 (the lowest value returned in any fashion by Ancestry).
        // This is independent of the MinCentimorgansInSharedMatches value.
        private double _minCentimorgansToCluster;
        public double MinCentimorgansToCluster
        {
            get => _minCentimorgansToCluster;
            set
            {
                if (SetFieldValue(ref _minCentimorgansToCluster, value, nameof(MinCentimorgansToCluster)))
                {
                    Settings.Default.MinCentimorgansToCluster = MinCentimorgansToCluster;
                    UpdateCanProcessSavedData();
                    ClusterTypeVeryClose = MinCentimorgansToCluster == 90 && MinCentimorgansInSharedMatches == 90;
                    ClusterTypeOver20 = MinCentimorgansToCluster == 20 && MinCentimorgansInSharedMatches == 20;
                    ClusterTypeComplete = MinCentimorgansToCluster <= 6 && MinCentimorgansInSharedMatches <= 6;
                }
            }
        }

        // The centimorgans value of the lowest match that will be considered when evaluating shared matches.
        // Typical values are 20 (the lowest value shown on the Ancestry website)
        // and 6 (the lowest value returned in any fashion by Ancestry).
        // This is independent of the MinCentimorgansToCluster value.
        private double _minCentimorgansInSharedMatches;
        public double MinCentimorgansInSharedMatches
        {
            get => _minCentimorgansInSharedMatches;
            set
            {
                if (SetFieldValue(ref _minCentimorgansInSharedMatches, value, nameof(MinCentimorgansInSharedMatches)))
                {
                    Settings.Default.MinCentimorgansInSharedMatches = MinCentimorgansInSharedMatches;
                    ClusterTypeVeryClose = (MinCentimorgansToCluster == 90 && MinCentimorgansInSharedMatches == 90);
                    ClusterTypeOver20 = (MinCentimorgansToCluster == 20 && MinCentimorgansInSharedMatches == 20);
                    ClusterTypeComplete = (MinCentimorgansToCluster <= 6 && MinCentimorgansInSharedMatches <= 6);
                }
            }
        }

        // The maximum number of matches included per cluster file. If more than this many matches are
        // included in the complete cluster diagram, then the complete diagram will be split across multiple files.
        // Each file will include as many columns as matches specified here, plus a small number of additional
        // header columns at the start of each row. This option is necessary because while Excel supports up to 16384 columns,
        // other spreadsheet programs such as Google Sheets or Open Office support only 1024 or even as few as 256 columns.
        private int _maxMatchesPerClusterFile;
        public int MaxMatchesPerClusterFile
        {
            get => _maxMatchesPerClusterFile;
            set
            {
                if (SetFieldValue(ref _maxMatchesPerClusterFile, value, nameof(MaxMatchesPerClusterFile)))
                {
                    Settings.Default.MaxMatchesPerClusterFile = MaxMatchesPerClusterFile;
                }
            }
        }

        // The highest percentage of non-red cells that are allowed to be gray (0...100).
        // A value of 100 allows a solid gray background; a value of zero shows red cells only (no gray).
        // If the number of naturally calculated gray cells occupies a greater percentage of the
        // non-red cells than indicated here, then the lowest value gray cells will be suppressed
        // and will display as white. This can reduce the "sea of gray" seen in output that exhibits much pedigree collapse.
        private double _maxGrayPercentage;
        public double MaxGrayPercentage
        {
            get => _maxGrayPercentage;
            set
            {
                if (SetFieldValue(ref _maxGrayPercentage, value, nameof(MaxGrayPercentage)))
                {
                    Settings.Default.MaxGrayPercentage = MaxGrayPercentage;
                }
            }
        }

        // If this value is specified, only those GUIDs specified (one per line) in this list will be included in the cluster diagram.
        private string _filterToGuids;
        public string FilterToGuids
        {
            get => _filterToGuids;
            set
            {
                if (SetFieldValue(ref _filterToGuids, value, nameof(FilterToGuids)))
                {
                    Settings.Default.FilterToGuids = FilterToGuids;
                }
            }
        }

        // The Ancestry host name to be used in links within the cluster diagram.
        private string _ancestryHostName;
        public string AncestryHostName
        {
            get => _ancestryHostName;
            set
            {
                if (SetFieldValue(ref _ancestryHostName, value, nameof(AncestryHostName)))
                {
                    Settings.Default.AncestryHostName = AncestryHostName;
                }
            }
        }

        // The Ancestry host name to be used in links within the cluster diagram.
        private int? _excludeClustersGreaterThan;
        public int? ExcludeClustersGreaterThan
        {
            get => _excludeClustersGreaterThan;
            set
            {
                if (SetFieldValue(ref _excludeClustersGreaterThan, value, nameof(ExcludeClustersGreaterThan)))
                {
                    Settings.Default.ExcludeClustersGreaterThan = ExcludeClustersGreaterThan ?? 0;
                }
            }
        }

        // The file name (full path name) where the final cluster diagram should be saved.
        private string _correlationFilename;
        public string CorrelationFilename
        {
            get => _correlationFilename;
            set
            {
                if (SetFieldValue(ref _correlationFilename, value, nameof(CorrelationFilename)))
                {
                    Settings.Default.CorrelationFilename = CorrelationFilename;
                }
            }
        }

        // Display a Save File dialog to specify where the final cluster diagram should be saved.
        private void SelectCorrelationFile()
        {
            var saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = FileUtils.GetDefaultDirectory(CorrelationFilename),
                FileName = CorrelationFilename,
                DefaultExt = ".xlsx",
                Filter = "Excel Workbook|*.xlsx",
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                CorrelationFilename = saveFileDialog.FileName;
                Settings.Default.LastUsedDirectory = Path.GetDirectoryName(saveFileDialog.FileName);
                Settings.Default.Save();
            }
        }

        // Whether the advanced options should be displayed.
        private bool _showAdvancedClusteringOptions;
        public bool ShowAdvancedClusteringOptions
        {
            get => _showAdvancedClusteringOptions;
            set
            {
                if (SetFieldValue(ref _showAdvancedClusteringOptions, value, nameof(ShowAdvancedClusteringOptions)))
                {
                    Settings.Default.ShowAdvancedClusteringOptions = ShowAdvancedClusteringOptions;
                }
            }
        }

        private bool _clusterTypeVeryClose;
        public bool ClusterTypeVeryClose
        {
            get => _clusterTypeVeryClose;
            set
            {
                if (SetFieldValue(ref _clusterTypeVeryClose, value, nameof(ClusterTypeVeryClose)))
                {
                    Settings.Default.ClusterTypeVeryClose = ClusterTypeVeryClose;
                    if (ClusterTypeVeryClose)
                    {
                        MinCentimorgansToCluster = MinCentimorgansInSharedMatches = 90;
                    }
                }
            }
        }

        private bool _clusterTypeOver20;
        public bool ClusterTypeOver20
        {
            get => _clusterTypeOver20;
            set
            {
                if (SetFieldValue(ref _clusterTypeOver20, value, nameof(ClusterTypeOver20)))
                {
                    Settings.Default.ClusterTypeOver20 = ClusterTypeOver20;
                    if (ClusterTypeOver20)
                    {
                        MinCentimorgansToCluster = MinCentimorgansInSharedMatches = 20;
                    }
                }
            }
        }

        private bool _clusterTypeComplete;
        public bool ClusterTypeComplete
        {
            get => _clusterTypeComplete;
            set
            {
                if (SetFieldValue(ref _clusterTypeComplete, value, nameof(ClusterTypeComplete)))
                {
                    Settings.Default.ClusterTypeComplete = ClusterTypeComplete;
                    if (ClusterTypeComplete)
                    {
                        MinCentimorgansToCluster = MinCentimorgansInSharedMatches = 6;
                    }
                }
            }
        }

        private bool _openClusterFileWhenComplete;
        public bool OpenClusterFileWhenComplete
        {
            get => _openClusterFileWhenComplete;
            set
            {
                if (SetFieldValue(ref _openClusterFileWhenComplete, value, nameof(OpenClusterFileWhenComplete)))
                {
                    Settings.Default.OpenClusterFileWhenComplete = OpenClusterFileWhenComplete;
                }
            }
        }

        // The AnonymizeOutput value is intentionally not saved between launches, to protect against leaving it on by accident.
        private bool _anonymizeOutput;
        public bool AnonymizeOutput
        {
            get => _anonymizeOutput;
            set => SetFieldValue(ref _anonymizeOutput, value, nameof(AnonymizeOutput));
        }

        private async Task ProcessSavedDataAsync()
        {
            Settings.Default.Save();

            var startTime = DateTime.Now;

            try
            {
                CanProcessSavedData = false;

                var (testTakerTestId, clusterableMatches, tags) = await _matchesLoader.LoadClusterableMatchesAsync(Filename, MinCentimorgansToCluster, MinCentimorgansInSharedMatches, AnonymizeOutput ? _anonymizer : null, ProgressData);
                if (clusterableMatches == null || clusterableMatches.Count == 0)
                {
                    return;
                }

                var testIdsToFilter = new HashSet<string>(Regex.Split(FilterToGuids, @"\s+").Where(guid => !string.IsNullOrEmpty(guid)), StringComparer.OrdinalIgnoreCase);

                var matchesByIndex = clusterableMatches.ToDictionary(match => match.Index);
                var clusterableCoords = clusterableMatches
                    .Where(match => match.Match.SharedCentimorgans >= MinCentimorgansToCluster 
                        && (testIdsToFilter.Count == 0 || testIdsToFilter.Contains(match.Match.TestGuid)))
                    .SelectMany(match => testIdsToFilter.Count == 0
                        ? match.Coords.Where(coord => coord != match.Index)
                        : new[] { match.Index })
                    .Distinct()
                    .Where(coord => matchesByIndex.ContainsKey(coord))
                    .ToList();

                if (clusterableCoords.Count == 0)
                {
                    MessageBox.Show("Unable to read ICW data", "Unexpected failure", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var lowestClusterableCentimorgans = Math.Max(clusterableCoords.Min(coord => matchesByIndex[coord].Match.SharedCentimorgans), 20);

                var hierarchicalClustering = new HierarchicalClustering(
                    MinClusterSize,
                    _ => new OverlapWeightedEuclideanDistanceSquared(),
                    new AppearanceWeightedMatrixBuilder(lowestClusterableCentimorgans, MaxGrayPercentage / 100, ProgressData),
                    new HalfMatchPrimaryClusterFinder(),
                    new ExcelCorrelationWriter(CorrelationFilename, testTakerTestId, AncestryHostName, _minClusterSize, MaxMatchesPerClusterFile, lowestClusterableCentimorgans, ProgressData),
                    ProgressData);

                if (ExcludeClustersGreaterThan != null)
                {
                    clusterableMatches = hierarchicalClustering.ExcludeLargeClusters(clusterableMatches, ExcludeClustersGreaterThan.Value);
                    if (clusterableMatches.Count == 0)
                    {
                        MessageBox.Show($"All matches excluded as being clusters with more than {ExcludeClustersGreaterThan} members", "All matches excluded", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }

                var files = await hierarchicalClustering.ClusterAsync(clusterableMatches, matchesByIndex, testIdsToFilter, lowestClusterableCentimorgans, MinCentimorgansToCluster, tags, "heatmap");

                if (OpenClusterFileWhenComplete)
                {
                    foreach (var file in files)
                    {
                        FileUtils.LaunchFile(file);
                    }
                }
            }
            finally
            {
                ProgressData.Reset(DateTime.Now - startTime, "Done");
                UpdateCanProcessSavedData();
            }
        }
    }
}
